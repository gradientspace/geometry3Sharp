﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    /// <summary>
    /// Sweep a 2D Profile Polygon along a 3D Path.
    /// Supports closed and open paths, and capping open paths.
    /// However caps are triangulated using a fan around a center vertex (which you
    /// can set using CapCenter). If Polygon is non-convex, this will have foldovers.
    /// In that case, you have to triangulate and append it yourself.
    /// 
    /// If your profile curve does not contain the origin, use CapCenter.
    /// 
    /// The output normals are currently set to those for a circular profile.
    /// Call MeshNormals.QuickCompute() on the output DMesh to estimate proper
    /// vertex normals
    /// 
    /// </summary>
    public class TubeGenerator : MeshGenerator
    {
        public List<Vector3d> Vertices;
        public Polygon2d Polygon;

        public bool Capped = true;

        // center of endcap triangle fan, relative to Polygon
        public Vector2d CapCenter = Vector2d.Zero;

        public bool ClosedLoop = false;

        // [TODO] Frame3d ??
        public Frame3f Frame = Frame3f.Identity;

        // set to true if you are going to texture this or want sharp edges
        public bool NoSharedVertices = true;

        public int startCapCenterIndex = -1;
        public int endCapCenterIndex = -1;

        public bool WeighedUV = false;

        public TubeGenerator()
        {
        }

        public TubeGenerator(Polygon2d tubePath, Frame3f pathPlane, Polygon2d tubeShape, int nPlaneNormal = 2)
        {
            Vertices = new List<Vector3d>();
            foreach (Vector2d v in tubePath.Vertices)
                Vertices.Add(pathPlane.FromPlaneUV((Vector2f)v, nPlaneNormal));
            Polygon = new Polygon2d(tubeShape);
            ClosedLoop = true;
            Capped = false;
        }



        override public MeshGenerator Generate()
        {
            if (Polygon == null)
                Polygon = Polygon2d.MakeCircle(1.0f, 8);

            int Slices = Polygon.VertexCount;
            int nRings = Vertices.Count;
            int nRingSize = (NoSharedVertices) ? Slices + 1 : Slices;
            int nCapVertices = (NoSharedVertices) ? Slices + 1 : 1;
            if (Capped == false || ClosedLoop == true)
                nCapVertices = 0;

            vertices = new VectorArray3d(nRings * nRingSize + 2 * nCapVertices);
            uv = new VectorArray2f(vertices.Count);
            normals = new VectorArray3f(vertices.Count);

            int quad_strips = ClosedLoop ? (nRings) : (nRings-1);
            int nSpanTris = quad_strips * (2 * Slices);
            int nCapTris = (Capped && ClosedLoop == false) ? 2 * Slices : 0;
            triangles = new IndexArray3i(nSpanTris + nCapTris);

            Frame3f fCur = new Frame3f(Frame);
            Frame3f fNext = new Frame3f(Frame);
            Vector3d dv = CurveUtils.GetTangent(Vertices, 0); ;
            fCur.Origin = (Vector3f)Vertices[0];
            fCur.AlignAxis(2, (Vector3f)dv);
            Frame3f fStart = new Frame3f(fCur);

            float pathLength = WeighedUV ? (float)CurveUtils.ArcLength(Vertices) : 0;
            float running_uv_along = 0;

            // generate tube
            for (int ri = 0; ri < nRings; ++ri) {

                // propagate frame
                if (ri != 0) {
                    Vector3d tan = CurveUtils.GetTangent(Vertices, ri);
                    fCur.Origin = (Vector3f)Vertices[ri];
                    fCur.AlignAxis(2, (Vector3f)tan);
                }

                float uv_along = (float)ri / (float)(nRings - 1);

                // generate vertices
                int nStartR = ri * nRingSize;

                double circumference = 0;

                if (WeighedUV) {
                    // calculate ring circumference
                    for (int j = 0; j < nRingSize; ++j) {
                        Vector2d pv = Polygon.Vertices[j % Slices];
                        Vector2d pvNext = Polygon.Vertices[(j + 1) % Slices];
                        circumference += pv.Distance(pvNext);
                    }
                }

                float running_uv_around = 0;

                for (int j = 0; j < nRingSize; ++j) {
                    int k = nStartR + j;
                    Vector2d pv = Polygon.Vertices[j % Slices];
                    Vector2d pvNext = Polygon.Vertices[(j + 1) % Slices];
                    Vector3d v = fCur.FromPlaneUV((Vector2f)pv, 2);
                    vertices[k] = v;

                    if (WeighedUV) {
                        uv[k] = new Vector2f(running_uv_along, running_uv_around);
                        running_uv_around += (float)(pv.Distance(pvNext) / circumference);
                    } else {
                        float uv_around = (float)j / (float)(nRingSize);
                        uv[k] = new Vector2f(uv_along, uv_around);
                    }

                    Vector3f n = (Vector3f)(v - fCur.Origin).Normalized;
                    normals[k] = n;
                }

                if (WeighedUV) {
                    int riNext = (ri + 1) % nRings;
                    Vector3d tanNext = CurveUtils.GetTangent(Vertices, riNext);
                    fNext.Origin = (Vector3f)Vertices[riNext];
                    fNext.AlignAxis(2, (Vector3f)tanNext);

                    float d = fCur.Origin.Distance(fNext.Origin);
                    running_uv_along += d / pathLength;
                }
            }


            // generate triangles
            int ti = 0;
            int nStop = (ClosedLoop) ? nRings : (nRings - 1);
            for (int ri = 0; ri < nStop; ++ri) {
                int r0 = ri * nRingSize;
                int r1 = r0 + nRingSize;
                if (ClosedLoop && ri == nStop - 1)
                    r1 = 0;
                for (int k = 0; k < nRingSize - 1; ++k) {
                    triangles.Set(ti++, r0 + k, r0 + k + 1, r1 + k + 1, Clockwise);
                    triangles.Set(ti++, r0 + k, r1 + k + 1, r1 + k, Clockwise);
                }
                if (NoSharedVertices == false) {      // close disc if we went all the way
                    triangles.Set(ti++, r1 - 1, r0, r1, Clockwise);
                    triangles.Set(ti++, r1 - 1, r1, r1 + nRingSize - 1, Clockwise);
                }
            }

            if (Capped && ClosedLoop == false) {

                // add endcap verts
                int nBottomC = nRings * nRingSize;
                vertices[nBottomC] = fStart.FromPlaneUV((Vector2f)CapCenter,2);
                uv[nBottomC] = new Vector2f(0.5f, 0.5f);
                normals[nBottomC] = -fStart.Z;
                startCapCenterIndex = nBottomC;

                int nTopC = nBottomC + 1;
                vertices[nTopC] = fCur.FromPlaneUV((Vector2f)CapCenter, 2);
                uv[nTopC] = new Vector2f(0.5f, 0.5f);
                normals[nTopC] = fCur.Z;
                endCapCenterIndex = nTopC;

                if (NoSharedVertices) {
                    // duplicate first loop and make a fan w/ bottom-center
                    int nExistingB = 0;
                    int nStartB = nTopC + 1;
                    for (int k = 0; k < Slices; ++k) {
                        vertices[nStartB + k] = vertices[nExistingB + k];
                        uv[nStartB + k] = (Vector2f)Polygon.Vertices[k].Normalized;
                        normals[nStartB + k] = normals[nBottomC];
                    }
                    append_disc(Slices, nBottomC, nStartB, true, Clockwise, ref ti);

                    // duplicate second loop and make fan
                    int nExistingT = nRingSize * (nRings - 1);
                    int nStartT = nStartB + Slices;
                    for (int k = 0; k < Slices; ++k) {
                        vertices[nStartT + k] = vertices[nExistingT + k];
                        uv[nStartT + k] = (Vector2f)Polygon.Vertices[k].Normalized;
                        normals[nStartT + k] = normals[nTopC];
                    }
                    append_disc(Slices, nTopC, nStartT, true, !Clockwise, ref ti);

                } else {
                    append_disc(Slices, nBottomC, 0, true, Clockwise, ref ti);
                    append_disc(Slices, nTopC, nRingSize * (nRings-1), true, !Clockwise, ref ti);
                }
            }

            return this;
        }
    }
}