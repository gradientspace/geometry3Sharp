using System;
using System.Collections.Generic;
using System.Linq;

namespace g3
{

    /// <summary>
    /// Cut mesh with a path of 2D line segments
    /// Assumptions:
    ///   - mesh vertex x/y coordinates are 2D coordinates we want to use. Replace PointF if this is not the case.
    ///   - segments of Curve lie entirely within UV-triangles
    /// </summary>
    public class MeshInsertUVPolyCurve
	{
		public DMesh3 Mesh;
        public PolyLine2d Curve;
        public bool IsLoop;             // if true, Curve is closed polygon

        // This function provides the UV-space coordinates. Default is to return vertex_pos.xy
        public Func<int, Vector2d> PointF;

        // this function sets UV-space coordinates. Default is to set x=x, y=y, z=0
        public Action<int, Vector2d> SetPointF;

        // the spans & loops take some compute time and can be disabled if you don't need it...
        public bool EnableCutSpansAndLoops = true;


        // Results

        // (ordered) vertex ids of Curve vertices after insertion into mesh
        public int[] CurveVertices;

        // (unordered) edges that lie on the curve.
        public HashSet<int> OnCutEdges;


        // Edge loops and spans inserted into the mesh by this operation
        public List<EdgeSpan> Spans;
        public List<EdgeLoop> Loops;



        public MeshInsertUVPolyCurve(DMesh3 mesh, PolyLine2d curve, bool isLoop = false)
		{
			Mesh = mesh;
            Curve = curve;
            IsLoop = isLoop;

            PointF = (vid) => { return Mesh.GetVertex(vid).xy; };
            SetPointF = (vid, pos) => { Mesh.SetVertex(vid, new Vector3d(pos.x, pos.y, 0)); };
        }


        public MeshInsertUVPolyCurve(DMesh3 mesh, Polygon2d loop)
        {
            Mesh = mesh;
            Curve = new PolyLine2d(loop.Vertices);
            IsLoop = true;

            PointF = (vid) => { return Mesh.GetVertex(vid).xy; };
            SetPointF = (vid, pos) => { Mesh.SetVertex(vid, new Vector3d(pos.x, pos.y, 0)); };
        }

        public MeshInsertUVPolyCurve(DMesh3 mesh, PolyLine2d path)
        {
            Mesh = mesh;
            Curve = new PolyLine2d(path.Vertices);
            IsLoop = false;

            PointF = (vid) => { return Mesh.GetVertex(vid).xy; };
            SetPointF = (vid, pos) => { Mesh.SetVertex(vid, new Vector3d(pos.x, pos.y, 0)); };
        }


        public virtual ValidationStatus Validate(double fDegenerateTol = MathUtil.ZeroTolerancef)
		{
            double dist_sqr_thresh = fDegenerateTol * fDegenerateTol;

            int nStop = IsLoop ? Curve.VertexCount - 1 : Curve.VertexCount;
            for ( int k = 0; k < nStop; ++k ) {
                Vector2d v0 = Curve[k];
                Vector2d v1 = Curve[(k + 1) % Curve.VertexCount];
                if (v0.DistanceSquared(v1) < dist_sqr_thresh)
                    return ValidationStatus.NearDenegerateInputGeometry;
            }

            foreach ( int eid in Mesh.EdgeIndices()) {
                Index2i ev = Mesh.GetEdgeV(eid);
                if (PointF(ev.a).DistanceSquared(PointF(ev.b)) < dist_sqr_thresh)
                    return ValidationStatus.NearDegenerateMeshEdges;
            }

			return ValidationStatus.Ok;
		}


        // (sequentially) find each triangle that path point lies in, and insert a vertex for
        // that point into mesh.
        void insert_corners()
        {
            PrimalQuery2d query = new PrimalQuery2d(PointF);

            // [TODO] can do everythnig up to PokeTriangle in parallel, 
            // except if we are poking same tri w/ multiple points!

            CurveVertices = new int[Curve.VertexCount];
            for ( int i = 0; i < Curve.VertexCount; ++i ) {
                Vector2d vInsert = Curve[i];
                bool inserted = false;

                foreach (int tid in Mesh.TriangleIndices()) {
                    Index3i tv = Mesh.GetTriangle(tid);
                    // [RMS] using unsigned query here because we do not need to care about tri CW/CCW orientation
                    //   (right? otherwise we have to explicitly invert mesh. Nothing else we do depends on tri orientation)
                    //int query_result = query.ToTriangle(vInsert, tv.a, tv.b, tv.c);
                    int query_result = query.ToTriangleUnsigned(vInsert, tv.a, tv.b, tv.c);
                    if (query_result == -1 || query_result == 0) {
                        Vector3d bary = MathUtil.BarycentricCoords(vInsert, PointF(tv.a), PointF(tv.b), PointF(tv.c));
                        int vid = insert_corner_from_bary(i, tid, bary);
                        if ( vid > 0 ) {    // this should be always happening..
                            CurveVertices[i] = vid;
                            inserted = true;

                            //Util.WriteDebugMesh(Mesh, string.Format("C:\\git\\geometry3SharpDemos\\geometry3Test\\test_output\\after_insert_corner_{0}.obj", i));
                            break;
                        }
                    } 
                }

                if (inserted == false) {
                    throw new Exception("MeshInsertUVPolyCurve.insert_corners: curve vertex " 
                        + i.ToString() + " is not inside or on any mesh triangle!");
                }
            }
        }



        // insert point at bary_coords inside tid. If point is at vtx, just use that vtx.
        // If it is on an edge, do an edge split. Otherwise poke face.
        int insert_corner_from_bary(int iCorner, int tid, Vector3d bary_coords, double tol = MathUtil.ZeroTolerance)
        {
            Vector2d vInsert = Curve[iCorner];
            Index3i tv = Mesh.GetTriangle(tid);

            // handle cases where corner is on a vertex
            if (bary_coords.x > 1 - tol)
                return tv.a;
            else if (bary_coords.y > 1 - tol)
                return tv.b;
            else if ( bary_coords.z > 1 - tol )
                return tv.c;

            // handle cases where corner is on an edge
            int split_edge = -1;
            if (bary_coords.x < tol)
                split_edge = 1;
            else if (bary_coords.y < tol)
                split_edge = 2;
            else if (bary_coords.z < tol)
                split_edge = 0;
            if (split_edge >= 0) {
                int eid = Mesh.GetTriEdge(tid, split_edge);

                DMesh3.EdgeSplitInfo split_info;
                MeshResult splitResult = Mesh.SplitEdge(eid, out split_info);
                if (splitResult != MeshResult.Ok)
                    throw new Exception("MeshInsertUVPolyCurve.insert_corner_special: edge split failed in case sum==2");
                SetPointF(split_info.vNew, vInsert);
                return split_info.vNew;
            }

            // otherwise corner is inside triangle
            DMesh3.PokeTriangleInfo pokeinfo;
            MeshResult result = Mesh.PokeTriangle(tid, bary_coords, out pokeinfo);
            if (result != MeshResult.Ok)
                throw new Exception("MeshInsertUVPolyCurve.insert_corner_special: face poke failed!");

            SetPointF(pokeinfo.new_vid, vInsert);
            return pokeinfo.new_vid;
        }



        


        public virtual bool Apply()
		{
            insert_corners();

            // [RMS] not using this?
            //HashSet<int> corner_v = new HashSet<int>(CurveVertices);

            // not sure we need to track all of these
            HashSet<int> ZeroEdges = new HashSet<int>();
            HashSet<int> ZeroVertices = new HashSet<int>();
            OnCutEdges = new HashSet<int>();

            // loop over segments, insert each one in sequence
            int N = (IsLoop) ? Curve.VertexCount : Curve.VertexCount - 1;
            for ( int si = 0; si < N; ++si ) {
                int i0 = si;
                int i1 = (si + 1) % Curve.VertexCount;
                Segment2d seg = new Segment2d(Curve[i0], Curve[i1]);

                int i0_vid = CurveVertices[i0];
                int i1_vid = CurveVertices[i1];

                // If these vertices are already connected by an edge, we can just continue.
                int existing_edge = Mesh.FindEdge(i0_vid, i1_vid);
                if ( existing_edge != DMesh3.InvalidID ) {
                    OnCutEdges.Add(existing_edge);
                    continue;
                }

                // compute edge-crossing signs
                // [TODO] could walk along mesh from a to b, rather than computing for entire mesh?
                int MaxVID = Mesh.MaxVertexID;
                int[] signs = new int[MaxVID];
                gParallel.ForEach(Interval1i.Range(MaxVID), (vid) => {
                    if (Mesh.IsVertex(vid)) {
                        if (vid == i0_vid || vid == i1_vid) {
                            signs[vid] = 0;
                        } else {
                            Vector2d v2 = PointF(vid);
                            // tolerance defines band in which we will consider values to be zero
                            signs[vid] = seg.WhichSide(v2, MathUtil.ZeroTolerance);
                        }
                    } else
                        signs[vid] = int.MaxValue;
                });

                // have to skip processing of new edges. If edge id
                // is > max at start, is new. Otherwise if in NewEdges list, also new.
                // (need both in case we re-use an old edge index)
                int MaxEID = Mesh.MaxEdgeID;
                HashSet<int> NewEdges = new HashSet<int>();
                HashSet<int> NewCutVertices = new HashSet<int>();
                NewCutVertices.Add(i0_vid);
                NewCutVertices.Add(i1_vid);

                // cut existing edges with segment
                for (int eid = 0; eid < MaxEID; ++eid) {
                    if (Mesh.IsEdge(eid) == false)
                        continue;
                    if (eid >= MaxEID || NewEdges.Contains(eid))
                        continue;

                    // cannot cut boundary edges?
                    if (Mesh.IsBoundaryEdge(eid))
                        continue;

                    Index2i ev = Mesh.GetEdgeV(eid);
                    int eva_sign = signs[ev.a];
                    int evb_sign = signs[ev.b];

                    bool eva_in_segment = false;
                    if ( eva_sign == 0 ) 
                        eva_in_segment = Math.Abs(seg.Project(PointF(ev.a))) < (seg.Extent + MathUtil.ZeroTolerance);
                    bool evb_in_segment = false;
                    if (evb_sign == 0)
                        evb_in_segment = Math.Abs(seg.Project(PointF(ev.b))) < (seg.Extent + MathUtil.ZeroTolerance);

                    // If one or both vertices are on-segment, we have special case.
                    // If just one vertex is on the segment, we can skip this edge.
                    // If both vertices are on segment, then we can just re-use this edge.
                    if (eva_in_segment || evb_in_segment) {
                        if (eva_in_segment && evb_in_segment) {
                            ZeroEdges.Add(eid);
                            OnCutEdges.Add(eid); 
                        } else {
                            ZeroVertices.Add(eva_in_segment ? ev.a : ev.b);
                        }
                        continue;
                    }

                    // no crossing
                    if (eva_sign * evb_sign > 0)
                        continue;

                    // compute segment/segment intersection
                    Vector2d va = PointF(ev.a);
                    Vector2d vb = PointF(ev.b);
                    Segment2d edge_seg = new Segment2d(va, vb);
                    IntrSegment2Segment2 intr = new IntrSegment2Segment2(seg, edge_seg);
                    intr.Compute();
                    if (intr.Type == IntersectionType.Segment) {
                        // [RMS] we should have already caught this above, so if it happens here it is probably spurious?
                        // we should have caught this case above, but numerics are different so it might occur again
                        ZeroEdges.Add(eid);
                        OnCutEdges.Add(eid);
                        continue;
                    } else if (intr.Type != IntersectionType.Point) {
                        continue; // no intersection
                    }
                    Vector2d x = intr.Point0;

                    // this case happens if we aren't "on-segment" but after we do the test the intersection pt 
                    // is within epsilon of one end of the edge. This is a spurious t-intersection and we
                    // can ignore it. Some other edge should exist that picks up this vertex as part of it.
                    // [TODO] what about if this edge is degenerate?
                    bool x_in_segment = Math.Abs(edge_seg.Project(x)) < (edge_seg.Extent - MathUtil.ZeroTolerance);
                    if (! x_in_segment ) {
                        continue;
                    }

                    // split edge at this segment
                    DMesh3.EdgeSplitInfo splitInfo;
                    MeshResult result = Mesh.SplitEdge(eid, out splitInfo);
                    if (result != MeshResult.Ok) {
                        throw new Exception("MeshInsertUVSegment.Cut: failed in SplitEdge");
                        //return false;
                    }

                    // move split point to intersection position
                    SetPointF(splitInfo.vNew, x);
                    NewCutVertices.Add(splitInfo.vNew);

                    NewEdges.Add(splitInfo.eNewBN);
                    NewEdges.Add(splitInfo.eNewCN);

                    // some splits - but not all - result in new 'other' edges that are on
                    // the polypath. We want to keep track of these edges so we can extract loop later.
                    Index2i ecn = Mesh.GetEdgeV(splitInfo.eNewCN);
                    if (NewCutVertices.Contains(ecn.a) && NewCutVertices.Contains(ecn.b))
                        OnCutEdges.Add(splitInfo.eNewCN);

                    // since we don't handle bdry edges this should never be false, but maybe we will handle bdry later...
                    if (splitInfo.eNewDN != DMesh3.InvalidID) {
                        NewEdges.Add(splitInfo.eNewDN);
                        Index2i edn = Mesh.GetEdgeV(splitInfo.eNewDN);
                        if (NewCutVertices.Contains(edn.a) && NewCutVertices.Contains(edn.b))
                            OnCutEdges.Add(splitInfo.eNewDN);
                    }
                }
            }


            //MeshEditor editor = new MeshEditor(Mesh);
            //foreach (int eid in OnCutEdges)
            //    editor.AppendBox(new Frame3f(Mesh.GetEdgePoint(eid, 0.5)), 0.1f);
            //Util.WriteDebugMesh(Mesh, string.Format("C:\\git\\geometry3SharpDemos\\geometry3Test\\test_output\\after_inserted.obj"));


            // extract the cut paths
            if (EnableCutSpansAndLoops)
                find_cut_paths(OnCutEdges);

            return true;

		} // Apply()







        /// <summary>
        /// Generally after calling Apply(), we have over-triangulated the mesh, because we have split
        /// the original edges multiple times, etc. This function will walk the edges and collapse 
        /// the unnecessary edges/vertices along the inserted loops. 
        /// </summary>
        public void Simplify()
        {
            for ( int k = 0; k < Loops.Count; ++k) {
                EdgeLoop newloop = simplify(Loops[k]);
                Loops[k] = newloop;
            }
        }

        // Walk along edge loop and collapse to inserted curve vertices. 
        EdgeLoop simplify(EdgeLoop loop)
        {
            HashSet<int> curve_verts = new HashSet<int>(CurveVertices);

            List<int> remaining_edges = new List<int>();
            for ( int li = 0; li < loop.EdgeCount; ++li) {
                int eid = loop.Edges[li];
                Index2i ev = Mesh.GetEdgeV(eid);
                
                // cannot collapse edge between two "original" polygon verts (ie created by face pokes)
                if (curve_verts.Contains(ev.a) && curve_verts.Contains(ev.b)) {
                    remaining_edges.Add(eid);
                    continue;
                }

                // if we have an original vert, we need to keep it (and its position!)
                int keep = ev.a, discard = ev.b;
                Vector3d set_to = Vector3d.Zero;
                if (curve_verts.Contains(ev.b)) {
                    keep = ev.b;
                    discard = ev.a;
                    set_to = Mesh.GetVertex(ev.b);
                } else if ( curve_verts.Contains(ev.a) ) {
                    set_to = Mesh.GetVertex(ev.a);
                } else {
                    set_to = 0.5 * (Mesh.GetVertex(ev.a) + Mesh.GetVertex(ev.b));
                }
                
                // make sure we are not going to flip any normals
                // [OPTIMIZATION] May be possible to do this more efficiently because we know we are in
                //   2D and each tri should have same cw/ccw orientation. But we don't quite "know" we
                //   are in 2D here, as CollapseEdge function is operating on the mesh coordinates...
                if (MeshUtil.CheckIfCollapseCreatesFlip(Mesh, eid, set_to)) {
                    remaining_edges.Add(eid);
                    continue;
                }

                // cannot collapse if the 'other' edges we would discard are OnCutEdges. This would
                // result in loop potentially being broken. bad!
                Index4i einfo = Mesh.GetEdge(eid);
                int c = IndexUtil.find_tri_other_vtx(keep, discard, Mesh.GetTriangle(einfo.c));
                int d = IndexUtil.find_tri_other_vtx(keep, discard, Mesh.GetTriangle(einfo.d));
                int ec = Mesh.FindEdge(discard, c);
                int ed = Mesh.FindEdge(discard, d);
                if (OnCutEdges.Contains(ec) || OnCutEdges.Contains(ed)) {
                    remaining_edges.Add(eid);
                    continue;
                }

                // do collapse and update internal data structures
                DMesh3.EdgeCollapseInfo collapse;
                MeshResult result = Mesh.CollapseEdge(keep, discard, out collapse);
                if ( result == MeshResult.Ok ) {
                    Mesh.SetVertex(collapse.vKept, set_to);
                    OnCutEdges.Remove(collapse.eCollapsed);
                } else {
                    remaining_edges.Add(eid);
                }
            }

            return EdgeLoop.FromEdges(Mesh, remaining_edges);
        }





        void find_cut_paths(HashSet<int> CutEdges)
        {
            Spans = new List<EdgeSpan>();
            Loops = new List<EdgeLoop>();

            // [TODO] what about if vert appears more than twice in list? we should check for that!

            HashSet<int> Remaining = new HashSet<int>(CutEdges);
            while ( Remaining.Count > 0 ) {
                int start_edge = Remaining.First();
                Remaining.Remove(start_edge);
                Index2i start_edge_v = Mesh.GetEdgeV(start_edge);

                bool isLoop;
                List<int> forwardSpan = walk_edge_span_forward(Mesh, start_edge, start_edge_v.a, Remaining, out isLoop);
                if (isLoop == false) {
                    List<int> backwardSpan = walk_edge_span_forward(Mesh, start_edge, start_edge_v.b, Remaining, out isLoop);
                    if (isLoop)
                        throw new Exception("find_cut_paths: how did this possibly happen?!?");
                    if (backwardSpan.Count > 1) {
                        backwardSpan.Reverse();
                        backwardSpan.RemoveAt(backwardSpan.Count - 1);
                        backwardSpan.AddRange(forwardSpan);
                        Index2i start_ev = Mesh.GetEdgeV(backwardSpan[0]);
                        Index2i end_ev = Mesh.GetEdgeV(backwardSpan[backwardSpan.Count - 1]);
                        // [RMS] >2 check here catches two-edge span case, where we do have shared vert but
                        //   can never be loop unless we have duplicate edge (!)
                        isLoop = backwardSpan.Count > 2 && IndexUtil.find_shared_edge_v(ref start_ev, ref end_ev) != DMesh3.InvalidID;
                        forwardSpan = backwardSpan;
                    }
                }

                if (isLoop) {
                    EdgeLoop loop = EdgeLoop.FromEdges(Mesh, forwardSpan);
                    Util.gDevAssert(loop.CheckValidity());
                    Loops.Add(loop);
                } else {
                    EdgeSpan span = EdgeSpan.FromEdges(Mesh, forwardSpan);
                    Util.gDevAssert(span.CheckValidity());
                    Spans.Add(span);
                }
            }

        }




        static List<int> walk_edge_span_forward(DMesh3 mesh, int start_edge, int start_pivot_v, HashSet<int> EdgeSet, out bool bClosedLoop)
        {
            bClosedLoop = false;

            List<int> edgeSpan = new List<int>();
            edgeSpan.Add(start_edge);

            // we update this as we step
            //int cur_edge = start_edge;
            int cur_pivot_v = start_pivot_v;
            int stop_pivot_v = IndexUtil.find_edge_other_v(mesh.GetEdgeV(start_edge), start_pivot_v);
            Util.gDevAssert(stop_pivot_v != DMesh3.InvalidID);

            bool done = false;
            while (!done) {

                // fink outgoing edge in set and connected to current pivot vtx
                int next_edge = -1;
                foreach (int nbr_edge in mesh.VtxEdgesItr(cur_pivot_v)) {
                    if (EdgeSet.Contains(nbr_edge)) {
                        next_edge = nbr_edge;
                        break;
                    }
                }

                // could not find - must be done span
                if (next_edge == -1) {
                    done = true;
                    break;
                }

                // figure out next pivot vtx (is 'other' from current pivot on next edge)
                Index2i next_edge_v = mesh.GetEdgeV(next_edge);
                if (next_edge_v.a == cur_pivot_v) {
                    cur_pivot_v = next_edge_v.b;
                } else if (next_edge_v.b == cur_pivot_v) {
                    cur_pivot_v = next_edge_v.a;
                } else {
                    throw new Exception("walk_edge_span_forward: found valid next edge but not connected to previous vertex??");
                }

                edgeSpan.Add(next_edge);
                EdgeSet.Remove(next_edge);

                // if this happens, we closed a loop
                if (cur_pivot_v == stop_pivot_v) {
                    done = true;
                    bClosedLoop = true;
                }
            }

            return edgeSpan;
        }




    }
}
