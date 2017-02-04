using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace g3
{
    public static class MeshConstraintUtil
    {
        // for all mesh boundary edges, disable flip/split/collapse
        // for all mesh boundary vertices, pin in current position
        public static void FixAllBoundaryEdges(MeshConstraints cons, DMesh3 mesh)
        {
            int NE = mesh.MaxEdgeID;
            for ( int ei = 0; ei < NE; ++ei ) {
                if ( mesh.IsEdge(ei) && mesh.edge_is_boundary(ei) ) {
                    cons.SetOrUpdateEdgeConstraint(ei, EdgeConstraint.FullyConstrained);

                    Index2i ev = mesh.GetEdgeV(ei);
                    cons.SetOrUpdateVertexConstraint(ev.a, VertexConstraint.Pinned);
                    cons.SetOrUpdateVertexConstraint(ev.b, VertexConstraint.Pinned);
                }
            }
        }



        // for all vertices in loopV, constrain to target
        // for all edges in loopV, disable flips and constrain to target
        public static void ConstrainVtxLoopTo(MeshConstraints cons, DMesh3 mesh, int[] loopV, IProjectionTarget target)
        {
            VertexConstraint vc = new VertexConstraint(target);
            for (int i = 0; i < loopV.Length; ++i)
                cons.SetOrUpdateVertexConstraint(loopV[i], vc);

            EdgeConstraint ec = new EdgeConstraint(EdgeRefineFlags.NoFlip, target);
            for ( int i = 0; i < loopV.Length; ++i ) {
                int v0 = loopV[i];
                int v1 = loopV[(i + 1) % loopV.Length];

                int eid = mesh.FindEdge(v0, v1);
                Debug.Assert(eid != DMesh3.InvalidID);
                if ( eid != DMesh3.InvalidID )
                    cons.SetOrUpdateEdgeConstraint(eid, ec);
            }

        }

    }
}
