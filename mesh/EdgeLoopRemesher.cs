using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace g3
{
    /// <summary>
    /// This is a custom Remesher that only affects the edges along an EdgeLoop.
    /// The edges are only split and collapsed, flipping is not permitted.
    /// The loop vertices are smoothed along the loop, ie using curve laplacian
    /// rather than one-ring laplacian.
    /// 
    /// [TODO] avoid rebuild_edge_list(). requires handling various cases below...
    /// [TODO] Precompute() seems overly expensive...?
    /// [TODO] local-smoothing impl is not very efficient. Should not be necessary to
    ///    rebuild nbrhood each time if we are not changing it.
    /// </summary>
    public class EdgeLoopRemesher : Remesher
    {
        public EdgeLoop InputLoop;
        public EdgeLoop OutputLoop;

        /// <summary>
        /// Can optionally include N one-rings around the loop in
        /// the smoothing/projection pass. This will produce cleaner results.
        /// </summary>
        public int LocalSmoothingRings = 0;

        List<int> CurrentLoopE;
        List<int> CurrentLoopV;


        public EdgeLoopRemesher(DMesh3 m, EdgeLoop loop) : base(m) {
            UpdateLoop(loop);

            EnableFlips = false;

            CustomSmoothF = loop_smooth_vertex;
        }


        public void UpdateLoop(EdgeLoop loop)
        {
            InputLoop = loop;
            OutputLoop = null;
            CurrentLoopE = new List<int>(loop.Edges);
            CurrentLoopV = new List<int>(loop.Vertices);
        }


        public override void Precompute()
        {
            // this does a full pass over mesh, which is not necessary if we
            // are just doing an edge loop....right?
            base.Precompute();
        }



        // We override the base Remesher edge iteration to be restricted to loop edges.
        // This is done by explicitly constructin a list of edges to process each pass,
        // and popping these edges as we deal with them. 
        List<int> RemainingE;         // edges to process this pass


        const int nPrime = 31337;     // any prime will do...
        protected override int start_edges()
        {
            Debug.Assert(check_loop());
            RemainingE = new List<int>(CurrentLoopE.Count);

            int nPrime = 31337;
            int i = 0;
            do {
                RemainingE.Add(CurrentLoopE[i]);
                i = (i + nPrime) % CurrentLoopE.Count;
            } while (i != 0);

            int eid = RemainingE[RemainingE.Count - 1];
            RemainingE.RemoveAt(RemainingE.Count - 1);
            return eid;
        }

        protected override int next_edge(int cur_eid, out bool bDone)
        {
            if ( RemainingE.Count == 0 ) {
                bDone = true;
                return 0;
            }
            bDone = false;
            int eid = RemainingE[RemainingE.Count - 1];
            RemainingE.RemoveAt(RemainingE.Count - 1);
            return eid;
        }


        

        protected override void end_pass()
        {
            OutputLoop = new EdgeLoop(mesh, CurrentLoopV.ToArray(), CurrentLoopE.ToArray(), false);
        }



        HashSet<int> smoothV = new HashSet<int>();

        protected override void begin_smooth() {
            base.begin_smooth();

            if (LocalSmoothingRings > 0) {
                smoothV.Clear();
                if (LocalSmoothingRings == 1) {
                    for (int i = 0; i < CurrentLoopV.Count; ++i) {
                        smoothV.Add(CurrentLoopV[i]);
                        foreach (int nbrv in mesh.VtxVerticesItr(CurrentLoopV[i]))
                            smoothV.Add(nbrv);
                    }
                } else {
                    MeshVertexSelection select = new MeshVertexSelection(mesh);
                    select.Select(CurrentLoopV);
                    select.ExpandToOneRingNeighbours(LocalSmoothingRings);
                    foreach (int vid in select)
                        smoothV.Add(vid);
                }
            }
        }


        protected override IEnumerable<int> smooth_vertices()
        {
            if (LocalSmoothingRings > 0)
                return smoothV;
            else
                return CurrentLoopV;
        }


        Vector3d loop_smooth_vertex(DMesh3 mesh, int vid, double alpha)
        {
            if ( LocalSmoothingRings > 0 && CurrentLoopV.Contains(vid) == false) {
                bool bModified = false;
                Vector3d vPos = base.ComputeSmoothedVertexPos(vid, MeshUtil.UniformSmooth, out bModified);
                return vPos;
            }

            int idx = CurrentLoopV.FindIndex((i) => { return i == vid; });
            if (idx < 0)
                return mesh.GetVertex(vid);
            int iPrev = (idx + CurrentLoopV.Count - 1) % CurrentLoopV.Count;
            int iNext = (idx + 1) % CurrentLoopV.Count;

            Vector3d c = mesh.GetVertex(CurrentLoopV[iPrev]) + mesh.GetVertex(CurrentLoopV[iNext]);
            c *= 0.5f;
            return (1.0 - alpha) * mesh.GetVertex(vid) + (alpha) * c;
        }


        protected override IEnumerable<int> project_vertices()
        {
            if (LocalSmoothingRings > 0)
                return smoothV;
            else
                return CurrentLoopV;
        }



        protected override void OnEdgeSplit(int edgeID, int va, int vb, DMesh3.EdgeSplitInfo splitInfo)
        {
            int aidx = CurrentLoopV.FindIndex((i) => { return i == va; });
            int bidx = CurrentLoopV.FindIndex((i) => { return i == vb; });
            int eidx = CurrentLoopE.FindIndex((i) => { return i == edgeID; });

            if ( eidx == CurrentLoopE.Count-1 ) {    // list-wrap case
                CurrentLoopV.Add(splitInfo.vNew);
            } else if ( aidx < bidx ) {
                CurrentLoopV.Insert(bidx, splitInfo.vNew);
            } else {
                CurrentLoopV.Insert(aidx, splitInfo.vNew);
            }

            // ugh! expensive!
            rebuild_edge_list();

            Debug.Assert(check_loop());
        }

        protected override void OnEdgeCollapse(int edgeID, int va, int vb, DMesh3.EdgeCollapseInfo collapseInfo)
        {
            int vidx = CurrentLoopV.FindIndex((i) => { return i == collapseInfo.vRemoved; });
            CurrentLoopV.RemoveAt(vidx);

            int eidx = CurrentLoopE.FindIndex((i) => { return i == edgeID; });
            CurrentLoopE.RemoveAt(eidx);

            // if we removed the "loop-wrap" edge, ie verts [n-1,0], then
            // our edge list needs to be cycled by one. Right now just rebuilding it.
            if ( vidx == 0 && eidx == CurrentLoopE.Count ) {
                rebuild_edge_list();
            }

            Debug.Assert(check_loop());
        }



        bool check_loop()
        {
            for ( int i = 0; i < CurrentLoopV.Count; ++i ) {
                int eid = mesh.FindEdge(CurrentLoopV[i], CurrentLoopV[(i + 1) % CurrentLoopV.Count]);
                Debug.Assert(eid != DMesh3.InvalidID);
                Debug.Assert(CurrentLoopE[i] == eid);
            }
            return true;
        }



        void rebuild_edge_list()
        {
            CurrentLoopE.Clear();
            int NV = CurrentLoopV.Count;
            for (int i = 0; i < NV; ++i)
                CurrentLoopE.Add(mesh.FindEdge(CurrentLoopV[i], CurrentLoopV[(i + 1) % NV]));
        }



    }
}
