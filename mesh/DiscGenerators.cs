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

        public void Generate()
        {
            vertices = new VectorArray3d(Slices + 1);
            uv = new VectorArray2f(Slices + 1);
            normals = new VectorArray3f(Slices + 1);
            triangles = new VectorArray3i(Slices);

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
            if (Clockwise == true) {
                for (int k = 1; k < Slices; ++k)
                    triangles.Set(ti++, k, 0, k + 1);
                if (bFullDisc)      // close disc if we went all the way
                    triangles.Set(ti++, Slices, 0, 1);
            } else {
                for (int k = 1; k < Slices; ++k)
                    triangles.Set(ti++, 0, k, k + 1);
                if (bFullDisc)
                    triangles.Set(ti++, 0, Slices, 1);
            }
        }
    }




}
