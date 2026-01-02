using System;
using System.Collections.Generic;

namespace g3
{

	/// <summary>
	/// Cut the Mesh with the Plane. The *positive* side, ie (p-o).n > 0, is removed.
    /// If possible, returns boundary loop(s) along cut
	/// (this will fail if cut intersected with holes in mesh).
	/// Also FillHoles() for a topological fill. Or use CutLoops and fill yourself.
	/// 
	/// Algorithm is:
	///    1) find all edge crossings
	///    2) Do edge splits at crossings
	///    3) delete all vertices on positive side
    ///    4) (optionally) collapse any degenerate boundary edges 
	///    5) find loops through valid boundary edges (ie connected to splits, or on-plane edges)
	/// 
	/// [TODO] could run into trouble w/ on-plane degenerate triangles. Should optionally
	///   discard any triangles with all vertex distances < epsilon. But this complicates
	///   boundary edge tracking...
	/// 
	/// </summary>
	public class MeshPlaneCut
	{
        // Inputs
		public DMesh3 Mesh;
		public Vector3d PlaneOrigin;
		public Vector3d PlaneNormal;

        // a plane cut very near a vertex can result in degenerate edges on the open loops/spans, which 
        // can cause problems downstream (eg if hole-filling). It is easy for us to collapse these before
        // we construct the loops.
        public bool CollapseDegenerateEdgesOnCut = true;

        // the min-edge-length if we are collapsing degenerate edges
        public double DegenerateEdgeTol = MathUtil.ZeroTolerancef;

        // if non-null, we will only iterate through these edges
        public MeshFaceSelection CutFaceSet = null;

        // Outputs
		public List<EdgeLoop> CutLoops;
        public List<EdgeSpan> CutSpans;
        public bool CutLoopsFailed = false;		// set to true if we could not compute cut loops/spans
        public bool FoundOpenSpans = false;     // set to true if we found open spans in cut

		public List<int[]> LoopFillTriangles;

		/// <summary>
		/// Cut mesh with plane. Assumption is that plane normal is Z value.
		/// </summary>
		public MeshPlaneCut(DMesh3 mesh, Vector3d origin, Vector3d normal)
		{
			Mesh = mesh;
			PlaneOrigin = origin;
			PlaneNormal = normal;
		}


		public virtual ValidationStatus Validate()
		{
			// [TODO]
			return ValidationStatus.Ok;
		}


		public virtual bool Cut()
		{
			double invalidDist = double.MinValue;

            MeshEdgeSelection CutEdgeSet = null;
            MeshVertexSelection CutVertexSet = null;
            if (CutFaceSet != null) {
                CutEdgeSet = new MeshEdgeSelection(Mesh, CutFaceSet);
                CutVertexSet = new MeshVertexSelection(Mesh, CutEdgeSet);
            }

			// compute signs
			int MaxVID = Mesh.MaxVertexID;
			double[] signs = new double[MaxVID];
			gParallel.ForEach(Interval1i.Range(MaxVID), (vid) => {
				if (Mesh.IsVertex(vid)) {
					Vector3d v = Mesh.GetVertex(vid);
					signs[vid] = (v - PlaneOrigin).Dot(PlaneNormal);
				} else
					signs[vid] = invalidDist;
			});

			HashSet<int> ZeroEdges = new HashSet<int>();
			HashSet<int> ZeroVertices = new HashSet<int>();
			HashSet<int> OnCutEdges = new HashSet<int>();

			// have to skip processing of new edges. If edge id
			// is > max at start, is new. Otherwise if in NewEdges list, also new.
			int MaxEID = Mesh.MaxEdgeID;
			HashSet<int> NewEdges = new HashSet<int>();

            IEnumerable<int> edgeItr = Interval1i.Range(MaxEID);
            if (CutEdgeSet != null)
                edgeItr = CutEdgeSet;

            // cut existing edges with plane, using edge split
            foreach (int eid in edgeItr) { 
				if (Mesh.IsEdge(eid) == false)
					continue;
				if (eid >= MaxEID || NewEdges.Contains(eid))
					continue;

				Index2i ev = Mesh.GetEdgeV(eid);
				double f0 = signs[ev.a];
				double f1 = signs[ev.b];

				// If both signs are 0, this edge is on-contour
				// If one sign is 0, that vertex is on-contour
				int n0 = (Math.Abs(f0) < MathUtil.Epsilon) ? 1 : 0;
				int n1 = (Math.Abs(f1) < MathUtil.Epsilon) ? 1 : 0;
				if (n0 + n1 > 0) {
					if (n0 + n1 == 2)
						ZeroEdges.Add(eid);
					else
						ZeroVertices.Add((n0 == 1) ? ev[0] : ev[1]);
					continue;
				}

				// no crossing
				if (f0 * f1 > 0)
					continue;

				DMesh3.EdgeSplitInfo splitInfo;
				MeshResult result = Mesh.SplitEdge(eid, out splitInfo);
				if (result != MeshResult.Ok) {
					throw new Exception("MeshPlaneCut.Cut: failed in SplitEdge");
					//return false;
				}

				// SplitEdge just bisects edge - use plane intersection instead
				double t = f0 / (f0 - f1);
				Vector3d newPos = (1 - t) * Mesh.GetVertex(ev.a) + (t) * Mesh.GetVertex(ev.b);
				Mesh.SetVertex(splitInfo.vNew, newPos);

                // TODO something kinda wrong here because OnCutEdges will end up including interior edges too...
                // (possibly due to later cuts?). Probably should be doing this triangle-based, not edge-based...
				NewEdges.Add(splitInfo.eNewBN);
				NewEdges.Add(splitInfo.eNewCN);  OnCutEdges.Add(splitInfo.eNewCN);
				if (splitInfo.eNewDN != DMesh3.InvalidID) {
					NewEdges.Add(splitInfo.eNewDN);
					OnCutEdges.Add(splitInfo.eNewDN);
				}
			}

            // remove one-rings of all positive-side vertices. 
            IEnumerable<int> vertexSet = Interval1i.Range(MaxVID);
            if (CutVertexSet != null)
                vertexSet = CutVertexSet;
            foreach ( int vid in vertexSet ) { 
				if (signs[vid] > 0 && Mesh.IsVertex(vid))
					Mesh.RemoveVertex(vid, true, false);
			}

            // This is older code that collapses more than just on-cut boundary edges, it
            // also collapses interior edges connected to the cut. Possibly this was an
            // unintended bug, or possibly it was desired behavior...tbh I'm not sure.
            // Might be worth revisiting as it does seem like it would handle degenerate
            // edges connected *to* the cut loops/spans, and not just along them, which
            // might be useful in some cases?
            //if (CollapseDegenerateEdgesOnCut) {
            //    collapse_degenerate_edges(OnCutEdges, ZeroEdges);
            //}


			// ok now we extract boundary loops, but restricted
			// to either the zero-edges we found, or the edges we created! bang!!
			Func<int, bool> CutEdgeFilterF = (eid) => {
				if (OnCutEdges.Contains(eid) || ZeroEdges.Contains(eid))
					return true;
				return false;
			};
			try {
				MeshBoundaryLoops loops = new MeshBoundaryLoops(Mesh, false);
				loops.EdgeFilterF = CutEdgeFilterF;
				loops.Compute();

				CutLoops = loops.Loops;
                CutSpans = loops.Spans;
				CutLoopsFailed = false;
                FoundOpenSpans = CutSpans.Count > 0;

			} catch {
				CutLoops = new List<EdgeLoop>();
				CutLoopsFailed = true;
			}

            // if we want degenerate edge collapses, do that now, it's expensive though...
            if (CollapseDegenerateEdgesOnCut)
                do_simplify_cut_edges();

            return true;

		} // Cut()



        protected void do_simplify_cut_edges()
        {
            // TODO: can't we do this per-loop/span? no reason to do them all at once...

            // have to rebuild list of all cut edges
            HashSet<int> AllCutEdges = new();
            foreach (EdgeLoop loop in CutLoops) {
                foreach (int eid in loop.Edges)
                    AllCutEdges.Add(eid);
            }
            foreach (EdgeSpan span in CutSpans) {
                foreach (int eid in span.Edges)
                    AllCutEdges.Add(eid);
            }

            // run simplification
            int NumCollapsed = simplify_cut_edges(AllCutEdges);
            if (NumCollapsed == 0)
                return;

            // have to rebuild boundary loops/spans (hopefully this works...?)
            Func<int, bool> CutEdgeFilter = (eid) => {
                return AllCutEdges.Contains(eid);
            };
            try {
                MeshBoundaryLoops loops = new MeshBoundaryLoops(Mesh, false);
                loops.EdgeFilterF = CutEdgeFilter;
                loops.Compute();

                CutLoops = loops.Loops;
                CutSpans = loops.Spans;
                CutLoopsFailed = false;
                FoundOpenSpans = CutSpans.Count > 0;

            } catch {
                CutLoops = new List<EdgeLoop>();
                CutLoopsFailed = true;
            }
        }


        // incrementally collapse edges along the cut (ie must be boundary edges),
        // trying to achieve a min-edge-length requirement
        protected int simplify_cut_edges(HashSet<int> OnCutEdges)
        {
            var edge_len_func = (int eid) => { Index2i ev = Mesh.GetEdgeV(eid); return Mesh.GetVertex(ev.a).Distance(Mesh.GetVertex(ev.b)); };

            int[] cur_edges = new int[OnCutEdges.Count];
            double[] edge_lengths = new double[OnCutEdges.Count];
            Vector3d a = Vector3d.Zero, b = Vector3d.Zero;
            int collapsed = 0, total_num_collapsed = 0;

            // The are two stages, each with one or more iterations
            int stage_num = 0;
            do {
                collapsed = 0;

                // construct list of edges to process, sorted by increasing edge length
                // (if collapsed == 0 on last do-loop iter, we don't need to do this again...)
                int N = 0;
                foreach (int eid in OnCutEdges)
                    cur_edges[N++] = eid;
                for (int i = 0; i < N; ++i)
                    edge_lengths[i] = edge_len_func(cur_edges[i]);
                Array.Sort(edge_lengths, cur_edges, 0, N);

                for ( int idx = 0; idx < N; ++idx) { 
                    int eid = cur_edges[idx];
                    //Debug.Assert(Mesh.IsEdge(eid));
                    //Debug.Assert(Mesh.IsBoundaryEdge(eid));

                    // recalc edge length in case it changed due to adjacent collapses...
                    edge_lengths[idx] = edge_len_func(eid);
                    double eid_len = edge_lengths[idx];

                    // probably should break here at some point...once edges get very large
                    // we aren't likely to encounter any further collapses
                    if (edge_lengths[idx] > DegenerateEdgeTol)
                        continue;

                    Index2i ev = Mesh.GetEdgeV(eid);
                    Index2i et = Mesh.GetEdgeT(eid);
                    a = Mesh.GetVertex(ev.a); b = Mesh.GetVertex(ev.b);
                    Vector3d midpoint = 0.5 * (a+b);

                    // compute current max distance from a/b to connected cut edges (should only be one except in messy situations...)
                    double max_a_nbr_len = 0;
                    foreach (int nbr_eid in Mesh.VtxEdgesItr(ev.a)) {
                        if (nbr_eid == eid || OnCutEdges.Contains(nbr_eid) == false) continue;
                        double nbr_len = Mesh.GetVertex(Mesh.edge_other_v(nbr_eid, ev.a)).Distance(a);
                        max_a_nbr_len = Math.Max(max_a_nbr_len, nbr_len);
                    }
                    double max_b_nbr_len = 0;
                    foreach (int nbr_eid in Mesh.VtxEdgesItr(ev.b)) {
                        if (nbr_eid == eid || OnCutEdges.Contains(nbr_eid) == false) continue;
                        double nbr_len = Mesh.GetVertex(Mesh.edge_other_v(nbr_eid, ev.b)).Distance(b);
                        max_b_nbr_len = Math.Max(max_b_nbr_len, nbr_len);
                    }

                    // compute max edge length that will result if we collapse to a, b, or midpoint
                    // (these distances are only correct on straight lines...)
                    double to_a_len = max_b_nbr_len + eid_len;
                    double to_b_len = max_a_nbr_len + eid_len;
                    double midpoint_len = Math.Max(max_b_nbr_len+eid_len/2, max_a_nbr_len+eid_len/2);

                    // choose which collapse to do, if we can (disallow normal flips)
                    // collapse will make one or both adjacent edges larger, we want to limit that but <= tolerance is too strict
                    // preferentially collapse to the midpoint, this distributes lengths the best
                    // otherwise collapse to a or to b, this keeps one length the same and the other grows
                    // in stage 0 we try to control edge lengths, in stage 1 we don't

                    double MaxEdgeTol = 2*DegenerateEdgeTol;
                    int keep = -1, collapse = -1; Vector3d pos = Vector3d.Zero;
                    if (midpoint_len < MaxEdgeTol && MeshRefinerBase.collapse_creates_flip_or_invalid(Mesh, ev.a, ev.b, ref midpoint, et.a, et.b) == false) {
                        keep = ev.a; collapse = ev.b; pos = midpoint;
                    } else if (to_a_len < MaxEdgeTol && MeshRefinerBase.collapse_creates_flip_or_invalid(Mesh, ev.a, ev.b, ref a, et.a, et.b) == false) {
                        keep = ev.a; collapse = ev.b; pos = a;
                    } else if (to_b_len < MaxEdgeTol && MeshRefinerBase.collapse_creates_flip_or_invalid(Mesh, ev.b, ev.a, ref b, et.a, et.b) == false) {
                        keep = ev.b; collapse = ev.a; pos = b;
                    } else if (stage_num == 1 && MeshRefinerBase.collapse_creates_flip_or_invalid(Mesh, ev.a, ev.b, ref midpoint, et.a, et.b) == false) {
                        keep = ev.a; collapse = ev.b; pos = midpoint;
                    } else if (stage_num == 1 && MeshRefinerBase.collapse_creates_flip_or_invalid(Mesh, ev.b, ev.a, ref midpoint, et.a, et.b) == false) {
                        keep = ev.b; collapse = ev.a; pos = midpoint;
                    } else {
                        continue;       // can't collapse this edge right now...
                    }

                    // do the collapse
                    DMesh3.EdgeCollapseInfo collapseInfo;
                    MeshResult result = Mesh.CollapseEdge(keep, collapse, out collapseInfo);
                    if (result == MeshResult.Ok) {
                        Mesh.SetVertex(collapseInfo.vKept, pos);
                        collapsed++; total_num_collapsed++;
                        OnCutEdges.Remove(eid);
                    }
                }
                
                if (collapsed == 0)     // proceed to second stage
                    stage_num++;

            } while (collapsed != 0 || stage_num < 2);

            return total_num_collapsed;
        }


        // this version is not currently callled - see comment on the commented-out block that calls it
        protected void collapse_degenerate_edges(HashSet<int> OnCutEdges, HashSet<int> ZeroEdges)
        {
            HashSet<int>[] sets = new HashSet<int>[2] { OnCutEdges, ZeroEdges };

            double tol2 = DegenerateEdgeTol * DegenerateEdgeTol;
            Vector3d a = Vector3d.Zero, b = Vector3d.Zero;
            int collapsed = 0;
            do {
                collapsed = 0;
                foreach (var edge_set in sets) {
                    foreach (int eid in edge_set) {
                        if (Mesh.IsEdge(eid) == false)
                            continue;
                        Mesh.GetEdgeV(eid, ref a, ref b);
                        if (a.DistanceSquared(b) > tol2)
                            continue;

                        Index2i ev = Mesh.GetEdgeV(eid);
                        DMesh3.EdgeCollapseInfo collapseInfo;
                        MeshResult result = Mesh.CollapseEdge(ev.a, ev.b, out collapseInfo);
                        if (result == MeshResult.Ok)
                            collapsed++;
                    }
                }
            } while (collapsed != 0);
        }





		/// <summary>
		/// A quick-and-dirty hole filling. If you want something better,
		/// process the returned CutLoops yourself.
		/// </summary>
		public bool FillHoles(int constantGroupID = -1)
		{
			bool bAllOk = true;

			LoopFillTriangles = new List<int[]>(CutLoops.Count);

			foreach ( EdgeLoop loop in CutLoops) {
				SimpleHoleFiller filler = new SimpleHoleFiller(Mesh, loop);
				int gid = (constantGroupID >= 0) ? constantGroupID : Mesh.AllocateTriangleGroup();
				if ( filler.Fill(gid) ) {
					bAllOk = false;
					LoopFillTriangles.Add(filler.NewTriangles);
				} else {
					LoopFillTriangles.Add(null);
				}
			}

			return bAllOk;

		} // FillHoles




	}
}
