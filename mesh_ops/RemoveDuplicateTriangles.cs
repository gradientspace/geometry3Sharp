// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Distributed under the Boost Software License, Version 1.0. http://www.boost.org/LICENSE_1_0.txt
using System;
using System.Collections.Generic;
using g3;

namespace gs
{
	/// <summary>
	/// Remove duplicate triangles.
	/// </summary>
	public class RemoveDuplicateTriangles
	{
		public DMesh3 Mesh;

		public double VertexTolerance = MathUtil.ZeroTolerancef;
        public bool CheckOrientation = true;

        public int Removed = 0;

		public RemoveDuplicateTriangles(DMesh3 mesh)
		{
			Mesh = mesh;
		}


		public virtual bool Apply() {
            Removed = 0;

            double merge_r2 = VertexTolerance * VertexTolerance;

            // construct hash table for edge midpoints
            TriCentroids pointset = new TriCentroids() { Mesh = this.Mesh };
			PointSetHashtable hash = new PointSetHashtable(pointset);
            int hashN = (Mesh.TriangleCount > 100000) ? 128 : 64;
			hash.Build(hashN);

            Vector3d a = Vector3d.Zero, b = Vector3d.Zero, c = Vector3d.Zero;
            Vector3d x = Vector3d.Zero, y = Vector3d.Zero, z = Vector3d.Zero;

            int MaxTriID = Mesh.MaxTriangleID;

			// remove duplicate triangles
			int[] buffer = new int[1024];
            for ( int tid = 0; tid < MaxTriID; ++tid ) {
                if (!Mesh.IsTriangle(tid))
                    continue;

                Vector3d centroid = Mesh.GetTriCentroid(tid);
				int N;
				while (hash.FindInBall(centroid, VertexTolerance, buffer, out N) == false)
					buffer = new int[buffer.Length];
				if (N == 1 && buffer[0] != tid)
					throw new Exception("RemoveDuplicateTriangles.Apply: how could this happen?!");
				if (N <= 1)
					continue;  // unique edge

				Mesh.GetTriVertices(tid, ref a, ref b, ref c);
                Vector3d n = MathUtil.Normal(a, b, c);

				for (int i = 0; i < N; ++i) {
					if (buffer[i] != tid) {
                        Mesh.GetTriVertices(buffer[i], ref x, ref y, ref z);
                        if (is_same_triangle(ref a, ref b, ref c, ref x, ref y, ref z, merge_r2) == false)
                            continue;

                        if (CheckOrientation) {
                            Vector3d n2 = MathUtil.Normal(x, y, z);
                            if (n.Dot(n2) < 0.99)
                                continue;
                        }

                        MeshResult result =  Mesh.RemoveTriangle(buffer[i], true, false);
                        if (result == MeshResult.Ok)
                            ++Removed;
					}
				}
			}

			return true;
		}



		bool is_same_triangle(ref Vector3d a, ref Vector3d b, ref Vector3d c,
                              ref Vector3d x, ref Vector3d y, ref Vector3d z, double tolSqr) {
            if ( a.DistanceSquared(x) < tolSqr) {
                if (b.DistanceSquared(y) < tolSqr && c.DistanceSquared(z) < tolSqr)
                    return true;
                if (b.DistanceSquared(z) < tolSqr && c.DistanceSquared(y) < tolSqr)
                    return true;
            } else if (a.DistanceSquared(y) < tolSqr) {
                if (b.DistanceSquared(x) < tolSqr && c.DistanceSquared(z) < tolSqr)
                    return true;
                if (b.DistanceSquared(z) < tolSqr && c.DistanceSquared(x) < tolSqr)
                    return true;
            } else if (a.DistanceSquared(z) < tolSqr) {
                if (b.DistanceSquared(x) < tolSqr && c.DistanceSquared(y) < tolSqr)
                    return true;
                if (b.DistanceSquared(y) < tolSqr && c.DistanceSquared(x) < tolSqr)
                    return true;
            }
            return false;
		}



		// present mesh tri centroids as a PointSet
		class TriCentroids : IPointSet {
			public DMesh3 Mesh;

			public int VertexCount { get { return Mesh.TriangleCount; } }
			public int MaxVertexID { get { return Mesh.MaxTriangleID; } }

			public bool HasVertexNormals { get { return false; } }
			public bool HasVertexColors { get { return false; } }

			public Vector3d GetVertex(int i) { return Mesh.GetTriCentroid(i); }
			public Vector3f GetVertexNormal(int i) { return Vector3f.AxisY; }
			public Vector3f GetVertexColor(int i) { return Vector3f.One; }

			public bool IsVertex(int tID) { return Mesh.IsTriangle(tID); }

			// iterators allow us to work with gaps in index space
			public System.Collections.Generic.IEnumerable<int> VertexIndices() {
				return Mesh.TriangleIndices();
			}

            public int Timestamp { get { return Mesh.Timestamp; } }

        }



	}
}
