using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace g3
{

    public class EdgeLoop
    {
        public int[] Vertices;
        public int[] Edges;

        public int[] BowtieVertices;
    }


    public class MeshBoundaryLoops
    {
        public DMesh3 Mesh;
        public List<EdgeLoop> Loops;

        public MeshBoundaryLoops(DMesh3 mesh)
        {
            this.Mesh = mesh;
            Compute();
        }


        // This algorithm assumes that triangles are oriented consistently, 
        // so boundary-loop can be followed
        public bool Compute()
        {
            Loops = new List<EdgeLoop>();

            int NE = Mesh.MaxEdgeID;
            byte[] used_edge = new byte[NE];
            Array.Clear(used_edge, 0, used_edge.Length);

            List<int> loop_edges = new List<int>();     // [RMS] not sure we need this...
            List<int> loop_verts = new List<int>();
            List<int> bowties = new List<int>();

            int[] all_e = new int[16];

            for ( int eid = 0; eid < NE; ++eid ) {
                if (used_edge[eid] > 0)
                    continue;
                if (Mesh.edge_is_boundary(eid) == false)
                    continue;

                // ok this is start of a boundary chain
                int eStart = eid;
                used_edge[eStart] = 1;
                loop_edges.Add(eStart);

                int eCur = eid;

                // follow the chain in order of oriented edges
                bool bClosed = false;
                while ( ! bClosed ) {
                    Index2i ev = Mesh.GetOrientedBoundaryEdgeV(eCur);
                    int cure_a = ev.a, cure_b = ev.b;
                    loop_verts.Add(cure_a);

                    int e0 = -1, e1 = 1;
                    int bdry_nbrs = Mesh.VtxBoundaryEdges(cure_b, ref e0, ref e1);

                    if (bdry_nbrs < 2)
                        throw new Exception("MeshBoundaryLoops.Compute: found broken neighbourhood at vertex " + cure_b);

                    int eNext = -1;
                    if (bdry_nbrs > 2) {
                        // found "bowtie" vertex...things just got complicated!

                        if (cure_b == loop_verts[0]) {
                            // we can close loop right now - might as well!
                            eNext = -2;

                        } else {
                            // try to find an unused outgoing edge that is oriented properly.
                            // This could create sub-loops, we will handle those later
                            if (bdry_nbrs >= all_e.Length)
                                all_e = new int[bdry_nbrs];
                            int num_be = Mesh.VtxAllBoundaryEdges(cure_b, all_e);
                            Debug.Assert(num_be == bdry_nbrs);
                            for (int i = 0; i < num_be; ++i) {
                                int bdry_eid = all_e[i];
                                if (used_edge[bdry_eid] != 0)
                                    continue;       // this edge is already used
                                Index2i bdry_ev = Mesh.GetOrientedBoundaryEdgeV(bdry_eid);
                                if (bdry_ev.a != cure_b)
                                    continue;       // have to be able to chain to end of current edge
                                eNext = bdry_eid;
                                break;
                            }
                            if (eNext == -1)
                                throw new Exception("MeshBoundaryLoops.Compute: cannot find valid outgoing edge at bowtie vertex " + cure_b);
                        }

                        if ( bowties.Contains(cure_b) == false )
                            bowties.Add(cure_b);

                    } else {
                        Debug.Assert(e0 == eCur || e1 == eCur);
                        eNext = (e0 == eCur) ? e1 : e0;
                    }

                    if (eNext == -2) {
                        bClosed = true;
                    } else if (eNext == eStart) {
                        bClosed = true;      // done loop
                    } else {
                        Debug.Assert(used_edge[eNext] == 0);
                        loop_edges.Add(eNext);
                        eCur = eNext;
                        used_edge[eCur] = 1;
                    }
                }

                if (bowties.Count > 0) {
                    List<EdgeLoop> subloops = extract_subloops(loop_verts, loop_edges, bowties);
                    for (int i = 0; i < subloops.Count; ++i)
                        Loops.Add(subloops[i]);
                } else {
                    // convert loop
                    EdgeLoop loop = new EdgeLoop();
                    loop.Vertices = loop_verts.ToArray();
                    loop.Edges = loop_edges.ToArray();
                    Loops.Add(loop);
                }

                // reset
                loop_edges.Clear();
                loop_verts.Clear();
                bowties.Clear();
            }

            return true;
        }








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
                subs.Add(new EdgeLoop() {
                    Vertices = loopV.ToArray(), Edges = loopE.ToArray(), BowtieVertices = bowties.ToArray()
                });
                return subs;
            }


            while ( dupes.Count > 0 ) {

                // find a simple loop
                int bi = 0, bv = 0;
                int start_i = -1, end_i = -1;
                for ( ; bi < dupes.Count; ++bi ) {
                    bv = dupes[bi];
                    if (is_simple_bowtie_loop(loopV, dupes, bv, out start_i, out end_i))
                        break; // found one!
                }

                if (start_i == -1) {
                    throw new Exception("extract_subloops: argh");
                }

                Debug.Assert(loopV[start_i] == bv && loopV[end_i] == bv );

                EdgeLoop loop = new EdgeLoop();
                loop.Vertices = extract_span(loopV, start_i, end_i, true);
                loop.BowtieVertices = bowties.ToArray();
                subs.Add(loop);

                // check if we still have duplicates of this vert
                if (count_in_list(loopV, bv) < 2)
                    dupes.Remove(bv);
            }

            // will normally still have one loop left...
            int nLeft = 0;
            for ( int i = 0; i < loopV.Count; ++i ) {
                if (loopV[i] != -1)
                    nLeft++;
            }
            if (nLeft > 0) {
                EdgeLoop loop = new EdgeLoop();
                loop.Vertices = new int[nLeft];
                int vi = 0;
                for (int i = 0; i < loopV.Count; ++i) {
                    if (loopV[i] != -1)
                        loop.Vertices[vi++] = loopV[i];
                }
                loop.BowtieVertices = bowties.ToArray();
                subs.Add(loop);
            }

            return subs;
        }

    
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



        int[] extract_span(List<int> l, int i0, int i1, bool bMarkInvalid)
        {
            int num = count_span(l, i0, i1);
            int[] a = new int[num];
            int ai = 0;
            int N = l.Count;
            for (int i = i0; i != i1; i = (i + 1) % N) {
                if (l[i] != -1) {
                    a[ai++] = l[i];
                    if (bMarkInvalid)
                        l[i] = -1;
                }
            }
            return a;
        }


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


        int find_index(List<int> l, int start, int item)
        {
            for (int i = start; i < l.Count; ++i)
                if (l[i] == item)
                    return i;
            return -1;
        }
        
        int count_in_list(List<int> l, int item) {
            int c = 0;
            for (int i = 0; i < l.Count; ++i)
                if (l[i] == item)
                    c++;
            return c;
        }            

    }




}
