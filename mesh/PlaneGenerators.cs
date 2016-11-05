using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
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

        override public void Generate()
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
