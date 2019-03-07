// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Distributed under the Boost Software License, Version 1.0. http://www.boost.org/LICENSE_1_0.txt
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using g3;

namespace gs
{
	public class MeshRepairOrientation
	{
		public DMesh3 Mesh;

		DMeshAABBTree3 spatial;
		protected DMeshAABBTree3 Spatial {
			get {
				if (spatial == null)
					spatial = new DMeshAABBTree3(Mesh, true);
				return spatial; 
			}
		}

		public MeshRepairOrientation(DMesh3 mesh3, DMeshAABBTree3 spatial = null)
		{
			Mesh = mesh3;
			this.spatial = spatial;
		}


		class Component
		{
			public List<int> triangles;
			public double outFacing;
			public double inFacing;
		}
		List<Component> Components = new List<Component>();




		// TODO:
		//  - (in merge coincident) don't merge tris with same/opposite normals (option)
		//  - after orienting components, try to find adjacent open components and
		//    transfer orientation between them
		//  - orient via nesting


		public void OrientComponents()
		{
			Components = new List<Component>();

			HashSet<int> remaining = new HashSet<int>(Mesh.TriangleIndices());
			List<int> stack = new List<int>();
			while (remaining.Count > 0) {
				Component c = new Component();
				c.triangles = new List<int>();

				stack.Clear();
				int start = remaining.First();
				remaining.Remove(start);
				c.triangles.Add(start);
				stack.Add(start);
				while (stack.Count > 0) {
					int cur = stack[stack.Count - 1];
					stack.RemoveAt(stack.Count - 1);
					Index3i tcur = Mesh.GetTriangle(cur);

					Index3i nbrs = Mesh.GetTriNeighbourTris(cur);
					for (int j = 0; j < 3; ++j) {
						int nbr = nbrs[j];
						if (remaining.Contains(nbr) == false)
							continue;

						int a = tcur[j];
						int b = tcur[(j+1)%3];

						Index3i tnbr = Mesh.GetTriangle(nbr);
						if (IndexUtil.find_tri_ordered_edge(b, a, ref tnbr) == DMesh3.InvalidID) {
							Mesh.ReverseTriOrientation(nbr);
						}
						stack.Add(nbr);
						remaining.Remove(nbr);
						c.triangles.Add(nbr);
					}

				}

				Components.Add(c);
			}
		}





		public void ComputeStatistics()
		{
			var s = this.Spatial;  // make sure this exists
            // Cannot do in parallel because we set a filter on spatial DS. 
            // Also we are doing rays in parallel anyway...
            foreach ( var c in Components ) {
                compute_statistics(c);
            }
		}
		void compute_statistics(Component c)
		{
			int NC = c.triangles.Count;
			c.inFacing = c.outFacing = 0;
            double dist = 2 * Mesh.CachedBounds.DiagonalLength;

            // only want to raycast triangles in this 
            HashSet<int> tris = new HashSet<int>(c.triangles);
            spatial.TriangleFilterF = tris.Contains;

            // We want to try to figure out what is 'outside' relative to the world.
            // Assumption is that faces we can hit from far away should be oriented outwards.
            // So, for each triangle we construct far-away points in positive and negative normal
            // direction, then raycast back towards the triangle. If we hit the triangle from
            // one side and not the other, that is evidence we should keep/reverse that triangle.
            // If it is not hit, or hit from both, that does not provide any evidence.
            // We collect up this keep/reverse evidence and use the larger to decide on the global orientation.

            SpinLock count_lock = new SpinLock();

			gParallel.BlockStartEnd(0, NC - 1, (a, b) => {
				for (int i = a; i <= b; ++i) {
					int ti = c.triangles[i];
					Vector3d normal, centroid; double area;
					Mesh.GetTriInfo(ti, out normal, out area, out centroid);
					if (area < MathUtil.ZeroTolerancef)
						continue;

                    // construct far away points
                    Vector3d pos_pt = centroid + dist * normal;
                    Vector3d neg_pt = centroid - dist * normal;

                    // raycast towards triangle from far-away point
                    int hit_pos = spatial.FindNearestHitTriangle(new Ray3d(pos_pt, -normal));
                    int hit_neg = spatial.FindNearestHitTriangle(new Ray3d(neg_pt, normal));
                    if (hit_pos != ti && hit_neg != ti)
                        continue;       // no evidence
                    if (hit_pos == ti && hit_neg == ti)
                        continue;       // no evidence (?)

                    bool taken = false;
                    count_lock.Enter(ref taken);

                    if (hit_neg == ti)
                        c.inFacing += area;
                    else if (hit_pos == ti)
                        c.outFacing += area;

                    count_lock.Exit();
				}
			});

            spatial.TriangleFilterF = null;
        }



		public void SolveGlobalOrientation()
		{
			ComputeStatistics();
			MeshEditor editor = new MeshEditor(Mesh);
			foreach (Component c in Components) {
				if (c.inFacing > c.outFacing) {
					editor.ReverseTriangles(c.triangles);
				}
			}
		}



	}
}
