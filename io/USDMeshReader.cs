// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Distributed under the Boost Software License, Version 1.0. http://www.boost.org/LICENSE_1_0.txt
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using static g3.GLTFFile;
using static g3.USDFile;

#nullable enable

namespace g3
{
    public partial class USDMeshReader : IMeshReader
    {
        // connect to this to get warning messages
        public event ParsingMessagesHandler? warningEvent;

        public IOReadResult Read(BinaryReader reader, ReadOptions options, IMeshBuilder builder)
        {
            USDCReader usdReader = new USDCReader();
            usdReader.warningEvent += this.warningEvent;
            IOReadResult result = usdReader.Read(reader, options, out USDScene scene);
            if (! result.IsOk)
                return result;

            Matrix4d BaseTransform = Matrix4d.Identity;
            build_meshes(scene.Root, options, builder, BaseTransform);
            return IOReadResult.Ok;
        }

        public IOReadResult Read(TextReader reader, ReadOptions options, IMeshBuilder builder)
        {
            USDAReader usdReader = new USDAReader();
            usdReader.warningEvent += this.warningEvent;
            IOReadResult result = usdReader.Read(reader, options, out USDScene scene);
            if (!result.IsOk)
                return result;

            Matrix4d BaseTransform = Matrix4d.Identity;
            build_meshes(scene.Root, options, builder, BaseTransform);
            return IOReadResult.Ok;
        }

        protected void build_meshes(USDPrim prim, ReadOptions options, IMeshBuilder builder, Matrix4d ParentTransform)
        {
            Matrix4d CurTransform = ParentTransform;
            if (prim.PrimType == EDefType.Mesh) {
                USDAttrib? points = prim.FindAttribByName("points");
                USDAttrib? faceVertexCounts = prim.FindAttribByName("faceVertexCounts");
                USDAttrib? faceVertexIndices = prim.FindAttribByName("faceVertexIndices");
                USDAttrib? normals = prim.FindAttribByName("normals");
                USDAttrib? uvs = prim.FindAttribByName("primvars:st");
                if (uvs == null)
                    uvs = prim.FindAttribByName("primvars:UVMap");

                bool bHaveRequiredFields = true;
                if (points == null) { warningEvent?.Invoke($"Mesh {prim.FullPath} is missing points field", null); bHaveRequiredFields = false; }
                if (faceVertexCounts == null) { warningEvent?.Invoke($"Mesh {prim.FullPath} is missing faceVertexCounts field", null); bHaveRequiredFields = false; }
                if (faceVertexIndices == null) { warningEvent?.Invoke($"Mesh {prim.FullPath} is missing faceVertexIndices field", null); bHaveRequiredFields = false; }

                if (bHaveRequiredFields) {
                    DMesh3 Mesh = new DMesh3();
                    Mesh.EnableTriangleGroups();
                    AppendAsVertices(Mesh, prim, points!, CurTransform);
                    AppendAsTriangles(Mesh, prim, faceVertexCounts!, faceVertexIndices!, normals, uvs, CurTransform);
                    Mesh.CheckValidity();
                    builder.AppendNewMesh(Mesh);
                }
            }

            foreach (USDPrim childPrim in prim.Children)
                build_meshes(childPrim, options, builder, CurTransform);
        }


        protected bool AppendAsVertices(DMesh3 mesh, USDPrim prim, USDAttrib points, Matrix4d Transform)
        {
            if (points.USDType != EUSDType.Point3f || points.IsArray == false) {
                warningEvent?.Invoke($"Mesh field {prim.FullPath}.points has incorrect type {points.USDType}", null);
                return false;
            }
            if (points.Value.data is vec3f[] vectorList && vectorList.Length > 0) {
                for (int i = 0; i < vectorList.Length; ++i) {
                    Vector3d v = vectorList[i];
                    v = Transform.TransformPointAffine(v);
                    mesh.AppendVertex(v);
                }
                return true;
            } else {
                warningEvent?.Invoke($"Mesh field {prim.FullPath}.points data is invalid or 0-length", null);
                return false;
            }
        }


        protected bool AppendAsTriangles(DMesh3 mesh, USDPrim prim, USDAttrib vertexCounts, USDAttrib vertexIndices,
            USDAttrib? normals, USDAttrib? uvs, Matrix4d Transform)
        {
            if (vertexCounts.USDType != EUSDType.Int || vertexCounts.IsArray == false) {
                warningEvent?.Invoke($"Mesh field {prim.FullPath}.faceVertexCounts has incorrect type {vertexCounts.USDType}", null);
                return false;
            }
            if (vertexIndices.USDType != EUSDType.Int || vertexIndices.IsArray == false) {
                warningEvent?.Invoke($"Mesh field {prim.FullPath}.faceVertexIndices has incorrect type {vertexIndices.USDType}", null);
                return false;
            }

            int[]? countsList = vertexCounts.Value.data as int[];
            int[]? indexList = vertexIndices.Value.data as int[];
            if (countsList == null || countsList.Length == 0 ||
                indexList == null || indexList.Length == 0) {
                warningEvent?.Invoke($"Mesh field {prim.FullPath}.faceVertexCounts or .faceVertexIndices data is invalid or 0-length", null);
                return false;
            }

            Transform.GetAffineNormalTransform(out Matrix3d NormalTransform);
            vec3f[]? normalsList = normals?.Value.data as vec3f[] ?? null;
            bool bHaveNormals = (normalsList != null && normalsList.Length > 0 && normalsList.Length == indexList.Length);
            if (bHaveNormals)
                mesh.Attribs.EnableTriNormals();
            TriNormalsGeoAttribute? NormalsAttrib = (bHaveNormals) ? mesh.Attribs.TriNormals : null;

            vec2f[]? uvList = uvs?.Value.data as vec2f[] ?? null;
            bool bHaveUVs = (uvList != null && uvList.Length > 0 && uvList.Length == indexList.Length);
            if (bHaveUVs)
                mesh.Attribs.EnableTriUVs(1);
            TriUVsGeoAttribute? UVsAttrib = (bHaveUVs) ? mesh.Attribs.TriUVChannel(0) : null;

            int cur_idx = 0;
            for (int i = 0; i < countsList.Length; ++i) {
                int count = countsList[i];

                int start_idx = cur_idx;
                int a = indexList[cur_idx];
                int b = indexList[cur_idx + 1];

                Vector3f na = Vector3f.UnitZ, nb = Vector3f.UnitZ, nc = Vector3f.UnitZ;
                if (bHaveNormals) {
                    na = NormalTransform * normalsList![cur_idx];
                    nb = NormalTransform * normalsList[cur_idx+1];
                }

                Vector2f uva = Vector2f.Zero, uvb = Vector2f.Zero, uvc = Vector2f.Zero;
                if (bHaveUVs) {
                    uva = uvList![cur_idx];
                    uvb = uvList[cur_idx+1];
                }

                for (int k = 2; k < count; ++k) {
                    int c = indexList[cur_idx + k];
                    int tid = mesh.AppendTriangle(new Index3i(a, b, c), i);
                    if (tid < 0) {
                        string errType = (tid == DMesh3.NonManifoldID) ? "nonmanifold" : "invalid";
                        warningEvent?.Invoke($"triangle ({a},{b},{c}) is {errType} - skipping", null);
                    }
                    b = c;

                    if (tid >= 0 && bHaveNormals) {
                        nc = NormalTransform * normalsList![cur_idx+k];
                        NormalsAttrib!.SetValue(tid, new TriNormals(na, nb, nc));
                        nb = nc;
                    }
                    if (tid >= 0 && bHaveUVs) {
                        uvc = uvList![cur_idx+k];
                        UVsAttrib!.SetValue(tid, new TriUVs(uva, uvb, uvc));
                        uvb = uvc;
                    }
                }

                cur_idx += count;
            }

            return true;
        }


    }
}