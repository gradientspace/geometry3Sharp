using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    /// <summary>
    /// This class is used to bisect an existing DGraph2 with infinite lines.
    /// This is easier than inserting new segments, which can be done using Arrangement2d.
    /// 
    /// Computations are done in double precision. Use at your own risk.
    /// 
    /// [TODO]
    ///   - computation of signs for a split-line is currently O(N). If inserting many
    ///     parallel lines, can improve this using standard sorting.
    /// </summary>
    public class GraphSplitter2d
    {
        public DGraph2 Graph;

        /// <summary>
        /// tolerance for WhichSide(vtx) tests
        /// </summary>
        public double OnVertexTol = MathUtil.Epsilonf;

        /// <summary>
        /// default ID for new edges, can override in specific functions
        /// </summary>
        public int InsertedEdgesID = 1;

        /// <summary>
        /// when inserting new segments, we check if their midpoint passes this test
        /// </summary>
        public Func<Vector2d, bool> InsideTestF = null;


        public GraphSplitter2d(DGraph2 graph)
        {
            Graph = graph;
        }

        /// <summary>
        /// split all graph edges that intersect line, and insert segments
        /// connecting these points
        /// </summary>
        public void InsertLine(Line2d line, int insert_edges_id = -1)
        {
            if (insert_edges_id == -1)
                insert_edges_id = InsertedEdgesID;
            do_split(line, true, insert_edges_id);
        }





        DVector<int> EdgeSigns = new DVector<int>();

        struct edge_hit
        {
            public int hit_eid;
            public Index2i vtx_signs;
            public int hit_vid;
            public Vector2d hit_pos;
            public double line_t;
        }

        List<edge_hit> hits = new List<edge_hit>();

        protected virtual void do_split(Line2d line, bool insert_edges, int insert_gid)
        {
            if (EdgeSigns.Length < Graph.MaxVertexID)
                EdgeSigns.resize(Graph.MaxVertexID);
            foreach ( int vid in Graph.VertexIndices()) {
                EdgeSigns[vid] = line.WhichSide(Graph.GetVertex(vid), OnVertexTol);
            }


            hits.Clear();
            foreach ( int eid in Graph.EdgeIndices() ) {
                Index2i ev = Graph.GetEdgeV(eid);
                Index2i signs = new Index2i(EdgeSigns[ev.a], EdgeSigns[ev.b]);
                if (signs.a * signs.b > 0)
                    continue;   // both positive or negative, ignore

                edge_hit hit = new edge_hit() { hit_eid = eid, vtx_signs = signs, hit_vid = -1 };
                Vector2d a = Graph.GetVertex(ev.a);
                Vector2d b = Graph.GetVertex(ev.b);

                // parallel-edge case (both are zero)
                if (signs.a == signs.b) {
                    if ( a.DistanceSquared(b) > MathUtil.Epsilon ) {
                        // we need to somehow not insert a new segment for this span below. 
                        // so, insert two hit points for the ray-interval, with same eid.
                        // This will result in this span being skipped by the same-eid test below
                        // *however*, if other edges self-intersect w/ this segment, this will *not work*
                        // and duplicate edges will be inserted
                        hit.hit_vid = ev.a;
                        hit.line_t = line.Project(a);
                        hits.Add(hit);
                        hit.hit_vid = ev.b;
                        hit.line_t = line.Project(b);
                        hits.Add(hit);
                    } else {
                        // degenerate edge - fall through to a == 0 case below
                        signs.b = 1;
                    }
                }

                if ( signs.a == 0 ) {
                    hit.hit_pos = a;
                    hit.hit_vid = ev.a;
                    hit.line_t = line.Project(a);
                } else if (signs.b == 0 ) {
                    hit.hit_pos = b;
                    hit.hit_vid = ev.b;
                    hit.line_t = line.Project(b);
                } else {
                    IntrLine2Segment2 intr = new IntrLine2Segment2(line, new Segment2d(a, b));
                    if (intr.Find() == false)
                        throw new Exception("GraphSplitter2d.Split: signs are different but ray did not it?");
                    if ( intr.IsSimpleIntersection ) {
                        hit.hit_pos = intr.Point;
                        hit.line_t = intr.Parameter;
                    } else {
                        throw new Exception("GraphSplitter2d.Split: got parallel edge case!");
                    }
                }
                hits.Add(hit);
            }

            // sort by increasing ray-t
            hits.Sort((hit0, hit1) => { return hit0.line_t.CompareTo(hit1.line_t); });

            // insert segments between successive intersection points
            int N = hits.Count;
            for ( int i = 0; i < N-1; ++i ) {
                int j = i + 1;
                // note: skipping parallel segments depends on this eid == eid test (see above)
                if (hits[i].line_t == hits[j].line_t || hits[i].hit_eid == hits[j].hit_eid)
                    continue;

                int vi = hits[i].hit_vid;
                int vj = hits[j].hit_vid;
                if (vi == vj && vi >= 0)
                    continue;

                if (vi >= 0 && vj >= 0) { 
                    int existing = Graph.FindEdge(vi, vj);
                    if (existing >= 0 )
                        continue;
                }

                if (vi == -1) {
                    DGraph2.EdgeSplitInfo split;
                    var result = Graph.SplitEdge(hits[i].hit_eid, out split);
                    if (result != MeshResult.Ok)
                        throw new Exception("GraphSplitter2d.Split: first edge split failed!");
                    vi = split.vNew;
                    Graph.SetVertex(vi, hits[i].hit_pos);
                    edge_hit tmp = hits[i]; tmp.hit_vid = vi; hits[i] = tmp;
                }

                if ( vj == -1) {
                    DGraph2.EdgeSplitInfo split;
                    var result = Graph.SplitEdge(hits[j].hit_eid, out split);
                    if (result != MeshResult.Ok)
                        throw new Exception("GraphSplitter2d.Split: second edge split failed!");
                    vj = split.vNew;
                    Graph.SetVertex(vj, hits[j].hit_pos);
                    edge_hit tmp = hits[j]; tmp.hit_vid = vj; hits[j] = tmp;
                }

                // check if we actually want to add this segment
                if ( InsideTestF != null ) {
                    Vector2d midpoint = 0.5 * (Graph.GetVertex(vi) + Graph.GetVertex(vj));
                    if (InsideTestF(midpoint) == false)
                        continue;
                }

                if (insert_edges)
                    Graph.AppendEdge(vi, vj, insert_gid);
            }


        }



    }
}
