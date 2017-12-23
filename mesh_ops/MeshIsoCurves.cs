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

        public DGraph3 Graph = null;

        public enum TriangleCase
        {
            EdgeEdge = 1,
            EdgeVertex = 2,
            OnEdge = 3
        }

        public bool WantGraphEdgeInfo = false;

        public struct GraphEdgeInfo
        {
            public TriangleCase caseType;
            public int mesh_tri;
            public Index2i mesh_edges;
        }
        public DVector<GraphEdgeInfo> GraphEdges = null;


        public MeshIsoCurves(DMesh3 mesh, Func<Vector3d, double> valueF)
        {
            Mesh = mesh;
            ValueF = valueF;
        }

        public void Compute()
        {
            compute_full(Mesh.TriangleIndices());
        }
        public void Compute(IEnumerable<int> Triangles)
        {
            compute_full(Triangles);
        }


        /*
         * Internals
         */

        Dictionary<Vector3d, int> Vertices;

        protected void compute_full(IEnumerable<int> Triangles)
        {
            Graph = new DGraph3();
            if (WantGraphEdgeInfo)
                GraphEdges = new DVector<GraphEdgeInfo>();

            Vertices = new Dictionary<Vector3d, int>();

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
                        int e0 = add_or_append_vertex(Mesh.GetVertex(triVerts[z0]));
                        int e1 = add_or_append_vertex(Mesh.GetVertex(triVerts[z1]));
                        int graph_eid = Graph.AppendEdge(e0, e1, (int)TriangleCase.OnEdge);
                        if (WantGraphEdgeInfo)
                            add_on_edge(graph_eid, tid, triEdges[z0]);

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

                        int graph_eid = Graph.AppendEdge(vert_vid, cross_vid, (int)TriangleCase.EdgeVertex);
                        if (WantGraphEdgeInfo)
                            add_edge_edge(graph_eid, tid, new Index2i(triEdges[(z0+1)%3], triVerts[z0]));
                    }

                } else {
                    Index3i cross_verts = Index3i.Min;
                    for (int ti = 0; ti < 3; ++ti) {
                        int i = ti, j = (ti + 1) % 3;
                        if (f[i] * f[j] > 0)
                            continue;
                        if ( triVerts[j] < triVerts[i] ) {
                            int tmp = i; i = j; j = tmp;
                        }
                        Vector3d cross = find_crossing(tv[i], tv[j], f[i], f[j]);
                        cross_verts[ti] = add_or_append_vertex(cross);
                    }
                    int e0 = (cross_verts.a == int.MinValue) ? 1 : 0;
                    int e1 = (cross_verts.c == int.MinValue) ? 1 : 2;
                    int ev0 = cross_verts[e0];
                    int ev1 = cross_verts[e1];
                    // [RMS] if function is garbage, we can end up w/ case where both crossings
                    //   happen at same vertex, even though values are not the same (eg if
                    //   some values are double.MaxValue). We will just fail in these cases.
                    if (ev0 != ev1) {
                        Util.gDevAssert(ev0 != int.MinValue && ev1 != int.MinValue);
                        int graph_eid = Graph.AppendEdge(ev0, ev1, (int)TriangleCase.EdgeEdge);
                        if (WantGraphEdgeInfo)
                            add_edge_edge(graph_eid, tid, new Index2i(triEdges[e0], triEdges[e1]));
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


        void add_edge_edge(int graph_eid, int mesh_tri, Index2i mesh_edges)
        {
            GraphEdgeInfo einfo = new GraphEdgeInfo() {
                caseType = TriangleCase.EdgeEdge,
                mesh_edges = mesh_edges,
                mesh_tri = mesh_tri
            };
            GraphEdges.insertAt(einfo, graph_eid);
        }

        void add_edge_vert(int graph_eid, int mesh_tri, int mesh_edge, int mesh_vert)
        {
            GraphEdgeInfo einfo = new GraphEdgeInfo() {
                caseType = TriangleCase.EdgeVertex,
                mesh_edges = new Index2i(mesh_edge, mesh_vert),
                mesh_tri = mesh_tri
            };
            GraphEdges.insertAt(einfo, graph_eid);
        }

        void add_on_edge(int graph_eid, int mesh_tri, int mesh_edge)
        {
            GraphEdgeInfo einfo = new GraphEdgeInfo() {
                caseType = TriangleCase.OnEdge,
                mesh_edges = new Index2i(mesh_edge, -1),
                mesh_tri = mesh_tri
            };
            GraphEdges.insertAt(einfo, graph_eid);
        }


        Vector3d find_crossing(Vector3d a, Vector3d b, double fA, double fB)
        {
            double t = 0.5;
            if (fA < fB) {
                t = (0 - fA) / (fB - fA);
                t = MathUtil.Clamp(t, 0, 1);
                return (1 - t) * a + (t) * b;
            } else if ( fB < fA ) {
                t = (0 - fB) / (fA - fB);
                t = MathUtil.Clamp(t, 0, 1);
                return (1 - t) * b + (t) * a;
            } else
                return a;
        }



    }



}
