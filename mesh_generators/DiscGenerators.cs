using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    // generate a triangle fan, no subdvisions
    public class TrivialDiscGenerator : MeshGenerator
    {
        public float Radius = 1.0f;
        public float StartAngleDeg = 0.0f;
        public float EndAngleDeg = 360.0f;
        public int Slices = 32;

        override public MeshGenerator Generate()
        {
            vertices = new VectorArray3d(Slices + 1);
            uv = new VectorArray2f(Slices + 1);
            normals = new VectorArray3f(Slices + 1);
            triangles = new IndexArray3i(Slices);

            int vi = 0;
            vertices[vi] = Vector3d.Zero;
            uv[vi] = new Vector2f(0.5f, 0.5f);
            normals[vi] = Vector3f.AxisY;
            vi++;

            bool bFullDisc = ((EndAngleDeg - StartAngleDeg) > 359.99f);
            float fTotalRange = (EndAngleDeg - StartAngleDeg) * MathUtil.Deg2Radf;
            float fStartRad = StartAngleDeg * MathUtil.Deg2Radf;
            float fDelta = (bFullDisc) ? fTotalRange / Slices : fTotalRange / (Slices - 1);
            for (int k = 0; k < Slices; ++k) {
                float a = fStartRad + (float)k * fDelta;
                double cosa = Math.Cos(a), sina = Math.Sin(a);
                vertices[vi] = new Vector3d(Radius * cosa, 0, Radius * sina);
                uv[vi] = new Vector2f(0.5f * (1.0f + cosa), 0.5f * (1 + sina));
                normals[vi] = Vector3f.AxisY;
                vi++;
            }

            int ti = 0;
            for (int k = 1; k < Slices; ++k)
                triangles.Set(ti++, k, 0, k + 1, Clockwise);
            if (bFullDisc)      // close disc if we went all the way
                triangles.Set(ti++, Slices, 0, 1, Clockwise);

            return this;
        }
    }







    // generate a triangle fan, no subdvisions
    public class PuncturedDiscGenerator : MeshGenerator
    {
        public float OuterRadius = 1.0f;
        public float InnerRadius = 0.5f;
        public float StartAngleDeg = 0.0f;
        public float EndAngleDeg = 360.0f;
        public int Slices = 32;

        override public MeshGenerator Generate()
        {
            vertices = new VectorArray3d(2*Slices);
            uv = new VectorArray2f(2*Slices);
            normals = new VectorArray3f(2*Slices);
            triangles = new IndexArray3i(2*Slices);

            bool bFullDisc = ((EndAngleDeg - StartAngleDeg) > 359.99f);
            float fTotalRange = (EndAngleDeg - StartAngleDeg) * MathUtil.Deg2Radf;
            float fStartRad = StartAngleDeg * MathUtil.Deg2Radf;
            float fDelta = (bFullDisc) ? fTotalRange / Slices : fTotalRange / (Slices - 1);
            float fUVRatio = InnerRadius / OuterRadius;
            for (int k = 0; k < Slices; ++k) {
                float angle = fStartRad + (float)k * fDelta;
                double cosa = Math.Cos(angle), sina = Math.Sin(angle);
                vertices[k] = new Vector3d(InnerRadius * cosa, 0, InnerRadius * sina);
                vertices[Slices+k] = new Vector3d(OuterRadius * cosa, 0, OuterRadius * sina);
                uv[k] = new Vector2f(0.5f * (1.0f + fUVRatio * cosa), 0.5f * (1.0f + fUVRatio * sina));
                uv[Slices + k] = new Vector2f(0.5f * (1.0f + cosa), 0.5f * (1.0f + sina));
                normals[k] = normals[Slices + k] = Vector3f.AxisY;
            }

            int ti = 0;
            for (int k = 0; k < Slices-1; ++k) {
                triangles.Set(ti++, k, k + 1, Slices + k + 1, Clockwise);
                triangles.Set(ti++, k, Slices + k + 1, Slices + k, Clockwise);
            }
            if (bFullDisc) {      // close disc if we went all the way
                triangles.Set(ti++, Slices - 1, 0, Slices, Clockwise);
                triangles.Set(ti++, Slices - 1, Slices, 2 * Slices - 1, Clockwise);
            }

            return this;
        }
    }


}
