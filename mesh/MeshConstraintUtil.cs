using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    public static class MeshConstraintUtil
    {

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

    }
}
