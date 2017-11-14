using System;
using System.Diagnostics;
using System.Text;
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

        [Conditional("DEBUG")]
        public void debug_check_is_triangle(int t)
        {
            if (!IsTriangle(t))
                throw new Exception("DMesh3.debug_is_triangle - not a triangle!");
        }

        [Conditional("DEBUG")]
        public void debug_check_is_edge(int e)
        {
            if (!IsEdge(e))
                throw new Exception("DMesh3.debug_is_edge - not an edge!");
        }

        public void debug_print_vertex(int v) {
			System.Console.WriteLine("Vertex " + v.ToString());
			List<int> tris = new List<int>();
			GetVtxTriangles(v, tris, false);
			System.Console.WriteLine(string.Format("  Tris {0}  Edges {1}  refcount {2}", tris.Count, GetVtxEdgeCount(v), vertices_refcount.refCount(v) ));
			foreach ( int t in tris ) {
				Index3i tv = GetTriangle(t), te = GetTriEdges(t);
				System.Console.WriteLine(string.Format("  t{6} {0} {1} {2}   te {3} {4} {5}", tv[0],tv[1],tv[2], te[0],te[1],te[2],t));
			}
			foreach ( int e in VtxEdgesItr(v) ) {
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

        public string MeshInfoString()
        {
            StringBuilder b = new StringBuilder();
            b.AppendFormat("Vertices  count {0} max {1} {2}", VertexCount, MaxVertexID, vertices_refcount.UsageStats);  b.AppendLine();
            b.AppendFormat("Triangles count {0} max {1} {2}", TriangleCount, MaxTriangleID, triangles_refcount.UsageStats); b.AppendLine();
            b.AppendFormat("Edges     count {0} max {1} {2}", EdgeCount, MaxEdgeID, edges_refcount.UsageStats); b.AppendLine();
            b.AppendFormat("Normals {0}  Colors {1}  UVs {2}  Groups {3}", HasVertexNormals, HasVertexColors, HasVertexUVs, HasTriangleGroups); b.AppendLine();
            b.AppendFormat("Closed {0} Compact {1} timestamp {2} shape_timestamp {3}  MaxGroupID {4}", CachedIsClosed, IsCompact, timestamp, shape_timestamp, max_group_id); b.AppendLine();
            b.AppendFormat("VertexEdges " + vertex_edges.MemoryUsage); b.AppendLine();
            return b.ToString();
        }



        /// <summary>
        /// Check if this m2 is the same as this mesh. By default only checks
        /// vertices and triangles, turn on other parameters w/ flags
        /// </summary>
        public bool IsSameMesh(DMesh3 m2, bool bCheckConnectivity, bool bCheckEdgeIDs = false, 
            bool bCheckNormals = false, bool bCheckColors = false, bool bCheckUVs = false,
            bool bCheckGroups = false,
            float Epsilon = MathUtil.Epsilonf )
        {
            if (VertexCount != m2.VertexCount)
                return false;
            if (TriangleCount != m2.TriangleCount)
                return false;
            foreach ( int vid in VertexIndices() ) {
                if (m2.IsVertex(vid) == false || GetVertex(vid).EpsilonEqual(m2.GetVertex(vid), Epsilon) == false)
                    return false;
            }
            foreach(int tid in TriangleIndices()) {
                if (m2.IsTriangle(tid) == false || GetTriangle(tid).Equals(m2.GetTriangle(tid)) == false)
                    return false;
            }
            if (bCheckConnectivity) {
                foreach (int eid in EdgeIndices()) {
                    Index4i e = GetEdge(eid);
                    int other_eid = m2.FindEdge(e.a, e.b);
                    if (other_eid == InvalidID)
                        return false;
                    Index4i oe = m2.GetEdge(other_eid);
                    if (Math.Min(e.c, e.d) != Math.Min(oe.c, oe.d) || Math.Max(e.c, e.d) != Math.Max(oe.c, oe.d))
                        return false;
                }
            }
            if (bCheckEdgeIDs) {
                if (EdgeCount != m2.EdgeCount)
                    return false;
                foreach (int eid in EdgeIndices()) {
                    if (m2.IsEdge(eid) == false || GetEdge(eid).Equals(m2.GetEdge(eid)) == false)
                        return false;
                }
            }
            if (bCheckNormals) {
                if (HasVertexNormals != m2.HasVertexNormals)
                    return false;
                if (HasVertexNormals) {
                    foreach ( int vid in VertexIndices() ) {
                        if (GetVertexNormal(vid).EpsilonEqual(m2.GetVertexNormal(vid), Epsilon) == false)
                            return false;
                    }
                }
            }
            if (bCheckColors) {
                if (HasVertexColors != m2.HasVertexColors)
                    return false;
                if (HasVertexColors) {
                    foreach ( int vid in VertexIndices() ) {
                        if (GetVertexColor(vid).EpsilonEqual(m2.GetVertexColor(vid), Epsilon) == false)
                            return false;
                    }
                }
            }
            if (bCheckUVs) {
                if (HasVertexUVs != m2.HasVertexUVs)
                    return false;
                if (HasVertexUVs) {
                    foreach ( int vid in VertexIndices() ) {
                        if (GetVertexUV(vid).EpsilonEqual(m2.GetVertexUV(vid), Epsilon) == false)
                            return false;
                    }
                }
            }
            if (bCheckGroups) {
                if (HasTriangleGroups != m2.HasTriangleGroups)
                    return false;
                if (HasTriangleGroups) {
                    foreach ( int tid in TriangleIndices() ) {
                        if (GetTriangleGroup(tid) != m2.GetTriangleGroup(tid))
                            return false;
                    }
                }
            }
            return true;
        }


        /// <summary>
        // This function checks that the mesh is well-formed, ie all internal data
        // structures are consistent
        /// </summary>
        public bool CheckValidity(bool bAllowNonManifoldVertices = false, FailMode eFailMode = FailMode.Throw ) {

			int[] triToVtxRefs = new int[this.MaxVertexID];

            bool is_ok = true;
            Action<bool> CheckOrFailF = (b) => { is_ok = is_ok && b; };
            if ( eFailMode == FailMode.DebugAssert ) {
                CheckOrFailF = (b) => { Debug.Assert(b); is_ok = is_ok && b; };
            } else if ( eFailMode == FailMode.gDevAssert ) {
                CheckOrFailF = (b) => { Util.gDevAssert(b); is_ok = is_ok && b; };
            } else if ( eFailMode == FailMode.Throw ) {
                CheckOrFailF = (b) => { if (b == false) throw new Exception("DMesh3.CheckValidity: check failed"); };
            }

			if ( normals != null )
				CheckOrFailF(normals.size == vertices.size);
			if ( colors != null )
				CheckOrFailF(colors.size == vertices.size);
			if ( uv != null )
				CheckOrFailF(uv.size/2 == vertices.size/3);
			if ( triangle_groups != null )
				CheckOrFailF(triangle_groups.size == triangles.size/3);

            foreach (int tID in TriangleIndices() ) { 

                CheckOrFailF(IsTriangle(tID));
                CheckOrFailF(triangles_refcount.refCount(tID) == 1);

                // vertices must exist
                Index3i tv = GetTriangle(tID);
                for (int j = 0; j < 3; ++j) {
                    CheckOrFailF(IsVertex(tv[j]));
                    triToVtxRefs[tv[j]] += 1;
                }

                // edges must exist and reference this tri
                Index3i e = new Index3i();
                for (int j = 0; j < 3; ++j) {
                    int a = tv[j], b = tv[(j + 1) % 3];
                    e[j] = FindEdge(a, b);
                    CheckOrFailF(e[j] != InvalidID);
                    CheckOrFailF(edge_has_t(e[j], tID));
                    CheckOrFailF(e[j] == FindEdgeFromTri(a, b, tID));
                }
                CheckOrFailF(e[0] != e[1] && e[0] != e[2] && e[1] != e[2]);

                // tri nbrs must exist and reference this tri, or same edge must be boundary edge
                Index3i te = GetTriEdges(tID);
                for (int j = 0; j < 3; ++j) {
                    int eid = te[j];
                    CheckOrFailF(IsEdge(eid));
                    int tOther = edge_other_t(eid, tID);
                    if (tOther == InvalidID) {
                        CheckOrFailF(tri_is_boundary(tID));
                        continue;
                    }

                    CheckOrFailF( tri_has_neighbour_t(tOther, tID) == true);

                    // edge must have same two verts as tri for same index
                    int a = tv[j], b = tv[(j + 1) % 3];
                    Index2i ev = GetEdgeV(te[j]);
                    CheckOrFailF(IndexUtil.same_pair_unordered(a, b, ev[0], ev[1]));

                    // also check that nbr edge has opposite orientation
                    Index3i othertv = GetTriangle(tOther);
                    int found = IndexUtil.find_tri_ordered_edge(b, a, othertv.array);
                    CheckOrFailF(found != InvalidID);
                }
            }


            // edge verts/tris must exist
            foreach (int eID in EdgeIndices() ) { 
                CheckOrFailF(IsEdge(eID));
                CheckOrFailF(edges_refcount.refCount(eID) == 1);
                Index2i ev = GetEdgeV(eID);
                Index2i et = GetEdgeT(eID);
                CheckOrFailF(IsVertex(ev[0]));
                CheckOrFailF(IsVertex(ev[1]));
                CheckOrFailF(et[0] != InvalidID);
                CheckOrFailF(ev[0] < ev[1]);
                CheckOrFailF(IsTriangle(et[0]));
                if (et[1] != InvalidID) {
                    CheckOrFailF(IsTriangle(et[1]));
                }
            }

            // verify compact check
            bool is_compact = vertices_refcount.is_dense;
            if (is_compact) {
                for (int vid = 0; vid < vertices.Length / 3; ++vid) {
                    CheckOrFailF(vertices_refcount.isValid(vid));
                }
            }

            // vertex edges must exist and reference this vert
            foreach( int vID in VertexIndices()) { 
                CheckOrFailF(IsVertex(vID));

                Vector3d v = GetVertex(vID);
                CheckOrFailF(double.IsNaN(v.LengthSquared) == false);
                CheckOrFailF(double.IsInfinity(v.LengthSquared) == false);

                foreach(int edgeid in vertex_edges.ValueItr(vID)) { 
                    CheckOrFailF(IsEdge(edgeid));
                    CheckOrFailF(edge_has_v(edgeid, vID));

                    int otherV = edge_other_v(edgeid, vID);
                    int e2 = find_edge(vID, otherV);
                    CheckOrFailF(e2 != InvalidID);
                    CheckOrFailF(e2 == edgeid);
                    e2 = find_edge(otherV, vID);
                    CheckOrFailF(e2 != InvalidID);
                    CheckOrFailF(e2 == edgeid);
                }

                foreach ( int nbr_vid in VtxVerticesItr(vID) ) {
                    CheckOrFailF(IsVertex(nbr_vid));
                    int edge = find_edge(vID, nbr_vid);
                    CheckOrFailF(IsEdge(edge));
                }

				List<int> vTris = new List<int>(), vTris2 = new List<int>();
                GetVtxTriangles(vID, vTris, false);
				GetVtxTriangles(vID, vTris2, true);
				CheckOrFailF(vTris.Count == vTris2.Count);
				//System.Console.WriteLine(string.Format("{0} {1} {2}", vID, vTris.Count, GetVtxEdges(vID).Count));
                if ( bAllowNonManifoldVertices )
    				CheckOrFailF(vTris.Count <= GetVtxEdgeCount(vID));
                else
    				CheckOrFailF(vTris.Count == GetVtxEdgeCount(vID) || vTris.Count == GetVtxEdgeCount(vID) - 1);
                CheckOrFailF(vertices_refcount.refCount(vID) == vTris.Count + 1);
                CheckOrFailF(triToVtxRefs[vID] == vTris.Count);
                foreach( int tID in vTris) {
                    CheckOrFailF(tri_has_v(tID, vID));
                }

				// check that edges around vert only references tris above, and reference all of them!
				List<int> vRemoveTris = new List<int>(vTris);
				foreach ( int edgeid in vertex_edges.ValueItr(vID)) {
					Index2i edget = GetEdgeT(edgeid);
					CheckOrFailF( vTris.Contains(edget[0]) );
					if ( edget[1] != InvalidID )
						CheckOrFailF( vTris.Contains(edget[1]) );
					vRemoveTris.Remove(edget[0]);
					if ( edget[1] != InvalidID )
						vRemoveTris.Remove(edget[1]);
				}
				CheckOrFailF(vRemoveTris.Count == 0);
            }

            return is_ok;
        }

    }
}
