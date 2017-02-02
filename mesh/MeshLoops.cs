using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace g3
{

    public class EdgeLoop
    {
        public int[] Vertices;
        public int[] Edges;
    }


    public class MeshBoundaryLoops
    {
        public DMesh3 Mesh;
        public List<EdgeLoop> Loops;

        public MeshBoundaryLoops(DMesh3 mesh)
        {
            this.Mesh = mesh;
            Compute();
        }


        // This algorithm assumes that triangles are oriented consistently, 
        // so boundary-loop can be followed
        public bool Compute()
        {
            Loops = new List<EdgeLoop>();

            int NE = Mesh.MaxEdgeID;
            byte[] used_edge = new byte[NE];
            Array.Clear(used_edge, 0, used_edge.Length);

            List<int> loop_edges = new List<int>();     // [RMS] not sure we need this...
            List<int> loop_verts = new List<int>();

            for ( int eid = 0; eid < NE; ++eid ) {
                if (used_edge[eid] > 0)
                    continue;
                if (Mesh.edge_is_boundary(eid) == false)
                    continue;

                // ok this is start of a boundary chain
                int eStart = eid;
                used_edge[eStart] = 1;
                loop_edges.Add(eStart);

                int eCur = eid;

                // follow the chain in order of oriented edges
                bool bClosed = false;
                while (!bClosed) {
                    Index2i ev = Mesh.GetOrientedBoundaryEdgeV(eCur);
                    int v0 = ev.a, v1 = ev.b;
                    loop_verts.Add(v0);

                    int e0 = -1, e1 = 1;
                    int bdry_nbrs = Mesh.VtxBoundaryEdges(v1, ref e0, ref e1);

                    if (bdry_nbrs < 2)
                        throw new Exception("MeshBoundaryLoops.Compute: found broken neighbourhood at vertex " + v1);
                    if (bdry_nbrs > 2)
                        throw new NotImplementedException("MeshBoundaryLoops.Compute: not handling bowtie vertices yet!");

                    Debug.Assert(e0 == eCur || e1 == eCur);
                    int eNext = (e0 == eCur) ? e1 : e0;

                    if (eNext == eStart) {
                        bClosed = true;      // done loop
                    } else {
                        Debug.Assert(used_edge[eNext] == 0);
                        loop_edges.Add(eNext);
                        eCur = eNext;
                        used_edge[eCur] = 1;
                    }
                }


                // convert loop
                EdgeLoop loop = new EdgeLoop();
                loop.Vertices = loop_verts.ToArray();
                loop.Edges = loop_edges.ToArray();
                Loops.Add(loop);

                // reset
                loop_edges.Clear();
                loop_verts.Clear();
            }

            return true;
        }
    }




}
