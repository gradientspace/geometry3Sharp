using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{


    /// <summary>
    /// Utility functions for DGraph3 data structure
    /// </summary>
    public static class DGraph3Util
    {
        public struct Curves
        {
            public List<DCurve3> Loops;
            public List<DCurve3> Paths;

            public HashSet<int> BoundaryV;
            public HashSet<int> JunctionV;

            public List<List<int>> LoopEdges;
            public List<List<int>> PathEdges;
        }


        /// <summary>
        /// Decompose graph into simple polylines and polygons. 
        /// </summary>
        public static Curves ExtractCurves(DGraph3 graph,
            bool bWantLoopIndices = false,
            Func<int, bool> CurveOrientationF = null )
        {
            Curves c = new Curves();
            c.Loops = new List<DCurve3>();
            c.Paths = new List<DCurve3>();
            if (bWantLoopIndices) {
                c.LoopEdges = new List<List<int>>();
                c.PathEdges = new List<List<int>>();
            }

            HashSet<int> used = new HashSet<int>();

            // find boundary and junction vertices
            HashSet<int> boundaries = new HashSet<int>();
            HashSet<int> junctions = new HashSet<int>();
            foreach ( int vid in graph.VertexIndices() ) {
                if (graph.IsBoundaryVertex(vid))
                    boundaries.Add(vid);
                if (graph.IsJunctionVertex(vid))
                    junctions.Add(vid);
            }

            // walk paths from boundary vertices
            foreach (int start_vid in boundaries) {
                int vid = start_vid;
                int eid = graph.GetVtxEdges(vid)[0];
                if (used.Contains(eid))
                    continue;
                bool reverse = (CurveOrientationF != null) ? CurveOrientationF(eid) : false;

                DCurve3 path = new DCurve3() { Closed = false };
                List<int> pathE = (bWantLoopIndices) ? new List<int>() : null;
                path.AppendVertex(graph.GetVertex(vid));
                if (pathE != null)
                    pathE.Add(eid);
                while ( true ) {
                    used.Add(eid);
                    Index2i next = NextEdgeAndVtx(eid, vid, graph);
                    eid = next.a;
                    vid = next.b;
                    path.AppendVertex(graph.GetVertex(vid));
                    if (boundaries.Contains(vid) || junctions.Contains(vid))
                        break;  // done!
                    if (pathE != null)
                        pathE.Add(eid);
                }
                if (reverse) 
                    path.Reverse();
                c.Paths.Add(path);

                if ( pathE != null ) {
                    Util.gDevAssert(pathE.Count == path.VertexCount - 1);
                    if (reverse)
                        pathE.Reverse();
                    c.PathEdges.Add(pathE);
                }
            }

            // ok we should be done w/ boundary verts now...
            //boundaries.Clear();
            c.BoundaryV = boundaries;


            foreach ( int start_vid in junctions ) {
                foreach (int outgoing_eid in graph.VtxEdgesItr(start_vid) ) {
                    if (used.Contains(outgoing_eid))
                        continue;
                    int vid = start_vid;
                    int eid = outgoing_eid;

                    bool reverse = (CurveOrientationF != null) ? CurveOrientationF(eid) : false;

                    DCurve3 path = new DCurve3() { Closed = false };
                    List<int> pathE = (bWantLoopIndices) ? new List<int>() : null;
                    path.AppendVertex(graph.GetVertex(vid));
                    if (pathE != null)
                        pathE.Add(eid);
                    while (true) {
                        used.Add(eid);
                        Index2i next = NextEdgeAndVtx(eid, vid, graph);
                        eid = next.a;
                        vid = next.b;
                        path.AppendVertex(graph.GetVertex(vid));
                        if (eid == int.MaxValue || junctions.Contains(vid))
                            break;  // done!
                        if (pathE != null)
                            pathE.Add(eid);
                    }

                    // we could end up back at our start junction vertex!
                    if (vid == start_vid) {
                        path.RemoveVertex(path.VertexCount - 1);
                        path.Closed = true;
                        if (reverse)
                            path.Reverse();
                        c.Loops.Add(path);

                        if (pathE != null) {
                            Util.gDevAssert(pathE.Count == path.VertexCount);
                            if (reverse)
                                pathE.Reverse();
                            c.LoopEdges.Add(pathE);
                        }

                        // need to mark incoming edge as used...but is it valid now?
                        //Util.gDevAssert(eid != int.MaxValue);
                        if (eid != int.MaxValue)
                            used.Add(eid);

                    } else {
                        if (reverse)
                            path.Reverse();
                        c.Paths.Add(path);

                        if (pathE != null) {
                            Util.gDevAssert(pathE.Count == path.VertexCount - 1);
                            if (reverse)
                                pathE.Reverse();
                            c.PathEdges.Add(pathE);
                        }
                    }
                }

            }
            c.JunctionV = junctions;


            // all that should be left are continuous loops...
            foreach ( int start_eid in graph.EdgeIndices() ) {
                if (used.Contains(start_eid))
                    continue;

                int eid = start_eid;
                Index2i ev = graph.GetEdgeV(eid);
                int vid = ev.a;

                bool reverse = (CurveOrientationF != null) ? CurveOrientationF(eid) : false;

                DCurve3 poly = new DCurve3() { Closed = true };
                List<int> polyE = (bWantLoopIndices) ? new List<int>() : null;
                poly.AppendVertex(graph.GetVertex(vid));
                if (polyE != null)
                    polyE.Add(eid);
                while (true) {
                    used.Add(eid);
                    Index2i next = NextEdgeAndVtx(eid, vid, graph);
                    eid = next.a;
                    vid = next.b;
                    poly.AppendVertex(graph.GetVertex(vid));
                    if (polyE != null)
                        polyE.Add(eid);
                    if (eid == int.MaxValue || junctions.Contains(vid))
                        throw new Exception("how did this happen??");
                    if (used.Contains(eid))
                        break;
                }
                poly.RemoveVertex(poly.VertexCount - 1);
                if (reverse)
                    poly.Reverse();
                c.Loops.Add(poly);

                if (polyE != null) {
                    polyE.RemoveAt(polyE.Count - 1);
                    Util.gDevAssert(polyE.Count == poly.VertexCount);
                    if (reverse)
                        polyE.Reverse();
                    c.LoopEdges.Add(polyE);
                }
            }

            return c;
        }




        /// <summary>
        /// foreach edge [vid,b] connected to junction vertex vid, remove, add new vertex c, 
        /// and then add new edge [b,c]. Optionally move c a bit back along edge from vid.
        /// </summary>
        public static void DisconnectJunction(DGraph3 graph, int vid, double shrinkFactor = 1.0)
        {
            Vector3d v = graph.GetVertex(vid);
            int[] nbr_verts = graph.VtxVerticesItr(vid).ToArray();
            for (int k = 0; k < nbr_verts.Length; ++k) {
                int eid = graph.FindEdge(vid, nbr_verts[k]);
                graph.RemoveEdge(eid, true);
                if (graph.IsVertex(nbr_verts[k])) {
                    Vector3d newpos = Vector3d.Lerp(graph.GetVertex(nbr_verts[k]), v, shrinkFactor);
                    int newv = graph.AppendVertex(newpos);
                    graph.AppendEdge(nbr_verts[k], newv);
                }
            }
        }



        /// <summary>
        /// If we are at edge eid, which as one vertex prev_vid, find 'other' vertex, and other edge connected to that vertex,
        /// and return pair [next_edge, shared_vtx]
        /// Returns [int.MaxValue, shared_vtx] if shared_vtx is not valence=2   (ie stops at boundaries and complex junctions)
        /// </summary>
        public static Index2i NextEdgeAndVtx(int eid, int prev_vid, DGraph3 graph)
        {
            Index2i ev = graph.GetEdgeV(eid);
            if (ev.a == DGraph3.InvalidID)
                return Index2i.Max;
            int next_vid = (ev.a == prev_vid) ? ev.b : ev.a;

            if (graph.GetVtxEdgeCount(next_vid) != 2)
                return new Index2i(int.MaxValue, next_vid);

            foreach (int next_eid in graph.VtxEdgesItr(next_vid)) {
                if (next_eid != eid)
                    return new Index2i(next_eid, next_vid);
            }
            return Index2i.Max;
        }




        /// <summary>
        /// walk through graph from fromVtx, in direction of eid, until we hit the next junction vertex
        /// </summary>
        public static List<int> WalkToNextNonRegularVtx(DGraph3 graph, int fromVtx, int eid)
        {
            List<int> path = new List<int>();
            path.Add(fromVtx);
            int cur_vid = fromVtx;
            int cur_eid = eid;
            bool bContinue = true;
            while (bContinue) {
                Index2i next = DGraph3Util.NextEdgeAndVtx(cur_eid, cur_vid, graph);
                int next_eid = next.a;
                int next_vtx = next.b;
                if (next_eid == int.MaxValue) {
                    if (graph.IsRegularVertex(next_vtx) == false ) {
                        path.Add(next_vtx);
                        bContinue = false;
                    } else {
                        throw new Exception("WalkToNextNonRegularVtx: have no next edge but vtx is regular - how?");
                    }
                } else {
                    path.Add(next_vtx);
                    cur_vid = next_vtx;
                    cur_eid = next_eid;
                }
            }
            return path;
        }





        /// <summary>
        /// Erode inwards from open boundary vertices of graph (ie vtx with single edge).
        /// Resulting graph is not compact (!)
        /// </summary>
        public static void ErodeOpenSpurs(DGraph3 graph)
        {
            HashSet<int> used = new HashSet<int>();     // do we need this?

            // find boundary and junction vertices
            HashSet<int> boundaries = new HashSet<int>();
            HashSet<int> junctions = new HashSet<int>();
            foreach (int vid in graph.VertexIndices()) {
                if (graph.IsBoundaryVertex(vid))
                    boundaries.Add(vid);
                if (graph.IsJunctionVertex(vid))
                    junctions.Add(vid);
            }

            // walk paths from boundary vertices
            foreach (int start_vid in boundaries) {
                if (graph.IsVertex(start_vid) == false)
                    continue;

                int vid = start_vid;
                int eid = graph.GetVtxEdges(vid)[0];
                if (used.Contains(eid))
                    continue;

                List<int> pathE = new List<int>();
                if (pathE != null)
                    pathE.Add(eid);
                while (true) {
                    used.Add(eid);
                    Index2i next = NextEdgeAndVtx(eid, vid, graph);
                    eid = next.a;
                    vid = next.b;
                    if (boundaries.Contains(vid) || junctions.Contains(vid))
                        break;  // done!
                    if (pathE != null)
                        pathE.Add(eid);
                }

                // delete this path
                foreach (int path_eid in pathE)
                    graph.RemoveEdge(path_eid, true);
            }

        }



    }
}
