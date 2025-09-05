// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Distributed under the Boost Software License, Version 1.0. http://www.boost.org/LICENSE_1_0.txt
using System;
using System.IO;
using System.Text;

namespace g3
{
#nullable enable
    public class GLTFReader : IMeshReader
    {
        // connect to this to get warning messages
        public event ParsingMessagesHandler? warningEvent;


        public IOReadResult Read(TextReader reader, ReadOptions options, IMeshBuilder builder)
        {
            GLTFFile.Root? root = null;

            string content = reader.ReadToEnd();
            byte[] utf8Bytes = Encoding.UTF8.GetBytes(content);
            using (Stream utf8Stream = new MemoryStream(utf8Bytes)) {
                var result = GLTFFile.ParseFile(utf8Stream);
                if (result.IsValid == false)
                    return new IOReadResult(IOCode.FileParsingError, "unknown parsing error");
                root = result.Value;
            }
            if (root == null)
                return new IOReadResult(IOCode.GenericReaderError, "error");

            root.RootPath = options.BaseFilePath;

            DMesh3 combinedMesh = ExtractCombinedMesh(root);
            builder.AppendNewMesh(combinedMesh);
            return IOReadResult.Ok;
        }

        public IOReadResult Read(BinaryReader reader, ReadOptions options, IMeshBuilder builder)
        {
            throw new NotImplementedException();
        }


        GLTFBufferSet BufferSet = new GLTFBufferSet();


        protected DMesh3 ExtractCombinedMesh(GLTFFile.Root root)
        {
            DMesh3 result = new DMesh3();
            result.EnableTriangleGroups();
            if (root.scenes == null || root.scenes.Length == 0)
                return result;      // todo: do we have to handle meshes w/o any scene??

            // read the buffers
            if (root.buffers != null) {
                foreach (GLTFFile.Buffer bufferInfo in root.buffers) {
                    if (bufferInfo.uri != null) 
                    {
                        // can URI be a web link?? ugh
                        string BufferFilePath = Path.Combine(root.RootPath, bufferInfo.uri);
                        GLTFBuffer buffer = new GLTFBuffer(BufferFilePath);
                        BufferSet.AddBuffer(buffer);
                    } else {
                        throw new NotImplementedException();        // todo: need to support inline buffers?? (only for glb??)
                    }
                }
                Util.gDevAssert(BufferSet.Buffers.Count == root.buffers.Length);
            }


            MeshEditor editor = new MeshEditor(result);

            //ref GLTFFile.Scene scene = ref root.scenes[0];
            foreach (GLTFFile.Mesh meshInfo in root.meshes!) {
                DMesh3 mesh = BuildMesh(root, meshInfo);
                if (mesh.HasVertexUVs && result.HasVertexUVs == false)
                    result.EnableVertexUVs(Vector2f.Zero);
                if (mesh.HasVertexNormals && result.HasVertexNormals == false)
                    result.EnableVertexNormals(Vector3f.AxisZ);
                editor.AppendMesh(mesh);
            }

            return result;
        }



        protected DMesh3 BuildMesh(GLTFFile.Root root, GLTFFile.Mesh meshInfo)
        {
            DMesh3 NewMesh = new DMesh3();
            if (meshInfo.primitives == null)
                return NewMesh;

            NewMesh.EnableTriangleGroups(0);

            int groupid = 0;
            foreach (GLTFFile.Primitive primitive in meshInfo.primitives) 
            {
                AppendPrimitive(root, primitive, NewMesh, groupid);
            }

            return NewMesh;
        }


        protected void AppendPrimitive(GLTFFile.Root root, GLTFFile.Primitive primitive, DMesh3 mesh, int use_groupid = 0)
        {
            bool bFoundPositions = primitive.attributes!.TryGetValue(GLTFFile.MeshAttribute_Position, out int positionAccessor);
            GLTFFile.Accessor positionsAccessor = root.accessors![positionAccessor];
            Span<float> positionsBuf = BufferSet.GetFloatBuffer(root, positionsAccessor);

            Util.gDevAssert(positionsBuf.Length % 3 == 0);
            int NumV = positionsBuf.Length/3;
            Util.gDevAssert(NumV == positionsAccessor.count);

            for (int vi = 0; vi < NumV; ++vi) {
                Vector3d v = new Vector3d(positionsBuf[3*vi], positionsBuf[3*vi+1], positionsBuf[3*vi+2]);
                mesh.AppendVertex(v);
            }


            bool bFoundNormals = primitive.attributes!.TryGetValue(GLTFFile.MeshAttribute_Normal, out int normalAccessor);
            if (bFoundNormals) {
                mesh.EnableVertexNormals(Vector3f.Zero);
                GLTFFile.Accessor normalsAccessor = root.accessors![normalAccessor];
                Span<float> normalsBuf = BufferSet.GetFloatBuffer(root, normalsAccessor);
                Util.gDevAssert(normalsBuf.Length == positionsBuf.Length);
                for ( int vi = 0; vi < NumV; ++vi ) {
                    Vector3f n = new Vector3f(normalsBuf[3*vi], normalsBuf[3*vi+1], normalsBuf[3*vi+2]);
                    mesh.SetVertexNormal(vi, n);
                }
            }


            string UV0 = GLTFFile.MeshAttribute_UVPrefix + "0";
            bool bFoundUV0 = primitive.attributes!.TryGetValue(UV0, out int uvAccessor);
            if ( bFoundUV0 ) {
                mesh.EnableVertexUVs(Vector2f.Zero);
                GLTFFile.Accessor uv0Accessor = root.accessors![uvAccessor];
                Span<float> uv0Buf = BufferSet.GetFloatBuffer(root, uv0Accessor);
                Util.gDevAssert(uv0Buf.Length/2 == positionsBuf.Length/3);
                for (int vi = 0; vi < NumV; ++vi) {
                    Vector2f uv = new Vector2f(uv0Buf[2*vi], uv0Buf[2*vi+1]);
                    mesh.SetVertexUV(vi, uv);
                }
            }



            Util.gDevAssert(primitive.indices > 0);     // otherwise need to handle this differently...

            GLTFFile.Accessor indicesAccessor = root.accessors![primitive.indices];
            GLTFFile.EElementType indicesElemType = indicesAccessor.GetElementType();
            
            // this gross switch avoids converting buffer types...but so much duplicated code :(
            switch (indicesAccessor.ComponentTypeEnum) {
                case GLTFFile.EComponentType.UnsignedInt:
                    Span<uint> indicesBuf_uint = BufferSet.GetUIntBuffer(root, indicesAccessor);
                    AppendMeshTriIndices_uint(indicesBuf_uint, mesh, use_groupid);
                    break;
                case GLTFFile.EComponentType.UnsignedShort:
                    Span<ushort> indicesBuf_ushort = BufferSet.GetUShortBuffer(root, indicesAccessor);
                    AppendMeshTriIndices_ushort(indicesBuf_ushort, mesh, use_groupid);
                    break;
                default:
                    throw new NotImplementedException($"support for indices of type {indicesAccessor.ComponentTypeEnum} not implemented yet");
            }

        }

        // ugh gross
        protected void AppendMeshTriIndices_uint(Span<uint> indicesBuf, DMesh3 mesh, int use_groupid)
        {
            Util.gDevAssert(indicesBuf.Length % 3 == 0);
            int NumT = indicesBuf.Length/3;
            for (int ti = 0; ti < NumT; ++ti) {
                Index3i tri = new Index3i((int)indicesBuf[3*ti], (int)indicesBuf[3*ti+1], (int)indicesBuf[3*ti+2]);
                int tid = mesh.AppendTriangle(tri, use_groupid);
                if (tid < 0) {
                    throw new Exception("todo");
                }
            }
        }
        protected void AppendMeshTriIndices_ushort(Span<ushort> indicesBuf, DMesh3 mesh, int use_groupid)
        {
            Util.gDevAssert(indicesBuf.Length % 3 == 0);
            int NumT = indicesBuf.Length/3;
            for (int ti = 0; ti < NumT; ++ti) {
                Index3i tri = new Index3i((int)indicesBuf[3*ti], (int)indicesBuf[3*ti+1], (int)indicesBuf[3*ti+2]);
                int tid = mesh.AppendTriangle(tri, use_groupid);
                if (tid < 0) {
                    throw new Exception("todo");
                }
            }
        }

    }
}
