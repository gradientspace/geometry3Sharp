using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace g3
{
    public partial class DMesh3
    {


        // edits

        public MeshResult ReverseTriOrientation(int tID) {
            if (!IsTriangle(tID))
                return MeshResult.Failed_NotATriangle;
            internal_reverse_tri_orientation(tID);
            updateTimeStamp(true);
            return MeshResult.Ok;
        }
        void internal_reverse_tri_orientation(int tID) {
            Index3i t = GetTriangle(tID);
            set_triangle(tID, t[1], t[0], t[2]);
            Index3i te = GetTriEdges(tID);
            set_triangle_edges(tID, te[0], te[2], te[1]);
        }

		public void ReverseOrientation(bool bFlipNormals = true) {
			foreach ( int tid in TriangleIndices() ) {
				internal_reverse_tri_orientation(tid);
			}
			if ( bFlipNormals && HasVertexNormals ) {
				foreach ( int vid in VertexIndices() ) {
					int i = 3*vid;
					normals[i] = -normals[i];
					normals[i+1] = -normals[i+1];
					normals[i+2] = -normals[i+2];
				}
			}
            updateTimeStamp(true);
		}




        /// <summary>
        /// Remove vertex vID, and all connected triangles if bRemoveAllTriangles = true
        /// (if false, them throws exception if there are still any triangles!)
        /// if bPreserveManifold, checks that we will not create a bowtie vertex first
        /// </summary>
        public MeshResult RemoveVertex(int vID, bool bRemoveAllTriangles = true, bool bPreserveManifold = false)
        {
            if (vertices_refcount.isValid(vID) == false)
                return MeshResult.Failed_NotAVertex;

            if ( bRemoveAllTriangles ) {

                // if any one-ring vtx is a boundary vtx and one of its outer-ring edges is an
                // interior edge then we will create a bowtie if we remove that triangle
                if ( bPreserveManifold ) {
                    foreach ( int tid in VtxTrianglesItr(vID) ) {
                        Index3i tri = GetTriangle(tid);
                        int j = IndexUtil.find_tri_index(vID, ref tri);
                        int oa = tri[(j + 1) % 3], ob = tri[(j + 2) % 3];
                        int eid = find_edge(oa,ob);
                        if (IsBoundaryEdge(eid))
                            continue;
                        if (IsBoundaryVertex(oa) || IsBoundaryVertex(ob))
                            return MeshResult.Failed_WouldCreateBowtie;
                    }
                }

                List<int> tris = new List<int>();
                GetVtxTriangles(vID, tris, true);
                foreach (int tID in tris) {
                    MeshResult result = RemoveTriangle(tID, false, bPreserveManifold);
                    if (result != MeshResult.Ok)
                        return result;
                }
            }

            if ( vertices_refcount.refCount(vID) != 1)
                throw new NotImplementedException("DMesh3.RemoveVertex: vertex is still referenced");

            vertices_refcount.decrement(vID);
            Debug.Assert(vertices_refcount.isValid(vID) == false);
            vertex_edges.Clear(vID);

            updateTimeStamp(true);
            return MeshResult.Ok;
        }



        /// <summary>
        /// Remove a tID from the mesh. Also removes any unreferenced edges after tri is removed.
        /// If bRemoveIsolatedVertices is false, then if you remove all tris from a vert, that vert is also removed.
        /// If bPreserveManifold, we check that you will not create a bowtie vertex (and return false).
        ///   If this check is not done, you have to make sure you don't create a bowtie, because other
        ///   code assumes we don't have bowties, and will not handle it properly
        /// </summary>
        public MeshResult RemoveTriangle(int tID, bool bRemoveIsolatedVertices = true, bool bPreserveManifold = false)
        {
            if ( ! triangles_refcount.isValid(tID) ) {
                Debug.Assert(false);
                return MeshResult.Failed_NotATriangle;
            }

            Index3i tv = GetTriangle(tID);
            Index3i te = GetTriEdges(tID);

            // if any tri vtx is a boundary vtx connected to two interior edges, then
            // we cannot remove this triangle because it would create a bowtie vertex!
            // (that vtx already has 2 boundary edges, and we would add two more)
            if (bPreserveManifold) {
                for (int j = 0; j < 3; ++j) {
                    if (IsBoundaryVertex(tv[j])) {
                        if (IsBoundaryEdge(te[j]) == false && IsBoundaryEdge(te[(j + 2) % 3]) == false)
                            return MeshResult.Failed_WouldCreateBowtie;
                    }
                }
            }

            // Remove triangle from its edges. if edge has no triangles left,
            // then it is removed.
            for (int j = 0; j < 3; ++j) {
                int eid = te[j];
                replace_edge_triangle(eid, tID, InvalidID);
                if (edges[4 * eid + 2] == InvalidID) {
                    int a = edges[4 * eid];
                    vertex_edges.Remove(a, eid);

                    int b = edges[4 * eid + 1];
                    vertex_edges.Remove(b, eid);

                    edges_refcount.decrement(eid);
                }
            }

            // free this triangle
			triangles_refcount.decrement( tID );
			Debug.Assert( triangles_refcount.isValid( tID ) == false );

            // Decrement vertex refcounts. If any hit 1 and we got remove-isolated flag,
            // we need to remove that vertex
            for (int j = 0; j < 3; ++j) {
                int vid = tv[j];
                vertices_refcount.decrement(vid);
                if ( bRemoveIsolatedVertices && vertices_refcount.refCount(vid) == 1) {
                    vertices_refcount.decrement(vid);
                    Debug.Assert(vertices_refcount.isValid(vid) == false);
                    vertex_edges.Clear(vid);
                }
            }

            updateTimeStamp(true);
            return MeshResult.Ok;
        }






        public virtual MeshResult SetTriangle(int tID, Index3i newv, bool bRemoveIsolatedVertices = true)
        {
            Index3i tv = GetTriangle(tID);
            Index3i te = GetTriEdges(tID);
            if (tv.a == newv.a && tv.b == newv.b)
                te.a = -1;
            if (tv.b == newv.b && tv.c == newv.c)
                te.b = -1;
            if (tv.c == newv.c && tv.a == newv.a)
                te.c = -1;

            if (!triangles_refcount.isValid(tID)) {
                Debug.Assert(false);
                return MeshResult.Failed_NotATriangle;
            }
            if (IsVertex(newv[0]) == false || IsVertex(newv[1]) == false || IsVertex(newv[2]) == false) {
                Util.gDevAssert(false);
                return MeshResult.Failed_NotAVertex;
            }
            if (newv[0] == newv[1] || newv[0] == newv[2] || newv[1] == newv[2]) {
                Util.gDevAssert(false);
                return MeshResult.Failed_BrokenTopology;
            }
            // look up edges. if any already have two triangles, this would 
            // create non-manifold geometry and so we do not allow it
            int e0 = find_edge(newv[0], newv[1]);
            int e1 = find_edge(newv[1], newv[2]);
            int e2 = find_edge(newv[2], newv[0]);
            if ((te.a != -1 && e0 != InvalidID && IsBoundaryEdge(e0) == false)
                 || (te.b != -1 && e1 != InvalidID && IsBoundaryEdge(e1) == false)
                 || (te.c != -1 && e2 != InvalidID && IsBoundaryEdge(e2) == false)) {
                return MeshResult.Failed_BrokenTopology;
            }


            // [TODO] check that we are not going to create invalid stuff...

            // Remove triangle from its edges. if edge has no triangles left, then it is removed.
            for (int j = 0; j < 3; ++j) {
                int eid = te[j];
                if (eid == -1)      // we don't need to modify this edge
                    continue;
                replace_edge_triangle(eid, tID, InvalidID);
                if (edges[4 * eid + 2] == InvalidID) {
                    int a = edges[4 * eid];
                    vertex_edges.Remove(a, eid);

                    int b = edges[4 * eid + 1];
                    vertex_edges.Remove(b, eid);

                    edges_refcount.decrement(eid);
                }
            }

            // Decrement vertex refcounts. If any hit 1 and we got remove-isolated flag,
            // we need to remove that vertex
            for (int j = 0; j < 3; ++j) {
                int vid = tv[j];
                if (vid == newv[j])     // we don't need to modify this vertex
                    continue;
                vertices_refcount.decrement(vid);
                if (bRemoveIsolatedVertices && vertices_refcount.refCount(vid) == 1) {
                    vertices_refcount.decrement(vid);
                    Debug.Assert(vertices_refcount.isValid(vid) == false);
                    vertex_edges.Clear(vid);
                }
            }


            // ok now re-insert with new vertices
            int i = 3 * tID;
            for (int j = 0; j < 3; ++j) {
                if (newv[j] != tv[j]) {
                    triangles[i + j] = newv[j];
                    vertices_refcount.increment(newv[j]);
                }
            }

            if ( te.a != -1 )
                add_tri_edge(tID, newv[0], newv[1], 0, e0);
            if (te.b != -1)
                add_tri_edge(tID, newv[1], newv[2], 1, e1);
            if (te.c != -1)
                add_tri_edge(tID, newv[2], newv[0], 2, e2);

            updateTimeStamp(true);
            return MeshResult.Ok;
        }







        public struct EdgeSplitInfo {
			public bool bIsBoundary;
			public int vNew;
			public int eNewBN;      // new edge [vNew,vB] (original was AB)
			public int eNewCN;      // new edge [vNew,vC] (C is "first" other vtx in ring)
			public int eNewDN;		// new edge [vNew,vD] (D is "second" other, which doesn't exist on bdry)
            public int eNewT2;
            public int eNewT3;
		}
		public MeshResult SplitEdge(int vA, int vB, out EdgeSplitInfo split)
		{
			int eid = find_edge(vA, vB);
			if ( eid == InvalidID ) {
				split = new EdgeSplitInfo();
				return MeshResult.Failed_NotAnEdge;
			}
			return SplitEdge(eid, out split);
		}
        /// <summary>
        /// Split edge eab. 
        /// split_t defines position along edge, and is assumed to be based on order of vertices returned by GetEdgeV()
        /// </summary>
		public MeshResult SplitEdge(int eab, out EdgeSplitInfo split, double split_t = 0.5)
		{
			split = new EdgeSplitInfo();
			if (! IsEdge(eab) )
				return MeshResult.Failed_NotAnEdge;

			// look up primary edge & triangle
			int eab_i = 4*eab;
			int a = edges[eab_i], b = edges[eab_i + 1];
			int t0 = edges[eab_i + 2];
            if (t0 == InvalidID)
                return MeshResult.Failed_BrokenTopology;
			Index3i T0tv = GetTriangle(t0);
			int[] T0tv_array = T0tv.array;
			int c = IndexUtil.orient_tri_edge_and_find_other_vtx(ref a, ref b, T0tv_array);
            if (vertices_refcount.rawRefCount(c) > 32764)
                return MeshResult.Failed_HitValenceLimit;
            if (a != edges[eab_i])
                split_t = 1.0 - split_t;    // if we flipped a/b order we need to reverse t

            // quite a bit of code is duplicated between boundary and non-boundary case, but it
            //  is too hard to follow later if we factor it out...
            if ( IsBoundaryEdge(eab) ) {

                // create new vertex
                Vector3d vNew = Vector3d.Lerp(GetVertex(a), GetVertex(b), split_t);
                int f = AppendVertex(vNew);
                if (HasVertexNormals)
                    SetVertexNormal(f, Vector3f.Lerp(GetVertexNormal(a), GetVertexNormal(b), (float)split_t).Normalized);
                if (HasVertexColors)
                    SetVertexColor(f, Colorf.Lerp(GetVertexColor(a), GetVertexColor(b), (float)split_t));
                if (HasVertexUVs)
                    SetVertexUV(f, Vector2f.Lerp(GetVertexUV(a), GetVertexUV(b), (float)split_t));

                // look up edge bc, which needs to be modified
                Index3i T0te = GetTriEdges(t0);
				int ebc = T0te[ IndexUtil.find_edge_index_in_tri(b, c, T0tv_array) ];

				// rewrite existing triangle
				replace_tri_vertex(t0, b, f);

				// add new second triangle
				int t2 = add_triangle_only(f,b,c, InvalidID, InvalidID, InvalidID);
				if ( triangle_groups != null )
					triangle_groups.insert(triangle_groups[t0], t2);

				// rewrite edge bc, create edge af
				replace_edge_triangle(ebc, t0, t2);
				int eaf = eab; 
				replace_edge_vertex(eaf, b, f);
                vertex_edges.Remove(b, eab);
                vertex_edges.Insert(f, eaf);

				// create new edges fb and fc 
				int efb = add_edge(f, b, t2);
				int efc = add_edge(f, c, t0, t2);

				// update triangle edge-nbrs
				replace_triangle_edge(t0, ebc, efc);
				set_triangle_edges(t2, efb, ebc, efc);

				// update vertex refcounts
				vertices_refcount.increment(c);
				vertices_refcount.increment(f, 2);

				split.bIsBoundary = true;
				split.vNew = f;
                split.eNewBN = efb;
				split.eNewCN = efc;
				split.eNewDN = InvalidID;
                split.eNewT2 = t2;
                split.eNewT3 = InvalidID;

				updateTimeStamp(true);
				return MeshResult.Ok;

			} else {		// interior triangle branch
				
				// look up other triangle
				int t1 = edges[eab_i + 3];
				Index3i T1tv = GetTriangle(t1);
				int[] T1tv_array = T1tv.array;
				int d = IndexUtil.find_tri_other_vtx( a, b, T1tv_array );
                if (vertices_refcount.rawRefCount(d) > 32764) 
                    return MeshResult.Failed_HitValenceLimit;

                // create new vertex
                Vector3d vNew = Vector3d.Lerp(GetVertex(a), GetVertex(b), split_t);
                int f = AppendVertex(vNew);
                if (HasVertexNormals)
                    SetVertexNormal(f, Vector3f.Lerp(GetVertexNormal(a), GetVertexNormal(b), (float)split_t).Normalized);
                if (HasVertexColors)
                    SetVertexColor(f, Colorf.Lerp(GetVertexColor(a), GetVertexColor(b), (float)split_t));
                if (HasVertexUVs)
                    SetVertexUV(f, Vector2f.Lerp(GetVertexUV(a), GetVertexUV(b), (float)split_t));

                // look up edges that we are going to need to update
                // [TODO OPT] could use ordering to reduce # of compares here
                Index3i T0te = GetTriEdges(t0);
				int ebc = T0te[IndexUtil.find_edge_index_in_tri( b, c, T0tv_array )];
				Index3i T1te = GetTriEdges(t1);
				int edb = T1te[IndexUtil.find_edge_index_in_tri( d, b, T1tv_array )];

				// rewrite existing triangles
				replace_tri_vertex(t0, b, f);
				replace_tri_vertex(t1, b, f);

				// add two new triangles to close holes we just created
				int t2 = add_triangle_only(f,b,c, InvalidID, InvalidID, InvalidID);
				int t3 = add_triangle_only(f, d, b, InvalidID, InvalidID, InvalidID);
				if ( triangle_groups != null ) {
					triangle_groups.insert(triangle_groups[t0], t2);
					triangle_groups.insert(triangle_groups[t1], t3);
				}

				// update the edges we found above, to point to new triangles
				replace_edge_triangle(ebc, t0, t2);
				replace_edge_triangle(edb, t1, t3);

				// edge eab became eaf
				int eaf = eab; //Edge * eAF = eAB;
				replace_edge_vertex(eaf, b, f);

                // update a/b/f vertex-edges
                vertex_edges.Remove(b, eab);
                vertex_edges.Insert(f, eaf);

				// create new edges connected to f  (also updates vertex-edges)
				int efb = add_edge( f, b, t2, t3 );
				int efc = add_edge( f, c, t0, t2 );
				int edf = add_edge( d, f, t1, t3 );

				// update triangle edge-nbrs
				replace_triangle_edge(t0, ebc, efc);
				replace_triangle_edge(t1, edb, edf);
				set_triangle_edges(t2, efb, ebc, efc);
				set_triangle_edges(t3, edf, edb, efb);

				// update vertex refcounts
				vertices_refcount.increment( c );
				vertices_refcount.increment( d );
				vertices_refcount.increment( f, 4 );

				split.bIsBoundary = false;
				split.vNew = f;
                split.eNewBN = efb;
				split.eNewCN = efc;
				split.eNewDN = edf;
                split.eNewT2 = t2;
                split.eNewT3 = t3;

                updateTimeStamp(true);
				return MeshResult.Ok;
			}

		}






		public struct EdgeFlipInfo {
			public int eID;
			public int v0,v1;
			public int ov0,ov1;
			public int t0,t1;
		}
		public MeshResult FlipEdge(int vA, int vB, out EdgeFlipInfo flip) {
			int eid = find_edge(vA, vB);
			if ( eid == InvalidID ) {
				flip = new EdgeFlipInfo();
				return MeshResult.Failed_NotAnEdge;
			}
			return FlipEdge(eid, out flip);
		}
		public MeshResult FlipEdge(int eab, out EdgeFlipInfo flip) 
		{
			flip = new EdgeFlipInfo();
			if (! IsEdge(eab) )
				return MeshResult.Failed_NotAnEdge;
			if ( IsBoundaryEdge(eab) )
				return MeshResult.Failed_IsBoundaryEdge;

			// find oriented edge [a,b], tris t0,t1, and other verts c in t0, d in t1
			int eab_i = 4*eab;
			int a = edges[eab_i], b = edges[eab_i + 1];
			int t0 = edges[eab_i + 2], t1 = edges[eab_i + 3];
			int[] T0tv = GetTriangle(t0).array;
			int[] T1tv = GetTriangle(t1).array;
			int c = IndexUtil.orient_tri_edge_and_find_other_vtx( ref a, ref b, T0tv );
			int d = IndexUtil.find_tri_other_vtx(a, b, T1tv);
			if ( c == InvalidID || d == InvalidID ) {
				return MeshResult.Failed_BrokenTopology;
			}

			int flipped = find_edge(c,d);
			if ( flipped != InvalidID )
				return MeshResult.Failed_FlippedEdgeExists;

			// find edges bc, ca, ad, db
			int ebc = find_tri_neighbour_edge(t0, b, c);
			int eca = find_tri_neighbour_edge(t0, c, a);
			int ead = find_tri_neighbour_edge(t1,a,d);
			int edb = find_tri_neighbour_edge(t1,d,b);

			// update triangles
			set_triangle(t0, c, d, b);
			set_triangle(t1, d, c, a);

			// update edge AB, which becomes flipped edge CD
			set_edge_vertices(eab, c, d);
			set_edge_triangles(eab, t0,t1);
			int ecd = eab;

			// update the two other edges whose triangle nbrs have changed
			if ( replace_edge_triangle(eca, t0,t1) == -1 )
				throw new ArgumentException("DMesh3.FlipEdge: first replace_edge_triangle failed");
			if ( replace_edge_triangle(edb, t1, t0) == -1 )
				throw new ArgumentException("DMesh3.FlipEdge: second replace_edge_triangle failed");

			// update triangle nbr lists (these are edges)
			set_triangle_edges(t0, ecd, edb, ebc);
			set_triangle_edges(t1, ecd, eca, ead);

			// remove old eab from verts a and b, and decrement ref counts
			if ( vertex_edges.Remove(a, eab) == false ) 
				throw new ArgumentException("DMesh3.FlipEdge: first edge list remove failed");
			if ( vertex_edges.Remove(b, eab) == false ) 
				throw new ArgumentException("DMesh3.FlipEdge: second edge list remove failed");
			vertices_refcount.decrement(a);
			vertices_refcount.decrement(b);
			if ( IsVertex(a) == false || IsVertex(b) == false )
				throw new ArgumentException("DMesh3.FlipEdge: either a or b is not a vertex?");

            // add new edge ecd to verts c and d, and increment ref counts
            vertex_edges.Insert(c, ecd);
            vertex_edges.Insert(d, ecd);
			vertices_refcount.increment(c);
			vertices_refcount.increment(d);

			// success! collect up results
			flip.eID = eab;
			flip.v0 = a; flip.v1 = b;
			flip.ov0 = c; flip.ov1 = d;
			flip.t0 = t0; flip.t1 = t1;

			updateTimeStamp(true);
			return MeshResult.Ok;
		}



		void debug_fail(string s) {
		#if DEBUG
			System.Console.WriteLine("DMesh3.CollapseEdge: check failed: " + s);
			Debug.Assert(false);
			//throw new Exception("DMesh3.CollapseEdge: check failed: " + s);
		#endif
		}


		void check_tri(int t) {
			Index3i tv = GetTriangle(t);
			if ( tv[0] == tv[1] || tv[0] == tv[2] || tv[1] == tv[2] )
				Debug.Assert(false);
		}
		void check_edge(int e) {
			Index2i tv = GetEdgeT(e);
			if ( tv[0] == -1 )
				Debug.Assert(false);
		}


		public struct EdgeCollapseInfo {
			public int vKept;
			public int vRemoved;
			public bool bIsBoundary;

            public int eCollapsed;              // edge we collapsed
            public int tRemoved0, tRemoved1;    // tris we removed (second may be invalid)
            public int eRemoved0, eRemoved1;    // edges we removed (second may be invalid)
            public int eKept0, eKept1;          // edges we kept (second may be invalid)
		}
		public MeshResult CollapseEdge(int vKeep, int vRemove, out EdgeCollapseInfo collapse)
		{
			collapse = new EdgeCollapseInfo();
				
			if ( IsVertex(vKeep) == false || IsVertex(vRemove) == false )
				return MeshResult.Failed_NotAnEdge;

			int b = vKeep;		// renaming for sanity. We remove a and keep b
			int a = vRemove;

			int eab = find_edge( a, b );
			if (eab == InvalidID)
				return MeshResult.Failed_NotAnEdge;

			int t0 = edges[4*eab+2];
            if (t0 == InvalidID)
                return MeshResult.Failed_BrokenTopology;
			Index3i T0tv = GetTriangle(t0);
			int c = IndexUtil.find_tri_other_vtx(a, b, T0tv);

			// look up opposing triangle/vtx if we are not in boundary case
			bool bIsBoundaryEdge = false;
			int d = InvalidID;
			int t1 = edges[4*eab+3];
			if (t1 != InvalidID) {
				Index3i T1tv = GetTriangle(t1);
				d = IndexUtil.find_tri_other_vtx( a, b, T1tv );
				if (c == d)
					return MeshResult.Failed_FoundDuplicateTriangle;
			} else {
				bIsBoundaryEdge = true;
			}

			// We cannot collapse if edge lists of a and b share vertices other
			//  than c and d  (because then we will make a triangle [x b b].
			//  Unfortunately I cannot see a way to do this more efficiently than brute-force search
			//  [TODO] if we had tri iterator for a, couldn't we check each tri for b  (skipping t0 and t1) ?
            int edges_a_count = vertex_edges.Count(a); 
            int eac = InvalidID, ead = InvalidID, ebc = InvalidID, ebd = InvalidID;
            foreach ( int eid_a in vertex_edges.ValueItr(a) ) { 
				int vax =  edge_other_v(eid_a, a);
                if ( vax == c ) {
                    eac = eid_a;
                    continue;
                }
                if ( vax == d ) {
                    ead = eid_a;
                    continue;
                }
				if ( vax == b )
					continue;
                foreach (int eid_b in vertex_edges.ValueItr(b)) {
					if ( edge_other_v(eid_b, b) == vax )
						return MeshResult.Failed_InvalidNeighbourhood;
				}
			}

            // [RMS] I am not sure this tetrahedron case will detect bowtie vertices.
            // But the single-triangle case does

			// We cannot collapse if we have a tetrahedron. In this case a has 3 nbr edges,
			//  and edge cd exists. But that is not conclusive, we also have to check that
			//  cd is an internal edge, and that each of its tris contain a or b
			if (edges_a_count == 3 && bIsBoundaryEdge == false) {
				int edc = find_edge( d, c );
				int edc_i = 4*edc;
				if (edc != InvalidID && edges[edc_i+3] != InvalidID ) {
					int edc_t0 = edges[edc_i+2];
					int edc_t1 = edges[edc_i+3];

				    if ( (tri_has_v(edc_t0,a) && tri_has_v(edc_t1, b)) 
					    || (tri_has_v(edc_t0, b) && tri_has_v(edc_t1, a)) )
					return MeshResult.Failed_CollapseTetrahedron;
				}

			} else if (bIsBoundaryEdge == true && IsBoundaryEdge(eac) ) {
                // Cannot collapse edge if we are down to a single triangle
                ebc = find_edge_from_tri(b, c, t0);
                if ( IsBoundaryEdge(ebc) )
					return MeshResult.Failed_CollapseTriangle;
			}

			// [RMS] this was added from C++ version...seems like maybe I never hit
			//   this case? Conceivably could be porting bug but looking at the
			//   C++ code I cannot see how we could possibly have caught this case...
			//
			// cannot collapse an edge where both vertices are boundary vertices
			// because that would create a bowtie
			//
			// NOTE: potentially scanning all edges here...couldn't we
			//  pick up eac/bc/ad/bd as we go? somehow?
			if ( bIsBoundaryEdge == false && IsBoundaryVertex(a) && IsBoundaryVertex(b) )
				return MeshResult.Failed_InvalidNeighbourhood;


			// 1) remove edge ab from vtx b
			// 2) find edges ad and ac, and tris tad, tac across those edges  (will use later)
			// 3) for other edges, replace a with b, and add that edge to b
			// 4) replace a with b in all triangles connected to a
			int tad = InvalidID, tac = InvalidID;
            foreach ( int eid in vertex_edges.ValueItr(a)) { 
				int o = edge_other_v(eid, a);
				if (o == b) {
					if ( vertex_edges.Remove(b,eid) != true )
						debug_fail("remove case o == b");
				} else if (o == c) {
					if ( vertex_edges.Remove(c, eid) != true )
						debug_fail("remove case o == c");
					tac = edge_other_t(eid, t0);
				} else if (o == d) {
					if (vertex_edges.Remove(d, eid) != true )
						debug_fail("remove case o == c, step 1");
					tad = edge_other_t(eid, t1);
				} else {
					if ( replace_edge_vertex(eid, a, b) == -1 )
						debug_fail("remove case else");
                    vertex_edges.Insert(b, eid);
				}

				// [TODO] perhaps we can already have unique tri list because of the manifold-nbrhood check we need to do...
				for (int j = 0; j < 2; ++j) {
					int t_j = edges[4*eid + 2 + j];
					if (t_j != InvalidID && t_j != t0 && t_j != t1) {
						if ( tri_has_v(t_j, a) ) {
							if ( replace_tri_vertex(t_j, a, b) == -1 )
								debug_fail("remove last check");
							vertices_refcount.increment(b);
							vertices_refcount.decrement(a);
						}
					}
				}
			}

			if (bIsBoundaryEdge == false) {

                // remove all edges from vtx a, then remove vtx a
                vertex_edges.Clear(a);
				Debug.Assert( vertices_refcount.refCount(a) == 3 );		// in t0,t1, and initial ref
				vertices_refcount.decrement( a, 3 );
				Debug.Assert( vertices_refcount.isValid( a ) == false );

				// remove triangles T0 and T1, and update b/c/d refcounts
				triangles_refcount.decrement( t0 );
				triangles_refcount.decrement( t1 );
				vertices_refcount.decrement( c );
				vertices_refcount.decrement( d );
				vertices_refcount.decrement( b, 2 );
				Debug.Assert( triangles_refcount.isValid( t0 ) == false );
				Debug.Assert( triangles_refcount.isValid( t1 ) == false );

				// remove edges ead, eab, eac
				edges_refcount.decrement( ead );
				edges_refcount.decrement( eab );
				edges_refcount.decrement( eac );
				Debug.Assert( edges_refcount.isValid( ead ) == false );
				Debug.Assert( edges_refcount.isValid( eab ) == false );
				Debug.Assert( edges_refcount.isValid( eac ) == false );

				// replace t0 and t1 in edges ebd and ebc that we kept
				ebd = find_edge_from_tri( b, d, t1 );
                if ( ebc == InvalidID )   // we may have already looked this up
				    ebc = find_edge_from_tri( b, c, t0 );

				if( replace_edge_triangle(ebd, t1, tad ) == -1 )
					debug_fail("isboundary=false branch, ebd replace triangle");

				if ( replace_edge_triangle(ebc, t0, tac ) == -1 )
					debug_fail("isboundary=false branch, ebc replace triangle");

				// update tri-edge-nbrs in tad and tac
				if (tad != InvalidID) {
					if ( replace_triangle_edge(tad, ead, ebd ) == -1 )
						debug_fail("isboundary=false branch, ebd replace triangle");
				}
				if (tac != InvalidID) {
					if ( replace_triangle_edge(tac, eac, ebc ) == -1 )
						debug_fail("isboundary=false branch, ebd replace triangle");
				}

			} else {

                //  this is basically same code as above, just not referencing t1/d

                // remove all edges from vtx a, then remove vtx a
                vertex_edges.Clear(a);
                Debug.Assert( vertices_refcount.refCount( a ) == 2 );		// in t0 and initial ref
				vertices_refcount.decrement( a, 2 );
				Debug.Assert( vertices_refcount.isValid( a ) == false );

				// remove triangle T0 and update b/c refcounts
				triangles_refcount.decrement( t0 );
				vertices_refcount.decrement( c );
				vertices_refcount.decrement( b );
				Debug.Assert( triangles_refcount.isValid( t0 ) == false );

				// remove edges eab and eac
				edges_refcount.decrement( eab );
				edges_refcount.decrement( eac );
				Debug.Assert( edges_refcount.isValid( eab ) == false );
				Debug.Assert( edges_refcount.isValid( eac ) == false );

				// replace t0 in edge ebc that we kept
				ebc = find_edge_from_tri( b, c, t0 );
				if ( replace_edge_triangle(ebc, t0, tac ) == -1 )
					debug_fail("isboundary=false branch, ebc replace triangle");

				// update tri-edge-nbrs in tac
				if (tac != InvalidID) {
					if ( replace_triangle_edge(tac, eac, ebc ) == -1 )
						debug_fail("isboundary=true branch, ebd replace triangle");
				}
			}

			collapse.vKept = vKeep;
			collapse.vRemoved = vRemove;
			collapse.bIsBoundary = bIsBoundaryEdge;
            collapse.eCollapsed = eab;
            collapse.tRemoved0 = t0; collapse.tRemoved1 = t1;
            collapse.eRemoved0 = eac; collapse.eRemoved1 = ead;
            collapse.eKept0 = ebc; collapse.eKept1 = ebd;

			updateTimeStamp(true);
			return MeshResult.Ok;
		}





		public struct MergeEdgesInfo
		{
			public int eKept;
			public int eRemoved;

			public Vector2i vKept;
			public Vector2i vRemoved;           // either may be InvalidID if it was same as vKept

			public Vector2i eRemovedExtra;      // bonus collapsed edge, or InvalidID
			public Vector2i eKeptExtra;			// edge paired w/ eRemovedExtra
		}
		public MeshResult MergeEdges(int eKeep, int eDiscard, out MergeEdgesInfo merge_info) 
		{
			merge_info = new MergeEdgesInfo();
			if (IsEdge(eKeep) == false || IsEdge(eDiscard) == false)
				return MeshResult.Failed_NotAnEdge;

			Index4i edgeinfo_keep = GetEdge(eKeep);
			Index4i edgeinfo_discard = GetEdge(eDiscard);
			if (edgeinfo_keep.d != InvalidID || edgeinfo_discard.d != InvalidID)
				return MeshResult.Failed_NotABoundaryEdge;

			int a = edgeinfo_keep.a, b = edgeinfo_keep.b;
			int tab = edgeinfo_keep.c;
			int eab = eKeep;
			int c = edgeinfo_discard.a, d = edgeinfo_discard.b;
			int tcd = edgeinfo_discard.c;
			int ecd = eDiscard;

			// Need to correctly orient a,b and c,d and then check that 
			// we will not join triangles with incompatible winding order
			// I can't see how to do this purely topologically. 
			// So relying on closest-pairs testing.
			IndexUtil.orient_tri_edge(ref a, ref b, GetTriangle(tab));
			//int tcd_otherv = IndexUtil.orient_tri_edge_and_find_other_vtx(ref c, ref d, GetTriangle(tcd));
			IndexUtil.orient_tri_edge(ref c, ref d, GetTriangle(tcd));
			int x = c; c = d; d = x;   // joinable bdry edges have opposing orientations, so flip to get ac and b/d correspondences
			Vector3d Va = GetVertex(a), Vb = GetVertex(b), Vc = GetVertex(c), Vd = GetVertex(d);
			if ((Va.DistanceSquared(Vc) + Vb.DistanceSquared(Vd)) >
				(Va.DistanceSquared(Vd) + Vb.DistanceSquared(Vc)))
				return MeshResult.Failed_SameOrientation;

			// alternative that detects normal flip of triangle tcd. This is a more 
			// robust geometric test, but fails if tri is degenerate...also more expensive
			//Vector3d otherv = GetVertex(tcd_otherv);
			//Vector3d Ncd = MathUtil.FastNormalDirection(GetVertex(c), GetVertex(d), otherv);
			//Vector3d Nab = MathUtil.FastNormalDirection(GetVertex(a), GetVertex(b), otherv);
			//if (Ncd.Dot(Nab) < 0)
			//return MeshResult.Failed_SameOrientation;

			merge_info.eKept = eab;
			merge_info.eRemoved = ecd;

            // if a/c or b/d are connected by an existing edge, we can't merge
            if (a != c && find_edge(a,c) != DMesh3.InvalidID )
                return MeshResult.Failed_InvalidNeighbourhood;
            if (b != d && find_edge(b, d) != DMesh3.InvalidID)
                return MeshResult.Failed_InvalidNeighbourhood;

            // if vertices at either end already share a common neighbour vertex, and we 
            // do the merge, that would create duplicate edges. This is something like the
            // 'link condition' in edge collapses. 
            // Note that we have to catch cases where both edges to the shared vertex are
            // boundary edges, in that case we will also merge this edge later on
            if ( a != c ) {
                int ea = 0, ec = 0, other_v = (b == d) ? b : -1;
                foreach ( int cnbr in VtxVerticesItr(c) ) {
                    if (cnbr != other_v && (ea = find_edge(a, cnbr)) != DMesh3.InvalidID) {
                        ec = find_edge(c, cnbr);
                        if (IsBoundaryEdge(ea) == false || IsBoundaryEdge(ec) == false)
                            return MeshResult.Failed_InvalidNeighbourhood;
                    }
                }
            }
            if ( b != d ) {
                int eb = 0, ed = 0, other_v = (a == c) ? a : -1;
                foreach ( int dnbr in VtxVerticesItr(d)) {
                    if (dnbr != other_v && (eb = find_edge(b, dnbr)) != DMesh3.InvalidID) {
                        ed = find_edge(d, dnbr);
                        if (IsBoundaryEdge(eb) == false || IsBoundaryEdge(ed) == false)
                            return MeshResult.Failed_InvalidNeighbourhood;
                    }
                }
            }
            

            // [TODO] this acts on each interior tri twice. could avoid using vtx-tri iterator?
            if (a != c) {
				// replace c w/ a in edges and tris connected to c, and move edges to a
                foreach ( int eid in vertex_edges.ValueItr(c)) { 
					if (eid == eDiscard)
						continue;
					replace_edge_vertex(eid, c, a);
					short rc = 0;
					if (replace_tri_vertex(edges[4 * eid + 2], c, a) >= 0)
						rc++;
					if (edges[4 * eid + 3] != InvalidID) {
						if (replace_tri_vertex(edges[4 * eid + 3], c, a) >= 0)
							rc++;
					}
                    vertex_edges.Insert(a, eid);
					if (rc > 0) {
						vertices_refcount.increment(a, rc);
						vertices_refcount.decrement(c, rc);
					}
				}
                vertex_edges.Clear(c);
				vertices_refcount.decrement(c);
				merge_info.vRemoved[0] = c;
			} else {
                vertex_edges.Remove(a, ecd);
				merge_info.vRemoved[0] = InvalidID;
			}
			merge_info.vKept[0] = a;

			if (d != b) {
				// replace d w/ b in edges and tris connected to d, and move edges to b
                foreach (int eid in vertex_edges.ValueItr(d)) { 
					if (eid == eDiscard)
						continue;
					replace_edge_vertex(eid, d, b);
					short rc = 0;
					if (replace_tri_vertex(edges[4 * eid + 2], d, b) >= 0)
						rc++;
					if (edges[4 * eid + 3] != InvalidID) {
						if (replace_tri_vertex(edges[4 * eid + 3], d, b) >= 0)
							rc++;
					}
                    vertex_edges.Insert(b, eid);
					if (rc > 0) {
						vertices_refcount.increment(b, rc);
						vertices_refcount.decrement(d, rc);
					}

				}
                vertex_edges.Clear(d);
				vertices_refcount.decrement(d);
				merge_info.vRemoved[1] = d;
			} else {
                vertex_edges.Remove(b, ecd);
				merge_info.vRemoved[1] = InvalidID;
			}
			merge_info.vKept[1] = b;

			// replace edge cd with edge ab in triangle tcd
			replace_triangle_edge(tcd, ecd, eab);
			edges_refcount.decrement(ecd);

			// update edge-tri adjacency
			set_edge_triangles(eab, tab, tcd);

			// Once we merge ab to cd, there may be additional edges (now) connected
			// to either a or b that are connected to the same vertex on their 'other' side.
			// So we now have two boundary edges connecting the same two vertices - disaster!
			// We need to find and merge these edges. 
			// Q: I don't think it is possible to have multiple such edge-pairs at a or b
			//    But I am not certain...is a bit tricky to handle because we modify edges_v...
			merge_info.eRemovedExtra = new Vector2i(InvalidID, InvalidID);
			merge_info.eKeptExtra = merge_info.eRemovedExtra;
			for (int vi = 0; vi < 2; ++vi) {
				int v1 = a, v2 = c;   // vertices of merged edge
				if ( vi == 1 ) {
					v1 = b; v2 = d;
				}
				if (v1 == v2)
					continue;
                List<int> edges_v = vertex_edges_list(v1);
                int Nedges = edges_v.Count;
				bool found = false;
				// in this loop, we compare 'other' vert_1 and vert_2 of edges around v1.
				// problem case is when vert_1 == vert_2  (ie two edges w/ same other vtx).
                //restart_merge_loop:
				for (int i = 0; i < Nedges && found == false; ++i) {
					int edge_1 = edges_v[i];
					if ( IsBoundaryEdge(edge_1) == false)
						continue;
					int vert_1 = edge_other_v(edge_1, v1);
					for (int j = i + 1; j < Nedges; ++j) {
						int edge_2 = edges_v[j];
						int vert_2 = edge_other_v(edge_2, v1);
						if (vert_1 == vert_2 && IsBoundaryEdge(edge_2)) { // if ! boundary here, we are in deep trouble...
							// replace edge_2 w/ edge_1 in tri, update edge and vtx-edge-nbr lists
							int tri_1 = edges[4 * edge_1 + 2];
							int tri_2 = edges[4 * edge_2 + 2];
							replace_triangle_edge(tri_2, edge_2, edge_1);
							set_edge_triangles(edge_1, tri_1, tri_2);
                            vertex_edges.Remove(v1, edge_2);
                            vertex_edges.Remove(vert_1, edge_2);
							edges_refcount.decrement(edge_2);
							merge_info.eRemovedExtra[vi] = edge_2;
							merge_info.eKeptExtra[vi] = edge_1;

                            //edges_v = vertex_edges_list(v1);      // this code allows us to continue checking, ie in case we had
                            //Nedges = edges_v.Count;               // multiple such edges. but I don't think it's possible.
                            //goto restart_merge_loop;
                            found = true;			  // exit outer i loop
                            break;					  // exit inner j loop
                        }
					}
				}
			}

            updateTimeStamp(true);
            return MeshResult.Ok;
		}







        public struct PokeTriangleInfo
        {
            public int new_vid;
            public int new_t1, new_t2;
            public Index3i new_edges;
        }
        public virtual MeshResult PokeTriangle(int tid, out PokeTriangleInfo result)
        {
            return PokeTriangle(tid, Vector3d.One / 3.0, out result);
        }
        public virtual MeshResult PokeTriangle(int tid, Vector3d baryCoordinates, out PokeTriangleInfo result)
        {
            result = new PokeTriangleInfo();

            if (!IsTriangle(tid))
                return MeshResult.Failed_NotATriangle;

            Index3i tv = GetTriangle(tid);
            Index3i te = GetTriEdges(tid);

            // create new vertex with interpolated vertex attribs
            NewVertexInfo vinfo;
            GetTriBaryPoint(tid, baryCoordinates.x, baryCoordinates.y, baryCoordinates.z, out vinfo);
            int center = AppendVertex(vinfo);

            // add in new edges to center vtx, do not connect to triangles yet
            int eaC = add_edge(tv.a, center, -1, -1);
            int ebC = add_edge(tv.b, center, -1, -1);
            int ecC = add_edge(tv.c, center, -1, -1);
            vertices_refcount.increment(tv.a);
            vertices_refcount.increment(tv.b);
            vertices_refcount.increment(tv.c);
            vertices_refcount.increment(center, 3);

            // old triangle becomes tri along first edge
            set_triangle(tid, tv.a, tv.b, center);
            set_triangle_edges(tid, te.a, ebC, eaC);

            // add two new triangles
            int t1 = add_triangle_only(tv.b, tv.c, center, te.b, ecC, ebC );
            int t2 = add_triangle_only(tv.c, tv.a, center, te.c, eaC, ecC);

            // second and third edges of original tri have new neighbours
            replace_edge_triangle(te.b, tid, t1);
            replace_edge_triangle(te.c, tid, t2);

            // set the triangles for the new edges we created above
            set_edge_triangles(eaC, tid, t2);
            set_edge_triangles(ebC, tid, t1);
            set_edge_triangles(ecC, t1, t2);

            // transfer groups
            if ( HasTriangleGroups ) {
                int g = triangle_groups[tid];
                triangle_groups.insert(g, t1);
                triangle_groups.insert(g, t2);
            }

            result.new_vid = center;
            result.new_t1 = t1;
            result.new_t2 = t2;
            result.new_edges = new Index3i(eaC, ebC, ecC);

            updateTimeStamp(true);
            return MeshResult.Ok;
        }





        // internal

        void set_triangle(int tid, int v0, int v1, int v2)
        {
			int i = 3*tid;
            triangles[i] = v0;
            triangles[i + 1] = v1;
            triangles[i + 2] = v2;
        }
        void set_triangle_edges(int tid, int e0, int e1, int e2)
        {
			int i = 3*tid;
            triangle_edges[i] = e0;
            triangle_edges[i + 1] = e1;
            triangle_edges[i + 2] = e2;
        }

        int add_edge(int vA, int vB, int tA, int tB = InvalidID)
        {
            if (vB < vA) {
                int t = vB; vB = vA; vA = t;
            }
            int eid = edges_refcount.allocate();
			int i = 4*eid;
            edges.insert(vA, i);
            edges.insert(vB, i + 1);
            edges.insert(tA, i + 2);
            edges.insert(tB, i + 3);

            vertex_edges.Insert(vA, eid);
            vertex_edges.Insert(vB, eid);
            return eid;
        }

		int replace_tri_vertex(int tID, int vOld, int vNew) {
			int i = 3*tID;
			if ( triangles[i] == vOld ) { triangles[i] = vNew; return 0; }
			if ( triangles[i+1] == vOld ) { triangles[i+1] = vNew; return 1; }
			if ( triangles[i+2] == vOld ) { triangles[i+2] = vNew; return 2; }
			return -1;
		}

		int add_triangle_only(int a, int b, int c, int e0, int e1, int e2) {
			int tid = triangles_refcount.allocate();
			int i = 3*tid;
			triangles.insert(c, i + 2);
			triangles.insert(b, i + 1);
			triangles.insert(a, i);
			triangle_edges.insert(e2, i+2);
			triangle_edges.insert(e1, i+1);
			triangle_edges.insert(e0, i+0);	
			return tid;
		}



        void allocate_edges_list(int vid)
        {
            if ( vid < vertex_edges.Size )
                vertex_edges.Clear(vid);
            vertex_edges.AllocateAt(vid);
        }
        List<int> vertex_edges_list(int vid)
        {
            return new List<int>( vertex_edges.ValueItr(vid) );
        }
        List<int> vertex_vertices_list(int vid)
        {
            List<int> vnbrs = new List<int>();
            foreach (int eid in vertex_edges.ValueItr(vid))
                vnbrs.Add(edge_other_v(eid, vid));
            return vnbrs;
        }


        void set_edge_vertices(int eID, int a, int b) {
			int i = 4*eID;
			edges[i] = Math.Min(a,b);
			edges[i + 1] = Math.Max(a,b);
		}
		void set_edge_triangles(int eID, int t0, int t1) {
			int i = 4*eID;
			edges[i + 2] = t0;
			edges[i + 3] = t1;
		}

		int replace_edge_vertex(int eID, int vOld, int vNew) {
			int i = 4*eID;
			int a = edges[i], b = edges[i+1];
			if ( a == vOld ) {
				edges[i] = Math.Min(b, vNew);
				edges[i+1] = Math.Max(b, vNew);
				return 0;
			} else if ( b == vOld ) {
				edges[i] = Math.Min(a, vNew);
				edges[i+1] = Math.Max(a, vNew);
				return 1;				
			} else
				return -1;
		}


		int replace_edge_triangle(int eID, int tOld, int tNew) {
			int i = 4*eID;
			int a = edges[i+2], b = edges[i+3];
			if ( a == tOld ) {
				if ( tNew == InvalidID ) {
					edges[i+2] = b;
					edges[i+3] = InvalidID;
				} else 
					edges[i+2] = tNew;
				return 0;
			} else if ( b == tOld ) {
				edges[i+3] = tNew;
				return 1;				
			} else
				return -1;
		}

		int replace_triangle_edge(int tID, int eOld, int eNew) {
			int i = 3*tID;
			if ( triangle_edges[i] == eOld ) {
				triangle_edges[i] = eNew;
				return 0;
			} else if ( triangle_edges[i+1] == eOld ) {
				triangle_edges[i+1] = eNew;
				return 1;
			} else if ( triangle_edges[i+2] == eOld ) {
				triangle_edges[i+2] = eNew;
				return 2;
			} else
				return -1;
		}



    }
}
