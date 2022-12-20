using System;
using System.Collections;
using System.Collections.Generic;

namespace g3
{
    public static class BoundsUtil
    {

        public static AxisAlignedBox3d Bounds(IEnumerable<DMesh3> meshes) {
            AxisAlignedBox3d bounds = AxisAlignedBox3d.Empty;
            foreach (DMesh3 mesh in meshes)
                bounds.Contain(mesh.CachedBounds);
            return bounds;
        }


        public static AxisAlignedBox3d Bounds(IPointSet source) {
            AxisAlignedBox3d bounds = AxisAlignedBox3d.Empty;
            foreach (int vid in source.VertexIndices())
                bounds.Contain(source.GetVertex(vid));
            return bounds;
        }


        public static AxisAlignedBox3d Bounds(ref Triangle3d tri) {
            return Bounds(ref tri.V0, ref tri.V1, ref tri.V2);
        }

        public static AxisAlignedBox3d Bounds(ref Vector3d v0, ref Vector3d v1, ref Vector3d v2)
        {
            AxisAlignedBox3d box;
            MathUtil.MinMax(v0.x, v1.x, v2.x, out box.Min.x, out box.Max.x);
            MathUtil.MinMax(v0.y, v1.y, v2.y, out box.Min.y, out box.Max.y);
            MathUtil.MinMax(v0.z, v1.z, v2.z, out box.Min.z, out box.Max.z);
            return box;
        }

        public static AxisAlignedBox2d Bounds(ref Vector2d v0, ref Vector2d v1, ref Vector2d v2)
        {
            AxisAlignedBox2d box;
            MathUtil.MinMax(v0.x, v1.x, v2.x, out box.Min.x, out box.Max.x);
            MathUtil.MinMax(v0.y, v1.y, v2.y, out box.Min.y, out box.Max.y);
            return box;
        }

        // AABB of transformed AABB (corners)
        public static AxisAlignedBox3d Bounds(ref AxisAlignedBox3d boxIn, Func<Vector3d,Vector3d> TransformF)
        {
            if (TransformF == null)
                return boxIn;

            AxisAlignedBox3d box = new AxisAlignedBox3d(TransformF(boxIn.Corner(0)));
            for (int i = 1; i < 8; ++i)
                box.Contain(TransformF(boxIn.Corner(i)));
            return box;
        }


        public static AxisAlignedBox3d Bounds(IEnumerable<Vector3d> positions)
        {
            AxisAlignedBox3d box = AxisAlignedBox3d.Empty;
            foreach (Vector3d v in positions)
                box.Contain(v);
            return box;
        }
        public static AxisAlignedBox3f Bounds(IEnumerable<Vector3f> positions)
        {
            AxisAlignedBox3f box = AxisAlignedBox3f.Empty;
            foreach (Vector3f v in positions)
                box.Contain(v);
            return box;
        }


        public static AxisAlignedBox2d Bounds(IEnumerable<Vector2d> positions)
        {
            AxisAlignedBox2d box = AxisAlignedBox2d.Empty;
            foreach (Vector2d v in positions)
                box.Contain(v);
            return box;
        }
        public static AxisAlignedBox2f Bounds(IEnumerable<Vector2f> positions)
        {
            AxisAlignedBox2f box = AxisAlignedBox2f.Empty;
            foreach (Vector2f v in positions)
                box.Contain(v);
            return box;
        }


        public static AxisAlignedBox3d Bounds<T>(IEnumerable<T> values, Func<T, Vector3d> PositionF)
		{
			AxisAlignedBox3d box = AxisAlignedBox3d.Empty;
			foreach ( T t in values )
				box.Contain( PositionF(t) );
			return box;
		}
		public static AxisAlignedBox3f Bounds<T>(IEnumerable<T> values, Func<T, Vector3f> PositionF)
		{
			AxisAlignedBox3f box = AxisAlignedBox3f.Empty;
			foreach ( T t in values )
				box.Contain( PositionF(t) );
			return box;
		}


        /// <summary>
        /// compute axis-aligned bounds of set of points after transforming 
        /// </summary>
        public static AxisAlignedBox3d Bounds(IEnumerable<Vector3d> values, TransformSequence xform)
        {
            AxisAlignedBox3d box = AxisAlignedBox3d.Empty;
            foreach (Vector3d v in values)
                box.Contain(xform.TransformP(v));
            return box;
        }


        /// <summary>
        /// compute axis-aligned bounds of set of points after transforming into frame f
        /// </summary>
        public static AxisAlignedBox3d BoundsInFrame(IEnumerable<Vector3d> values, Frame3f f) {
			AxisAlignedBox3d box = AxisAlignedBox3d.Empty;
			foreach ( Vector3d v in values )
				box.Contain( f.ToFrameP(v) );
			return box;
        }        


        // Modes: 0: centroids, 1: any vertex, 2: 2 vertices, 3: all vertices
        // ContainF should return true if 3D position is in set (eg inside box, etc)
        // If mode = 0, will be called with (centroid, tri_idx)
        // If mode = 1/2/3, will be called with (vtx_pos, vtx_idx)
        // AddF is called with triangle IDs that are in set
        public static void TrianglesContained(DMesh3 mesh, Func<Vector3d,int,bool> ContainF, Action<int> AddF, int nMode = 0)
        {
            BitArray inV = null;
            if (nMode != 0) {
                inV = new BitArray(mesh.MaxVertexID);
                foreach (int vid in mesh.VertexIndices()) {
                    if (ContainF(mesh.GetVertex(vid), vid))
                        inV[vid] = true;
                }
            }

            foreach ( int tid in mesh.TriangleIndices() ) {
                Index3i tri = mesh.GetTriangle(tid);

                bool bIn = false;
                if ( nMode == 0 ) {
                    if (ContainF(mesh.GetTriCentroid(tid), tid))
                        bIn = true;
                } else {
                    int countIn = (inV[tri.a] ? 1 : 0) + (inV[tri.b] ? 1 : 0) + (inV[tri.c] ? 1 : 0);
                    bIn = (countIn >= nMode);
                }

                if (bIn)
                    AddF(tid);
            }
        }

    }
}
