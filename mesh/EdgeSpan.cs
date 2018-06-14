using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace g3
{
	/// <summary>
	/// An EdgeSpan is a continous set of edges in a Mesh that is *not* closed
	/// (that would be an EdgeLoop)
	/// </summary>
    public class EdgeSpan
    {
        public DMesh3 Mesh;

        public int[] Vertices;
        public int[] Edges;

        public int[] BowtieVertices;        // this may not be initialized!


        public EdgeSpan(DMesh3 mesh)
        {
            Mesh = mesh;
        }

        public EdgeSpan(DMesh3 mesh, int[] vertices, int[] edges, bool bCopyArrays)
        {
            Mesh = mesh;
            if (bCopyArrays) {
                Vertices = new int[vertices.Length];
                Array.Copy(vertices, Vertices, Vertices.Length);
                Edges = new int[edges.Length];
                Array.Copy(edges, Edges, Edges.Length);
            } else {
                Vertices = vertices;
                Edges = edges;
            }
        }

        /// <summary>
        /// construct EdgeSpan from a list of edges of mesh
        /// </summary>
        public static EdgeSpan FromEdges(DMesh3 mesh, IList<int> edges)
        {
            int[] Edges = new int[edges.Count];
            for (int i = 0; i < Edges.Length; ++i)
                Edges[i] = edges[i];
            int[] Vertices = new int[Edges.Length+1];
            Index2i start_ev = mesh.GetEdgeV(Edges[0]);
            Index2i prev_ev = start_ev;
            if (Edges.Length > 1) {
                for (int i = 1; i < Edges.Length; ++i) {
                    Index2i next_ev = mesh.GetEdgeV(Edges[i]);
                    Vertices[i] = IndexUtil.find_shared_edge_v(ref prev_ev, ref next_ev);
                    prev_ev = next_ev;
                }
                Vertices[0] = IndexUtil.find_edge_other_v(ref start_ev, Vertices[1]);
                Vertices[Vertices.Length - 1] = IndexUtil.find_edge_other_v(prev_ev, Vertices[Vertices.Length - 2]);
            } else {
                Vertices[0] = start_ev[0]; Vertices[1] = start_ev[1];
            }
            return new EdgeSpan(mesh, Vertices, Edges, false);
        }


        /// <summary>
        /// construct EdgeSpan from a list of vertices of mesh
        /// </summary>
        public static EdgeSpan FromVertices(DMesh3 mesh, IList<int> vertices)
        {
            int NV = vertices.Count;
            int[] Vertices = new int[NV];
            for (int i = 0; i < NV; ++i)
                Vertices[i] = vertices[i];
            int NE = NV - 1;
            int[] Edges = new int[NE];
            for ( int i = 0; i < NE; ++i ) {
                Edges[i] = mesh.FindEdge(Vertices[i], Vertices[i + 1]);
                if (Edges[i] == DMesh3.InvalidID)
                    throw new Exception("EdgeSpan.FromVertices: vertices are not connected by edge!");
            }
            return new EdgeSpan(mesh, Vertices, Edges, false);
        }



        public int VertexCount {
            get { return Vertices.Length; }
        }
        public int EdgeCount {
            get { return Edges.Length; }
        }

        public Vector3d GetVertex(int i) {
            return Mesh.GetVertex(Vertices[i]);
        }


        public AxisAlignedBox3d GetBounds()
        {
            AxisAlignedBox3d box = AxisAlignedBox3d.Empty;
            for (int i = 0; i < Vertices.Length; ++i)
                box.Contain(Mesh.GetVertex(Vertices[i]));
            return box;
        }


        public DCurve3 ToCurve(DMesh3 sourceMesh = null)
        {
            if (sourceMesh == null)
                sourceMesh = Mesh;
            DCurve3 curve = MeshUtil.ExtractLoopV(sourceMesh, Vertices);
            curve.Closed = false;
            return curve;
        }


        public bool IsInternalSpan()
        {
            int NV = Vertices.Length;
            for (int i = 0; i < NV-1; ++i ) {
                int eid = Mesh.FindEdge(Vertices[i], Vertices[i + 1]);
                Debug.Assert(eid != DMesh3.InvalidID);
                if (Mesh.IsBoundaryEdge(eid))
                    return false;
            }
            return true;
        }


        public bool IsBoundarySpan(DMesh3 testMesh = null)
        {
            DMesh3 useMesh = (testMesh != null) ? testMesh : Mesh;

            int NV = Vertices.Length;
            for (int i = 0; i < NV-1; ++i ) {
                int eid = useMesh.FindEdge(Vertices[i], Vertices[i + 1]);
                Debug.Assert(eid != DMesh3.InvalidID);
                if (useMesh.IsBoundaryEdge(eid) == false)
                    return false;
            }
            return true;
        }


        public int FindNearestVertex(Vector3d v)
        {
            int iNear = -1;
            double fNearSqr = double.MaxValue;
            int N = Vertices.Length;
            for ( int i = 0; i < N; ++i ) {
                Vector3d lv = Mesh.GetVertex(Vertices[i]);
                double d2 = v.DistanceSquared(lv);
                if ( d2 < fNearSqr ) {
                    fNearSqr = d2;
                    iNear = i;
                }
            }
            return iNear;
        }

        // count # of vertices in loop that are within tol of v
        // final param returns last encountered index within tolerance, or -1 if return is 0
        public int CountWithinTolerance(Vector3d v, double tol, out int last_in_tol)
        {
            last_in_tol = -1;
            int count = 0;
            int N = Vertices.Length;
            for (int i = 0; i < N; ++i) {
                Vector3d lv = Mesh.GetVertex(Vertices[i]);
                if (v.Distance(lv) < tol) {
                    count++;
                    last_in_tol = i;
                }
            }
            return count;
        }


        // Check if Spanw is the same set of positions on another mesh.
        // Does not require the indexing to be the same
        public bool IsSameSpan(EdgeSpan Spanw, bool bReverse2 = false, double tolerance = MathUtil.ZeroTolerance)
        {
			// [RMS] this is much easier than for a loop, because it has to have 
			//   same endpoints. But don't have time right now.
			throw new NotImplementedException("todo!");
        }



        /// <summary>
        /// Exhaustively check that verts and edges of this EdgeSpan are consistent. Not for production use.
        /// </summary>
        public bool CheckValidity(FailMode eFailMode = FailMode.Throw)
        {
            bool is_ok = true;
            Action<bool> CheckOrFailF = (b) => { is_ok = is_ok && b; };
            if (eFailMode == FailMode.DebugAssert) {
                CheckOrFailF = (b) => { Debug.Assert(b); is_ok = is_ok && b; };
            } else if (eFailMode == FailMode.gDevAssert) {
                CheckOrFailF = (b) => { Util.gDevAssert(b); is_ok = is_ok && b; };
            } else if (eFailMode == FailMode.Throw) {
                CheckOrFailF = (b) => { if (b == false) throw new Exception("EdgeSpan.CheckValidity: check failed"); };
            }

            CheckOrFailF(Vertices.Length == Edges.Length + 1);
            for (int ei = 0; ei < Edges.Length; ++ei) {
                Index2i ev = Mesh.GetEdgeV(Edges[ei]);
                CheckOrFailF(Mesh.IsVertex(ev.a));
                CheckOrFailF(Mesh.IsVertex(ev.b));
                CheckOrFailF(Mesh.FindEdge(ev.a, ev.b) != DMesh3.InvalidID);
                CheckOrFailF(Vertices[ei] == ev.a || Vertices[ei] == ev.b);
                CheckOrFailF(Vertices[ei + 1] == ev.a || Vertices[ei + 1] == ev.b);
            }
            for (int vi = 0; vi < Vertices.Length-1; ++vi) {
                int a = Vertices[vi], b = Vertices[vi + 1];
                CheckOrFailF(Mesh.IsVertex(a));
                CheckOrFailF(Mesh.IsVertex(b));
                CheckOrFailF(Mesh.FindEdge(a, b) != DMesh3.InvalidID);
                if (vi < Vertices.Length - 2) {
                    int n = 0, edge_before_b = Edges[vi], edge_after_b = Edges[vi + 1];
                    foreach (int nbr_e in Mesh.VtxEdgesItr(b)) {
                        if (nbr_e == edge_before_b || nbr_e == edge_after_b)
                            n++;
                    }
                    CheckOrFailF(n == 2);
                }
            }
            return true;
        }





        /// <summary>
        /// Convert vertex span to list of edges. This should be somewhere else.
        /// </summary>
        public static int[] VerticesToEdges(DMesh3 mesh, int[] vertex_span)
        {
            int NV = vertex_span.Length;
            int[] edges = new int[NV-1];
            for ( int i = 0; i < NV-1; ++i ) {
                int v0 = vertex_span[i];
                int v1 = vertex_span[(i + 1)];
                edges[i] = mesh.FindEdge(v0, v1);
            }
            return edges;
        }



    }
}
