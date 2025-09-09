// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Distributed under the Boost Software License, Version 1.0. http://www.boost.org/LICENSE_1_0.txt
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

#nullable enable

namespace g3
{

    public class GLTFWriter : IMeshWriter
    {
        // stream-opener. Override to write to something other than a file.
        public Func<string, Stream> OpenStreamF = (sFilename) => {
            return File.Open(sFilename, FileMode.Create);
        };
        public Action<Stream> CloseStreamF = (stream) => {
            stream.Dispose();
        };

        public bool bWritingGLB = false;

        public IOWriteResult Write(TextWriter writer, List<WriteMesh> vMeshes, WriteOptions options)
        {
            string basePath = options.WritePath;
            string baseFileName = Path.GetFileNameWithoutExtension(options.WriteFilename);
            if (baseFileName.Length == 0) {
                baseFileName = "gltf_data";     // should warn here?
            }

            GLTFFile.Root root = new GLTFFile.Root();

            root.asset = new GLTFFile.Asset() { generator = "geometry3Sharp: g3.GLTFWriter" };

            List<GLTFFile.Buffer> buffers = new();
            List<GLTFFile.BufferView> views = new();
            List<GLTFFile.Accessor> accessors = new();
            List<GLTFFile.Mesh> meshes = new();
            List<GLTFFile.Node> nodes = new();
            List<int> topLevelNodes = new();

            MemoryStream binStream = new MemoryStream();
            BinaryWriter binWriter = new BinaryWriter(binStream);
            int CurOffset = 0;
            var addbuffer_func = (Span<byte> buffer) => {
                int NumBytes = buffer.Length;

                GLTFFile.BufferView bufferView = new GLTFFile.BufferView();
                bufferView.buffer = 0;
                bufferView.byteLength = NumBytes;
                bufferView.byteOffset = CurOffset;

                binWriter.Write(buffer);
                CurOffset += NumBytes;

                return bufferView;
            };

            foreach (WriteMesh writeMesh in vMeshes) 
            {
                DMesh3 Mesh = (writeMesh.Mesh as DMesh3)!;
                if (Mesh.IsCompact == false)
                    Mesh = new DMesh3(Mesh, true);

                float[] vertices = new float[Mesh.VertexCount*3];
                Vector3f Max = Vector3f.MinValue;
                Vector3f Min = Vector3f.MaxValue;
                for (int i = 0; i < Mesh.VertexCount; ++i) {
                    Vector3f v = (Vector3f)Mesh.GetVertex(i);
                    vertices[3*i] = v.x; vertices[3*i+1] = v.y; vertices[3*i+2] = v.z;
                    Min.x = Math.Min(v.x, Min.x); Min.y = Math.Min(v.y, Min.y); Min.z = Math.Min(v.z, Min.z);
                    Max.x = Math.Max(v.x, Max.x); Max.y = Math.Max(v.y, Max.y); Max.z = Math.Max(v.z, Max.z);
                }

                float[]? normals = null;
                if (Mesh.HasVertexNormals) {
                    normals = new float[Mesh.VertexCount*3];
                    for (int i = 0; i < Mesh.VertexCount; ++i) {
                        Vector3f n = Mesh.GetVertexNormal(i);
                        normals[3*i] = n.x; normals[3*i+1] = n.y; normals[3*i+2] = n.z;
                    }
                }

                float[]? uvs = null;
                if (Mesh.HasVertexUVs) {
                    uvs = new float[Mesh.VertexCount*2];
                    for (int i = 0; i < Mesh.VertexCount; ++i) {
                        Vector2f uv = Mesh.GetVertexUV(i);
                        uvs[2*i] = uv.x; uvs[2*i+1] = uv.y;
                    }
                }

                uint[] triangles = new uint[Mesh.TriangleCount*3];
                for (int i = 0; i < Mesh.TriangleCount; ++i) {
                    Index3i t = Mesh.GetTriangle(i);
                    triangles[3*i] = (uint)t.a; triangles[3*i+1] = (uint)t.b; triangles[3*i+2] = (uint)t.c;
                }

                GLTFFile.BufferView verticesView = addbuffer_func(MemoryMarshal.Cast<float, byte>(vertices));
                verticesView.target = (int)GLTFFile.EBufferViewTarget.ARRAY_BUFFER;
                int vertsBufferIdx = views.Count; views.Add(verticesView);
                GLTFFile.Accessor verticesAccessor = new GLTFFile.Accessor();
                verticesAccessor.bufferView = vertsBufferIdx;
                verticesAccessor.ComponentTypeEnum = GLTFFile.EComponentType.Float;
                verticesAccessor.count = vertices.Length/3;
                verticesAccessor.type = GLTFFile.GetElementTypeString(GLTFFile.EElementType.Vec3);
                verticesAccessor.min = [Min.x, Min.y, Min.z];
                verticesAccessor.max = [Max.x, Max.y, Max.z];
                int vertsAccessorIdx = accessors.Count; accessors.Add(verticesAccessor);

                int normalsAccessorIdx = -1;
                if (normals != null) {
                    GLTFFile.BufferView normalsView = addbuffer_func(MemoryMarshal.Cast<float, byte>(normals));
                    normalsView.target = (int)GLTFFile.EBufferViewTarget.ARRAY_BUFFER;
                    int normalsBufferIdx = views.Count; views.Add(normalsView);
                    GLTFFile.Accessor normalsAccessor = new GLTFFile.Accessor();
                    normalsAccessor.bufferView = normalsBufferIdx;
                    normalsAccessor.ComponentTypeEnum = GLTFFile.EComponentType.Float;
                    normalsAccessor.count = verticesAccessor.count;
                    normalsAccessor.type = GLTFFile.GetElementTypeString(GLTFFile.EElementType.Vec3);
                    normalsAccessorIdx = accessors.Count; accessors.Add(normalsAccessor);
                }

                int uv0AccessorIdx = -1;
                if (uvs != null) {
                    GLTFFile.BufferView uv0View = addbuffer_func(MemoryMarshal.Cast<float, byte>(uvs));
                    uv0View.target = (int)GLTFFile.EBufferViewTarget.ARRAY_BUFFER;
                    int uv0BufferIdx = views.Count; views.Add(uv0View);
                    GLTFFile.Accessor uv0Accessor = new GLTFFile.Accessor();
                    uv0Accessor.bufferView = uv0BufferIdx;
                    uv0Accessor.ComponentTypeEnum = GLTFFile.EComponentType.Float;
                    uv0Accessor.count = verticesAccessor.count;
                    uv0Accessor.type = GLTFFile.GetElementTypeString(GLTFFile.EElementType.Vec2);
                    uv0AccessorIdx = accessors.Count; accessors.Add(uv0Accessor);
                }

                GLTFFile.BufferView trianglesView = addbuffer_func(MemoryMarshal.Cast<uint, byte>(triangles));
                trianglesView.target = (int)GLTFFile.EBufferViewTarget.ELEMENT_ARRAY_BUFFER;
                int trisBufferIdx = views.Count; views.Add(trianglesView);
                GLTFFile.Accessor trianglesAccessor = new GLTFFile.Accessor();
                trianglesAccessor.bufferView = trisBufferIdx;
                trianglesAccessor.ComponentTypeEnum = GLTFFile.EComponentType.UnsignedInt;
                trianglesAccessor.count = triangles.Length;
                trianglesAccessor.type = GLTFFile.GetElementTypeString(GLTFFile.EElementType.Scalar);
                int trisAccessorIdx = accessors.Count; accessors.Add(trianglesAccessor);

                GLTFFile.Mesh mesh = new GLTFFile.Mesh();
                if ( writeMesh.Name.Length > 0 )
                    mesh.name = writeMesh.Name;
                mesh.primitives = [new()];
                mesh.primitives[0].indices = trisAccessorIdx;
                mesh.primitives[0].attributes = new();
                mesh.primitives[0].attributes!.Add(GLTFFile.MeshAttribute_Position, vertsAccessorIdx);
                if (normalsAccessorIdx >= 0)
                    mesh.primitives[0].attributes!.Add(GLTFFile.MeshAttribute_Normal, normalsAccessorIdx);
                if (uv0AccessorIdx >= 0)
                    mesh.primitives[0].attributes!.Add(GLTFFile.MeshAttribute_UVPrefix+"0", uv0AccessorIdx);

                int meshIdx = meshes.Count; meshes.Add(mesh);

                GLTFFile.Node node = new GLTFFile.Node();
                node.name = mesh.name;
                node.mesh = meshIdx;
                int nodeIdx = nodes.Count; nodes.Add(node);
                topLevelNodes.Add(nodeIdx);
            }

            root.scene = 0;
            root.scenes = [new()];
            root.scenes[0].nodes = topLevelNodes.ToArray();

            root.nodes = nodes.ToArray();
            root.meshes = meshes.ToArray();
            root.accessors = accessors.ToArray();
            root.bufferViews = views.ToArray();

            string binFileName = Path.Combine(basePath, baseFileName + ".bin");
            long TotalNumBytes = binStream.Length;

            Stream writeBinaryStream = OpenStreamF(binFileName);
            binStream.Position = 0;
            binStream.CopyTo(writeBinaryStream);
            CloseStreamF(writeBinaryStream);

            binWriter.Close();

            root.buffers = [new()];
            if (bWritingGLB == false)           // in GLB version we don't have a filename here
                root.buffers[0].uri = binFileName;
            root.buffers[0].byteLength = (int)TotalNumBytes;

            JsonSerializerOptions jsonOptions = new JsonSerializerOptions();
            jsonOptions.WriteIndented = true;
            jsonOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault;
            
            string json = JsonSerializer.Serialize<GLTFFile.Root>(root, jsonOptions);
            writer.Write(json);

            return IOWriteResult.Ok;
        }

        public IOWriteResult Write(BinaryWriter writer, List<WriteMesh> vMeshes, WriteOptions options)
        {
            throw new NotImplementedException();
        }
    }
}
