using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    public static class MeshIterators
    {

        public static IEnumerable<int> FilteredVertices(DMesh3 mesh, Func<DMesh3, int, bool> FilterF )
        {
            int N = mesh.MaxVertexID;
            for ( int i = 0; i < N; ++i ) {
                if ( mesh.IsVertex(i) ) {
                    if (FilterF(mesh, i))
                        yield return i;
                }
            }
        }


        public static IEnumerable<int> FilteredEdges(DMesh3 mesh, Func<DMesh3, int, bool> FilterF )
        {
            int N = mesh.MaxEdgeID;
            for ( int i = 0; i < N; ++i ) {
                if ( mesh.IsEdge(i) ) {
                    if (FilterF(mesh, i))
                        yield return i;
                }
            }
        }


        public static IEnumerable<int> FilteredTriangles(DMesh3 mesh, Func<DMesh3, int, bool> FilterF )
        {
            int N = mesh.MaxTriangleID;
            for ( int i = 0; i < N; ++i ) {
                if ( mesh.IsTriangle(i) ) {
                    if (FilterF(mesh, i))
                        yield return i;
                }
            }
        }


        /// <summary>
        /// Boundary vertices of mesh
        /// </summary>
        public static IEnumerable<int> BoundaryVertices(DMesh3 mesh)
        {
            int N = mesh.MaxVertexID;
            for ( int i = 0; i < N; ++i ) {
                if ( mesh.IsVertex(i) ) {
                    if (mesh.IsBoundaryVertex(i))
                        yield return i;
                }
            }
        }


        /// <summary>
        /// boundary vertices of mesh, but based on edges, so returns each vertex twice!
        /// </summary>
        public static IEnumerable<int> BoundaryEdgeVertices(DMesh3 mesh)
        {
            int N = mesh.MaxEdgeID;
            for (int i = 0; i < N; ++i) {
                if (mesh.IsEdge(i) && mesh.IsBoundaryEdge(i)) {
                    Index2i ev = mesh.GetEdgeV(i);
                    yield return ev.a;
                    yield return ev.b;
                }
            }
        }


        public static IEnumerable<int> InteriorVertices(DMesh3 mesh)
        {
            int N = mesh.MaxVertexID;
            for ( int i = 0; i < N; ++i ) {
                if ( mesh.IsVertex(i) ) {
                    if (mesh.IsBoundaryVertex(i) == false)
                        yield return i;
                }
            }
        }



        public static IEnumerable<int> GroupBoundaryVertices(DMesh3 mesh)
        {
            int N = mesh.MaxVertexID;
            for ( int i = 0; i < N; ++i ) {
                if ( mesh.IsVertex(i) ) {
                    if (mesh.IsGroupBoundaryVertex(i))
                        yield return i;
                }
            }
        }


        public static IEnumerable<int> GroupJunctionVertices(DMesh3 mesh)
        {
            int N = mesh.MaxVertexID;
            for ( int i = 0; i < N; ++i ) {
                if ( mesh.IsVertex(i) ) {
                    if (mesh.IsGroupJunctionVertex(i))
                        yield return i;
                }
            }
        }


        public static IEnumerable<int> BoundaryEdges(DMesh3 mesh)
        {
            int N = mesh.MaxEdgeID;
            for ( int i = 0; i < N; ++i ) {
                if ( mesh.IsEdge(i) ) {
                    if (mesh.IsBoundaryEdge(i))
                        yield return i;
                }
            }
        }


		public static IEnumerable<int> InteriorEdges(DMesh3 mesh)
		{
			int N = mesh.MaxEdgeID;
			for (int i = 0; i < N; ++i) {
				if (mesh.IsEdge(i)) {
					if (mesh.IsBoundaryEdge(i) == false)
						yield return i;
				}
			}
		}


        public static IEnumerable<int> GroupBoundaryEdges(DMesh3 mesh)
        {
            int N = mesh.MaxEdgeID;
            for ( int i = 0; i < N; ++i ) {
                if ( mesh.IsEdge(i) ) {
                    if (mesh.IsGroupBoundaryEdge(i))
                        yield return i;
                }
            }
        }




        public static IEnumerable<int> BowtieVertices(DMesh3 mesh)
        {
            int N = mesh.MaxVertexID;
            for ( int i = 0; i < N; ++i ) {
                if ( mesh.IsVertex(i) ) {
                    if (mesh.IsBowtieVertex(i))
                        yield return i;
                }
            }
        }



    }
}
