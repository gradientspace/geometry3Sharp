using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    public class Curve3Axis3RevolveGenerator : MeshGenerator
    {
        public Vector3d[] Curve;

        public Frame3f Axis = Frame3f.Identity;
        public int RevolveAxis = 1;
        public bool Capped = true;
        public int Slices = 16;
        public bool NoSharedVertices = true;

        public int startCapCenterIndex = -1;
        public int endCapCenterIndex = -1;

        public override MeshGenerator Generate()
        {
            int nRings = Curve.Length;
            int nRingSize = (NoSharedVertices) ? Slices + 1 : Slices;
            int nCapVertices = (NoSharedVertices) ? Slices + 1 : 1;
            if (Capped == false)
                nCapVertices = 0;

            vertices = new VectorArray3d(nRingSize*nRings + 2*nCapVertices);
            uv = new VectorArray2f(vertices.Count);
            normals = new VectorArray3f(vertices.Count);

            int nSpanTris = (nRings - 1) * (2 * Slices);
            int nCapTris = (Capped) ? 2 * Slices : 0;
            triangles = new IndexArray3i(nSpanTris + nCapTris);

            float fDelta = (float)((Math.PI * 2.0) / Slices);

            Frame3f f = Axis;

            // generate tube
            for (int ri = 0; ri < nRings; ++ri) {

                Vector3d v_along = Curve[ri];
                Vector3f v_frame = f.ToFrameP((Vector3f)v_along);
                float uv_along = (float)ri / (float)(nRings - 1);

                // generate vertices
                int nStartR = ri * nRingSize;
                for (int j = 0; j < nRingSize; ++j) {
                    float angle = (float)j * fDelta;

                    // [TODO] this is not efficient...use Matrix3f?
                    Vector3f v_rot = Quaternionf.AxisAngleR(Vector3f.AxisY, angle) * v_frame;
                    Vector3d v_new = f.FromFrameP(v_rot);
                    int k = nStartR + j;
                    vertices[k] = v_new;

                    float uv_around = (float)j / (float)(nRingSize);
                    uv[k] = new Vector2f(uv_along, uv_around);

                    // [TODO] proper normal
                    Vector3f n = (Vector3f)(v_new - f.Origin).Normalized;
                    normals[k] = n;
                }
            }


            // generate triangles
            int ti = 0;
            for (int ri = 0; ri < nRings - 1; ++ri) {
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



            if (Capped) {

                // find avg start loop size
                Vector3d vAvgStart = Vector3d.Zero, vAvgEnd = Vector3d.Zero;
                for (int k = 0; k < Slices; ++k) {
                    vAvgStart += vertices[k];
                    vAvgEnd += vertices[(nRings - 1) * nRingSize + k];
                }
                vAvgStart /= (double)Slices;
                vAvgEnd /= (double)Slices;

                Frame3f fStart = f;
                fStart.Origin = (Vector3f)vAvgStart;
                Frame3f fEnd = f;
                fEnd.Origin = (Vector3f)vAvgEnd;



                // add endcap verts
                int nBottomC = nRings * nRingSize;
                vertices[nBottomC] = fStart.Origin;
                uv[nBottomC] = new Vector2f(0.5f, 0.5f);
                normals[nBottomC] = -fStart.Z;
                startCapCenterIndex = nBottomC;

                int nTopC = nBottomC + 1;
                vertices[nTopC] = fEnd.Origin;
                uv[nTopC] = new Vector2f(0.5f, 0.5f);
                normals[nTopC] = fEnd.Z;
                endCapCenterIndex = nTopC;

                if (NoSharedVertices) {
                    // duplicate first loop and make a fan w/ bottom-center
                    int nExistingB = 0;
                    int nStartB = nTopC + 1;
                    for (int k = 0; k < Slices; ++k) {
                        vertices[nStartB + k] = vertices[nExistingB + k];
                        //uv[nStartB + k] = (Vector2f)Polygon.Vertices[k].Normalized;

                        float angle = (float)k * fDelta;
                        double cosa = Math.Cos(angle), sina = Math.Sin(angle);
                        uv[nStartB + k] = new Vector2f(0.5f * (1.0f + cosa), 0.5f * (1 + sina));

                        normals[nStartB + k] = normals[nBottomC];
                    }
                    append_disc(Slices, nBottomC, nStartB, true, Clockwise, ref ti);

                    // duplicate second loop and make fan
                    int nExistingT = nRingSize * (nRings - 1);
                    int nStartT = nStartB + Slices;
                    for (int k = 0; k < Slices; ++k) {
                        vertices[nStartT + k] = vertices[nExistingT + k];
                        //uv[nStartT + k] = (Vector2f)Polygon.Vertices[k].Normalized;

                        float angle = (float)k * fDelta;
                        double cosa = Math.Cos(angle), sina = Math.Sin(angle);
                        uv[nStartT + k] = new Vector2f(0.5f * (1.0f + cosa), 0.5f * (1 + sina));


                        normals[nStartT + k] = normals[nTopC];
                    }
                    append_disc(Slices, nTopC, nStartT, true, !Clockwise, ref ti);

                } else {
                    append_disc(Slices, nBottomC, 0, true, Clockwise, ref ti);
                    append_disc(Slices, nTopC, nRingSize * (nRings - 1), true, !Clockwise, ref ti);
                }
            }

            return this;
        }
    }





    public class Curve3Curve3RevolveGenerator : MeshGenerator
    {
        public Vector3d[] Curve;
        public Vector3d[] Axis;

        public bool Capped = true;
        public int Slices = 16;
        public bool NoSharedVertices = true;

        public int startCapCenterIndex = -1;
        public int endCapCenterIndex = -1;

        public override MeshGenerator Generate()
        {
            double tCurveLen = CurveUtils.ArcLength(Curve);
            SampledArcLengthParam pAxis = new SampledArcLengthParam(Axis, Axis.Length);
            double tAxisLen = pAxis.ArcLength;
            double tScale = tAxisLen / tCurveLen;

            int nRings = Curve.Length;
            int nRingSize = (NoSharedVertices) ? Slices + 1 : Slices;
            int nCapVertices = (NoSharedVertices) ? Slices + 1 : 1;
            if (Capped == false)
                nCapVertices = 0;

            vertices = new VectorArray3d(nRingSize * nRings + 2 * nCapVertices);
            uv = new VectorArray2f(vertices.Count);
            normals = new VectorArray3f(vertices.Count);

            int nSpanTris = (nRings - 1) * (2 * Slices);
            int nCapTris = (Capped) ? 2 * Slices : 0;
            triangles = new IndexArray3i(nSpanTris + nCapTris);

            float fDelta = (float)((Math.PI * 2.0) / Slices);

            double tCur = 0;
            CurveSample s = pAxis.Sample(tCur);
            Frame3f f0 = new Frame3f((Vector3f)s.position, (Vector3f)s.tangent, 1);
            Frame3f fCur = f0;

            // generate tube
            for (int ri = 0; ri < nRings; ++ri) {

                if ( ri > 0 ) {
                    tCur += (Curve[ri] - Curve[ri - 1]).Length;
                    s = pAxis.Sample(tCur * tScale);
                    fCur.Origin = (Vector3f)s.position;
                    fCur.AlignAxis(1, (Vector3f)s.tangent);
                }

                Vector3d v_along = Curve[ri];
                Vector3f v_frame = fCur.ToFrameP((Vector3f)v_along);
                float uv_along = (float)ri / (float)(nRings - 1);

                // generate vertices
                int nStartR = ri * nRingSize;
                for (int j = 0; j < nRingSize; ++j) {
                    float angle = (float)j * fDelta;

                    // [TODO] this is not efficient...use Matrix3f?
                    Vector3f v_rot = Quaternionf.AxisAngleR(Vector3f.AxisY, angle) * v_frame;
                    Vector3d v_new = fCur.FromFrameP(v_rot);
                    int k = nStartR + j;
                    vertices[k] = v_new;

                    float uv_around = (float)j / (float)(nRingSize);
                    uv[k] = new Vector2f(uv_along, uv_around);

                    // [TODO] proper normal
                    Vector3f n = (Vector3f)(v_new - fCur.Origin).Normalized;
                    normals[k] = n;
                }
            }


            // generate triangles
            int ti = 0;
            for (int ri = 0; ri < nRings - 1; ++ri) {
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



            if (Capped) {

                // find avg start loop size
                Vector3d vAvgStart = Vector3d.Zero, vAvgEnd = Vector3d.Zero;
                for (int k = 0; k < Slices; ++k) {
                    vAvgStart += vertices[k];
                    vAvgEnd += vertices[(nRings - 1) * nRingSize + k];
                }
                vAvgStart /= (double)Slices;
                vAvgEnd /= (double)Slices;

                Frame3f fStart = f0;
                fStart.Origin = (Vector3f)vAvgStart;
                Frame3f fEnd = fCur;
                fEnd.Origin = (Vector3f)vAvgEnd;



                // add endcap verts
                int nBottomC = nRings * nRingSize;
                vertices[nBottomC] = fStart.Origin;
                uv[nBottomC] = new Vector2f(0.5f, 0.5f);
                normals[nBottomC] = -fStart.Z;
                startCapCenterIndex = nBottomC;

                int nTopC = nBottomC + 1;
                vertices[nTopC] = fEnd.Origin;
                uv[nTopC] = new Vector2f(0.5f, 0.5f);
                normals[nTopC] = fEnd.Z;
                endCapCenterIndex = nTopC;

                if (NoSharedVertices) {
                    // duplicate first loop and make a fan w/ bottom-center
                    int nExistingB = 0;
                    int nStartB = nTopC + 1;
                    for (int k = 0; k < Slices; ++k) {
                        vertices[nStartB + k] = vertices[nExistingB + k];
                        //uv[nStartB + k] = (Vector2f)Polygon.Vertices[k].Normalized;

                        float angle = (float)k * fDelta;
                        double cosa = Math.Cos(angle), sina = Math.Sin(angle);
                        uv[nStartB + k] = new Vector2f(0.5f * (1.0f + cosa), 0.5f * (1 + sina));

                        normals[nStartB + k] = normals[nBottomC];
                    }
                    append_disc(Slices, nBottomC, nStartB, true, Clockwise, ref ti);

                    // duplicate second loop and make fan
                    int nExistingT = nRingSize * (nRings - 1);
                    int nStartT = nStartB + Slices;
                    for (int k = 0; k < Slices; ++k) {
                        vertices[nStartT + k] = vertices[nExistingT + k];
                        //uv[nStartT + k] = (Vector2f)Polygon.Vertices[k].Normalized;

                        float angle = (float)k * fDelta;
                        double cosa = Math.Cos(angle), sina = Math.Sin(angle);
                        uv[nStartT + k] = new Vector2f(0.5f * (1.0f + cosa), 0.5f * (1 + sina));


                        normals[nStartT + k] = normals[nTopC];
                    }
                    append_disc(Slices, nTopC, nStartT, true, !Clockwise, ref ti);

                } else {
                    append_disc(Slices, nBottomC, 0, true, Clockwise, ref ti);
                    append_disc(Slices, nTopC, nRingSize * (nRings - 1), true, !Clockwise, ref ti);
                }
            }

            return this;
        }


    }

}
