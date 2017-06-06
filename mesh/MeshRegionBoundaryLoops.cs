using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

namespace g3
{


	/// <summary>
	/// Extract boundary EdgeLoops for subregions of Mesh
	/// </summary>
    public class MeshRegionBoundaryLoops : IEnumerable<EdgeLoop>
    {
        public DMesh3 Mesh;
        public List<EdgeLoop> Loops;

        // list of included triangles and edges
        IndexFlagSet triangles;
        IndexFlagSet edges;


        public MeshRegionBoundaryLoops(DMesh3 mesh, int[] RegionTris, bool bAutoCompute = true)
        {
            this.Mesh = mesh;

            // make flag set for included triangles
            triangles = new IndexFlagSet(mesh.MaxTriangleID, RegionTris.Length);
            for (int i = 0; i < RegionTris.Length; ++i) 
                triangles[RegionTris[i]] = true;

            // make flag set for included edges
            // NOTE: this currently processes non-boundary-edges twice. Could
            // avoid w/ another IndexFlagSet, but the check is inexpensive...
            edges = new IndexFlagSet(mesh.MaxEdgeID, RegionTris.Length);
            for (int i = 0; i < RegionTris.Length; ++i) {
                int tid = RegionTris[i];
                Index3i te = Mesh.GetTriEdges(tid);
                for (int j = 0; j < 3; ++j) {
                    int eid = te[j];
                    if (!edges.Contains(eid)) {
                        Index2i et = mesh.GetEdgeT(eid);
                        if (et.b == DMesh3.InvalidID || triangles[et.a] != triangles[et.b])
                            edges.Add(eid);
                    }
                }
            }


			if (bAutoCompute)
           		Compute();
        }

        public int Count {
            get { return Loops.Count; }
        }

        public EdgeLoop this[int index] {
            get { return Loops[index]; }
        }

        public IEnumerator<EdgeLoop> GetEnumerator() {
            return Loops.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator() {
            return Loops.GetEnumerator();
        }


        /// <summary>
        /// Index of Loop with largest vertex count
        /// </summary>
        public int MaxVerticesLoopIndex {
            get {
                int j = 0;
                for (int i = 1; i < Loops.Count; ++i) {
                    if (Loops[i].Vertices.Length > Loops[j].Vertices.Length)
                        j = i;
                }
                return j;
            }
        }



        bool edge_is_boundary(int eid)
        {
            return edges.Contains(eid);
        }

        // returns true for both internal and mesh boundary edges
        // tid_in and tid_out are triangles 'in' and 'out' of set, respectively
        bool edge_is_boundary(int eid, ref int tid_in, ref int tid_out)
        {
            if (edges.Contains(eid) == false)
                return false;

            tid_in = tid_out = DMesh3.InvalidID;
            Index2i et = Mesh.GetEdgeT(eid);
            if ( et.b == DMesh3.InvalidID ) {       // boundary edge!
                tid_in = et.a;
                tid_out = et.b;
                return true;
            }

            bool in0 = triangles[et.a];
            bool in1 = triangles[et.b];
            if ( in0 != in1 ) {
                tid_in = (in0) ? et.a : et.b;
                tid_out = (in0) ? et.b : et.a;
                return true;
            }
            return false;
        }


        // return same indices as GetEdgeV, but oriented based on attached triangle
        Index2i get_oriented_edgev(int eID, int tid_in, int tid_out)
        {
            Index2i edgev = Mesh.GetEdgeV(eID);
            int a = edgev.a, b = edgev.b;
            Index3i tri = Mesh.GetTriangle(tid_in);
            int ai = IndexUtil.find_edge_index_in_tri(a, b, ref tri);
            return new Index2i(tri[ai], tri[(ai + 1) % 3]);
        }



        // returns first two boundary edges, and count of total boundary edges
        public int vertex_boundary_edges(int vID, ref int e0, ref int e1)
        {
            int count = 0;
            foreach (int eid in Mesh.VtxEdgesItr(vID)) {
                if (edge_is_boundary(eid)) {
                    if (count == 0)
                        e0 = eid;
                    else if (count == 1)
                        e1 = eid;
                    count++;
                }
            }
            return count;
        }


        // e needs to be large enough (ie call VtxBoundaryEdges, or as large as max one-ring)
        // returns count, ie number of elements of e that were filled
        public int all_vertex_boundary_edges(int vID, int[] e)
        {
            int count = 0;
            foreach (int eid in Mesh.VtxEdgesItr(vID)) {
                if (edge_is_boundary(eid))
                    e[count++] = eid;
            }
            return count;
        }





		/// <summary>
		/// Find the set of EdgeLoops bounding 'in' triangles. Note that if we encounter topological
		/// issues, we will throw MeshBoundaryLoopsException w/ more info (if possible)
		/// </summary>
		public bool Compute()
        {
			// This algorithm assumes that triangles are oriented consistently, 
			// so closed boundary-loop can be followed by walking edges in-order

			Loops = new List<EdgeLoop>();

            // Temporary memory used to indicate when we have "used" an edge.
            IndexFlagSet used_edge = new IndexFlagSet(Mesh.MaxEdgeID, edges.Count);

            // current loop is stored here, cleared after each loop extracted
            List<int> loop_edges = new List<int>();     // [RMS] not sure we need this...
            List<int> loop_verts = new List<int>();
            List<int> bowties = new List<int>();

            // Temp buffer for reading back all boundary edges of a vertex.
            // probably always small but in pathological cases it could be large...
            int[] all_e = new int[16];

            // process all edges of mesh
            foreach ( int eid in edges ) { 

                if ( used_edge[eid] == true )
                    continue;

                if ( edge_is_boundary(eid) == false )
                    continue;

                // ok this is start of a boundary chain
                int eStart = eid;
                used_edge[eStart] = true;
                loop_edges.Add(eStart);

                int eCur = eid;

                // follow the chain in order of oriented edges
                bool bClosed = false;
                while ( ! bClosed ) {

                    // [TODO] can do this more efficienty?
                    int tid_in = DMesh3.InvalidID, tid_out = DMesh3.InvalidID;
                    edge_is_boundary(eCur, ref tid_in, ref tid_out);

                    Index2i ev = get_oriented_edgev(eCur, tid_in, tid_out);
                    int cure_a = ev.a, cure_b = ev.b;
                    loop_verts.Add(cure_a);

                    int e0 = -1, e1 = 1;
                    int bdry_nbrs = vertex_boundary_edges(cure_b, ref e0, ref e1);

					if (bdry_nbrs < 2)
						throw new MeshBoundaryLoopsException("MeshRegionBoundaryLoops.Compute: found broken neighbourhood at vertex " + cure_b) { UnclosedLoop = true };

                    int eNext = -1;
                    if (bdry_nbrs > 2) {
						// found "bowtie" vertex...things just got complicated!

						if (cure_b == loop_verts[0]) {
							// The "end" of the current edge is the same as the start vertex.
							// This means we can close the loop here. Might as well!
							eNext = -2;   // sentinel value used below

						} else {
							// try to find an unused outgoing edge that is oriented properly.
							// This could create sub-loops, we will handle those later
							if (bdry_nbrs >= all_e.Length)
								all_e = new int[bdry_nbrs];
							int num_be = all_vertex_boundary_edges(cure_b, all_e);

							Debug.Assert(num_be == bdry_nbrs);

							// Try to pick the best "turn left" vertex.
							eNext = find_left_turn_edge(eCur, cure_b, all_e, num_be, used_edge);

							if (eNext == -1)
								throw new MeshBoundaryLoopsException("MeshRegionBoundaryLoops.Compute: cannot find valid outgoing edge at bowtie vertex " + cure_b) { BowtieFailure = true };
                        }

                        if ( bowties.Contains(cure_b) == false )
                            bowties.Add(cure_b);

                    } else {
                        Debug.Assert(e0 == eCur || e1 == eCur);
                        eNext = (e0 == eCur) ? e1 : e0;
                    }

                    if (eNext == -2) {
                        // found a bowtie vert that is the same as start-of-loop, so we
                        // are just closing it off explicitly
                        bClosed = true;
                    } else if (eNext == eStart) {
                        // found edge at start of loop, so loop is done.
                        bClosed = true;      
                    } else {
                        // push onto accumulated list
                        Debug.Assert( used_edge[eNext] == false );
                        loop_edges.Add(eNext);
                        eCur = eNext;
                        used_edge[eCur] = true;
                    }
                }

                // if we saw a bowtie vertex, we might need to break up this loop,
                // so call extract_subloops
                if (bowties.Count > 0) {
                    List<EdgeLoop> subloops = extract_subloops(loop_verts, loop_edges, bowties);
                    for (int i = 0; i < subloops.Count; ++i)
                        Loops.Add(subloops[i]);
                } else {
                    // clean simple loop, convert to EdgeLoop instance
                    EdgeLoop loop = new EdgeLoop(Mesh);
                    loop.Vertices = loop_verts.ToArray();
                    loop.Edges = loop_edges.ToArray();
                    Loops.Add(loop);
                }

                // reset these lists
                loop_edges.Clear();
                loop_verts.Clear();
                bowties.Clear();
            }

            return true;
        }




        // [TODO] cache this in a dictionary? we will not need very many, but we will
        //   need each multiple times!
        Vector3d get_vtx_normal(int vid)
        {
            Vector3d n = Vector3d.Zero;
            foreach (int ti in Mesh.VtxTrianglesItr(vid))
                n += Mesh.GetTriNormal(ti);
            n.Normalize();
            return n;
        }



        //
        // [TODO] for internal vertices, there is no ambiguity in which is the left-turn edge,
        //   we should be using 'closest' left-neighbour edge.
        //
        // ok, bdry_edges[0...bdry_edges_count] contains the boundary edges coming out of bowtie_v.
        // We want to pick the best one to continue the loop that came in to bowtie_v on incoming_e.
        // If the loops are all sane, then we will get the smallest loops by "turning left" at bowtie_v.
        // So, we compute the tangent plane at bowtie_v, and then the signed angle for each
        // viable edge in this plane. 
        int find_left_turn_edge(int incoming_e, int bowtie_v, int[] bdry_edges, int bdry_edges_count, IndexFlagSet used_edges  )
        {
            // compute normal and edge [a,bowtie]
            Vector3d n = get_vtx_normal(bowtie_v);
            int other_v = Mesh.edge_other_v(incoming_e, bowtie_v);
            Vector3d ab = Mesh.GetVertex(bowtie_v) - Mesh.GetVertex(other_v);
 
            // our winner
            int best_e = -1;
            double best_angle = double.MaxValue;

            for (int i = 0; i < bdry_edges_count; ++i) {
                int bdry_eid = bdry_edges[i];
                if ( used_edges[bdry_eid] == true )
                    continue;       // this edge is already used


                // [TODO] can do this more efficienty?
                int tid_in = DMesh3.InvalidID, tid_out = DMesh3.InvalidID;
                edge_is_boundary(bdry_eid, ref tid_in, ref tid_out);
                Index2i bdry_ev = get_oriented_edgev(bdry_eid, tid_in, tid_out);
                //Index2i bdry_ev = Mesh.GetOrientedBoundaryEdgeV(bdry_eid);

                if (bdry_ev.a != bowtie_v)
                    continue;       // have to be able to chain to end of current edge, orientation-wise

                // compute projected angle
                Vector3d bc = Mesh.GetVertex(bdry_ev.b) - Mesh.GetVertex(bowtie_v);
                float fAngleS = MathUtil.PlaneAngleSignedD((Vector3f)ab, (Vector3f)bc, (Vector3f)n);

                // turn left!
                if ( best_angle == double.MaxValue || fAngleS < best_angle ) {
                    best_angle = fAngleS;
                    best_e = bdry_eid;
                }
            }
            Debug.Assert(best_e != -1);

            return best_e;
        }





        // This is called when loopV contains one or more "bowtie" vertices.
        // These vertices *might* be duplicated in loopV (but not necessarily)
        // If they are, we have to break loopV into subloops that don't contain duplicates.
        //
        // The list bowties contains all the possible duplicates 
        // (all v in bowties occur in loopV at least once)
        //
        // Currently loopE is not used, and the returned EdgeLoop objects do not have their Edges
        // arrays initialized. Perhaps to improve in future.
        List<EdgeLoop> extract_subloops(List<int> loopV, List<int> loopE, List<int> bowties )
        {
            List<EdgeLoop> subs = new List<EdgeLoop>();

            // figure out which bowties we saw are actually duplicated in loopV
            List<int> dupes = new List<int>();
            foreach ( int bv in bowties ) {
                if (count_in_list(loopV, bv) > 1)
                    dupes.Add(bv);
            }

            // we might not actually have any duplicates, if we got luck. Early out in that case
            if ( dupes.Count == 0 ) {
                subs.Add(new EdgeLoop(Mesh) {
                    Vertices = loopV.ToArray(), Edges = loopE.ToArray(), BowtieVertices = bowties.ToArray()
                });
                return subs;
            }

            // This loop extracts subloops until we have dealt with all the
            // duplicate vertices in loopV
            while ( dupes.Count > 0 ) {

                // Find shortest "simple" loop, ie a loop from a bowtie to itself that
                // does not contain any other bowties. This is an independent loop.
                // We're doing a lot of extra work here if we only have one element in dupes...
                int bi = 0, bv = 0;
                int start_i = -1, end_i = -1;
                int bv_shortest = -1; int shortest = int.MaxValue;
                for ( ; bi < dupes.Count; ++bi ) {
                    bv = dupes[bi];
                    if (is_simple_bowtie_loop(loopV, dupes, bv, out start_i, out end_i)) {
                        int len = count_span(loopV, start_i, end_i);
                        if (len < shortest) {
                            bv_shortest = bv;
                            shortest = len;
                        }
                    }
                }
                if (bv_shortest == -1) {
                    throw new MeshBoundaryLoopsException("MeshRegionBoundaryLoops.Compute: Cannot find a valid simple loop");
                }
                if (bv != bv_shortest) {
                    bv = bv_shortest;
                    // running again just to get start_i and end_i...
                    is_simple_bowtie_loop(loopV, dupes, bv, out start_i, out end_i);
                }

                Debug.Assert(loopV[start_i] == bv && loopV[end_i] == bv );

                EdgeLoop loop = new EdgeLoop(Mesh);
                loop.Vertices = extract_span(loopV, start_i, end_i, true);
                loop.Edges = EdgeLoop.VertexLoopToEdgeLoop(Mesh, loop.Vertices);
                loop.BowtieVertices = bowties.ToArray();
                subs.Add(loop);

                // If there are no more duplicates of this bowtie, we can treat
                // it like a regular vertex now
                if (count_in_list(loopV, bv) < 2)
                    dupes.Remove(bv);
            }

            // Should have one loop left that contains duplicates. 
            // Extract this as a separate loop
            int nLeft = 0;
            for ( int i = 0; i < loopV.Count; ++i ) {
                if (loopV[i] != -1)
                    nLeft++;
            }
            if (nLeft > 0) {
                EdgeLoop loop = new EdgeLoop(Mesh);
                loop.Vertices = new int[nLeft];
                int vi = 0;
                for (int i = 0; i < loopV.Count; ++i) {
                    if (loopV[i] != -1)
                        loop.Vertices[vi++] = loopV[i];
                }
                loop.Edges = EdgeLoop.VertexLoopToEdgeLoop(Mesh, loop.Vertices);
                loop.BowtieVertices = bowties.ToArray();
                subs.Add(loop);
            }

            return subs;
        }



        /*
         * In all the functions below, the list loopV is assumed to possibly
         * contain "removed" vertices indicated by -1. These are ignored.
         */

    
        // Check if the loop from bowtieV to bowtieV inside loopV contains any other bowtie verts.
        // Also returns start and end indices in loopV of "clean" loop
        // Note that start may be < end, if the "clean" loop wraps around the end
        bool is_simple_bowtie_loop(List<int> loopV, List<int> bowties, int bowtieV, out int start_i, out int end_i)
        {
            // find two indices of bowtie vert
            start_i = find_index(loopV, 0, bowtieV);
            end_i = find_index(loopV, start_i + 1, bowtieV);

            if (is_simple_path(loopV, bowties, bowtieV, start_i, end_i)) {
                return true;

            } else if (is_simple_path(loopV, bowties, bowtieV, end_i, start_i)) {
                int tmp = start_i; start_i = end_i; end_i = tmp;
                return true;

            } else 
                return false;       // not a simple bowtie loop!
        }
        

        // check if forward path from loopV[i1] to loopV[i2] contains any bowtie verts other than bowtieV
        bool is_simple_path(List<int> loopV, List<int> bowties, int bowtieV, int i1, int i2)
        {
            int N = loopV.Count;
            for ( int i = i1; i != i2; i = (i+1)%N ) {
                int vi = loopV[i];
                if (vi == -1)
                    continue;       // skip removed vertices
                if (vi != bowtieV && bowties.Contains(vi))
                    return false;
            }
            return true;
        }


        // Read out the span from loop[i0] to loop [i1-1] into an array.
        // If bMarkInvalid, then these values are set to -1 in loop
        int[] extract_span(List<int> loop, int i0, int i1, bool bMarkInvalid)
        {
            int num = count_span(loop, i0, i1);
            int[] a = new int[num];
            int ai = 0;
            int N = loop.Count;
            for (int i = i0; i != i1; i = (i + 1) % N) {
                if (loop[i] != -1) {
                    a[ai++] = loop[i];
                    if (bMarkInvalid)
                        loop[i] = -1;
                }
            }
            return a;
        }

        // count number of valid vertices in l between loop[i0] and loop[i1-1]
        int count_span(List<int> l, int i0, int i1)
        {
            int c = 0;
            int N = l.Count;
            for (int i = i0; i != i1; i = (i + 1) % N) {
                if (l[i] != -1)
                    c++;
            }
            return c;
        }

        // find the index of item in loop, starting at start index
        int find_index(List<int> loop, int start, int item)
        {
            for (int i = start; i < loop.Count; ++i)
                if (loop[i] == item)
                    return i;
            return -1;
        }
        
        // count number of times item appears in loop
        int count_in_list(List<int> loop, int item) {
            int c = 0;
            for (int i = 0; i < loop.Count; ++i)
                if (loop[i] == item)
                    c++;
            return c;
        }            

    }




}
