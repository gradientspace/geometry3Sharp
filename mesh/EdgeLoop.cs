using System;
using System.Collections.Generic;
using System.Diagnostics;


namespace g3
{
    /// <summary>
    /// Sequential set of vertices/edges in a mesh, that form a closed loop.
    /// 
    /// If all you have are the vertices, use EdgeLoop.VertexLoopToEdgeLoop() to construct an EdgeLoop
    /// </summary>
    public class EdgeLoop
    {
        public DMesh3 Mesh;

        public int[] Vertices;
        public int[] Edges;

        public int[] BowtieVertices;        // this may not be initialized!


        public EdgeLoop(DMesh3 mesh)
        {
            Mesh = mesh;
        }
        public EdgeLoop(DMesh3 mesh, int[] vertices, int[] edges, bool bCopyArrays)
        {
            Mesh = mesh;
            if ( bCopyArrays ) {
                Vertices = new int[vertices.Length];
                Array.Copy(vertices, Vertices, Vertices.Length);
                Edges = new int[edges.Length];
                Array.Copy(edges, Edges, Edges.Length);
            } else {
                Vertices = vertices;
                Edges = edges;
            }
        }

        public EdgeLoop(EdgeLoop copy)
        {
            Mesh = copy.Mesh;
            Vertices = new int[copy.Vertices.Length];
            Array.Copy(copy.Vertices, Vertices, Vertices.Length);
            Edges = new int[copy.Edges.Length];
            Array.Copy(copy.Edges, Edges, Edges.Length);
            if (copy.BowtieVertices != null) {
                BowtieVertices = new int[copy.BowtieVertices.Length];
                Array.Copy(copy.BowtieVertices, BowtieVertices, BowtieVertices.Length);
            }
        }


        /// <summary>
        /// construct EdgeLoop from a list of edges of mesh
        /// </summary>
        public static EdgeLoop FromEdges(DMesh3 mesh, IList<int> edges)
        {
            int[] Edges = new int[edges.Count];
            for (int i = 0; i < Edges.Length; ++i)
                Edges[i] = edges[i];

            int[] Vertices = new int[Edges.Length];
            Index2i start_ev = mesh.GetEdgeV(Edges[0]);
            Index2i prev_ev = start_ev;
            for (int i = 1; i < Edges.Length; ++i) {
                Index2i next_ev = mesh.GetEdgeV(Edges[i % Edges.Length]);
                Vertices[i] = IndexUtil.find_shared_edge_v(ref prev_ev, ref next_ev);
                prev_ev = next_ev;
            }
            Vertices[0] = IndexUtil.find_edge_other_v(ref start_ev, Vertices[1]);
            return new EdgeLoop(mesh, Vertices, Edges, false);
        }


        /// <summary>
        /// construct EdgeLoop from a list of vertices of mesh
        /// </summary>
        public static EdgeLoop FromVertices(DMesh3 mesh, IList<int> vertices)
        {
            int NV = vertices.Count;
            int[] Vertices = new int[NV];
            for (int i = 0; i < NV; ++i)
                Vertices[i] = vertices[i];
            int NE = NV;
            int[] Edges = new int[NE];
            for (int i = 0; i < NE; ++i) {
                Edges[i] = mesh.FindEdge(Vertices[i], Vertices[(i + 1)%NE]);
                if (Edges[i] == DMesh3.InvalidID)
                    throw new Exception("EdgeLoop.FromVertices: vertices are not connected by edge!");
            }
            return new EdgeLoop(mesh, Vertices, Edges, false);
        }



        /// <summary>
        /// construct EdgeLoop from a list of vertices of mesh
        /// if loop is a boundary edge, we can correct orientation if requested
        /// </summary>
        public static EdgeLoop FromVertices(DMesh3 mesh, IList<int> vertices, bool bAutoOrient = true)
        {
            int[] Vertices = new int[vertices.Count];
            for (int i = 0; i < Vertices.Length; i++) 
                Vertices[i] = vertices[i];

            if ( bAutoOrient ) {
                int a = Vertices[0], b = Vertices[1];
                int eid = mesh.FindEdge(a, b);
                if (mesh.IsBoundaryEdge(eid)) {
                    Index2i ev = mesh.GetOrientedBoundaryEdgeV(eid);
                    if (ev.a == b && ev.b == a)
                        Array.Reverse(Vertices);
                }
            }

            int[] Edges = new int[Vertices.Length];
            for (int i = 0; i < Edges.Length; ++i) {
                int a = Vertices[i], b = Vertices[(i + 1) % Vertices.Length];
                Edges[i] = mesh.FindEdge(a, b);
                if (Edges[i] == DMesh3.InvalidID)
                    throw new Exception("EdgeLoop.FromVertices: invalid edge [" + a + "," + b + "]");
            }

            return new EdgeLoop(mesh, Vertices, Edges, false);
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
            curve.Closed = true;
            return curve;
        }


        /// <summary>
        /// if this is a border edge-loop, we can check that it is oriented correctly, and
        /// if not, reverse it.
        /// Returns true if we reversed orientation.
        /// </summary>
        public bool CorrectOrientation()
        {
            int a = Vertices[0], b = Vertices[1];
            int eid = Mesh.FindEdge(a, b);
            if (Mesh.IsBoundaryEdge(eid)) {
                Index2i ev = Mesh.GetOrientedBoundaryEdgeV(eid);
                if (ev.a == b && ev.b == a) {
                    Reverse();
                    return true;
                }
            }
            return false;
        }


        public void Reverse()
        {
            Array.Reverse(Vertices);
            Array.Reverse(Edges);
        }


        /// <summary>
        /// check if all edges of this loop are internal edges (ie none on boundary)
        /// </summary>
        /// <returns></returns>
        public bool IsInternalLoop()
        {
            int NV = Vertices.Length;
            for (int i = 0; i < NV; ++i ) {
                int eid = Mesh.FindEdge(Vertices[i], Vertices[(i + 1) % NV]);
                Debug.Assert(eid != DMesh3.InvalidID);
                if (Mesh.IsBoundaryEdge(eid))
                    return false;
            }
            return true;
        }


        /// <summary>
        /// Check if all edges of this loop are boundary edges.
        /// If testMesh != null, will check that mesh instead of internal Mesh
        /// </summary>
        public bool IsBoundaryLoop(DMesh3 testMesh = null)
        {
            DMesh3 useMesh = (testMesh != null) ? testMesh : Mesh;

            int NV = Vertices.Length;
            for (int i = 0; i < NV; ++i ) {
                int eid = useMesh.FindEdge(Vertices[i], Vertices[(i + 1) % NV]);
                Debug.Assert(eid != DMesh3.InvalidID);
                if (useMesh.IsBoundaryEdge(eid) == false)
                    return false;
            }
            return true;
        }


        /// <summary>
        /// find index of vertex vID in Vertices list, or -1 if not found
        /// </summary>
        public int FindVertexIndex(int vID)
        {
            int N = Vertices.Length;
            for (int i = 0; i < N; ++i) {
                if (Vertices[i] == vID)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// find index of vertices of loop that is closest to point v
        /// </summary>
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


        // Check if Loop2 is the same set of positions on another mesh.
        // Does not require the indexing to be the same
        // Currently doesn't handle loop-reversal
        public bool IsSameLoop(EdgeLoop Loop2, bool bReverse2 = false, double tolerance = MathUtil.ZeroTolerance)
        {
            // find a duplicate starting vertex
            int N = Vertices.Length;
            int N2 = Loop2.Vertices.Length;
            if (N != N2)
                return false;

            DMesh3 Mesh2 = Loop2.Mesh;

            int start_i = 0, start_j = -1;

            // try to find a unique same-vertex on each loop. Do not
            // use vertices that have duplicate positions.
            bool bFoundGoodStart = false;
            while ( !bFoundGoodStart && start_i < N ) {
                Vector3d start_v = Mesh.GetVertex(start_i);
                int count = Loop2.CountWithinTolerance(start_v, tolerance, out start_j);
                if (count == 1)
                    bFoundGoodStart = true;
                else
                    start_i++;
            }
            if (!bFoundGoodStart)
                return false;       // no within-tolerance duplicate vtx to start at

            for ( int ii = 0; ii < N; ++ii ) {
                int i = (start_i + ii) % N;
                int j = (bReverse2) ? 
                    MathUtil.WrapSignedIndex(start_j - ii, N2)
                    : (start_j + ii) % N2;
                Vector3d v = Mesh.GetVertex(Vertices[i]);
                Vector3d v2 = Mesh2.GetVertex(Loop2.Vertices[j]);
                if (v.Distance(v2) > tolerance)
                    return false;
            }

            return true;
        }



        /// <summary>
        /// stores vertices [starti, starti+1, ... starti+count-1] in span, and returns span, or null if invalid range
        /// </summary>
        public int[] GetVertexSpan(int starti, int count, int[] span, bool reverse = false)
        {
            int N = Vertices.Length;
            if (starti < 0 || starti >= N || count > N - 1)
                return null;
            if (reverse) {
                for (int k = 0; k < count; ++k)
                    span[count-k-1] = Vertices[(starti + k) % N];
            } else {
                for (int k = 0; k < count; ++k)
                    span[k] = Vertices[(starti + k) % N];
            }
            return span;
        }




        /// <summary>
        /// Exhaustively check that verts and edges of this EdgeLoop are consistent. Not for production use.
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
                CheckOrFailF = (b) => { if (b == false) throw new Exception("EdgeLoop.CheckValidity: check failed"); };
            }

            CheckOrFailF(Vertices.Length == Edges.Length);
            for (int ei = 0; ei < Edges.Length; ++ei) {
                Index2i ev = Mesh.GetEdgeV(Edges[ei]);
                CheckOrFailF(Mesh.IsVertex(ev.a));
                CheckOrFailF(Mesh.IsVertex(ev.b));
                CheckOrFailF(Mesh.FindEdge(ev.a, ev.b) != DMesh3.InvalidID);
                CheckOrFailF(Vertices[ei] == ev.a || Vertices[ei] == ev.b);
                CheckOrFailF(Vertices[(ei + 1) % Edges.Length] == ev.a || Vertices[(ei + 1) % Edges.Length] == ev.b);
            }
            for ( int vi = 0; vi < Vertices.Length; ++vi ) {
                int a = Vertices[vi], b = Vertices[(vi + 1) % Vertices.Length];
                CheckOrFailF(Mesh.IsVertex(a));
                CheckOrFailF(Mesh.IsVertex(b));
                CheckOrFailF(Mesh.FindEdge(a,b) != DMesh3.InvalidID);
                int n = 0, edge_before_b = Edges[vi], edge_after_b = Edges[(vi + 1) % Vertices.Length];
                foreach ( int nbr_e in Mesh.VtxEdgesItr(b) ) {
                    if (nbr_e == edge_before_b || nbr_e == edge_after_b)
                        n++;
                }
                CheckOrFailF(n == 2);
            }
            return is_ok;
        }




        /// <summary>
        /// Convert a vertex loop to an edge loop. This should be somewhere else...
        /// </summary>
        public static int[] VertexLoopToEdgeLoop(DMesh3 mesh, int[] vertex_loop)
        {
            int NV = vertex_loop.Length;
            int[] edges = new int[NV];
            for ( int i = 0; i < NV; ++i ) {
                int v0 = vertex_loop[i];
                int v1 = vertex_loop[(i + 1) % NV];
                edges[i] = mesh.FindEdge(v0, v1);
            }
            return edges;
        }



    }
}
