// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Distributed under the Boost Software License, Version 1.0. http://www.boost.org/LICENSE_1_0.txt
using System;
using System.Collections.Generic;
using g3;

namespace gs
{
	/// <summary>
	/// Merge coincident edges.
	/// </summary>
	public class MergeCoincidentEdges
	{
		public DMesh3 Mesh;

		public double MergeDistance = MathUtil.ZeroTolerancef;

        public bool OnlyUniquePairs = false;

		public MergeCoincidentEdges(DMesh3 mesh)
		{
			Mesh = mesh;
		}

		double merge_r2;

		public virtual bool Apply() {
			merge_r2 = MergeDistance * MergeDistance;

            // construct hash table for edge midpoints
            MeshBoundaryEdgeMidpoints pointset = new MeshBoundaryEdgeMidpoints(this.Mesh);
			PointSetHashtable hash = new PointSetHashtable(pointset);
            int hashN = 64;
            if (Mesh.TriangleCount > 100000)   hashN = 128;
            if (Mesh.TriangleCount > 1000000)  hashN = 256;
			hash.Build(hashN);

			Vector3d a = Vector3d.Zero, b = Vector3d.Zero;
			Vector3d c = Vector3d.Zero, d = Vector3d.Zero;

			// find edge equivalence sets. First we find all other edges with same
			// midpoint, and then we check if endpoints are the same in second loop
			int[] buffer = new int[1024];
			List<int>[] EquivSets = new List<int>[Mesh.MaxEdgeID];
			HashSet<int> remaining = new HashSet<int>();
			foreach ( int eid in Mesh.BoundaryEdgeIndices() ) {
				Vector3d midpt = Mesh.GetEdgePoint(eid, 0.5);
				int N;
				while (hash.FindInBall(midpt, MergeDistance, buffer, out N) == false)
					buffer = new int[buffer.Length];
				if (N == 1 && buffer[0] != eid)
					throw new Exception("MergeCoincidentEdges.Apply: how could this happen?!");
				if (N <= 1)
					continue;  // unique edge

				Mesh.GetEdgeV(eid, ref a, ref b);

				// if same endpoints, add to equivalence set
				List<int> equiv = new List<int>(N - 1);
				for (int i = 0; i < N; ++i) {
					if (buffer[i] != eid) {
						Mesh.GetEdgeV(buffer[i], ref c, ref d);
						if ( is_same_edge(ref a, ref b, ref c, ref d))
							equiv.Add(buffer[i]);
					}
				}
				if (equiv.Count > 0) {
					EquivSets[eid] = equiv;
					remaining.Add(eid);
				}
			}

			// [TODO] could replace remaining hashset w/ PQ, and use conservative count?

			// add potential duplicate edges to priority queue, sorted by
			// number of possible matches. 
			// [TODO] Does this need to be a PQ? Not updating PQ below anyway...
			DynamicPriorityQueue<DuplicateEdge> Q = new DynamicPriorityQueue<DuplicateEdge>();
			foreach ( int i in remaining ) {
                if (OnlyUniquePairs) {
                    if (EquivSets[i].Count != 1)
                        continue;
                    foreach (int j in EquivSets[i]) {
                        if (EquivSets[j].Count != 1 || EquivSets[j][0] != i)
                            continue;
                    }
                }

                Q.Enqueue(new DuplicateEdge() { eid = i }, EquivSets[i].Count);
			}

			while ( Q.Count > 0 ) {
				DuplicateEdge e = Q.Dequeue();
				if (Mesh.IsEdge(e.eid) == false || EquivSets[e.eid] == null || remaining.Contains(e.eid) == false )
					continue;               // dealt with this edge already
                if (Mesh.IsBoundaryEdge(e.eid) == false)
                    continue;

				List<int> equiv = EquivSets[e.eid];

				// find viable match
				// [TODO] how to make good decisions here? prefer planarity?
				bool merged = false;
				int failed = 0;
				for (int i = 0; i < equiv.Count && merged == false; ++i ) {
					int other_eid = equiv[i];
					if ( Mesh.IsEdge(other_eid) == false || Mesh.IsBoundaryEdge(other_eid) == false )
						continue;

					DMesh3.MergeEdgesInfo info;
					MeshResult result = Mesh.MergeEdges(e.eid, other_eid, out info);
					if ( result != MeshResult.Ok ) {
						equiv.RemoveAt(i);
                        i--;

						EquivSets[other_eid].Remove(e.eid);
						//Q.UpdatePriority(...);  // how need ref to queue node to do this...??
						//   maybe equiv set is queue node??

						failed++;
					} else {
						// ok we merged, other edge is no longer free
						merged = true;
						EquivSets[other_eid] = null;
						remaining.Remove(other_eid);
					}
				}

				if ( merged ) {
					EquivSets[e.eid] = null;
					remaining.Remove(e.eid);					
				} else {
					// should we do something else here? doesn't make sense to put
					// back into Q, as it should be at the top, right?
					EquivSets[e.eid] = null;
					remaining.Remove(e.eid);
				}

			}

			return true;
		}



		bool is_same_edge(ref Vector3d a, ref Vector3d b, ref Vector3d c, ref Vector3d d) {
			return (a.DistanceSquared(c) < merge_r2 && b.DistanceSquared(d) < merge_r2) ||
				(a.DistanceSquared(d) < merge_r2 && b.DistanceSquared(c) < merge_r2);
		}



		class DuplicateEdge : DynamicPriorityQueueNode {
			public int eid;
		}


    }
}
