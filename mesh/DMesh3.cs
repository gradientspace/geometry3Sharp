using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Generic;

namespace g3
{
    public enum MeshResult
    {
        Ok = 0,
        Failed_NotAVertex = 1,
        Failed_NotATriangle = 2,
        Failed_NotAnEdge = 3,

        Failed_BrokenTopology = 10,

        Failed_IsBoundaryEdge = 20,
        Failed_FlippedEdgeExists = 21,
        Failed_IsBowtieVertex = 22,
        Failed_InvalidNeighbourhood = 23,       // these are all failures for CollapseEdge
        Failed_FoundDuplicateTriangle = 24,
        Failed_CollapseTetrahedron = 25,
        Failed_CollapseTriangle = 26
    };


	//
	// DMesh3 is a dynamic triangle mesh class. The mesh has has connectivity, 
	//  is an inexed mesh, and allows for gaps in the index space.
	//
	// internally, all data is stored in POD-type buffers, except for the vertex->edge
	// links, which are stored as List<int>'s. The arrays of POD data are stored in
	// DVector's, so they grow in chunks, which is relatively efficient. 
	//
	// Reference counts for verts/tris/edges are stored as separate RefCountVector
	// instances. 
	//
	// For each vertex, vertex_edges[i] is the unordered list of connected edges. This
	// can be traversed in-order at some cost
	//
	// For each triangle, triangle_edges stores the three nbr edges. 
	//
    public class DMesh3 : IMesh
    {
        public const int InvalidID = -1;
        public const int NonManifoldID = -2;


        public static readonly Vector3d InvalidVertex = new Vector3d(Double.MaxValue, 0, 0);
        public static readonly Vector3i InvalidTriangle = new Vector3i(InvalidID, InvalidID, InvalidID);
        public static readonly Vector2i InvalidEdge = new Vector2i(InvalidID, InvalidID);


        RefCountVector vertices_refcount;
        DVector<double> vertices;
		DVector<float> normals;
		DVector<float> colors;
		DVector<float> uv;

        // [TODO] this seems like it will not be efficient! 
        //   do our own short_list backed my a memory pool?
        // [TODO] this is optional if we only want to use this class as an iterable mesh-with-nbrs
        //   make it optional with a flag? (however find_edge depends on it...)
        DVector<List<int>> vertex_edges;

        RefCountVector triangles_refcount;
        DVector<int> triangles;
        DVector<int> triangle_edges;
		DVector<int> triangle_groups;

        RefCountVector edges_refcount;
        DVector<int> edges;



        public DMesh3(bool bWantNormals = true, bool bWantColors = false, bool bWantUVs = false, bool bWantTriGroups = false)
        {
            vertices = new DVector<double>();
			if ( bWantNormals)
				normals = new DVector<float>();
			if ( bWantColors )
				colors = new DVector<float>();
			if ( bWantUVs )
				uv = new DVector<float>();
            vertex_edges = new DVector<List<int>>();
            vertices_refcount = new RefCountVector();

            triangles = new DVector<int>();
            triangle_edges = new DVector<int>();
            triangles_refcount = new RefCountVector();
			if ( bWantTriGroups )
				triangle_groups = new DVector<int>();

            edges = new DVector<int>();
            edges_refcount = new RefCountVector();
        }


		void updateTimeStamp() {
		}


        // IMesh impl

        public int VertexCount {
            get { return vertices_refcount.count; }
        }
        public int TriangleCount {
            get { return triangles_refcount.count; }
        }
		public int EdgeCount {
			get { return edges_refcount.count; }
		}

        public bool HasVertexColors { get { return colors != null; } }
        public bool HasVertexNormals { get { return normals != null; } }
        public bool HasVertexUVs { get { return uv != null; } }
		public bool HasTriangleGroups { get { return triangle_groups != null; } }

        // info

        public bool IsVertex(int vID) {
            return vertices_refcount.isValid(vID);
        }
        public bool IsTriangle(int tID) {
            return triangles_refcount.isValid(tID);
        }
        public bool IsEdge(int eID) {
            return edges_refcount.isValid(eID);
        }


        public int GetMaxVertexID() {
            return vertices_refcount.max_index;
        }
        public int GetMaxTriangleID() {
            return triangles_refcount.max_index;
        }
        public int GetMaxEdgeID(int vID) {
            return edges_refcount.max_index;
        }



        // getters


        public Vector3d GetVertex(int vID) {
            return vertices_refcount.isValid(vID) ?
                new Vector3d(vertices[3 * vID], vertices[3 * vID + 1], vertices[3 * vID + 2]) : InvalidVertex;
        }

		public Vector3d GetVertexNormal(int vID) { 
			return vertices_refcount.isValid(vID) ?
                new Vector3d(normals[3 * vID], normals[3 * vID + 1], normals[3 * vID + 2]) : Vector3d.AxisY;
		}

		public Vector3d GetVertexColor(int vID) { 
			return vertices_refcount.isValid(vID) ?
                new Vector3d(colors[3 * vID], colors[3 * vID + 1], colors[3 * vID + 2]) : Vector3d.One;
		}

		public Vector2f GetVertexUV(int vID) { 
			return vertices_refcount.isValid(vID) ?
                new Vector2f(uv[2 * vID], uv[2 * vID + 1]) : Vector2f.Zero;
		}


        public ReadOnlyCollection<int> GetVtxEdges(int vID) {
            return vertices_refcount.isValid(vID) ?
                vertex_edges[vID].AsReadOnly() : null;
        }

        public Vector3i GetTriangle(int tID) {
            return triangles_refcount.isValid(tID) ?
                new Vector3i(triangles[3 * tID], triangles[3 * tID + 1], triangles[3 * tID + 2]) : InvalidTriangle;
        }

        public Vector3i GetTriEdges(int tID) {
            return triangles_refcount.isValid(tID) ?
                new Vector3i(triangle_edges[3 * tID], triangle_edges[3 * tID + 1], triangle_edges[3 * tID + 2]) : InvalidTriangle;
        }

		public int GetTriangleGroup(int tID) { 
			return triangles_refcount.isValid(tID) ?
                 triangle_groups[tID] : 0;
		}


        public Vector2i GetEdgeV(int eID) {
            return edges_refcount.isValid(eID) ?
                new Vector2i(edges[4 * eID], edges[4 * eID + 1]) : InvalidEdge;
        }
        public Vector2i GetEdgeT(int eID) {
            return edges_refcount.isValid(eID) ?
                new Vector2i(edges[4 * eID + 2], edges[4 * eID + 3]) : InvalidEdge;
        }



        // mesh-building


        public int AppendVertex(Vector3d v) {
            return AppendVertex(new NewVertexInfo() {
                v = v, bHaveC = false, bHaveUV = false, bHaveN = false
            });
        }
        public int AppendVertex(NewVertexInfo info)
        {
            int vid = vertices_refcount.allocate();
            vertices.insert(info.v[2], 3 * vid + 2);
            vertices.insert(info.v[1], 3 * vid + 1);
            vertices.insert(info.v[0], 3 * vid);

			if ( normals != null ) {
				Vector3f n = (info.bHaveN) ? info.n : Vector3f.AxisY;
				normals.insert(n[2], 3 * vid + 2);
				normals.insert(n[1], 3 * vid + 1);
				normals.insert(n[0], 3 * vid);
			}

			if ( colors != null ) {
				Vector3f c = (info.bHaveC) ? info.c : Vector3f.One;
				colors.insert(c[2], 3 * vid + 2);
				colors.insert(c[1], 3 * vid + 1);
				colors.insert(c[0], 3 * vid);
			}

			if ( uv != null ) {
				Vector2f u = (info.bHaveUV) ? info.uv : Vector2f.Zero;
				uv.insert(u[1], 2*vid + 1);
				uv.insert(u[0], 2*vid);
			}

            vertex_edges.insert(new List<int>(), vid);

            return vid;
        }


        public int AppendTriangle(int v0, int v1, int v2, int gid = -1) {
            return AppendTriangle(new Vector3i(v0, v1, v2), gid);
        }
        public int AppendTriangle(Vector3i tv, int gid = -1) {
            if (IsVertex(tv[0]) == false || IsVertex(tv[1]) == false || IsVertex(tv[2]) == false) {
                Util.gDevAssert(false);
                return InvalidID;
            }
            if (tv[0] == tv[1] || tv[0] == tv[2] || tv[1] == tv[2]) {
                Util.gDevAssert(false);
                return InvalidID;
            }

            // look up edges. if any already have two triangles, this would 
            // create non-manifold geometry and so we do not allow it
            int e0 = find_edge(tv[0], tv[1]);
            int e1 = find_edge(tv[1], tv[2]);
            int e2 = find_edge(tv[2], tv[0]);
            if ((e0 != InvalidID && edge_is_boundary(e0) == false)
                 || (e1 != InvalidID && edge_is_boundary(e1) == false)
                 || (e2 != InvalidID && edge_is_boundary(e2) == false)) {
                return NonManifoldID;
            }

            // now safe to insert triangle
            int tid = triangles_refcount.allocate();
            triangles.insert(tv[2], 3 * tid + 2);
            triangles.insert(tv[1], 3 * tid + 1);
            triangles.insert(tv[0], 3 * tid);
			if ( triangle_groups != null )
				triangle_groups.insert(gid, tid);

            // increment ref counts and update/create edges
            vertices_refcount.increment(tv[0]);
            vertices_refcount.increment(tv[1]);
            vertices_refcount.increment(tv[2]);

            add_tri_edge(tid, tv[0], tv[1], 0, e0);
            add_tri_edge(tid, tv[1], tv[2], 1, e1);
            add_tri_edge(tid, tv[2], tv[0], 2, e2);

            return tid;
        }
        // helper fn for above, just makes code cleaner
        void add_tri_edge(int tid, int v0, int v1, int j, int eid)
        {
            if (eid != InvalidID) {
                edges[4 * eid + 3] = tid;
                triangle_edges.insert(eid, 3 * tid + j);
            } else
                triangle_edges.insert(add_edge(v0, v1, tid), 3 * tid + j);
        }





        // iterators

        public System.Collections.IEnumerable VertexIndices() {
            foreach (int vid in vertices_refcount)
                yield return vid;
        }
        public System.Collections.IEnumerable TriangleIndices() {
            foreach (int tid in triangles_refcount)
                yield return tid;
        }
        public System.Collections.IEnumerable EdgeIndices() {
            foreach (int eid in edges_refcount)
                yield return eid;
        }




        // queries

        int FindEdge(int vA, int vB) {
            return find_edge(vA, vB);
        }
        Vector2i GetEdgeOpposingV(int eID)
        {
            int a = edges[4 * eID], b = edges[4 * eID + 1];
            int t0 = edges[4 * eID + 2], t1 = edges[4 * eID + 3];
            int c = IndexUtil.orient_tri_edge_and_find_other_vtx(ref a, ref b, GetTriangle(t0).array);
            if (t1 != InvalidID) {
                int d = IndexUtil.find_tri_other_vtx(a, b, GetTriangle(t1).array);
                return new Vector2i(c, d);
            } else
                return new Vector2i(c, InvalidID);
        }



        Vector3i GetTriTriangles(int tID) {
            if (!IsTriangle(tID))
                return InvalidTriangle;
            return new Vector3i(
                edge_other_t(triangle_edges[3 * tID], tID),
                edge_other_t(triangle_edges[3 * tID + 1], tID),
                edge_other_t(triangle_edges[3 * tID + 2], tID));
        }

        MeshResult GetVtxTriangles(int vID, List<int> vTriangles, bool bUseOrientation)
        {
            if (!IsVertex(vID))
                return MeshResult.Failed_NotAVertex;
            List<int> vedges = vertex_edges[vID];

            if (bUseOrientation) {
                foreach (int eid in vedges) {
                    int vOther = edge_other_v(eid, vID);
                    int et0 = edges[4 * eid + 2];
                    if (tri_has_sequential_v(et0, vID, vOther))
                        vTriangles.Add(et0);
                    int et1 = edges[4 * eid + 3];
                    if (et1 != InvalidID && tri_has_sequential_v(et1, vID, vOther))
                        vTriangles.Add(et1);
                }
            } else {
                // brute-force method
                foreach (int eid in vedges) {
                    int t0 = edges[4 * eid + 2];
                    if (vTriangles.Contains(t0) == false)
                        vTriangles.Add(t0);
                    int t1 = edges[4 * eid + 3];
                    if (t1 != InvalidID && vTriangles.Contains(t1) == false)
                        vTriangles.Add(t1);
                }
            }
            return MeshResult.Ok;
        }


        bool tri_has_v(int tID, int vID) {
            return triangles[3 * tID] == vID 
                || triangles[3 * tID + 1] == vID
                || triangles[3 * tID + 2] == vID;
        }

        public bool tri_is_boundary(int tID) {
            return edge_is_boundary(triangle_edges[3 * tID])
                || edge_is_boundary(triangle_edges[3 * tID + 1])
                || edge_is_boundary(triangle_edges[3 * tID + 2]);
        }

        public bool tri_has_neighbour_t(int tCheck, int tNbr) {
            return edge_has_t(triangle_edges[3 * tCheck], tNbr)
                || edge_has_t(triangle_edges[3 * tCheck + 1], tNbr)
                || edge_has_t(triangle_edges[3 * tCheck + 2], tNbr);
        }

        public bool tri_has_sequential_v(int tID, int vA, int vB)
        {
            int v0 = triangles[3 * tID], v1 = triangles[3 * tID + 1], v2 = triangles[3 * tID + 2];
            if (v0 == vA && v1 == vB) return true;
            if (v1 == vA && v2 == vB) return true;
            if (v2 == vA && v0 == vB) return true;
            return false;
        }

		int replace_tri_vertex(int tID, int vOld, int vNew) {
			if ( triangles[3 * tID] == vOld ) { triangles[3 * tID] = vNew; return 0; }
			if ( triangles[3 * tID+1] == vOld ) { triangles[3 * tID+1] = vNew; return 1; }
			if ( triangles[3 * tID+2] == vOld ) { triangles[3 * tID+2] = vNew; return 2; }
			return -1;
		}

        public bool edge_is_boundary(int eid) {
            return edges[4 * eid + 3] == InvalidID;
        }
        public bool edge_has_v(int eid, int vid) {
            return (edges[4 * eid] == vid) || (edges[4 * eid + 1] == vid);
        }
        public bool edge_has_t(int eid, int tid) {
            return (edges[4 * eid + 2] == tid) || (edges[4 * eid + 3] == tid);
        }
        public int edge_other_v(int eID, int vID)
        {
            int ev0 = edges[4 * eID], ev1 = edges[4 * eID + 1];
            return (ev0 == vID) ? ev1 : ((ev1 == vID) ? ev0 : InvalidID);
        }
        public int edge_other_t(int eID, int tid) {
            int et0 = edges[4 * eID + 2], et1 = edges[4 * eID + 3];
            return (et0 == tid) ? et1 : ((et1 == tid) ? et0 : InvalidID);
        }




        // internal

        void set_triangle(int tid, int v0, int v1, int v2)
        {
            triangles[3 * tid] = v0;
            triangles[3 * tid + 1] = v1;
            triangles[3 * tid + 2] = v2;
        }
        void set_triangle_edges(int tid, int e0, int e1, int e2)
        {
            triangle_edges[3 * tid] = e0;
            triangle_edges[3 * tid + 1] = e1;
            triangle_edges[3 * tid + 2] = e2;
        }

        int add_edge(int vA, int vB, int tA, int tB = InvalidID)
        {
            if (vB < vA) {
                int t = vB; vB = vA; vA = t;
            }
            int eid = edges_refcount.allocate();
            edges.insert(vA, 4 * eid);
            edges.insert(vB, 4 * eid + 1);
            edges.insert(tA, 4 * eid + 2);
            edges.insert(tB, 4 * eid + 3);

            vertex_edges[vA].Add(eid);
            vertex_edges[vB].Add(eid);
            return eid;
        }

		int add_triangle_only(int a, int b, int c, int e0, int e1, int e2) {
			int tid = triangles_refcount.allocate();
			triangles.insert(c, 3 * tid + 2);
			triangles.insert(b, 3 * tid + 1);
			triangles.insert(a, 3 * tid);
			triangle_edges.insert(e2, 3*tid+2);
			triangle_edges.insert(e1, 3*tid+1);
			triangle_edges.insert(e0, 3*tid+0);	
			return tid;
		}

        int find_edge(int vA, int vB)
        {
            int vO = Math.Max(vA, vB);
            List<int> e0 = vertex_edges[Math.Min(vA, vB)];
            int idx = e0.FindIndex((x) => edge_has_v(x, vO));
            return (idx == -1) ? InvalidID : e0[idx];
        }

		void set_edge_vertices(int eID, int a, int b) {
			edges[4 * eID] = Math.Min(a,b);
			edges[4 * eID + 1] = Math.Max(a,b);
		}

		int replace_edge_vertex(int eID, int vOld, int vNew) {
			int a = edges[4*eID], b = edges[4*eID+1];
			if ( a == vOld ) {
				edges[4*eID] = Math.Min(b, vNew);
				edges[4*eID+1] = Math.Max(b, vNew);
				return 0;
			} else if ( b == vOld ) {
				edges[4*eID] = Math.Min(a, vNew);
				edges[4*eID+1] = Math.Max(a, vNew);
				return 1;				
			} else
				return -1;
		}


		int replace_edge_triangle(int eID, int tOld, int tNew) {
			int a = edges[4*eID+2], b = edges[4*eID+3];
			if ( a == tOld ) {
				if ( tNew == InvalidID ) {
					edges[4*eID+2] = b;
					edges[4*eID+3] = InvalidID;
				} else 
					edges[4*eID+2] = tNew;
				return 0;
			} else if ( b == tOld ) {
				edges[4*eID+3] = tNew;
				return 1;				
			} else
				return -1;
		}

		int replace_triangle_edge(int tID, int eOld, int eNew) {
			if ( triangle_edges[3*tID] == eOld ) {
				triangle_edges[3*tID] = eNew;
				return 0;
			} else if ( triangle_edges[3*tID+1] == eOld ) {
				triangle_edges[3*tID+1] = eNew;
				return 1;
			} else if ( triangle_edges[3*tID+2] == eOld ) {
				triangle_edges[3*tID+2] = eNew;
				return 2;
			} else
				return -1;
		}






        // edits

        MeshResult ReverseTriOrientation(int tID) {
            if (!IsTriangle(tID))
                return MeshResult.Failed_NotATriangle;
            Vector3i t = GetTriangle(tID);
            set_triangle(tID, t[1], t[0], t[2]);
            Vector3i te = GetTriEdges(tID);
            set_triangle_edges(tID, te[0], te[2], te[1]);
            return MeshResult.Ok;
        }


		public struct EdgeSplitInfo {
			public bool bIsBoundary;
			public int vNew;
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
		public MeshResult SplitEdge(int eab, out EdgeSplitInfo split)
		{
			split = new EdgeSplitInfo();

			if (! IsEdge(eab) )
				return MeshResult.Failed_NotAnEdge;

			//Edge * eAB = & m_vEdges[eab];

			// look up primary edge & triangle
			int a = edges[4 * eab], b = edges[4 * eab + 1];
			int t0 = edges[4 * eab + 2];
			Vector3i T0tv = GetTriangle(t0);
			int[] T0tv_array = T0tv.array;
			int c = IndexUtil.orient_tri_edge_and_find_other_vtx(ref a, ref b, T0tv_array);

			//Triangle & T0 = m_vTriangles[t0];

			// create new vertex
			Vector3d vNew = 0.5 * ( GetVertex(a) + GetVertex(b) );
			int f = AppendVertex( vNew );

			// quite a bit of code is duplicated between boundary and non-boundary case, but it
			//  is too hard to follow later if we factor it out...
			if ( edge_is_boundary(eab) ) {

				// look up edge bc, which needs to be modified
				Vector3i T0te = GetTriEdges(t0);
				int ebc = T0te[ IndexUtil.find_edge_index_in_tri(b, c, T0tv_array) ];
				//Edge & eBC = m_vEdges[ebc];

				// rewrite existing triangle
				replace_tri_vertex(t0, b, f);

				// add new second triangle
				int t2 = add_triangle_only(f,b,c, InvalidID, InvalidID, InvalidID);
				if ( triangle_groups != null )
					triangle_groups.insert(triangle_groups[t0], t2);
				
				//Triangle & T2 = m_vTriangles[t2];

				// rewrite edge bc, create edge af
				replace_edge_triangle(ebc, t0, t2);
				int eaf = eab; 
				//Edge * eAF = eAB;
				replace_edge_vertex(eaf, b, f);
				vertex_edges[b].Remove(eab);
				vertex_edges[f].Add(eaf);

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

				updateTimeStamp();
				return MeshResult.Ok;

			} else {		// interior triangle branch
				
				// look up other triangle
				int t1 = edges[4 * eab + 3];
				//Triangle & T1 = m_vTriangles[t1];
				Vector3i T1tv = GetTriangle(t1);
				int[] T1tv_array = T1tv.array;
				int d = IndexUtil.find_tri_other_vtx( a, b, T1tv_array );

				// look up edges that we are going to need to update
				// [TODO OPT] could use ordering to reduce # of compares here
				Vector3i T0te = GetTriEdges(t0);
				int ebc = T0te[IndexUtil.find_edge_index_in_tri( b, c, T0tv_array )];
				//Edge & eBC = m_vEdges[ebc];
				Vector3i T1te = GetTriEdges(t1);
				int edb = T1te[IndexUtil.find_edge_index_in_tri( d, b, T1tv_array )];
				//Edge & eDB = m_vEdges[edb];

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
				
				//Triangle & T2 = m_vTriangles[t2];
				//Triangle & T3 = m_vTriangles[t3];

				// update the edges we found above, to point to new triangles
				replace_edge_triangle(ebc, t0, t2);
				replace_edge_triangle(edb, t1, t3);

				// edge eab became eaf
				int eaf = eab; //Edge * eAF = eAB;
				replace_edge_vertex(eaf, b, f);

				// update a/b/f vertex-edges
				vertex_edges[b].Remove( eab );
				vertex_edges[f].Add( eaf );

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

				updateTimeStamp();
				return MeshResult.Ok;
			}

		}





        // debug

        public void DMESH_CHECK_OR_FAIL(bool b) { Util.gDevAssert(b); }

        // This function checks that the mesh is well-formed, ie all internal data
        // structures are consistent
        public bool CheckValidity() {

            int[] triToVtxRefs = new int[GetMaxVertexID()];

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
                Vector3i tv = GetTriangle(tID);
                for (int j = 0; j < 3; ++j) {
                    DMESH_CHECK_OR_FAIL(IsVertex(tv[j]));
                    triToVtxRefs[tv[j]] += 1;
                }

                // edges must exist and reference this tri
                Vector3i e = new Vector3i();
                for (int j = 0; j < 3; ++j) {
                    int a = tv[j], b = tv[(j + 1) % 3];
                    e[j] = FindEdge(a, b);
                    DMESH_CHECK_OR_FAIL(e[j] != InvalidID);
                    DMESH_CHECK_OR_FAIL(edge_has_t(e[j], tID));
                }
                DMESH_CHECK_OR_FAIL(e[0] != e[1] && e[0] != e[2] && e[1] != e[2]);

                // tri nbrs must exist and reference this tri, or same edge must be boundary edge
                Vector3i te = GetTriEdges(tID);
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
                    Vector2i ev = GetEdgeV(te[j]);
                    DMESH_CHECK_OR_FAIL(IndexUtil.same_pair_unordered(a, b, ev[0], ev[1]));

                    // also check that nbr edge has opposite orientation
                    Vector3i othertv = GetTriangle(tOther);
                    int found = IndexUtil.find_tri_ordered_edge(b, a, othertv.array);
                    DMESH_CHECK_OR_FAIL(found != InvalidID);
                }
            }


            // edge verts/tris must exist
            foreach (int eID in EdgeIndices() ) { 
                DMESH_CHECK_OR_FAIL(IsEdge(eID));
                DMESH_CHECK_OR_FAIL(edges_refcount.refCount(eID) == 1);
                Vector2i ev = GetEdgeV(eID);
                Vector2i et = GetEdgeT(eID);
                DMESH_CHECK_OR_FAIL(IsVertex(ev[0]));
                DMESH_CHECK_OR_FAIL(IsVertex(ev[1]));
                DMESH_CHECK_OR_FAIL(ev[0] < ev[1]);
                DMESH_CHECK_OR_FAIL(IsTriangle(et[0]));
                if (et[1] != InvalidID) {
                    DMESH_CHECK_OR_FAIL(IsTriangle(et[1]));
                }
            }

            // vertex edges must exist and reference this vert
            foreach( int vID in VertexIndices()) { 
                DMESH_CHECK_OR_FAIL(IsVertex(vID));
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

                List<int> vTris = new List<int>();
                GetVtxTriangles(vID, vTris, false);
                DMESH_CHECK_OR_FAIL(vertices_refcount.refCount(vID) == vTris.Count + 1);
                DMESH_CHECK_OR_FAIL(triToVtxRefs[vID] == vTris.Count);
                foreach( int tID in vTris) {
                    DMESH_CHECK_OR_FAIL(tri_has_v(tID, vID));
                }
            }
            return true;
        }
        void check_or_fail(bool bCondition) {
            Util.gDevAssert(bCondition);
        }
	}
}

