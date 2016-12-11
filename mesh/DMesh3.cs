using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Diagnostics;

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


        public bool tri_has_v(int tID, int vID) {
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

		//! returns edge ID
		public int find_tri_neighbour_edge(int tID, int vA, int vB)
		{
			int tv0 = triangles[3*tID], tv1 = triangles[3*tID+1];
			if ( IndexUtil.same_pair_unordered(tv0, tv1, vA, vB) ) return triangle_edges[3*tID];
			int tv2 = triangles[3*tID+2];
			if ( IndexUtil.same_pair_unordered(tv1, tv2, vA, vB) ) return triangle_edges[3*tID+1];
			if ( IndexUtil.same_pair_unordered(tv2, tv0, vA, vB) ) return triangle_edges[3*tID+2];
			return InvalidID;	
		}

		// returns 0/1/2
		public int find_tri_neighbour_index(int tID, int vA, int vB)
		{
			int tv0 = triangles[3*tID], tv1 = triangles[3*tID+1];
			if ( IndexUtil.same_pair_unordered(tv0, tv1, vA, vB) ) return 0;
			int tv2 = triangles[3*tID+2];
			if ( IndexUtil.same_pair_unordered(tv1, tv2, vA, vB) ) return 1;
			if ( IndexUtil.same_pair_unordered(tv2, tv0, vA, vB) ) return 2;
			return InvalidID;	
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


		public bool vertex_is_boundary(int vid) {
			foreach ( int e in vertex_edges[vid] )
				if ( edge_is_boundary(e) )
					return true;
			return false;
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

		int replace_tri_vertex(int tID, int vOld, int vNew) {
			if ( triangles[3 * tID] == vOld ) { triangles[3 * tID] = vNew; return 0; }
			if ( triangles[3 * tID+1] == vOld ) { triangles[3 * tID+1] = vNew; return 1; }
			if ( triangles[3 * tID+2] == vOld ) { triangles[3 * tID+2] = vNew; return 2; }
			return -1;
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
		void set_edge_triangles(int eID, int t0, int t1) {
			edges[4 * eID + 2] = t0;
			edges[4 * eID + 3] = t1;
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

			// look up primary edge & triangle
			int a = edges[4 * eab], b = edges[4 * eab + 1];
			int t0 = edges[4 * eab + 2];
			Vector3i T0tv = GetTriangle(t0);
			int[] T0tv_array = T0tv.array;
			int c = IndexUtil.orient_tri_edge_and_find_other_vtx(ref a, ref b, T0tv_array);

			// create new vertex
			Vector3d vNew = 0.5 * ( GetVertex(a) + GetVertex(b) );
			int f = AppendVertex( vNew );

			// quite a bit of code is duplicated between boundary and non-boundary case, but it
			//  is too hard to follow later if we factor it out...
			if ( edge_is_boundary(eab) ) {

				// look up edge bc, which needs to be modified
				Vector3i T0te = GetTriEdges(t0);
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
				Vector3i T1tv = GetTriangle(t1);
				int[] T1tv_array = T1tv.array;
				int d = IndexUtil.find_tri_other_vtx( a, b, T1tv_array );

				// look up edges that we are going to need to update
				// [TODO OPT] could use ordering to reduce # of compares here
				Vector3i T0te = GetTriEdges(t0);
				int ebc = T0te[IndexUtil.find_edge_index_in_tri( b, c, T0tv_array )];
				Vector3i T1te = GetTriEdges(t1);
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
			if ( edge_is_boundary(eab) )
				return MeshResult.Failed_IsBoundaryEdge;

			// find oriented edge [a,b], tris t0,t1, and other verts c in t0, d in t1
			int a = edges[4 * eab], b = edges[4 * eab + 1];
			int t0 = edges[4 * eab + 2], t1 = edges[4 * eab + 3];
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
			if ( vertex_edges[a].Remove(eab) == false ) 
				throw new ArgumentException("DMesh3.FlipEdge: first vertex_edges remove failed");
			if ( vertex_edges[b].Remove(eab) == false ) 
				throw new ArgumentException("DMesh3.FlipEdge: second vertex_edges remove failed");
			vertices_refcount.decrement(a);
			vertices_refcount.decrement(b);
			if ( IsVertex(a) == false || IsVertex(b) == false )
				throw new ArgumentException("DMesh3.FlipEdge: either a or b is not a vertex?");

			// add new edge ecd to verts c and d, and increment ref counts
			vertex_edges[c].Add(ecd);
			vertex_edges[d].Add(ecd);
			vertices_refcount.increment(c);
			vertices_refcount.increment(d);

			// success! collect up results
			flip.eID = eab;
			flip.v0 = a; flip.v1 = b;
			flip.ov0 = c; flip.ov1 = d;
			flip.t0 = t0; flip.t1 = t1;

			updateTimeStamp();
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
			Vector3i tv = GetTriangle(t);
			if ( tv[0] == tv[1] || tv[0] == tv[2] || tv[1] == tv[2] )
				Debug.Assert(false);
		}
		void check_edge(int e) {
			Vector2i tv = GetEdgeT(e);
			if ( tv[0] == -1 )
				Debug.Assert(false);
		}


		public struct EdgeCollapseInfo {
			public int vKept;
			public int vRemoved;
			public bool bIsBoundary;	
		}
		public MeshResult CollapseEdge(int vKeep, int vRemove, out EdgeCollapseInfo collapse)
		{
			collapse = new EdgeCollapseInfo();
				
			if ( IsVertex(vKeep) == false || IsVertex(vRemove) == false )
				return MeshResult.Failed_NotAnEdge;

			int b = vKeep;		// renaming for sanity. We remove a and keep b
			int a = vRemove;
			List<int> edges_b = vertex_edges[b];

			int eab = find_edge( a, b );
			if (eab == InvalidID)
				return MeshResult.Failed_NotAnEdge;

			int t0 = edges[4*eab+2];
			Vector3i T0tv = GetTriangle(t0);
			int c = IndexUtil.find_tri_other_vtx(a, b, T0tv);

			// look up opposing triangle/vtx if we are not in boundary case
			bool bIsBoundary = false;
			int d = InvalidID;
			int t1 = edges[4*eab+3];
			if (t1 != InvalidID) {
				Vector3i T1tv = GetTriangle(t1);
				d = IndexUtil.find_tri_other_vtx( a, b, T1tv );
				if (c == d)
					return MeshResult.Failed_FoundDuplicateTriangle;
			} else {
				bIsBoundary = true;
			}

			// We cannot collapse if edge lists of a and b share vertices other
			//  than c and d  (because then we will make a triangle [x b b].
			//  Unfortunately I cannot see a way to do this more efficiently than brute-force search
			//  [TODO] if we had tri iterator for a, couldn't we check each tri for b  (skipping t0 and t1) ?
			List<int> edges_a = vertex_edges[a];
			int edges_a_count = 0;
			foreach (int eid_a in edges_a) {
				int vax =  edge_other_v(eid_a, a);
				edges_a_count++;
				if ( vax == b || vax == c || vax == d )
					continue;
				foreach (int eid_b in edges_b) {
					if ( edge_other_v(eid_b, b) == vax )
						return MeshResult.Failed_InvalidNeighbourhood;
				}
			}


			// We cannot collapse if we have a tetrahedron. In this case a has 3 nbr edges,
			//  and edge cd exists. But that is not conclusive, we also have to check that
			//  cd is an internal edge, and that each of its tris contain a or b
			if (edges_a_count == 3 && bIsBoundary == false) {
				int edc = find_edge( d, c );
				if (edc != InvalidID && edges[4*edc+3] != InvalidID ) {
					int edc_t0 = edges[4*edc+2];
					int edc_t1 = edges[4*edc+3];

				    if ( (tri_has_v(edc_t0,a) && tri_has_v(edc_t1, b)) 
					    || (tri_has_v(edc_t0, b) && tri_has_v(edc_t1, a)) )
					return MeshResult.Failed_CollapseTetrahedron;
				}

			} else if (edges_a_count == 2 && bIsBoundary == true) {
				// cannot collapse edge if we are down to a single triangle
				if ( edges_b.Count == 2 && vertex_edges[c].Count == 2 )
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
			if ( vertex_is_boundary(a) && vertex_is_boundary(b) && d != InvalidID )
				return MeshResult.Failed_InvalidNeighbourhood;



			// 1) remove edge ab from vtx b
			// 2) find edges ad and ac, and tris tad, tac across those edges  (will use later)
			// 3) for other edges, replace a with b, and add that edge to b
			// 4) replace a with b in all triangles connected to a
			int ead = InvalidID, eac = InvalidID;
			int tad = InvalidID, tac = InvalidID;
			foreach (int eid in edges_a) {
				int o = edge_other_v(eid, a);
				if (o == b) {
					if (edges_b.Remove(eid) != true )
						debug_fail("remove case o == b");
				} else if (o == c) {
					eac = eid;
					if ( vertex_edges[c].Remove(eid) != true )
						debug_fail("remove case o == c");
					tac = edge_other_t(eid, t0);
				} else if (o == d) {
					ead = eid;
					if ( vertex_edges[d].Remove(eid) != true )
						debug_fail("remove case o == c, step 1");
					tad = edge_other_t(eid, t1);
				} else {
					if ( replace_edge_vertex(eid, a, b) == -1 )
						debug_fail("remove case else");
					edges_b.Add(eid);
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

			if (bIsBoundary == false) {

				// remove all edges from vtx a, then remove vtx a
				edges_a.Clear();
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
				int ebd = find_edge( b, d );
				int ebc = find_edge( b, c );

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

				//  this is basically same code as above, just not referencing t0/d

				// remove all edges from vtx a, then remove vtx a
				edges_a.Clear();
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
				int ebc = find_edge( b, c );
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
			collapse.bIsBoundary = bIsBoundary;

			updateTimeStamp();
			return MeshResult.Ok;
		}







		public void debug_print_vertex(int v) {
			System.Console.WriteLine("Vertex " + v.ToString());
			List<int> tris = new List<int>();
			GetVtxTriangles(v, tris, false);
			System.Console.WriteLine(string.Format("  Tris {0}  Edges {1}  refcount {2}", tris.Count, GetVtxEdges(v).Count, vertices_refcount.refCount(v) ));
			foreach ( int t in tris ) {
				Vector3i tv = GetTriangle(t), te = GetTriEdges(t);
				System.Console.WriteLine(string.Format("  t{6} {0} {1} {2}   te {3} {4} {5}", tv[0],tv[1],tv[2], te[0],te[1],te[2],t));
			}
			foreach ( int e in GetVtxEdges(v) ) {
				Vector2i ev = GetEdgeV(e), et = GetEdgeT(e);
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

				List<int> vTris = new List<int>(), vTris2 = new List<int>();
                GetVtxTriangles(vID, vTris, false);
				GetVtxTriangles(vID, vTris2, true);
				DMESH_CHECK_OR_FAIL(vTris.Count == vTris2.Count);
				//System.Console.WriteLine(string.Format("{0} {1} {2}", vID, vTris.Count, GetVtxEdges(vID).Count));
				DMESH_CHECK_OR_FAIL(vTris.Count == GetVtxEdges(vID).Count || vTris.Count == GetVtxEdges(vID).Count-1);
                DMESH_CHECK_OR_FAIL(vertices_refcount.refCount(vID) == vTris.Count + 1);
                DMESH_CHECK_OR_FAIL(triToVtxRefs[vID] == vTris.Count);
                foreach( int tID in vTris) {
                    DMESH_CHECK_OR_FAIL(tri_has_v(tID, vID));
                }

				// check that edges around vert only references tris above, and reference all of them!
				List<int> vRemoveTris = new List<int>(vTris);
				foreach ( int edgeid in l ) {
					Vector2i edget = GetEdgeT(edgeid);
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

