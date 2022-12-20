using System;
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
    /// If your profile curve does not contain the polygon bbox center, 
    /// set OverrideCapCenter=true and set CapCenter to a suitable center point.
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
        public bool OverrideCapCenter = false;
        public Vector2d CapCenter = Vector2d.Zero;

        public bool ClosedLoop = false;

        // [TODO] Frame3d ??
        public Frame3f Frame = Frame3f.Identity;

        // set to true if you are going to texture this or want sharp edges
        public bool NoSharedVertices = true;

        public int startCapCenterIndex = -1;
        public int endCapCenterIndex = -1;

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
        public TubeGenerator(PolyLine2d tubePath, Frame3f pathPlane, Polygon2d tubeShape, int nPlaneNormal = 2)
        {
            Vertices = new List<Vector3d>();
            foreach (Vector2d v in tubePath.Vertices)
                Vertices.Add(pathPlane.FromPlaneUV((Vector2f)v, nPlaneNormal));
            Polygon = new Polygon2d(tubeShape);
            ClosedLoop = false;
            Capped = true;
        }
        public TubeGenerator(DCurve3 tubePath, Polygon2d tubeShape)
        {
            Vertices = new List<Vector3d>(tubePath.Vertices);
            Polygon = new Polygon2d(tubeShape);
            ClosedLoop = tubePath.Closed;
            Capped = ! ClosedLoop;
        }



        override public MeshGenerator Generate()
        {
            if (Polygon == null)
                Polygon = Polygon2d.MakeCircle(1.0f, 8);

            int NV = Vertices.Count;
            int Slices = Polygon.VertexCount;
            int nRings = (ClosedLoop && NoSharedVertices) ? NV + 1 : NV;
            int nRingSize = (NoSharedVertices) ? Slices + 1 : Slices;
            int nCapVertices = (NoSharedVertices) ? Slices + 1 : 1;
            if (Capped == false || ClosedLoop == true)
                nCapVertices = 0;

            vertices = new VectorArray3d(nRings * nRingSize + 2 * nCapVertices);
            uv = new VectorArray2f(vertices.Count);
            normals = new VectorArray3f(vertices.Count);

            int quad_strips = (ClosedLoop) ? NV : NV-1;
            int nSpanTris = quad_strips * (2 * Slices);
            int nCapTris = (Capped && ClosedLoop == false) ? 2 * Slices : 0;
            triangles = new IndexArray3i(nSpanTris + nCapTris);

            Frame3f fCur = new Frame3f(Frame);
            Vector3d dv = CurveUtils.GetTangent(Vertices, 0, ClosedLoop);
            fCur.Origin = (Vector3f)Vertices[0];
            fCur.AlignAxis(2, (Vector3f)dv);
            Frame3f fStart = new Frame3f(fCur);

            double circumference = Polygon.ArcLength;
            double pathLength = CurveUtils.ArcLength(Vertices, ClosedLoop);
            double accum_path_u = 0;

            // generate tube
            for (int ri = 0; ri < nRings; ++ri) {
                int vi = ri % NV;

                // propagate frame
                Vector3d tangent = CurveUtils.GetTangent(Vertices, vi, ClosedLoop);
                fCur.Origin = (Vector3f)Vertices[vi];
                fCur.AlignAxis(2, (Vector3f)tangent);

                // generate vertices
                int nStartR = ri * nRingSize;

                double accum_ring_v = 0;
                for (int j = 0; j < nRingSize; ++j) {
                    int k = nStartR + j;
                    Vector2d pv = Polygon.Vertices[j % Slices];
                    Vector2d pvNext = Polygon.Vertices[(j + 1) % Slices];
                    Vector3d v = fCur.FromPlaneUV((Vector2f)pv, 2);
                    vertices[k] = v;

                    uv[k] = new Vector2f(accum_path_u, accum_ring_v);
                    accum_ring_v += (pv.Distance(pvNext) / circumference);

                    Vector3f n = (Vector3f)(v - fCur.Origin).Normalized;
                    normals[k] = n;
                }

                int viNext = (ri + 1) % NV;
                double d = Vertices[vi].Distance(Vertices[viNext]);
                accum_path_u += d / pathLength;
            }


            // generate triangles
            int ti = 0;
            int nStop = (ClosedLoop && NoSharedVertices == false) ? nRings : (nRings - 1);
            for (int ri = 0; ri < nStop; ++ri) {
                int r0 = ri * nRingSize;
                int r1 = r0 + nRingSize;
                if (ClosedLoop && ri == nStop - 1 && NoSharedVertices == false)
                    r1 = 0;
                for (int k = 0; k < nRingSize - 1; ++k) {
                    triangles.Set(ti++, r0 + k, r0 + k + 1, r1 + k + 1, Clockwise);
                    triangles.Set(ti++, r0 + k, r1 + k + 1, r1 + k, Clockwise);
                }
                if (NoSharedVertices == false) {      // last quad if we aren't sharing vertices
                    int M = nRingSize-1;
                    triangles.Set(ti++, r0 + M, r0, r1, Clockwise);
                    triangles.Set(ti++, r0 + M, r1, r1 + M, Clockwise);
                }
            }

            if (Capped && ClosedLoop == false) {
                Vector2d c = (OverrideCapCenter) ? CapCenter : Polygon.Bounds.Center;

                // add endcap verts
                int nBottomC = nRings * nRingSize;
                vertices[nBottomC] = fStart.FromPlaneUV((Vector2f)c,2);
                uv[nBottomC] = new Vector2f(0.5f, 0.5f);
                normals[nBottomC] = -fStart.Z;
                startCapCenterIndex = nBottomC;

                int nTopC = nBottomC + 1;
                vertices[nTopC] = fCur.FromPlaneUV((Vector2f)c, 2);
                uv[nTopC] = new Vector2f(0.5f, 0.5f);
                normals[nTopC] = fCur.Z;
                endCapCenterIndex = nTopC;

                if (NoSharedVertices) {
                    // duplicate first loop and make a fan w/ bottom-center
                    int nExistingB = 0;
                    int nStartB = nTopC + 1;
                    for (int k = 0; k < Slices; ++k) {
                        vertices[nStartB + k] = vertices[nExistingB + k];
                        Vector2d vuv = ((Polygon[k] - c).Normalized + Vector2d.One) * 0.5;
                        uv[nStartB + k] = (Vector2f)vuv;
                        normals[nStartB + k] = normals[nBottomC];
                    }
                    append_disc(Slices, nBottomC, nStartB, true, Clockwise, ref ti);

                    // duplicate second loop and make fan
                    int nExistingT = nRingSize * (nRings - 1);
                    int nStartT = nStartB + Slices;
                    for (int k = 0; k < Slices; ++k) {
                        vertices[nStartT + k] = vertices[nExistingT + k];
                        uv[nStartT + k] = uv[nStartB + k];
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