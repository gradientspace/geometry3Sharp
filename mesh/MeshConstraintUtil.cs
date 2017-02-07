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


        // loop through submesh border edges on basemesh, map to submesh, and
        // pin those edges / vertices
        public static void FixSubmeshBoundaryEdges(MeshConstraints cons, DSubmesh3 sub)
        {
            Debug.Assert(sub.BaseBorderE != null);
            foreach ( int base_eid in sub.BaseBorderE ) {
                Index2i base_ev = sub.BaseMesh.GetEdgeV(base_eid);
                Index2i sub_ev = sub.MapVerticesToSubmesh(base_ev);
                int sub_eid = sub.SubMesh.FindEdge(sub_ev.a, sub_ev.b);
                Debug.Assert(sub_eid != DMesh3.InvalidID);
                Debug.Assert(sub.SubMesh.edge_is_boundary(sub_eid));
            
                cons.SetOrUpdateEdgeConstraint(sub_eid, EdgeConstraint.FullyConstrained);
                cons.SetOrUpdateVertexConstraint(sub_ev.a, VertexConstraint.Pinned);
                cons.SetOrUpdateVertexConstraint(sub_ev.b, VertexConstraint.Pinned);
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
