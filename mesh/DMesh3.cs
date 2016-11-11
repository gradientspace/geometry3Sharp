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



    public class DMesh3 : IMesh
    {
        public const int InvalidID = -1;
        public const int NonManifoldID = -2;


        public static readonly Vector3d InvalidVertex = new Vector3d(Double.MaxValue, 0, 0);
        public static readonly Vector3i InvalidTriangle = new Vector3i(InvalidID, InvalidID, InvalidID);
        public static readonly Vector2i InvalidEdge = new Vector2i(InvalidID, InvalidID);


        RefCountVector vertices_refcount;
        DVector<double> vertices;
        // [TODO] this seems like it will not be efficient! 
        //   do our own short_list backed my a memory pool?
        // [TODO] this is optional if we only want to use this class as an iterable mesh-with-nbrs
        //   make it optional with a flag? (however find_edge depends on it...)
        DVector<List<int>> vertex_edges;

        RefCountVector triangles_refcount;
        DVector<int> triangles;
        DVector<int> triangle_edges;

        RefCountVector edges_refcount;
        DVector<int> edges;



        public DMesh3()
        {
            vertices = new DVector<double>();
            vertex_edges = new DVector<List<int>>();
            vertices_refcount = new RefCountVector();

            triangles = new DVector<int>();
            triangle_edges = new DVector<int>();
            triangles_refcount = new RefCountVector();

            edges = new DVector<int>();
            edges_refcount = new RefCountVector();
        }



        // IMesh impl

        public int VertexCount {
            get { return vertices_refcount.count; }
        }
        public int TriangleCount {
            get { return triangles_refcount.count; }
        }

        public bool HasVertexColors { get { return false; } }
        public bool HasVertexNormals { get { return false; } }
        public bool HasVertexUVs { get { return false; } }

        public Vector3d GetVertexNormal(int i) { return Vector3d.AxisY; }
        public Vector3d GetVertexColor(int i) { return Vector3d.Zero; }
        public Vector2f GetVertexUV(int i) { return Vector2f.Zero; }

        public bool HasTriangleGroups { get { return false; } }
        public int GetTriangleGroup(int i) { return 0; }

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

            // TODO normals, colors

            vertex_edges.insert(new List<int>(), vid);

            return vid;
        }


        public int AppendTriangle(int v0, int v1, int v2) {
            return AppendTriangle(new Vector3i(v0, v1, v2));
        }
        public int AppendTriangle(Vector3i tv) {
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
            int c = IndexUtil.orient_tri_edge_and_find_other_vtx(ref a, ref b, GetTriangle(t0).v);
            if (t1 != InvalidID) {
                int d = IndexUtil.find_tri_other_vtx(a, b, GetTriangle(t1).v);
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

        int find_edge(int vA, int vB)
        {
            int vO = Math.Max(vA, vB);
            List<int> e0 = vertex_edges[Math.Min(vA, vB)];
            int idx = e0.FindIndex((x) => edge_has_v(x, vO));
            return (idx == -1) ? InvalidID : e0[idx];
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





        // debug

        public void DMESH_CHECK_OR_FAIL(bool b) { Util.gDevAssert(b); }

        // This function checks that the mesh is well-formed, ie all internal data
        // structures are consistent
        public bool CheckValidity() {

            int[] triToVtxRefs = new int[GetMaxVertexID()];

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
                    int found = IndexUtil.find_tri_ordered_edge(b, a, othertv.v);
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

