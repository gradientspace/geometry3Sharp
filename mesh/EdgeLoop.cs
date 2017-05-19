using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace g3
{
    public class EdgeLoop
    {
        public DMesh3 Mesh;
        public EdgeLoop(DMesh3 mesh)
        {
            Mesh = mesh;
        }

        public int[] Vertices;
        public int[] Edges;

        public int[] BowtieVertices;


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


        public bool IsInternalLoop()
        {
            int NV = Vertices.Length;
            for (int i = 0; i < NV; ++i ) {
                int eid = Mesh.FindEdge(Vertices[i], Vertices[(i + 1) % NV]);
                Debug.Assert(eid != DMesh3.InvalidID);
                if (Mesh.edge_is_boundary(eid))
                    return false;
            }
            return true;
        }


        public bool IsBoundaryLoop()
        {
            int NV = Vertices.Length;
            for (int i = 0; i < NV; ++i ) {
                int eid = Mesh.FindEdge(Vertices[i], Vertices[(i + 1) % NV]);
                Debug.Assert(eid != DMesh3.InvalidID);
                if (Mesh.edge_is_boundary(eid) == false)
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





        // utility function
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
