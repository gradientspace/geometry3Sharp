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

        // [TODO] Frame3d ??
        public Frame3f Frame = Frame3f.Identity;

        // set to true if you are going to texture this or want sharp edges
        public bool NoSharedVertices = true;

        public int startCapCenterIndex = -1;
        public int endCapCenterIndex = -1;

        override public void Generate()
        {
            if (Polygon == null)
                Polygon = Polygon2d.MakeCircle(1.0f, 8);

            int Slices = Polygon.VertexCount;
            int nRings = Vertices.Count;
            int nRingSize = (NoSharedVertices) ? Slices + 1 : Slices;
            int nCapVertices = (NoSharedVertices) ? Slices + 1 : 1;
            if (Capped == false)
                nCapVertices = 0;

            vertices = new VectorArray3d(nRings * nRingSize + 2 * nCapVertices);
            uv = new VectorArray2f(vertices.Count);
            normals = new VectorArray3f(vertices.Count);

            int nSpanTris = (Vertices.Count - 1) * (2 * Slices);
            int nCapTris = (Capped) ? 2 * Slices : 0;
            triangles = new VectorArray3i(nSpanTris + nCapTris);

            Frame3f fCur = new Frame3f(Frame);
            Vector3d dv = CurveUtils.GetTangent(Vertices, 0); ;
            fCur.Origin = (Vector3f)Vertices[0];
            fCur.AlignAxis(2, (Vector3f)dv);

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
                    Vector3d v = fCur.FromFrameP((Vector2f)pv, 2);
                    vertices[k] = v;
                    uv[k] = new Vector2f(uv_along, uv_around);
                    Vector3f n = (Vector3f)(v - fCur.Origin).Normalized;
                    normals[k] = n;
                }
            }


            // generate triangles
            int ti = 0;
            for (int ri = 0; ri < nRings-1; ++ri) {
                int r0 = ri * nRingSize;
                int r1 = r0 + nRingSize;
                for (int k = 0; k < nRingSize - 1; ++k) {
                    triangles.Set(ti++, r0 + k, r0 + k + 1, r1 + k + 1, Clockwise);
                    triangles.Set(ti++, r0 + k, r1 + k + 1, r1 + k, Clockwise);
                }
                if (NoSharedVertices == false) {      // close disc if we went all the way
                    triangles.Set(ti++, r1 - 1, r0, r1, Clockwise);
                    triangles.Set(ti++, r1 - 1, r1, r1 + nRingSize - 1, Clockwise);
                }
            }


/*
            if (Capped) {
                // add endcap verts
                var s0 = Sections[0];
                var sN = Sections.Last();
                int nBottomC = nRings * nRingSize;
                vertices[nBottomC] = new Vector3d(0, s0.SectionY, 0);
                uv[nBottomC] = new Vector2f(0.5f, 0.5f);
                normals[nBottomC] = new Vector3f(0, -1, 0);
                startCapCenterIndex = nBottomC;

                int nTopC = nBottomC + 1;
                vertices[nTopC] = new Vector3d(0, sN.SectionY, 0);
                uv[nTopC] = new Vector2f(0.5f, 0.5f);
                normals[nTopC] = new Vector3f(0, 1, 0);
                endCapCenterIndex = nTopC;

                if (NoSharedVertices) {
                    int nStartB = nTopC + 1;
                    for (int k = 0; k < Slices; ++k) {
                        float a = (float)k * fDelta;
                        double cosa = Math.Cos(a), sina = Math.Sin(a);
                        vertices[nStartB + k] = new Vector3d(s0.Radius * cosa, s0.SectionY, s0.Radius * sina);
                        uv[nStartB + k] = new Vector2f(0.5f * (1.0f + cosa), 0.5f * (1 + sina));
                        normals[nStartB + k] = -Vector3f.AxisY;
                    }
                    append_disc(Slices, nBottomC, nStartB, true, Clockwise, ref ti);

                    int nStartT = nStartB + Slices;
                    for (int k = 0; k < Slices; ++k) {
                        float a = (float)k * fDelta;
                        double cosa = Math.Cos(a), sina = Math.Sin(a);
                        vertices[nStartT + k] = new Vector3d(sN.Radius * cosa, sN.SectionY, sN.Radius * sina);
                        uv[nStartT + k] = new Vector2f(0.5f * (1.0f + cosa), 0.5f * (1 + sina));
                        normals[nStartT + k] = Vector3f.AxisY;
                    }
                    append_disc(Slices, nTopC, nStartT, true, !Clockwise, ref ti);

                } else {
                    append_disc(Slices, nBottomC, 0, true, Clockwise, ref ti);
                    append_disc(Slices, nTopC, nRingSize * (Sections.Length - 1), true, !Clockwise, ref ti);
                }
            }
            */

        }
    }
}
