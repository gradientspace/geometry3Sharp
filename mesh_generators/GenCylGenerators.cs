using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    public class TubeGenerator : MeshGenerator
    {
        public List<Vector3d> Vertices;
        public Polygon2d Polygon;
        public bool Capped = true;
        public bool ClosedLoop = false;

        // [TODO] Frame3d ??
        public Frame3f Frame = Frame3f.Identity;

        // set to true if you are going to texture this or want sharp edges
        public bool NoSharedVertices = true;

        public int startCapCenterIndex = -1;
        public int endCapCenterIndex = -1;

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
            Vector3d dv = CurveUtils.GetTangent(Vertices, 0); ;
            fCur.Origin = (Vector3f)Vertices[0];
            fCur.AlignAxis(2, (Vector3f)dv);
            Frame3f fStart = new Frame3f(fCur);

            // generate tube
            for (int ri = 0; ri < nRings; ++ri) {

                // propagate frame
                if (ri != 0) {
                    Vector3d tan = CurveUtils.GetTangent(Vertices, ri);
                    fCur.Origin = (Vector3f)Vertices[ri];
                    if (ri == 11)
                        dv = tan;
                    fCur.AlignAxis(2, (Vector3f)tan);
                }

                float uv_along = (float)ri / (float)(nRings - 1);

                // generate vertices
                int nStartR = ri * nRingSize;
                for (int j = 0; j < nRingSize; ++j) {
                    float uv_around = (float)j / (float)(nRings);

                    int k = nStartR + j;
                    Vector2d pv = Polygon.Vertices[j % Slices];
                    Vector3d v = fCur.FromPlaneUV((Vector2f)pv, 2);
                    vertices[k] = v;
                    uv[k] = new Vector2f(uv_along, uv_around);
                    Vector3f n = (Vector3f)(v - fCur.Origin).Normalized;
                    normals[k] = n;
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
                vertices[nBottomC] = fStart.Origin;
                uv[nBottomC] = new Vector2f(0.5f, 0.5f);
                normals[nBottomC] = -fStart.Z;
                startCapCenterIndex = nBottomC;

                int nTopC = nBottomC + 1;
                vertices[nTopC] = fCur.Origin;
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
