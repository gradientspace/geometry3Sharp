// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Distributed under the Boost Software License, Version 1.0. http://www.boost.org/LICENSE_1_0.txt
using gs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace g3
{
#nullable enable
    public class GLTFReader : IMeshReader
    {
        // connect to this to get warning messages
        public event ParsingMessagesHandler? warningEvent;

        // if non-null, only meshes with names starting with strings in this list will be included
        public string[]? IncludeMeshNamePrefixes = null;
        //public string[]? IncludeMeshNamePrefixes = [""];
        // invert above filter (ie discard meshes w/ those name prefixes)
        bool bInvertMeshNameFilter = false;

        public enum WeldMode
        {
            NoWelding = 0,
            WeldPrimitive = 1,
            WeldMesh = 2
        }
        public WeldMode WeldingStrategy = WeldMode.NoWelding;


        public enum MeshingMode
        {
            SingleCombinedMesh = 0,
            WorldspaceMeshPerNode = 1
        }
        public MeshingMode MeshStrategy = MeshingMode.SingleCombinedMesh;


        public IOReadResult Read(TextReader reader, ReadOptions options, IMeshBuilder builder)
        {
            Reset();
            GLTFFile.Root? root = null;

            // read file content
            string content = reader.ReadToEnd();
            byte[] utf8Bytes = Encoding.UTF8.GetBytes(content);
            // parse to GLTFFile structures
            using (Stream utf8Stream = new MemoryStream(utf8Bytes)) {
                var result = GLTFFile.ParseFile(utf8Stream);
                if (result.IsValid == false)
                    return new IOReadResult(IOCode.FileParsingError, "unknown parsing error");
                root = result.Value;
            }
            if (root == null)
                return new IOReadResult(IOCode.GenericReaderError, "error");

			if (root.meshes == null)
				return new IOReadResult(IOCode.GenericReaderError, "GLTF file contains no meshes");

			root.RootPath = options.BaseFilePath;

            if (MeshStrategy == MeshingMode.SingleCombinedMesh) 
            {
                DMesh3 combinedMesh = ExtractCombinedMesh(root);
                if (combinedMesh.TriangleCount == 0)
                    return new IOReadResult(IOCode.GenericReaderError, "GLTF file or scene contains no triangles");
                builder.AppendNewMesh(combinedMesh);
            } 
            else if (MeshStrategy == MeshingMode.WorldspaceMeshPerNode)
            {
                int NumMeshes = ExtractPerNodeMeshes(root, builder);
                if (NumMeshes == 0)
                    return new IOReadResult(IOCode.GenericReaderError, "GLTF file or scene contains no meshes");
            }

            return IOReadResult.Ok;
        }

        public IOReadResult Read(BinaryReader reader, ReadOptions options, IMeshBuilder builder)
        {
            throw new NotImplementedException();
        }


        GLTFBufferSet BufferSet = new GLTFBufferSet();
        DMesh3[]? Meshes = null;
        DMesh3? GetMesh(int index)
        {
            return (Meshes != null && index >= 0 && index < Meshes.Length) ? Meshes[index] : null;
        }

        void Reset()
        {
            BufferSet = new GLTFBufferSet();
            Meshes = null;
        }



        protected int ExtractPerNodeMeshes(GLTFFile.Root root, IMeshBuilder meshBuilder)
        {
            DMesh3 result = new DMesh3();
            result.EnableTriangleGroups();
            if (root.scenes == null || root.scenes.Length == 0)
                return 0;

            BuildAllBuffersAndMeshes(root, out bool bHaveNormals, out bool bHaveUVs);
            if (Meshes == null || Meshes.Length == 0)
                return 0;

            // recursively append all nodes
            int total_meshes = 0;
            int use_scene = (root.scene >= 0 && root.scene < root.scenes.Length) ? root.scene : 0;
            GLTFFile.Scene scene = root.scenes[use_scene];
            foreach (int node in (scene.nodes ?? [])) {
                DescendNode(root, node, Matrix4d.Identity, meshBuilder, out int new_meshes);
                total_meshes += new_meshes;
            }
            return total_meshes;
        }
        protected void DescendNode(GLTFFile.Root root, int NodeIndex, Matrix4d ParentGlobalXForm, IMeshBuilder meshBuilder, out int new_meshes)
        {
            new_meshes = 0;
            ref GLTFFile.Node node = ref root.nodes![NodeIndex];
            Matrix4d NodeLocalXForm = node.GetTransformMatrix();
            Matrix4d NodeGlobalXForm = ParentGlobalXForm * NodeLocalXForm;

            if (node.mesh >= 0) {
                bool bIncludeMesh = true;
                if (node.mesh < 0 || node.mesh > root.meshes!.Length) {
                    warningEvent?.Invoke($"Node {NodeIndex}:[{node.name}] has out-of-bounds mesh field {node.mesh}", null);
                    bIncludeMesh = false;
                }

                if (bIncludeMesh && IncludeMeshNamePrefixes != null) {
                    GLTFFile.Mesh meshInfo = root.meshes![node.mesh];
                    bool bPassesFilters = Array.Exists(IncludeMeshNamePrefixes,
                        (string filter) => { return meshInfo.name.StartsWith(filter); });
                    if (bPassesFilters == bInvertMeshNameFilter)
                        bIncludeMesh = false;
                }

                if (bIncludeMesh) {
                    DMesh3? FoundMesh = GetMesh(node.mesh);
                    if (FoundMesh!= null && FoundMesh.TriangleCount > 0) {
                        DMesh3 Copy = new DMesh3(FoundMesh);
                        MeshTransforms.TransforMesh(Copy, NodeGlobalXForm);
                        meshBuilder.AppendNewMesh(Copy);
                        new_meshes++;
                        // todo could we append mesh name here somehow??
                    }
                }
            }

            foreach (int childNode in (node.children ?? [])) {
                DescendNode(root, childNode, NodeGlobalXForm, meshBuilder, out int child_meshes);
                new_meshes += child_meshes;
            }

        }





        protected DMesh3 ExtractCombinedMesh(GLTFFile.Root root)
        {
            DMesh3 result = new DMesh3();
            result.EnableTriangleGroups();
            if (root.scenes == null || root.scenes.Length == 0)
                return result;      // todo: do we have to handle meshes w/o any scene??

            BuildAllBuffersAndMeshes(root, out bool bHaveNormals, out bool bHaveUVs);
            if (Meshes == null || Meshes.Length == 0)
                return result;

            DMesh3 ResultMesh = new DMesh3(bHaveNormals, false, bHaveUVs, true);

            // recursively append all nodes
			int use_scene = (root.scene >= 0 && root.scene < root.scenes.Length) ? root.scene : 0;
			GLTFFile.Scene scene = root.scenes[use_scene];
			foreach(int node in (scene.nodes ?? [])) 
            {
				AppendNode(root, node, Matrix4d.Identity, ResultMesh);
			}

            return ResultMesh;
        }
		protected void AppendNode(GLTFFile.Root root, int NodeIndex, Matrix4d ParentGlobalXForm, DMesh3 AppendToMesh)
		{
			ref GLTFFile.Node node = ref root.nodes![NodeIndex];
			Matrix4d NodeLocalXForm = node.GetTransformMatrix();
			Matrix4d NodeGlobalXForm = ParentGlobalXForm * NodeLocalXForm;

			if (node.mesh >= 0) 
			{
                bool bIncludeMesh = true;
                if ( node.mesh < 0 || node.mesh > root.meshes!.Length ) {
                    warningEvent?.Invoke($"Node {NodeIndex}:[{node.name}] has out-of-bounds mesh field {node.mesh}", null);
                    bIncludeMesh = false;
                }

                if (bIncludeMesh && IncludeMeshNamePrefixes != null) 
                {
                    GLTFFile.Mesh meshInfo = root.meshes![node.mesh];
                    bool bPassesFilters = Array.Exists(IncludeMeshNamePrefixes,
                        (string filter) => { return meshInfo.name.StartsWith(filter); });
                    if (bPassesFilters == bInvertMeshNameFilter)
                        bIncludeMesh = false;
                }

                if (bIncludeMesh) {
                    DMesh3? FoundMesh = GetMesh(node.mesh);
                    if (FoundMesh!= null && FoundMesh.TriangleCount > 0)
                        AppendMesh(FoundMesh, NodeGlobalXForm, AppendToMesh);
                }
			}

			foreach (int childNode in (node.children ?? [])) {
				AppendNode(root, childNode, NodeGlobalXForm, AppendToMesh);
			}

		}




		protected void AppendMesh(DMesh3 Mesh, Matrix4d Transform, DMesh3 AppendToMesh)
		{
            TransformWrapper transformer = default;
            if (Transform.IsIdentity == false)
                transformer = TransformWrapper.MakeFromAffine(Transform);

            MeshEditor.AppendMesh(AppendToMesh, Mesh,
                out int[] mapV, out int[]? mapG, null,
                MeshEditor.AppendGroupPolicy.RemapGroupIDs, -1, transformer);
		}




        protected void BuildAllBuffersAndMeshes(GLTFFile.Root root, out bool bHaveNormals, out bool bHaveUVs)
        {
            bHaveNormals = false;
            bHaveUVs = false;

            if (root.buffers == null) {
                warningEvent?.Invoke($"no buffers found, aborting.", null);
                return;
            }

            // read the buffers
            if (root.buffers != null) 
            {
                for (int bufferidx = 0; bufferidx < root.buffers.Length; bufferidx++) 
                {
                    GLTFFile.Buffer bufferInfo = root.buffers[bufferidx];
    
                    if (bufferInfo.uri != null) {
                        // can URI be a web link?? ugh
                        string BufferFilePath = Path.Combine(root.RootPath, bufferInfo.uri);
                        if (File.Exists(BufferFilePath) == false) {
                            warningEvent?.Invoke($"buffer {bufferidx}:{bufferInfo.uri} - file not found", null);
                            continue;
                        }
                        GLTFBuffer buffer = new GLTFBuffer(BufferFilePath);
                        if ( buffer.IsValid == false) {
                            warningEvent?.Invoke($"buffer {bufferidx}:{bufferInfo.uri} - file could not be read or contains no data", null);
                            continue;
                        }
                        BufferSet.AddBuffer(buffer);
                    } 
                    else 
                    {
                        warningEvent?.Invoke($"buffer {bufferidx}:{bufferInfo} not supported", null);
                        throw new NotImplementedException();        // todo: need to support inline buffers?? (only for glb??)
                    }
                    bufferidx++;
                }
                if (BufferSet.Buffers.Count != root.buffers.Length) {
                    warningEvent?.Invoke($"failed to find some buffers. aborting.", null);
                    return;
                }
            }


            // build all the meshes  (can do this in parallel?)
            Meshes = new DMesh3[root.meshes!.Length];
            for (int i = 0; i < root.meshes.Length; ++i) {
                GLTFFile.Mesh meshInfo = root.meshes[i];
                DMesh3 mesh = BuildMesh(root, meshInfo);
                Meshes[i] = mesh;
                if (mesh.HasVertexNormals)
                    bHaveNormals = true;
                if (mesh.HasVertexUVs)
                    bHaveUVs = true;
            }
        }


        protected DMesh3 BuildMesh(GLTFFile.Root root, GLTFFile.Mesh meshInfo)
        {
            DMesh3 CombinedMesh = new DMesh3();
            if (meshInfo.primitives == null)
                return CombinedMesh;

            CombinedMesh.EnableTriangleGroups(0);

            int NumPrimitives = meshInfo.primitives.Length;
            int groupid = 0;
            for ( int i = 0; i < NumPrimitives; ++i )
            {
                GLTFFile.Primitive primitive = meshInfo.primitives[i];
                if (WeldingStrategy != WeldMode.NoWelding && NumPrimitives > 1) {
                    // even in full-mesh weld strategy, we want to weld each primitive 
                    // separately because it may avoid ambiguity at mesh level
                    DMesh3 primitiveMesh = new DMesh3(false, false, false, true);
                    AppendPrimitive(root, primitive, primitiveMesh, groupid);
                    ApplyWelding(primitiveMesh);
                    MeshEditor.AppendMesh(CombinedMesh, primitiveMesh, groupid);
                } else
                    AppendPrimitive(root, primitive, CombinedMesh, groupid);

                groupid++;
            }

			if ( WeldingStrategy == WeldMode.WeldMesh ) {
                ApplyWelding(CombinedMesh);
            }

            return CombinedMesh;
        }


        protected void ApplyWelding(DMesh3 mesh)
        {
            MergeCoincidentEdges merge = new MergeCoincidentEdges(mesh);
            merge.Apply();
        }


        protected void AppendPrimitive(
			GLTFFile.Root root, GLTFFile.Primitive primitive, 
			DMesh3 mesh, 
			int use_groupid = 0)
        {
            bool bFoundPositions = primitive.attributes!.TryGetValue(GLTFFile.MeshAttribute_Position, out int positionAccessor);
            GLTFFile.Accessor positionsAccessor = root.accessors![positionAccessor];
            Span<float> positionsBuf = BufferSet.GetFloatBuffer(root, positionsAccessor);

            Util.gDevAssert(positionsBuf.Length % 3 == 0);
            int NumV = positionsBuf.Length/3;
            Util.gDevAssert(NumV == positionsAccessor.count);

            int[] MapV = new int[NumV];
            for (int vi = 0; vi < NumV; ++vi) {
                Vector3d v = new Vector3d(positionsBuf[3*vi], positionsBuf[3*vi+1], positionsBuf[3*vi+2]);
				int new_vid = mesh.AppendVertex(v);
                MapV[vi] = new_vid;
            }


            bool bFoundNormals = primitive.attributes!.TryGetValue(GLTFFile.MeshAttribute_Normal, out int normalAccessor);
            if (bFoundNormals) {
                mesh.EnableVertexNormals(Vector3f.Zero);
                GLTFFile.Accessor normalsAccessor = root.accessors![normalAccessor];
                Span<float> normalsBuf = BufferSet.GetFloatBuffer(root, normalsAccessor);
                Util.gDevAssert(normalsBuf.Length == positionsBuf.Length);
                for ( int vi = 0; vi < NumV; ++vi ) {
                    Vector3f n = new Vector3f(normalsBuf[3*vi], normalsBuf[3*vi+1], normalsBuf[3*vi+2]);
                    int new_vid = MapV[vi];
                    mesh.SetVertexNormal(new_vid, n);
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
                    int new_vid = MapV[vi];
                    mesh.SetVertexUV(new_vid, uv);
                }
            }

            Util.gDevAssert(primitive.indices >= 0);     // otherwise need to handle this differently...

            GLTFFile.Accessor indicesAccessor = root.accessors![primitive.indices];
            GLTFFile.EElementType indicesElemType = indicesAccessor.GetElementType();
            
            // this gross switch avoids converting buffer types...but so much duplicated code :(
            switch (indicesAccessor.ComponentTypeEnum) {
                case GLTFFile.EComponentType.UnsignedInt:
                    Span<uint> indicesBuf_uint = BufferSet.GetUIntBuffer(root, indicesAccessor);
                    AppendMeshTriIndices_uint(indicesBuf_uint, mesh, MapV, use_groupid);
                    break;
                case GLTFFile.EComponentType.UnsignedShort:
                    Span<ushort> indicesBuf_ushort = BufferSet.GetUShortBuffer(root, indicesAccessor);
                    AppendMeshTriIndices_ushort(indicesBuf_ushort, mesh, MapV, use_groupid);
                    break;
                default:
                    throw new NotImplementedException($"support for indices of type {indicesAccessor.ComponentTypeEnum} not implemented yet");
            }

        }

        // ugh gross
        protected void AppendMeshTriIndices_uint(Span<uint> indicesBuf, DMesh3 mesh, int[] MapV, int use_groupid)
        {
            Util.gDevAssert(indicesBuf.Length % 3 == 0);
            int NumT = indicesBuf.Length/3;
            for (int ti = 0; ti < NumT; ++ti) {
                Index3i tri = new Index3i((int)indicesBuf[3*ti], (int)indicesBuf[3*ti+1], (int)indicesBuf[3*ti+2]);
                tri.a = MapV[tri.a]; tri.b = MapV[tri.b]; tri.c = MapV[tri.c];      // todo handle invalid ids
                int tid = mesh.AppendTriangle(tri, use_groupid);
                if (tid < 0) {
                    //throw new Exception("todo");
                }
            }
        }
        protected void AppendMeshTriIndices_ushort(Span<ushort> indicesBuf, DMesh3 mesh, int[] MapV, int use_groupid)
        {
            Util.gDevAssert(indicesBuf.Length % 3 == 0);
            int NumT = indicesBuf.Length/3;
            for (int ti = 0; ti < NumT; ++ti) {
                Index3i tri = new Index3i((int)indicesBuf[3*ti], (int)indicesBuf[3*ti+1], (int)indicesBuf[3*ti+2]);
                tri.a = MapV[tri.a]; tri.b = MapV[tri.b]; tri.c = MapV[tri.c];      // todo handle invalid ids
                int tid = mesh.AppendTriangle(tri, use_groupid);
                if (tid < 0) {
                    //throw new Exception("todo");
                }
            }
        }

    }
}
