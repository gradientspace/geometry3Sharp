using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    public class MeshIsoCurves
    {
        public DMesh3 Mesh;
        public Func<Vector3d, double> ValueF = null;

        /// <summary>
        /// Optional - provide value at vertex (which may be precomputed)
        /// </summary>
        public Func<int, double> VertexValueF = null;

        /// <summary>
        /// If true, then we internally precompute vertex values.
        /// ***THIS COMPUTATION IS MULTI-THREADED***
        /// </summary>
        public bool PrecomputeVertexValues = false;


        public enum RootfindingModes { SingleLerp, LerpSteps, Bisection }

        /// <summary>
        /// Which rootfinding method will be used to converge on surface along edges
        /// </summary>
        public RootfindingModes RootMode = RootfindingModes.SingleLerp;

        /// <summary>
        /// number of iterations of rootfinding method (ignored for SingleLerp)
        /// </summary>
        public int RootModeSteps = 5;


        public DGraph3 Graph = null;

        public enum TriangleCase
        {
            EdgeEdge = 1,
            EdgeVertex = 2,
            OnEdge = 3
        }

        public bool WantGraphEdgeInfo = false;

        /// <summary>
        /// Information about edge of the computed Graph. 
        /// mesh_tri is triangle ID of crossed triangle
        /// mesh_edges depends on case. EdgeEdge is [edgeid,edgeid], EdgeVertex is [edgeid,vertexid], and OnEdge is [edgeid,-1]
        /// </summary>
        public struct GraphEdgeInfo
        {
            public TriangleCase caseType;
            public int mesh_tri;
            public Index2i mesh_edges;
            public Index2i order;
        }
        public DVector<GraphEdgeInfo> GraphEdges = null;

        // locations of edge crossings that we found during rootfinding. key is edge id.
        Dictionary<int, Vector3d> EdgeLocations = new Dictionary<int, Vector3d>();


        public MeshIsoCurves(DMesh3 mesh, Func<Vector3d, double> valueF)
        {
            Mesh = mesh;
            ValueF = valueF;
        }

        public void Compute()
        {
            compute_full(Mesh.TriangleIndices(), true);
        }
        public void Compute(IEnumerable<int> Triangles)
        {
            compute_full(Triangles);
        }


        /*
         * Internals
         */

        Dictionary<Vector3d, int> Vertices;

        protected void compute_full(IEnumerable<int> Triangles, bool bIsFullMeshHint = false)
        {
            Graph = new DGraph3();
            if (WantGraphEdgeInfo)
                GraphEdges = new DVector<GraphEdgeInfo>();

            Vertices = new Dictionary<Vector3d, int>();


            // multithreaded precomputation of per-vertex values
            double[] vertex_values = null;
            if (PrecomputeVertexValues) {
                vertex_values = new double[Mesh.MaxVertexID];
                IEnumerable<int> verts = Mesh.VertexIndices();
                if (bIsFullMeshHint == false) {
                    MeshVertexSelection vertices = new MeshVertexSelection(Mesh);
                    vertices.SelectTriangleVertices(Triangles);
                    verts = vertices;
                }
                gParallel.ForEach(verts, (vid) => {
                    vertex_values[vid] = ValueF(Mesh.GetVertex(vid));
                });
                VertexValueF = (vid) => { return vertex_values[vid]; };
            }


            foreach (int tid in Triangles) {

                Vector3dTuple3 tv = new Vector3dTuple3();
                Mesh.GetTriVertices(tid, ref tv.V0, ref tv.V1, ref tv.V2);
                Index3i triVerts = Mesh.GetTriangle(tid);

                Vector3d f = (VertexValueF != null) ?
                    new Vector3d(VertexValueF(triVerts.a), VertexValueF(triVerts.b), VertexValueF(triVerts.c))
                    : new Vector3d(ValueF(tv.V0), ValueF(tv.V1), ValueF(tv.V2));

                // round f to 0 within epsilon?

                if (f.x < 0 && f.y < 0 && f.z < 0)
                    continue;
                if (f.x > 0 && f.y > 0 && f.z > 0)
                    continue;

                Index3i triEdges = Mesh.GetTriEdges(tid);

                if (f.x * f.y * f.z == 0) {
                    int z0 = (f.x == 0) ? 0 : ((f.y == 0) ? 1 : 2);
                    int i1 = (z0+1) % 3, i2 = (z0+2) % 3;
                    if (f[i1] * f[i2] > 0)  
                        continue;       // single-vertex-crossing case, skip here and let other edges catch it

                    if (f[i1] == 0 || f[i2] == 0) {
                        // on-edge case
                        int z1 = f[i1] == 0 ? i1 : i2;
                        if ( (z0+1)%3 != z1 ) {     
                            int tmp = z0; z0 = z1; z1 = tmp;        // catch reverse-orientation cases
                        }
                        int e0 = add_or_append_vertex(Mesh.GetVertex(triVerts[z0]));
                        int e1 = add_or_append_vertex(Mesh.GetVertex(triVerts[z1]));
                        int graph_eid = Graph.AppendEdge(e0, e1, (int)TriangleCase.OnEdge);
						if (graph_eid >= 0 && WantGraphEdgeInfo)
                            add_on_edge(graph_eid, tid, triEdges[z0], new Index2i(e0, e1));

                    } else {
                        // edge/vertex case
                        Util.gDevAssert(f[i1] * f[i2] < 0);

                        int vert_vid = add_or_append_vertex(Mesh.GetVertex(triVerts[z0]));

                        int i = i1, j = i2;
                        if (triVerts[j] < triVerts[i]) {
                            int tmp = i; i = j; j = tmp;
                        }
                        Vector3d cross = find_crossing(tv[i], tv[j], f[i], f[j]);
                        int cross_vid = add_or_append_vertex(cross);
                        add_edge_pos(triVerts[i], triVerts[j], cross);

                        if (vert_vid != cross_vid) {
                            int graph_eid = Graph.AppendEdge(vert_vid, cross_vid, (int)TriangleCase.EdgeVertex);
                            if (graph_eid >= 0 && WantGraphEdgeInfo)
                                add_edge_vert(graph_eid, tid, triEdges[(z0 + 1) % 3], triVerts[z0], new Index2i(vert_vid, cross_vid));
                        } // else degenerate edge
                    }

                } else {
                    Index3i cross_verts = Index3i.Min;
                    int less_than = 0;
                    for (int tei = 0; tei < 3; ++tei) {
                        int i = tei, j = (tei + 1) % 3;
                        if (f[i] < 0)
                            less_than++;
                        if (f[i] * f[j] > 0)
                            continue;
                        if ( triVerts[j] < triVerts[i] ) {
                            int tmp = i; i = j; j = tmp;
                        }
                        Vector3d cross = find_crossing(tv[i], tv[j], f[i], f[j]);
                        cross_verts[tei] = add_or_append_vertex(cross);
                        add_edge_pos(triVerts[i], triVerts[j], cross);
                    }
                    int e0 = (cross_verts.a == int.MinValue) ? 1 : 0;
                    int e1 = (cross_verts.c == int.MinValue) ? 1 : 2;
                    if (e0 == 0 && e1 == 2) {       // preserve orientation order
                        e0 = 2; e1 = 0;
                    }

                    // preserving orientation does not mean we get a *consistent* orientation across faces.
                    // To do that, we need to assign "sides". Either we have 1 less-than-0 or 1 greater-than-0 vtx.
                    // Arbitrary decide that we want loops oriented like bdry loops would be if we discarded less-than side.
                    // In that case, when we are "cutting off" one vertex, orientation would end up flipped
                    if (less_than == 1) {
                        int tmp = e0; e0 = e1; e1 = tmp;
                    }

                    int ev0 = cross_verts[e0];
                    int ev1 = cross_verts[e1];
                    // [RMS] if function is garbage, we can end up w/ case where both crossings
                    //   happen at same vertex, even though values are not the same (eg if
                    //   some values are double.MaxValue). We will just fail in these cases.
                    if (ev0 != ev1) {
                        Util.gDevAssert(ev0 != int.MinValue && ev1 != int.MinValue);
                        int graph_eid = Graph.AppendEdge(ev0, ev1, (int)TriangleCase.EdgeEdge);
						if (graph_eid >= 0 && WantGraphEdgeInfo)
                            add_edge_edge(graph_eid, tid, new Index2i(triEdges[e0], triEdges[e1]), new Index2i(ev0,ev1));
                    }
                }
            }


            Vertices = null;
        }


        int add_or_append_vertex(Vector3d pos)
        {
            int vid;
            if (Vertices.TryGetValue(pos, out vid) == false) {
                vid = Graph.AppendVertex(pos);
                Vertices.Add(pos, vid);
            }
            return vid;
        }


        void add_edge_edge(int graph_eid, int mesh_tri, Index2i mesh_edges, Index2i order)
        {
            GraphEdgeInfo einfo = new GraphEdgeInfo() {
                caseType = TriangleCase.EdgeEdge,
                mesh_edges = mesh_edges,
                mesh_tri = mesh_tri,
                order = order
            };
            GraphEdges.insertAt(einfo, graph_eid);
        }

        void add_edge_vert(int graph_eid, int mesh_tri, int mesh_edge, int mesh_vert, Index2i order)
        {
            GraphEdgeInfo einfo = new GraphEdgeInfo() {
                caseType = TriangleCase.EdgeVertex,
                mesh_edges = new Index2i(mesh_edge, mesh_vert),
                mesh_tri = mesh_tri,
                order = order
            };
            GraphEdges.insertAt(einfo, graph_eid);
        }

        void add_on_edge(int graph_eid, int mesh_tri, int mesh_edge, Index2i order)
        {
            GraphEdgeInfo einfo = new GraphEdgeInfo() {
                caseType = TriangleCase.OnEdge,
                mesh_edges = new Index2i(mesh_edge, -1),
                mesh_tri = mesh_tri,
                order = order
            };
            GraphEdges.insertAt(einfo, graph_eid);
        }


        // [TODO] should convert this to a utility function
        Vector3d find_crossing(Vector3d a, Vector3d b, double fA, double fB)
        {
            if (fB < fA) {
                Vector3d tmp = a; a = b; b = tmp;
                double f = fA; fA = fB; fB = f;
            }

            if (RootMode == RootfindingModes.Bisection) {
                for ( int k = 0; k < RootModeSteps; ++k ) {
                    Vector3d c = Vector3d.Lerp(a, b, 0.5);
                    double f = ValueF(c);
                    if ( f < 0 ) {
                        fA = f;  a = c;
                    } else {
                        fB = f;  b = c;
                    }
                }
                return Vector3d.Lerp(a, b, 0.5);

            } else {
                // really should check this every iteration...
                if ( Math.Abs(fB-fA) < MathUtil.ZeroTolerance )
                    return a;

                double t = 0;
                if (RootMode == RootfindingModes.LerpSteps) {
                    for (int k = 0; k < RootModeSteps; ++k) {
                        t = MathUtil.Clamp((0 - fA) / (fB - fA), 0, 1);
                        Vector3d c = (1 - t)*a + (t)*b;
                        double f = ValueF(c);
                        if (f < 0) {
                            fA = f; a = c;
                        } else {
                            fB = f; b = c;
                        }
                    }
                }

                t = MathUtil.Clamp((0 - fA) / (fB - fA), 0, 1);
                return (1 - t) * a + (t) * b;
            }
        }



        void add_edge_pos(int a, int b, Vector3d crossing_pos)
        {
            int eid = Mesh.FindEdge(a, b);
            if (eid == DMesh3.InvalidID)
                throw new Exception("MeshIsoCurves.add_edge_split: invalid edge?");
            if (EdgeLocations.ContainsKey(eid))
                return;
            EdgeLocations[eid] = crossing_pos;
        }


        /// <summary>
        /// Split the mesh edges at the iso-crossings, unless edge is
        /// shorter than min_len, or inserted point would be within min_len or vertex
        /// [TODO] do we want to return any info here??
        /// </summary>
        public void SplitAtIsoCrossings(double min_len = 0)
        {
            foreach ( var pair in EdgeLocations ) {
                int eid = pair.Key;
                Vector3d pos = pair.Value;
                if (!Mesh.IsEdge(eid))
                    continue;

                Index2i ev = Mesh.GetEdgeV(eid);
                Vector3d a = Mesh.GetVertex(ev.a);
                Vector3d b = Mesh.GetVertex(ev.b);
                if (a.Distance(b) < min_len)
                    continue;
                Vector3d mid = (a + b) * 0.5;
                if (a.Distance(mid) < min_len || b.Distance(mid) < min_len)
                    continue;

                DMesh3.EdgeSplitInfo splitInfo;
                if (Mesh.SplitEdge(eid, out splitInfo) == MeshResult.Ok)
                    Mesh.SetVertex(splitInfo.vNew, pos);
            }
        }




        /// <summary>
        /// DGraph3 edges are not oriented, which means they cannot inherit orientation from mesh.
        /// This function returns true if, for a given graph_eid, the vertex pair returned by
        /// Graph.GetEdgeV(graph_eid) should be reversed to be consistent with mesh orientation.
        /// Mainly inteded to be passed to DGraph3Util.ExtractCurves
        /// </summary>
        public bool ShouldReverseGraphEdge(int graph_eid)
        {
            if (GraphEdges == null)
                throw new Exception("MeshIsoCurves.OrientEdge: must track edge graph info to orient edge");

            Index2i graph_ev = Graph.GetEdgeV(graph_eid);
            GraphEdgeInfo einfo = GraphEdges[graph_eid];

            if (graph_ev.b == einfo.order.a && graph_ev.a == einfo.order.b) {
                return true;
            }
            Util.gDevAssert(graph_ev.a == einfo.order.a && graph_ev.b == einfo.order.b);
            return false;
        }


    }



}
