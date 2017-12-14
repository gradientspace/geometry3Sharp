using System;
using System.Collections;
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

        public Vector3f Normal = Vector3f.AxisY;

        /// <summary>
        /// How to map 2D indices to 3D. Default is (x,0,z). Set this value to (1,2) if you want (x,y,0).
        /// Set values to negative to mirror on that axis.
        /// </summary>
        public Index2i IndicesMap = new Index2i(1, 3);

        public enum UVModes
        {
            FullUVSquare,
            CenteredUVRectangle,
            BottomCornerUVRectangle
        }
        public UVModes UVMode = UVModes.FullUVSquare;


        virtual protected Vector3d make_vertex(float x, float y)
        {
            Vector3d v = Vector3d.Zero;
            v[Math.Abs(IndicesMap.a)-1] = (IndicesMap.a < 0) ? -x : x;
            v[Math.Abs(IndicesMap.b)-1] = (IndicesMap.b < 0) ? -y : y;
            return v;
        }

        override public MeshGenerator Generate()
        {
            if (MathUtil.InRange(IndicesMap.a, 1, 3) == false || MathUtil.InRange(IndicesMap.b, 1, 3) == false)
                throw new Exception("TrivialRectGenerator: Invalid IndicesMap!");

            vertices = new VectorArray3d(4);
            uv = new VectorArray2f(4);
            normals = new VectorArray3f(4);
            triangles = new IndexArray3i(2);

            vertices[0] = make_vertex(-Width / 2.0f, -Height / 2.0f);
            vertices[1] = make_vertex(Width / 2.0f, -Height / 2.0f);
            vertices[2] = make_vertex(Width / 2.0f, Height / 2.0f);
            vertices[3] = make_vertex(-Width / 2.0f, Height / 2.0f);

            normals[0] = normals[1] = normals[2] = normals[3] = Normal;

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

            return this;
        }
    }






    /// <summary>
    /// Generate a mesh of a rect that has "gridded" faces, ie grid of triangulated quads, 
    /// with EdgeVertices verts along each edge.
    /// [TODO] allow varying EdgeVertices in each dimension (tricky...)
    /// </summary>
    public class GriddedRectGenerator : TrivialRectGenerator
    {
        public int EdgeVertices = 8;

        override public MeshGenerator Generate()
        {
            if (MathUtil.InRange(IndicesMap.a, 1, 3) == false || MathUtil.InRange(IndicesMap.b, 1, 3) == false)
                throw new Exception("GriddedRectGenerator: Invalid IndicesMap!");

            int N = (EdgeVertices > 1) ? EdgeVertices : 2;
            int NT = N - 1, N2 = N * N;
            vertices = new VectorArray3d(N2);
            uv = new VectorArray2f(vertices.Count);
            normals = new VectorArray3f(vertices.Count);
            triangles = new IndexArray3i(2 * NT * NT);
            groups = new int[triangles.Count];

            // corner vertices
            Vector3d v00 = make_vertex(-Width / 2.0f, -Height / 2.0f);
            Vector3d v01 = make_vertex(Width / 2.0f, -Height / 2.0f);
            Vector3d v11 = make_vertex(Width / 2.0f, Height / 2.0f);
            Vector3d v10 = make_vertex(-Width / 2.0f, Height / 2.0f);

            // corner UVs
            float uvleft = 0.0f, uvright = 1.0f, uvbottom = 0.0f, uvtop = 1.0f;

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

            Vector2f uv00 = new Vector2f(uvleft, uvbottom);
            Vector2f uv01 = new Vector2f(uvright, uvbottom);
            Vector2f uv11 = new Vector2f(uvright, uvtop);
            Vector2f uv10 = new Vector2f(uvleft, uvtop);

            int vi = 0;
            int ti = 0;

            // add vertex rows
            int start_vi = vi;
            for (int yi = 0; yi < N; ++yi) {
                double ty = (double)yi / (double)(N - 1);
                for (int xi = 0; xi < N; ++xi) {
                    double tx = (double)xi / (double)(N - 1);
                    normals[vi] = Normal;
                    uv[vi] = bilerp(ref uv00, ref uv01, ref uv11, ref uv10, (float)tx, (float)ty);
                    vertices[vi++] = bilerp(ref v00, ref v01, ref v11, ref v10, tx, ty);
                }
            }

            // add faces
            for (int y0 = 0; y0 < NT; ++y0) {
                for (int x0 = 0; x0 < NT; ++x0) {
                    int i00 = start_vi + y0 * N + x0;
                    int i10 = start_vi + (y0 + 1) * N + x0;
                    int i01 = i00 + 1, i11 = i10 + 1;

                    groups[ti] = 0;
                    triangles.Set(ti++, i00, i11, i01, Clockwise);
                    groups[ti] = 0;
                    triangles.Set(ti++, i00, i10, i11, Clockwise);
                }
            }

            return this;
        }
    }












    // Generate a rounded rect centered at origin.
    // Force individual corners to be sharp using the SharpCorners flags field.
    public class RoundRectGenerator : MeshGenerator
    {
        public float Width = 1.0f;
        public float Height = 1.0f;
        public float Radius = 0.1f;
        public int CornerSteps = 4;


        [Flags]
        public enum Corner
        {
            BottomLeft = 1,
            BottomRight = 2,
            TopRight = 4,
            TopLeft = 8
        }
        public Corner SharpCorners = 0;


        public enum UVModes
        {
            FullUVSquare,
            CenteredUVRectangle,
            BottomCornerUVRectangle
        }
        public UVModes UVMode = UVModes.FullUVSquare;

        // order is [inner_corner, outer_1, outer_2]
        static int[] corner_spans = new int[] { 0, 11, 4,   1, 5, 6,   2, 7, 8,   3, 9, 10 };

        override public MeshGenerator Generate()
        {
            int corner_v = 0, corner_t = 0;
            for (int k = 0; k < 4; ++k) {
                if (((int)SharpCorners & (1 << k)) != 0) {
                    corner_v += 1;
                    corner_t += 2;
                } else {
                    corner_v += CornerSteps;
                    corner_t += (CornerSteps + 1);
                }
            }

            vertices = new VectorArray3d(12 + corner_v);
            uv = new VectorArray2f(vertices.Count);
            normals = new VectorArray3f(vertices.Count);
            triangles = new IndexArray3i(10 + corner_t);

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
                bool sharp = ((int)SharpCorners & (1 << j)) > 0;
                if (sharp) {
                    append_2d_disc_segment(corner_spans[3 * j], corner_spans[3 * j + 1], corner_spans[3 * j + 2], 1,
                        cycle, ref vi, ref ti, -1, MathUtil.SqrtTwo * Radius);
                } else {
                    append_2d_disc_segment(corner_spans[3 * j], corner_spans[3 * j + 1], corner_spans[3 * j + 2], CornerSteps,
                        cycle, ref vi, ref ti);
                }
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

            return this;
        }



        static readonly float[] signx = new float[] { 1, 1, -1, -1 };
        static readonly float[] signy = new float[] { -1, 1, 1, -1 };
        static readonly float[] startangle = new float[] { 270, 0, 90, 180 };
        static readonly float[] endangle = new float[] { 360, 90, 180, 270 };

        /// <summary>
        /// This is a utility function that returns the set of border points, which
        /// is useful when we use a roundrect as a UI element and want the border
        /// </summary>
        public Vector3d[] GetBorderLoop()
        {
            int corner_v = 0;
            for (int k = 0; k < 4; ++k) {
                if (((int)SharpCorners & (1 << k)) != 0)
                    corner_v += 1;
                else 
                    corner_v += CornerSteps;
            }

            float innerW = Width - 2 * Radius;
            float innerH = Height - 2 * Radius;

            Vector3d[] vertices = new Vector3d[4 + corner_v];
            int vi = 0;

            for ( int i = 0; i < 4; ++i ) { 
                vertices[vi++] = new Vector3d(signx[i] * Width / 2, 0, signy[i] * Height / 2);

                bool sharp = ((int)SharpCorners & (1 << i)) > 0;
                Arc2d arc = new Arc2d( new Vector2d(signx[i] * innerW, signy[i] * innerH), 
                    (sharp) ? MathUtil.SqrtTwo * Radius : Radius,
                    startangle[i], endangle[i]);
                int use_steps = (sharp) ? 1 : CornerSteps;
                for (int k = 0; k < use_steps; ++k) {
                    double t = (double)(i + 1) / (double)(use_steps + 1);
                    Vector2d pos = arc.SampleT(t);
                    vertices[vi++] = new Vector3d(pos.x, 0, pos.y);
                }
            }

            return vertices;
        }


    }

}
