using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    // generate a cylinder 
    public class OpenCylinderGenerator : MeshGenerator
    {
        public float BaseRadius = 1.0f;
        public float TopRadius = 1.0f;
        public float Height = 1.0f;
        public float StartAngleDeg = 0.0f;
        public float EndAngleDeg = 360.0f;
        public int Slices = 16;

        // set to true if you are going to texture this cylinder, otherwise
        // last panel will not have UVs going from 1 to 0
        public bool NoSharedVertices = false;

        override public MeshGenerator Generate()
        {
            bool bClosed = ((EndAngleDeg - StartAngleDeg) > 359.99f);
            int nRingSize = (NoSharedVertices && bClosed) ? Slices + 1 : Slices;
            vertices = new VectorArray3d(2 * nRingSize);
            uv = new VectorArray2f(vertices.Count);
            normals = new VectorArray3f(vertices.Count);
            triangles = new IndexArray3i(2 * Slices);

            float fTotalRange = (EndAngleDeg - StartAngleDeg) * MathUtil.Deg2Radf;
            float fStartRad = StartAngleDeg * MathUtil.Deg2Radf;
            float fDelta = (bClosed) ? fTotalRange / Slices : fTotalRange / (Slices - 1);
            for (int k = 0; k < nRingSize; ++k) {
                float angle = fStartRad + (float)k * fDelta;
                double cosa = Math.Cos(angle), sina = Math.Sin(angle);
                vertices[k] = new Vector3d(BaseRadius * cosa, 0, BaseRadius * sina);
                vertices[nRingSize + k] = new Vector3d(TopRadius * cosa, Height, TopRadius * sina);
                float t = (float)k / (float)Slices;
                uv[k] = new Vector2f(t, 0.0f);
                uv[nRingSize + k] = new Vector2f(t, 1.0f);
                Vector3f n = new Vector3f((float)cosa, 0, (float)sina);
                n.Normalize();
                normals[k] = normals[nRingSize + k] = n;
            }

            int ti = 0;
            for (int k = 0; k < nRingSize - 1; ++k) {
                triangles.Set(ti++, k, k + 1, nRingSize + k + 1, Clockwise);
                triangles.Set(ti++, k, nRingSize + k + 1, nRingSize + k, Clockwise);
            }
            if (bClosed && NoSharedVertices == false) {      // close disc if we went all the way
                triangles.Set(ti++, nRingSize - 1, 0, nRingSize, Clockwise);
                triangles.Set(ti++, nRingSize - 1, nRingSize, 2 * nRingSize - 1, Clockwise);
            }

            return this;
        }
    }




    /// <summary>
    /// Generate a Cylinder with caps. Supports sections of cylinder as well (eg wedges).
    /// Curently UV islands are overlapping for different mesh components, if NoSharedVertices
    /// Positioned along Y axis such that base-center is at Origin, and top is at Y=Height
    /// You get a cone unless BaseRadius = TopRadius
    /// No subdivisions along top/base rings or height steps.
    /// cylinder triangles have groupid = 1, top cap = 2, bottom cap = 3, wedge faces 5 and 6
    /// </summary>
    public class CappedCylinderGenerator : MeshGenerator
    {
        public float BaseRadius = 1.0f;
        public float TopRadius = 1.0f;
        public float Height = 1.0f;
        public float StartAngleDeg = 0.0f;
        public float EndAngleDeg = 360.0f;
        public int Slices = 16;

        // set to true if you are going to texture this cylinder or want sharp edges
        public bool NoSharedVertices = false;

        override public MeshGenerator Generate()
        {
            bool bClosed = ((EndAngleDeg - StartAngleDeg) > 359.99f);
            int nRingSize = (NoSharedVertices && bClosed) ? Slices + 1 : Slices;
            int nCapVertices = (NoSharedVertices) ? Slices + 1 : 1;
            int nFaceVertices = (NoSharedVertices && bClosed == false) ? 8 : 0;
            vertices = new VectorArray3d(2 * nRingSize + 2* nCapVertices + nFaceVertices);
            uv = new VectorArray2f(vertices.Count);
            normals = new VectorArray3f(vertices.Count);

            int nCylTris = 2 * Slices;
            int nCapTris = 2 * Slices;
            int nFaceTris = (bClosed == false) ? 4 : 0;
            triangles = new IndexArray3i(nCylTris + nCapTris + nFaceTris);
            groups = new int[triangles.Count];

            float fTotalRange = (EndAngleDeg - StartAngleDeg) * MathUtil.Deg2Radf;
            float fStartRad = StartAngleDeg * MathUtil.Deg2Radf;
            float fDelta = (bClosed) ? fTotalRange / Slices : fTotalRange / (Slices - 1);

            // generate top and bottom rings for cylinder
            for (int k = 0; k < nRingSize; ++k) {
                float angle = fStartRad + (float)k * fDelta;
                double cosa = Math.Cos(angle), sina = Math.Sin(angle);
                vertices[k] = new Vector3d(BaseRadius * cosa, 0, BaseRadius * sina);
                vertices[nRingSize + k] = new Vector3d(TopRadius * cosa, Height, TopRadius * sina);
                float t = (float)k / (float)Slices;
                uv[k] = new Vector2f(t, 0.0f);
                uv[nRingSize + k] = new Vector2f(t, 1.0f);
                Vector3f n = new Vector3f((float)cosa, 0, (float)sina);
                n.Normalize();
                normals[k] = normals[nRingSize + k] = n;
            }

            // generate cylinder panels
            int ti = 0;
            for (int k = 0; k < nRingSize - 1; ++k) {
                groups[ti] = 1;
                triangles.Set(ti++, k, k + 1, nRingSize + k + 1, Clockwise);
                groups[ti] = 1;
                triangles.Set(ti++, k, nRingSize + k + 1, nRingSize + k, Clockwise);
            }
            if (bClosed && NoSharedVertices == false) {      // close disc if we went all the way
                groups[ti] = 1;
                triangles.Set(ti++, nRingSize - 1, 0, nRingSize, Clockwise);
                groups[ti] = 1;
                triangles.Set(ti++, nRingSize - 1, nRingSize, 2 * nRingSize - 1, Clockwise);
            }

            int nBottomC = 2 * nRingSize;
            vertices[nBottomC] = new Vector3d(0, 0, 0);
            uv[nBottomC] = new Vector2f(0.5f, 0.5f);
            normals[nBottomC] = new Vector3f(0, -1, 0);

            int nTopC = 2 * nRingSize + 1;
            vertices[nTopC] = new Vector3d(0, Height, 0);
            uv[nTopC] = new Vector2f(0.5f, 0.5f);
            normals[nTopC] = new Vector3f(0, 1, 0);

            if (NoSharedVertices) {
                int nStartB = 2 * nRingSize + 2;
                for (int k = 0; k < Slices; ++k) {
                    float a = fStartRad + (float)k * fDelta;
                    double cosa = Math.Cos(a), sina = Math.Sin(a);
                    vertices[nStartB + k] = new Vector3d(BaseRadius * cosa, 0, BaseRadius * sina);
                    uv[nStartB + k] = new Vector2f(0.5f * (1.0f + cosa), 0.5f * (1 + sina));
                    normals[nStartB + k] = -Vector3f.AxisY;
                }
                append_disc(Slices, nBottomC, nStartB, bClosed, Clockwise, ref ti, 2);

                int nStartT = 2 * nRingSize + 2 + Slices;
                for (int k = 0; k < Slices; ++k) {
                    float a = fStartRad + (float)k * fDelta;
                    double cosa = Math.Cos(a), sina = Math.Sin(a);
                    vertices[nStartT + k] = new Vector3d(TopRadius * cosa, Height, TopRadius * sina);
                    uv[nStartT + k] = new Vector2f(0.5f * (1.0f + cosa), 0.5f * (1 + sina));
                    normals[nStartT + k] = Vector3f.AxisY;
                }
                append_disc(Slices, nTopC, nStartT, bClosed, !Clockwise, ref ti, 3);

                // ugh this is very ugly but hard to see the pattern...
                if (bClosed == false) {
                    int nStartF = 2 * nRingSize + 2 + 2 * Slices;
                    vertices[nStartF] = vertices[nStartF + 5] = vertices[nBottomC];
                    vertices[nStartF + 1] = vertices[nStartF + 4] = vertices[nTopC];
                    vertices[nStartF + 2] = vertices[nRingSize];
                    vertices[nStartF + 3] = vertices[0];
                    vertices[nStartF + 6] = vertices[nRingSize - 1];
                    vertices[nStartF + 7] = vertices[2 * nRingSize - 1];
                    normals[nStartF] = normals[nStartF + 1] = normals[nStartF + 2] = normals[nStartF + 3]
                        = estimate_normal(nStartF, nStartF + 1, nStartF + 2);
                    normals[nStartF + 4] = normals[nStartF + 5] = normals[nStartF + 6] = normals[nStartF + 7]
                        = estimate_normal(nStartF + 4, nStartF + 5, nStartF + 6);

                    uv[nStartF] = uv[nStartF + 5] = new Vector2f(0, 0);
                    uv[nStartF + 1] = uv[nStartF + 4] = new Vector2f(0, 1);
                    uv[nStartF + 2] = uv[nStartF + 7] = new Vector2f(1, 1);
                    uv[nStartF + 3] = uv[nStartF + 6] = new Vector2f(1, 0);

                    append_rectangle(nStartF + 0, nStartF + 1, nStartF + 2, nStartF + 3, !Clockwise, ref ti, 4);
                    append_rectangle(nStartF + 4, nStartF + 5, nStartF + 6, nStartF + 7, !Clockwise, ref ti, 5);
                }

            } else {
                append_disc(Slices, nBottomC, 0, bClosed, Clockwise, ref ti, 2);
                append_disc(Slices, nTopC, nRingSize, bClosed, !Clockwise, ref ti, 3);
                if (bClosed == false) {
                    append_rectangle(nBottomC, 0, nRingSize, nTopC, Clockwise, ref ti, 4);
                    append_rectangle(nRingSize - 1, nBottomC, nTopC, 2 * nRingSize - 1, Clockwise, ref ti, 5);
                }
            }

            return this;
        }
    }




    // Generate a cone with base caps. Supports sections of cone as well (eg wedges).
    // Curently UV islands are overlapping for different mesh components, if NoSharedVertices
    // Also, if NoSharedVertices, then the 'tip' vertex is duplicated Slices times.
    // This causes the normals to look...weird.
    // For the conical region, we use the planar disc parameterization (ie tip at .5,.5) rather than
    // a cylinder-like projection
    public class ConeGenerator : MeshGenerator
    {
        public float BaseRadius = 1.0f;
        public float Height = 1.0f;
        public float StartAngleDeg = 0.0f;
        public float EndAngleDeg = 360.0f;
        public int Slices = 16;

        // set to true if you are going to texture this cone or want sharp edges
        public bool NoSharedVertices = false;


        override public MeshGenerator Generate()
        {
            bool bClosed = ((EndAngleDeg - StartAngleDeg) > 359.99f);
            int nRingSize = (NoSharedVertices && bClosed) ? Slices + 1 : Slices;
            int nTipVertices = (NoSharedVertices) ? nRingSize : 1;
            int nCapVertices = (NoSharedVertices) ? Slices + 1 : 1;
            int nFaceVertices = (NoSharedVertices && bClosed == false) ? 6 : 0;
            vertices = new VectorArray3d(nRingSize + nTipVertices + nCapVertices + nFaceVertices);
            uv = new VectorArray2f(vertices.Count);
            normals = new VectorArray3f(vertices.Count);

            int nConeTris = (NoSharedVertices) ? 2 * Slices : Slices;
            int nCapTris = Slices;
            int nFaceTris = (bClosed == false) ? 2 : 0;
            triangles = new IndexArray3i(nConeTris + nCapTris + nFaceTris );

            float fTotalRange = (EndAngleDeg - StartAngleDeg) * MathUtil.Deg2Radf;
            float fStartRad = StartAngleDeg * MathUtil.Deg2Radf;
            float fDelta = (bClosed) ? fTotalRange / Slices : fTotalRange / (Slices - 1);

            // generate rings
            for (int k = 0; k < nRingSize; ++k) {
                float angle = fStartRad + (float)k * fDelta;
                double cosa = Math.Cos(angle), sina = Math.Sin(angle);
                vertices[k] = new Vector3d(BaseRadius * cosa, 0, BaseRadius * sina);
                uv[k] = new Vector2f(0.5f * (1.0f + cosa), 0.5f * (1 + sina));
                Vector3f n = new Vector3f(cosa * Height, BaseRadius / Height, sina * Height);
                n.Normalize();
                normals[k] = n;

                if (NoSharedVertices) {
                    vertices[nRingSize + k] = new Vector3d(0, Height, 0);
                    uv[nRingSize + k] = new Vector2f(0.5f, 0.5f);
                    normals[nRingSize + k] = n;
                }
            }
            if ( NoSharedVertices == false ) {
                vertices[nRingSize] = new Vector3d(0, Height, 0);
                normals[nRingSize] = Vector3f.AxisY;
                uv[nRingSize] = new Vector2f(0.5f, 0.5f);
            }

            // generate cylinder panels
            int ti = 0;
            if (NoSharedVertices) {
                for (int k = 0; k < nRingSize - 1; ++k) {
                    triangles.Set(ti++, k, k + 1, nRingSize + k + 1, Clockwise);
                    triangles.Set(ti++, k, nRingSize + k + 1, nRingSize + k, Clockwise);
                }

            } else
                append_disc(Slices, nRingSize, 0, bClosed, !Clockwise, ref ti);

            int nBottomC = nRingSize + nTipVertices;
            vertices[nBottomC] = new Vector3d(0, 0, 0);
            uv[nBottomC] = new Vector2f(0.5f, 0.5f);
            normals[nBottomC] = new Vector3f(0, -1, 0);

            if (NoSharedVertices) {
                int nStartB = nBottomC + 1;
                for (int k = 0; k < Slices; ++k) {
                    float a = fStartRad + (float)k * fDelta;
                    double cosa = Math.Cos(a), sina = Math.Sin(a);
                    vertices[nStartB + k] = new Vector3d(BaseRadius * cosa, 0, BaseRadius * sina);
                    uv[nStartB + k] = new Vector2f(0.5f * (1.0f + cosa), 0.5f * (1 + sina));
                    normals[nStartB + k] = -Vector3f.AxisY;
                }
                append_disc(Slices, nBottomC, nStartB, bClosed, Clockwise, ref ti);

                // ugh this is very ugly but hard to see the pattern...
                if (bClosed == false) {
                    int nStartF = nStartB + Slices;
                    vertices[nStartF] = vertices[nStartF + 4] = vertices[nBottomC];
                    vertices[nStartF + 1] = vertices[nStartF + 3] = new Vector3d(0, Height, 0); ;
                    vertices[nStartF + 2] = vertices[0]; ;
                    vertices[nStartF + 5] = vertices[nRingSize-1];
                    normals[nStartF] = normals[nStartF + 1] = normals[nStartF + 2]
                        = estimate_normal(nStartF, nStartF + 1, nStartF + 2);
                    normals[nStartF + 3] = normals[nStartF + 4] = normals[nStartF + 5]
                        = estimate_normal(nStartF + 3, nStartF + 4, nStartF + 5);

                    uv[nStartF] = uv[nStartF + 4] = new Vector2f(0, 0);
                    uv[nStartF + 1] = uv[nStartF + 3] = new Vector2f(0, 1);
                    uv[nStartF + 2] = uv[nStartF + 5] = new Vector2f(1, 0);

                    triangles.Set(ti++, nStartF + 0, nStartF + 1, nStartF + 2, !Clockwise);
                    triangles.Set(ti++, nStartF + 3, nStartF + 4, nStartF + 5, !Clockwise);
                }

            } else {
                append_disc(Slices, nBottomC, 0, bClosed, Clockwise, ref ti);
                if (bClosed == false) {
                    triangles.Set(ti++, nBottomC, nRingSize, 0, !Clockwise);
                    triangles.Set(ti++, nBottomC, nRingSize, nRingSize - 1, Clockwise);
                }
            }

            return this;
        }
    }



    public class VerticalGeneralizedCylinderGenerator : MeshGenerator
    {
        public CircularSection[] Sections;
        public int Slices = 16;
        public bool Capped = true;

        // set to true if you are going to texture this cone or want sharp edges
        public bool NoSharedVertices = true;

        public int startCapCenterIndex = -1;
        public int endCapCenterIndex = -1;

        override public MeshGenerator Generate()
        {
            int nRings = (NoSharedVertices) ? 2 * (Sections.Length-1) : Sections.Length;
            int nRingSize = (NoSharedVertices) ? Slices + 1 : Slices;
            int nCapVertices = (NoSharedVertices) ? Slices + 1 : 1;
            if (Capped == false)
                nCapVertices = 0;
            vertices = new VectorArray3d(nRings * nRingSize + 2 * nCapVertices);
            uv = new VectorArray2f(vertices.Count);
            normals = new VectorArray3f(vertices.Count);

            int nSpanTris = (Sections.Length - 1) * (2 * Slices);
            int nCapTris = (Capped) ? 2 * Slices : 0;
            triangles = new IndexArray3i(nSpanTris + nCapTris);

            float fDelta = (float)((Math.PI * 2.0) / Slices);

            float fYSpan = Sections.Last().SectionY - Sections[0].SectionY;
            if (fYSpan == 0)
                fYSpan = 1.0f;

            // generate top and bottom rings for cylinder
            int ri = 0;
            for (int si = 0; si < Sections.Length; ++si) {
                int nStartR = ri * nRingSize;
                float y = Sections[si].SectionY;
                float yt = (y - Sections[0].SectionY) / fYSpan;
                for (int j = 0; j < nRingSize; ++j) {
                    int k = nStartR + j;
                    float angle = (float)j * fDelta;
                    double cosa = Math.Cos(angle), sina = Math.Sin(angle);
                    vertices[k] = new Vector3d(Sections[si].Radius * cosa, y, Sections[si].Radius * sina);
                    float t = (float)j / (float)(Slices - 1);
                    uv[k] = new Vector2f(t, yt);
                    Vector3f n = new Vector3f((float)cosa, 0, (float)sina);
                    n.Normalize();
                    normals[k] = n;
                }
                ri++;
                if (NoSharedVertices && si != 0 && si != Sections.Length - 1) {
                    duplicate_vertex_span(nStartR, nRingSize);
                    ri++;
                }
            }

            // generate triangles
            int ti = 0;
            ri = 0;
            for (int si = 0; si < Sections.Length - 1; ++si) {
                int r0 = ri * nRingSize;
                int r1 = r0 + nRingSize;
                ri += (NoSharedVertices) ? 2 : 1;
                for (int k = 0; k < nRingSize - 1; ++k) {
                    triangles.Set(ti++, r0 + k, r0 + k + 1, r1 + k + 1, Clockwise);
                    triangles.Set(ti++, r0 + k, r1 + k + 1, r1 + k, Clockwise);
                }
                if (NoSharedVertices == false) {      // close disc if we went all the way
                    triangles.Set(ti++, r1 - 1, r0, r1, Clockwise);
                    triangles.Set(ti++, r1 - 1, r1, r1 + nRingSize - 1, Clockwise);
                }
            }

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

            return this;
        }
    }



}
