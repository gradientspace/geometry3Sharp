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

        static readonly string[] defaultTransformOrder = [];

        protected void build_meshes(USDPrim prim, ReadOptions options, IMeshBuilder builder, Matrix4d ParentTransform)
        {
            Matrix4d CurTransform = ParentTransform;

            // look for uniform token[] xformOpOrder = ["xformOp:transform", ...]
            ReadOnlySpan<string> transformOps = defaultTransformOrder;
            USDAttrib? xformOpOrder = prim.FindAttribByName("xformOpOrder");
            if (xformOpOrder != null && xformOpOrder.USDType == EUSDType.Token && xformOpOrder.IsArray) {
                if ( xformOpOrder.Data is string[] order ) 
                    transformOps = order;
            }

            //if (prim.Path.FullPath.Contains("Crate"))
            //    Debugger.Break();

            // now find the referenced transforms and accumulate them
            foreach (string op in transformOps) {
                USDAttrib? xformOp = null;
                bool bInvert = false;
                if (op.StartsWith("!invert!")) {
                    bInvert = true;
                    xformOp = prim.FindAttribByName(op.Substring(8));
                } else
                    xformOp = prim.FindAttribByName(op);
                if (xformOp != null) {
                    Matrix4d xform = ExtractTransform(xformOp, bInvert);
                    CurTransform = CurTransform * xform;
                } else
                    warningEvent?.Invoke($"Could not find xformOp {op} in {prim}", null);
            }


            if (prim.PrimType == EDefType.Mesh) 
            {
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
                    Matrix4d MeshTransform = CurTransform;
                    //Matrix4d MeshTransform = Matrix4d.Identity;
                    AppendAsVertices(Mesh, prim, points!, MeshTransform);
                    BuildMeshGroupConfig groupConfig = new BuildMeshGroupConfig(options.GroupMode, builder.NumMeshes);
                    AppendAsTriangles(Mesh, prim, faceVertexCounts!, faceVertexIndices!, normals, uvs, MeshTransform, groupConfig);
                    Mesh.CheckValidity();
                    builder.AppendNewMesh(Mesh);
                }
            }

            foreach (USDPrim childPrim in prim.Children)
                build_meshes(childPrim, options, builder, CurTransform);
        }


        protected Matrix4d ExtractTransform(USDAttrib xformOp, bool bInvert)
        {
            if (xformOp.USDType == EUSDType.Matrix4d && xformOp.IsArray == false) {
                if (xformOp.Value.data is matrix4d m) {
                    // note: USD has a very bizarre definition of "row-order", because they assume
                    // vector pre-multiplication v*M instead of M*v, so the "rows" are actually columns
                    // https://openusd.org/dev/api/usd_geom_page_front.html#UsdGeom_LinAlgBasics
                    Matrix4d mat = new Matrix4d(m.row0, m.row1, m.row2, m.row3, false);
                    if (bInvert)
                        mat = mat.Inverse();
                    return mat;
                } else
                    warningEvent?.Invoke($"Invalid Matrix4d transform data in {xformOp}", null);
            }
            else if (xformOp.Value.data is vec3d || xformOp.Value.data is vec3f) 
            {
                Vector3d xyz = Vector3d.Zero;
                if (xformOp.Value.data is vec3d)
                    xyz = ((vec3d)xformOp.Value.data);
                else if (xformOp.Value.data is vec3f)
                    xyz = ((vec3f)xformOp.Value.data);
                else
                    warningEvent?.Invoke($"Invalid Vector3d/f transform data in {xformOp}", null);

                if (xformOp.Name == "xformOp:translate") {
                    return Matrix4d.Translation((bInvert) ? -xyz : xyz);
                } else if (xformOp.Name == "xformOp:translate:pivot") {
                    // what to do here?? is it just a translation? do we need to track a pivot-point ?
                    return Matrix4d.Translation((bInvert) ? -xyz : xyz);
                } else if (xformOp.Name == "xformOp:scale") {
                    if (xyz.LengthSquared == 0)
                        xyz = Vector3d.One;
                    if (bInvert)
                        xyz = new Vector3d(1.0 / xyz.x, 1.0 / xyz.y, 1.0 / xyz.z);  // todo safe inverse scale!
                    return Matrix4d.Scale(xyz);
                } else if (xformOp.Name == "xformOp:rotateXYZ") {
                    Matrix3d rot = Matrix3d.RotateX(xyz.x, true) * Matrix3d.RotateY(xyz.y, true) * Matrix3d.RotateZ(xyz.z, true);
                    rot = rot.Transpose();
                    return new Matrix4d(rot, Vector3d.Zero);
                } else {
                    warningEvent?.Invoke($"Unsupported xformOp type {xformOp}", null);
                }
            }

            return Matrix4d.Identity;
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
            USDAttrib? normals, USDAttrib? uvs, Matrix4d Transform,
            BuildMeshGroupConfig groupConfig)
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
                int UseGroupID = (groupConfig.Mode == EBuildMeshGroupMode.GroupPerPolygon) ? i : groupConfig.ConstantGroupID;

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
                    int tid = mesh.AppendTriangle(new Index3i(a, b, c), UseGroupID);
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