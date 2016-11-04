using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    public class MeshGenerator
    {
        public VectorArray3d vertices;
        public VectorArray2f uv;
        public VectorArray3f normals;
        public VectorArray3i triangles;

        public bool WantUVs = true;
        public bool WantNormals = true;
        public bool Clockwise = false;

        public void MakeMesh(SimpleMesh m)
        {
            m.AppendVertices(vertices, (WantNormals) ? normals : null, null, (WantUVs) ? uv : null);
            m.AppendTriangles(triangles);
        }
    }


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







    // generate a two-triangle rect, centered at origin
    public class TrivialRectGenerator : MeshGenerator
    {
        public float Width = 1.0f;
        public float Height = 1.0f;

        public enum UVModes
        {
            FullUVSquare,
            CenteredUVRectangle,
            BottomCornerUVRectangle
        }
        public UVModes UVMode = UVModes.FullUVSquare;

        public void Generate()
        {
            vertices = new VectorArray3d(4);
            uv = new VectorArray2f(4);
            normals = new VectorArray3f(4);
            triangles = new VectorArray3i(2);

            vertices[0] = new Vector3d(-Width / 2.0f, 0, -Height / 2.0f);
            vertices[1] = new Vector3d(Width / 2.0f, 0, -Height / 2.0f);
            vertices[2] = new Vector3d(Width / 2.0f, 0, Height / 2.0f);
            vertices[3] = new Vector3d(-Width / 2.0f, 0, Height / 2.0f);

            normals[0] = normals[1] = normals[2] = normals[3] = Vector3f.AxisY;

            float uvleft = 0.0f, uvright = 1.0f, uvbottom = 0.0f, uvtop = 1.0f;

            // if we want the UV subregion, we assume it is 
            if (UVMode != UVModes.FullUVSquare) {
                if (Width > Height) {
                    float a = Height / Width;
                    if (UVMode == UVModes.CenteredUVRectangle) {
                        uvbottom = 0.5f - a / 2.0f; uvtop = 0.5f + a / 2.0f;
                    } else {
                        uvtop = a;
                    }
                } else if (Height > Width) {
                    float a = Width / Height;
                    if (UVMode == UVModes.CenteredUVRectangle) {
                        uvleft = 0.5f - a / 2.0f; uvright = 0.5f + a / 2.0f;
                    } else {
                        uvright = a;
                    }
                }
            }

            uv[0] = new Vector2f(uvleft, uvbottom);
            uv[1] = new Vector2f(uvright, uvbottom);
            uv[2] = new Vector2f(uvright, uvtop);
            uv[3] = new Vector2f(uvleft, uvtop);

            if (Clockwise == true) {
                triangles.Set(0, 0, 2, 1);
                triangles.Set(1, 0, 3, 2);
            } else {
                triangles.Set(0, 0, 1, 2);
                triangles.Set(1, 0, 2, 3);
            }
        }
    }








}
