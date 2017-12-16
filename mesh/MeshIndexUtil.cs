using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace g3
{
    /// <summary>
    /// Utility functions for manipulating sets/lists of mesh indices
    /// </summary>
    public static class MeshIndexUtil
    {

        /// <summary>
        /// given list of edges of MeshA, and vertex map from A to B, map to list of edges on B
        /// </summary>
        public static List<int> MapEdgesViaVertexMap(IIndexMap AtoBV, DMesh3 MeshA, DMesh3 MeshB, List<int> edges)
        {
            int N = edges.Count;
            List<int> result = new List<int>(N);
            for ( int i = 0; i < N; ++i ) {
                int eid_a = edges[i];
                Index2i aev = MeshA.GetEdgeV(eid_a);
                int bev0 = AtoBV[aev.a];
                int bev1 = AtoBV[aev.b];
                int eid_b = MeshB.FindEdge(bev0, bev1);
                Debug.Assert(eid_b != DMesh3.InvalidID);
                result.Add(eid_b);
            }
            return result;
        }



        /// <summary>
        /// given EdgeLoop on MeshA, and vertex map from A to B, map to EdgeLoop on B
        /// </summary>
        public static EdgeLoop MapLoopViaVertexMap(IIndexMap AtoBV, DMesh3 MeshA, DMesh3 MeshB, EdgeLoop loopIn)
        {
            int NV = loopIn.VertexCount, NE = loopIn.EdgeCount;
            int[] newVerts = new int[NV];
            for (int i = 0; i < NV; ++i)
                newVerts[i] = AtoBV[loopIn.Vertices[i]];
            
            int[] newEdges = new int[NE];
            for ( int i = 0; i <NE ; ++i ) {
                int eid_a = loopIn.Edges[i];
                Index2i aev = MeshA.GetEdgeV(eid_a);
                int bev0 = AtoBV[aev.a];
                int bev1 = AtoBV[aev.b];
                newEdges[i] = MeshB.FindEdge(bev0, bev1);
                Debug.Assert(newEdges[i] != DMesh3.InvalidID);
            }

            return new EdgeLoop(MeshB, newVerts, newEdges, false);
        }


    }
}
