using System;
using System.Collections.Generic;
using System.Linq;

namespace g3
{
    /// <summary>
    /// Create a mesh that contains a planar element for each point and normal
    /// (currently only triangles)
    /// </summary>
    public class PointSplatsGenerator : MeshGenerator
    {
        public IEnumerable<int> PointIndices;
        public int PointIndicesCount = -1;      // you can set this to avoid calling Count() on enumerable

        public Func<int, Vector3d> PointF;      // required
        public Func<int, Vector3d> NormalF;     // required
        public double Radius = 1.0f;

        public PointSplatsGenerator()
        {
            WantUVs = false;
        }

        public override MeshGenerator Generate()
        {
            int N = (PointIndicesCount == -1) ? PointIndices.Count() : PointIndicesCount;

            vertices = new VectorArray3d(N * 3);
            uv = null;
            normals = new VectorArray3f(vertices.Count);
            triangles = new IndexArray3i(N);

            Matrix2d matRot = new Matrix2d(120 * MathUtil.Deg2Rad);
            Vector2d uva = new Vector2d(0, Radius);
            Vector2d uvb = matRot * uva;
            Vector2d uvc = matRot * uvb;

            int vi = 0;
            int ti = 0;
            foreach (int pid in PointIndices) {
                Vector3d v = PointF(pid);
                Vector3d n = NormalF(pid);
                Frame3d f = new Frame3d(v, n);
                triangles.Set(ti++, vi, vi + 1, vi + 2, Clockwise);
                vertices[vi++] = f.FromPlaneUV(uva, 2);
                vertices[vi++] = f.FromPlaneUV(uvb, 2);
                vertices[vi++] = f.FromPlaneUV(uvc, 2);
            }

            return this;
        }



        /// <summary>
        /// shortcut utility
        /// </summary>
        public static DMesh3 Generate(IList<int> indices,
            Func<int, Vector3d> PointF, Func<int, Vector3d> NormalF,
            double radius)
        {
            var gen = new PointSplatsGenerator() {
                PointIndices = indices,
                PointIndicesCount = indices.Count,
                PointF = PointF, NormalF = NormalF, Radius = radius
            };
            return gen.Generate().MakeDMesh();
        }

    }
}
