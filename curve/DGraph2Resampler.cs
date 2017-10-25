using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    /// <summary>
    /// "Remesher" for DGraph2 
    /// </summary>
    public class DGraph2Resampler
    {
        public DGraph2 Graph;

        // if this returns true, edge cannot be modified
        public Func<int, bool> FixedEdgeFilterF = (eid) => { return false; };


        public DGraph2Resampler(DGraph2 graph)
        {
            this.Graph = graph;
        }


        public void SplitToMaxEdgeLength(double fMaxLen)
        {
            List<int> queue = new List<int>();
            int NE = Graph.MaxEdgeID;
            for (int eid = 0; eid < NE; ++eid) {
                if (!Graph.IsEdge(eid))
                    continue;
                if (FixedEdgeFilterF(eid))
                    continue;
                Index2i ev = Graph.GetEdgeV(eid);
                double dist = Graph.GetVertex(ev.a).Distance(Graph.GetVertex(ev.b));
                if (dist > fMaxLen) {
                    DGraph2.EdgeSplitInfo splitInfo;
                    if (Graph.SplitEdge(eid, out splitInfo) == MeshResult.Ok && dist > 2 * fMaxLen) {
                        queue.Add(eid);
                        queue.Add(splitInfo.eNewBN);
                    }
                }
            }
            while (queue.Count > 0) {
                int eid = queue[queue.Count - 1];
                queue.RemoveAt(queue.Count - 1);
                if (!Graph.IsEdge(eid))
                    continue;
                Index2i ev = Graph.GetEdgeV(eid);
                double dist = Graph.GetVertex(ev.a).Distance(Graph.GetVertex(ev.b));
                if (dist > fMaxLen) {
                    DGraph2.EdgeSplitInfo splitInfo;
                    if (Graph.SplitEdge(eid, out splitInfo) == MeshResult.Ok && dist > 2 * fMaxLen) {
                        queue.Add(eid);
                        queue.Add(splitInfo.eNewBN);
                    }
                }
            }
        }





        /// <summary>
        /// Remove vertices that are colinear w/ their two neighbours, within angle tolerance
        /// </summary>
        public void CollapseFlatVertices(double fMaxDeviationDeg = 5)
        {
            bool done = false;
            int max_passes = 200;
            int pass_count = 0;
            while (done == false && pass_count++ < max_passes) {
                done = true;

                // [RMS] do modulo-indexing here to avoid pathological cases where we do things like
                // continually collapse a short edge adjacent to a long edge (which will result in crazy over-collapse)
                int N = Graph.MaxVertexID;
                const int nPrime = 31337;     // any prime will do...
                int cur_vid = 0;
                do {
                    int vid = cur_vid;
                    cur_vid = (cur_vid + nPrime) % N;

                    if (!Graph.IsVertex(vid))
                        continue;
                    if (Graph.GetVtxEdgeCount(vid) != 2)
                        continue;

                    double open = Math.Abs(Graph.OpeningAngle(vid));
                    if (open < 180 - fMaxDeviationDeg)
                        continue;

                    var edges = Graph.GetVtxEdges(vid);
                    int eid = edges.First();
                    int eid2 = edges.Last();
                    if (FixedEdgeFilterF(eid) || FixedEdgeFilterF(eid2))
                        continue;

                    Index2i ev = Graph.GetEdgeV(eid);
                    int other_v = (ev.a == vid) ? ev.b : ev.a;

                    DGraph2.EdgeCollapseInfo collapseInfo;
                    MeshResult result = Graph.CollapseEdge(other_v, vid, out collapseInfo);
                    if (result == MeshResult.Ok) {
                        done = false;
                    } else {
                        throw new Exception("DGraph2Resampler.CollapseFlatVertices: failed!");
                    }

                } while (cur_vid != 0);
            }
        }







        public void CollapseDegenerateEdges(double fDegenLenThresh = MathUtil.Epsilonf)
        {
            bool done = false;
            int max_passes = 100;
            int pass_count = 0;
            while (done == false && pass_count++ < max_passes) {
                done = true;

                int N = Graph.MaxEdgeID;
                for ( int eid = 0; eid < N; eid++ ) {
                    if (!Graph.IsEdge(eid))
                        continue;
                    if (FixedEdgeFilterF(eid))
                        continue;
                    Index2i ev = Graph.GetEdgeV(eid);

                    Vector2d va = Graph.GetVertex(ev.a);
                    Vector2d vb = Graph.GetVertex(ev.b);
                    if ( va.Distance(vb) < fDegenLenThresh ) {
                        int keep = ev.a;
                        int remove = ev.b;

                        DGraph2.EdgeCollapseInfo collapseInfo;
                        if (Graph.CollapseEdge(keep, remove, out collapseInfo) == MeshResult.Ok) {
                            done = false;
                        }
                    }

                };
            }
        }







        public void CollapseToMinEdgeLength(double fMinLen)
        {
            double sharp_threshold_deg = 140.0f;

            double minLenSqr = fMinLen * fMinLen;
            bool done = false;
            int max_passes = 100;
            int pass_count = 0;
            while (done == false && pass_count++ < max_passes) {
                done = true;

                // [RMS] do modulo-indexing here to avoid pathological cases where we do things like
                // continually collapse a short edge adjacent to a long edge (which will result in crazy over-collapse)
                int N = Graph.MaxEdgeID;
                const int nPrime = 31337;     // any prime will do...
                int cur_eid = 0;
                do {
                    int eid = cur_eid;
                    cur_eid = (cur_eid + nPrime) % N;

                    if (!Graph.IsEdge(eid))
                        continue;
                    if (FixedEdgeFilterF(eid))
                        continue;
                    Index2i ev = Graph.GetEdgeV(eid);

                    Vector2d va = Graph.GetVertex(ev.a);
                    Vector2d vb = Graph.GetVertex(ev.b);
                    double distSqr = va.DistanceSquared(vb);
                    if (distSqr < minLenSqr) {

                        int vtx_idx = -1;    // collapse to this vertex

                        // check valences. want to preserve positions of non-valence-2
                        int na = Graph.GetVtxEdgeCount(ev.a);
                        int nb = Graph.GetVtxEdgeCount(ev.b);
                        if (na != 2 && nb != 2)
                            continue;
                        if (na != 2)
                            vtx_idx = 0;
                        else if (nb != 2)
                            vtx_idx = 1;

                        // check opening angles. want to preserve sharp(er) angles
                        if (vtx_idx == -1) {
                            double opena = Math.Abs(Graph.OpeningAngle(ev.a));
                            double openb = Math.Abs(Graph.OpeningAngle(ev.b));
                            if (opena < sharp_threshold_deg && openb < sharp_threshold_deg)
                                continue;
                            else if (opena < sharp_threshold_deg)
                                vtx_idx = 0;
                            else if (openb < sharp_threshold_deg)
                                vtx_idx = 1;
                        }

                        Vector2d newPos = (vtx_idx == -1) ? 0.5 * (va + vb) : ((vtx_idx == 0) ? va : vb);

                        int keep = ev.a, remove = ev.b;
                        if (vtx_idx == 1) {
                            remove = ev.a; keep = ev.b;
                        }

                        DGraph2.EdgeCollapseInfo collapseInfo;
                        if (Graph.CollapseEdge(keep, remove, out collapseInfo) == MeshResult.Ok) {
                            Graph.SetVertex(collapseInfo.vKept, newPos);
                            done = false;
                        }
                    }

                } while (cur_eid != 0);
            }
        }





    }
}
