using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    /// <summary>
    /// This class extracts the set of loops bounding the "cells" of a DGraph2, ie 
    /// each cell is a connected region with a polygonal boundary. 
    /// Precondition: the graph has no self-intersections.
    /// Precondition: at any vertex, the edges are sortable by angle (ie no outgoing edges overlap)
    ///     ** numerically this may not be 100% reliable....
    /// 
    /// Both "sides" of each edge are included in some cell boundary, ie so for a simple
    /// polygon, there are two cells, one infinitely large. The "inside" cells will be
    /// oriented clockwise, if converted to a Polygon2d.
    /// 
    /// </summary>
    public class GraphCells2d
    {
        public DGraph2 Graph;

        /*
         * Outputs
         */

        // set of boundary loops of cells
        public List<int[]> CellLoops;


        public GraphCells2d(DGraph2 graph)
        {
            Graph = graph;
        }

        public void FindCells()
        {
            // First we construct "wedges", which are pairs of sequential edges around each vtx.
            // we then put all in a hash set, we will iterate until we use up all the available wedges.
            // Edges are sorted by positive angle, so they are in clockwise order
            // Hence, going from edge n to n+1 is a left-turn, and so "inside" cells are oriented clockwise
            int NV = Graph.MaxVertexID;
            Index2i[][] wedges = new Index2i[NV][];
            HashSet<Index2i> remaining = new HashSet<Index2i>();
            foreach (int vid in Graph.VertexIndices()) {
                int[] sorted = Graph.SortedVtxEdges(vid);
                wedges[vid] = new Index2i[sorted.Length];

                for (int k = 0; k < sorted.Length; ++k) {
                    wedges[vid][k] = new Index2i(sorted[k], sorted[(k + 1) % sorted.Length]);
                    remaining.Add(new Index2i(vid, k));
                }
            }

            CellLoops = new List<int[]>();

            List<int> loopv = new List<int>();
            while (remaining.Count > 0 ) {
                Index2i idx = remaining.First();
                remaining.Remove(idx);

                int start_vid = idx.a;
                int wid = idx.b;
                int e0 = wedges[start_vid][wid].a; e0 = e0+1-1;   // get rid of unused variable warning, want to keep this for debugging
                int e1 = wedges[start_vid][wid].b;

                loopv.Clear();
                loopv.Add(start_vid);

                int cur_v = start_vid;
                int outgoing_e = e1;

                // walk around loop taking immediate left-turns
                bool done = false;
                while (!done) {
                    // find outgoing edge and vtx at far end
                    Index2i ev = Graph.GetEdgeV(outgoing_e);
                    int next_v = (ev.a == cur_v) ? ev.b : ev.a;

                    if ( next_v == start_vid ) {
                        done = true;
                        continue;
                    }

                    // now find wedge at that vtx where this is incoming edge
                    Index2i[] next_wedges = wedges[next_v];
                    int use_wedge_idx = -1;
                    for ( int k = 0; k < next_wedges.Length; ++k ) {
                        if ( next_wedges[k].a == outgoing_e ) {
                            use_wedge_idx = k;
                            break;
                        }
                    }
                    if (use_wedge_idx == -1)
                        throw new Exception("could not find next wedge?");

                    remaining.Remove(new Index2i(next_v, use_wedge_idx));
                    loopv.Add(next_v);
                    cur_v = next_v;
                    outgoing_e = next_wedges[use_wedge_idx].b;
                }

                CellLoops.Add(loopv.ToArray());
            }

        }


        /// <summary>
        /// Convert cells to polygons, with optional filter. 
        /// If filter returns false, polygon is not included in output
        /// </summary>
        public List<Polygon2d> CellsToPolygons(Func<Polygon2d, bool> FilterF = null)
        {
            List<Polygon2d> result = new List<Polygon2d>();

            for ( int i = 0; i < CellLoops.Count; ++i) {
                int[] loop = CellLoops[i];
                Polygon2d poly = new Polygon2d();
                for (int k = 0; k < loop.Length; ++k)
                    poly.AppendVertex(Graph.GetVertex(loop[k]));

                if (FilterF != null && FilterF(poly) == false)
                    continue;

                result.Add(poly);
            }
            return result;
        }


        /// <summary>
        /// Find cells that are "inside" the container polygon.
        /// Currently based on finding a point inside the cell and then
        /// checking that it is also inside the container. 
        /// This is perhaps not ideal!!
        /// </summary>
        public List<Polygon2d> ContainedCells(GeneralPolygon2d container)
        {
            Func<Polygon2d, bool> filterF = (poly) => {
                bool bIsCW = poly.IsClockwise;
                for (int k = 0; k < poly.VertexCount; k++) {
                    Segment2d s = poly.Segment(k);
                    Vector2d pt = s.Center + MathUtil.Epsilonf * s.Direction.Perp;
                    if (poly.Contains(pt) == bIsCW)
                        return container.Contains(pt);
                }
                // give up
                return false;
            };
            return CellsToPolygons(filterF);
        }


    }
}
