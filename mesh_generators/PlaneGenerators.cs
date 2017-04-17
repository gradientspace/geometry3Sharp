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
            triangles = new IndexArray3i(2);

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
                triangles.Set(0, 0, 1, 2);
                triangles.Set(1, 0, 2, 3);
            } else {
                triangles.Set(0, 0, 2, 1);
                triangles.Set(1, 0, 3, 2);
            }
        }
    }







   // generate a two-triangle rect, centered at origin
    public class RoundRectGenerator : MeshGenerator
    {
        public float Width = 1.0f;
        public float Height = 1.0f;
        public float Radius = 0.1f;
        public int CornerSteps = 4;

        public enum UVModes
        {
            FullUVSquare,
            CenteredUVRectangle,
            BottomCornerUVRectangle
        }
        public UVModes UVMode = UVModes.FullUVSquare;

        // order is [inner_corner, outer_1, outer_2]
        static int[] corner_spans = new int[] { 0, 11, 4,   1, 5, 6,   2, 7, 8,   3, 9, 10 };

        override public void Generate()
        {
            vertices = new VectorArray3d(12 + 4*CornerSteps);
            uv = new VectorArray2f(vertices.Count);
            normals = new VectorArray3f(vertices.Count);
            triangles = new IndexArray3i(10 + 4*(CornerSteps+1));

            float innerW = Width - 2 * Radius;
            float innerH = Height - 2 * Radius;

            // make vertices for inner "cross" (ie 5 squares)
            vertices[0] = new Vector3d(-innerW / 2.0f, 0, -innerH / 2.0f);
            vertices[1] = new Vector3d(innerW / 2.0f, 0, -innerH / 2.0f);
            vertices[2] = new Vector3d(innerW / 2.0f, 0, innerH / 2.0f);
            vertices[3] = new Vector3d(-innerW / 2.0f, 0, innerH / 2.0f);

            vertices[4] = new Vector3d(-innerW / 2, 0, -Height / 2);
            vertices[5] = new Vector3d(innerW / 2, 0, -Height / 2);

            vertices[6] = new Vector3d(Width / 2, 0, -innerH / 2);
            vertices[7] = new Vector3d(Width / 2, 0, innerH / 2);

            vertices[8] = new Vector3d(innerW / 2, 0, Height / 2);
            vertices[9] = new Vector3d(-innerW / 2, 0, Height / 2);

            vertices[10] = new Vector3d(-Width / 2, 0, innerH / 2);
            vertices[11] = new Vector3d(-Width / 2, 0, -innerH / 2);

            // make triangles for inner cross
            bool cycle = (Clockwise == false);
            int ti = 0;
            append_rectangle(0, 1, 2, 3, cycle, ref ti);
            append_rectangle(4,5,1,0, cycle, ref ti);
            append_rectangle(1,6,7,2, cycle, ref ti);
            append_rectangle(3,2,8,9, cycle, ref ti);
            append_rectangle(11,0,3,10, cycle, ref ti);

            int vi = 12;
            for ( int j = 0; j < 4; ++j ) {
                append_2d_disc_segment(corner_spans[3 * j], corner_spans[3 * j + 1], corner_spans[3 * j + 2], CornerSteps,
                    cycle, ref vi, ref ti);
            }


            for (int k = 0; k < vertices.Count; ++k)
                normals[k] = Vector3f.AxisY;

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

            Vector3d c = new Vector3d(-Width / 2, 0, -Height / 2);
            for ( int k = 0; k < vertices.Count; ++k ) {
                Vector3d v = vertices[k];
                double tx = (v.x - c.x) / Width;
                double ty = (v.y - c.y) / Height;
                uv[k] = new Vector2f( (1 - tx) * uvleft + (tx) * uvright, 
                                      (1 - ty) * uvbottom + (ty) * uvtop);
            }

        }
    }

}
