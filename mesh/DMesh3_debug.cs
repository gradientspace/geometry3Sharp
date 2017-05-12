using System;
using System.Diagnostics;
using System.Collections.Generic;

namespace g3
{
    public partial class DMesh3
    {

        [Conditional("DEBUG")]
        public void debug_check_is_vertex(int v) {
            if (!IsVertex(v))
                throw new Exception("DMesh3.debug_is_vertex - not a vertex!");
        }


		public void debug_print_vertex(int v) {
			System.Console.WriteLine("Vertex " + v.ToString());
			List<int> tris = new List<int>();
			GetVtxTriangles(v, tris, false);
			System.Console.WriteLine(string.Format("  Tris {0}  Edges {1}  refcount {2}", tris.Count, GetVtxEdges(v).Count, vertices_refcount.refCount(v) ));
			foreach ( int t in tris ) {
				Index3i tv = GetTriangle(t), te = GetTriEdges(t);
				System.Console.WriteLine(string.Format("  t{6} {0} {1} {2}   te {3} {4} {5}", tv[0],tv[1],tv[2], te[0],te[1],te[2],t));
			}
			foreach ( int e in GetVtxEdges(v) ) {
				Index2i ev = GetEdgeV(e), et = GetEdgeT(e);
				System.Console.WriteLine(string.Format("  e{4} {0} {1} / {2} {3}", ev[0],ev[1], et[0],et[1], e));
			}
		}


		public void debug_print_mesh() {
			for ( int k = 0; k < vertices_refcount.max_index; ++k ) {
				if ( vertices_refcount.isValid(k) == false )
					System.Console.WriteLine(string.Format("v{0} : invalid",k));
				else 
					debug_print_vertex(k);
			}
		}


        // debug

        public void DMESH_CHECK_OR_FAIL(bool b) { Util.gDevAssert(b); }

        // This function checks that the mesh is well-formed, ie all internal data
        // structures are consistent
        public bool CheckValidity(bool bAllowNonManifoldVertices = false) {

			int[] triToVtxRefs = new int[this.MaxVertexID];

			if ( normals != null )
				DMESH_CHECK_OR_FAIL(normals.size == vertices.size);
			if ( colors != null )
				DMESH_CHECK_OR_FAIL(colors.size == vertices.size);
			if ( uv != null )
				DMESH_CHECK_OR_FAIL(uv.size/2 == vertices.size/3);
			if ( triangle_groups != null )
				DMESH_CHECK_OR_FAIL(triangle_groups.size == triangles.size/3);

            foreach (int tID in TriangleIndices() ) { 

                DMESH_CHECK_OR_FAIL(IsTriangle(tID));
                DMESH_CHECK_OR_FAIL(triangles_refcount.refCount(tID) == 1);

                // vertices must exist
                Index3i tv = GetTriangle(tID);
                for (int j = 0; j < 3; ++j) {
                    DMESH_CHECK_OR_FAIL(IsVertex(tv[j]));
                    triToVtxRefs[tv[j]] += 1;
                }

                // edges must exist and reference this tri
                Index3i e = new Index3i();
                for (int j = 0; j < 3; ++j) {
                    int a = tv[j], b = tv[(j + 1) % 3];
                    e[j] = FindEdge(a, b);
                    DMESH_CHECK_OR_FAIL(e[j] != InvalidID);
                    DMESH_CHECK_OR_FAIL(edge_has_t(e[j], tID));
                    DMESH_CHECK_OR_FAIL(e[j] == FindEdgeFromTri(a, b, tID));
                }
                DMESH_CHECK_OR_FAIL(e[0] != e[1] && e[0] != e[2] && e[1] != e[2]);

                // tri nbrs must exist and reference this tri, or same edge must be boundary edge
                Index3i te = GetTriEdges(tID);
                for (int j = 0; j < 3; ++j) {
                    int eid = te[j];
                    DMESH_CHECK_OR_FAIL(IsEdge(eid));
                    int tOther = edge_other_t(eid, tID);
                    if (tOther == InvalidID) {
                        DMESH_CHECK_OR_FAIL(tri_is_boundary(tID));
                        continue;
                    }

                    DMESH_CHECK_OR_FAIL( tri_has_neighbour_t(tOther, tID) == true);

                    // edge must have same two verts as tri for same index
                    int a = tv[j], b = tv[(j + 1) % 3];
                    Index2i ev = GetEdgeV(te[j]);
                    DMESH_CHECK_OR_FAIL(IndexUtil.same_pair_unordered(a, b, ev[0], ev[1]));

                    // also check that nbr edge has opposite orientation
                    Index3i othertv = GetTriangle(tOther);
                    int found = IndexUtil.find_tri_ordered_edge(b, a, othertv.array);
                    DMESH_CHECK_OR_FAIL(found != InvalidID);
                }
            }


            // edge verts/tris must exist
            foreach (int eID in EdgeIndices() ) { 
                DMESH_CHECK_OR_FAIL(IsEdge(eID));
                DMESH_CHECK_OR_FAIL(edges_refcount.refCount(eID) == 1);
                Index2i ev = GetEdgeV(eID);
                Index2i et = GetEdgeT(eID);
                DMESH_CHECK_OR_FAIL(IsVertex(ev[0]));
                DMESH_CHECK_OR_FAIL(IsVertex(ev[1]));
                DMESH_CHECK_OR_FAIL(et[0] != InvalidID);
                DMESH_CHECK_OR_FAIL(ev[0] < ev[1]);
                DMESH_CHECK_OR_FAIL(IsTriangle(et[0]));
                if (et[1] != InvalidID) {
                    DMESH_CHECK_OR_FAIL(IsTriangle(et[1]));
                }
            }

            // verify compact check
            bool is_compact = vertices_refcount.is_dense;
            if (is_compact) {
                for (int vid = 0; vid < vertices.Length / 3; ++vid) {
                    DMESH_CHECK_OR_FAIL(vertices_refcount.isValid(vid));
                }
            }

            // vertex edges must exist and reference this vert
            foreach( int vID in VertexIndices()) { 
                DMESH_CHECK_OR_FAIL(IsVertex(vID));

                Vector3d v = GetVertex(vID);
                DMESH_CHECK_OR_FAIL(double.IsNaN(v.LengthSquared) == false);
                DMESH_CHECK_OR_FAIL(double.IsInfinity(v.LengthSquared) == false);

                List<int> l = vertex_edges[vID];
                foreach(int edgeid in l) { 
                    DMESH_CHECK_OR_FAIL(IsEdge(edgeid));
                    DMESH_CHECK_OR_FAIL(edge_has_v(edgeid, vID));

                    int otherV = edge_other_v(edgeid, vID);
                    int e2 = find_edge(vID, otherV);
                    DMESH_CHECK_OR_FAIL(e2 != InvalidID);
                    DMESH_CHECK_OR_FAIL(e2 == edgeid);
                    e2 = find_edge(otherV, vID);
                    DMESH_CHECK_OR_FAIL(e2 != InvalidID);
                    DMESH_CHECK_OR_FAIL(e2 == edgeid);
                }

				List<int> vTris = new List<int>(), vTris2 = new List<int>();
                GetVtxTriangles(vID, vTris, false);
				GetVtxTriangles(vID, vTris2, true);
				DMESH_CHECK_OR_FAIL(vTris.Count == vTris2.Count);
				//System.Console.WriteLine(string.Format("{0} {1} {2}", vID, vTris.Count, GetVtxEdges(vID).Count));
                if ( bAllowNonManifoldVertices )
    				DMESH_CHECK_OR_FAIL(vTris.Count <= GetVtxEdges(vID).Count);
                else
    				DMESH_CHECK_OR_FAIL(vTris.Count == GetVtxEdges(vID).Count || vTris.Count == GetVtxEdges(vID).Count-1);
                DMESH_CHECK_OR_FAIL(vertices_refcount.refCount(vID) == vTris.Count + 1);
                DMESH_CHECK_OR_FAIL(triToVtxRefs[vID] == vTris.Count);
                foreach( int tID in vTris) {
                    DMESH_CHECK_OR_FAIL(tri_has_v(tID, vID));
                }

				// check that edges around vert only references tris above, and reference all of them!
				List<int> vRemoveTris = new List<int>(vTris);
				foreach ( int edgeid in l ) {
					Index2i edget = GetEdgeT(edgeid);
					DMESH_CHECK_OR_FAIL( vTris.Contains(edget[0]) );
					if ( edget[1] != InvalidID )
						DMESH_CHECK_OR_FAIL( vTris.Contains(edget[1]) );
					vRemoveTris.Remove(edget[0]);
					if ( edget[1] != InvalidID )
						vRemoveTris.Remove(edget[1]);
				}
				DMESH_CHECK_OR_FAIL(vRemoveTris.Count == 0);
            }
            return true;
        }
        void check_or_fail(bool bCondition) {
            Util.gDevAssert(bCondition);
        }
    }
}
