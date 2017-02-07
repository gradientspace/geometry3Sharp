using System;
using System.Collections.Generic;


namespace g3
{
    public enum ValidationStatus
    {
        Ok,

        NotAVertex,

        NotBoundaryVertex,
        NotBoundaryEdge,

        VerticesNotConnectedByEdge,
        IncorrectLoopOrientation
    }


    public static class MeshValidation
    {

        public static ValidationStatus IsEdgeLoop(DMesh3 mesh, EdgeLoop loop)
        {
           int N = loop.Vertices.Length;
            for ( int i = 0; i < N; ++i ) {
                if ( ! mesh.IsVertex(loop.Vertices[i]) )
                    return ValidationStatus.NotAVertex;
            }
            for (int i = 0; i < N; ++i) {
                int a = loop.Vertices[i];
                int b = loop.Vertices[(i + 1) % N];

                int eid = mesh.FindEdge(a, b);
                if (eid == DMesh3.InvalidID)
                    return ValidationStatus.VerticesNotConnectedByEdge;
            }
            return ValidationStatus.Ok;
        }



        public static ValidationStatus IsBoundaryLoop(DMesh3 mesh, EdgeLoop loop)
        {
            int N = loop.Vertices.Length;

            for ( int i = 0; i < N; ++i ) {
                if ( ! mesh.vertex_is_boundary(loop.Vertices[i]) )
                    return ValidationStatus.NotBoundaryVertex;
            }

            for ( int i = 0; i < N; ++i ) {
                int a = loop.Vertices[i];
                int b = loop.Vertices[(i + 1) % N];

                int eid = mesh.FindEdge(a, b);
                if (eid == DMesh3.InvalidID)
                    return ValidationStatus.VerticesNotConnectedByEdge;

                if (mesh.edge_is_boundary(eid) == false)
                    return ValidationStatus.NotBoundaryEdge;

                Index2i ev = mesh.GetOrientedBoundaryEdgeV(eid);
                if (!(ev.a == a && ev.b == b))
                    return ValidationStatus.IncorrectLoopOrientation;
            }

            return ValidationStatus.Ok;
        }


    }
}
