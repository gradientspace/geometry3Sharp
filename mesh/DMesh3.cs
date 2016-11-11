using System;
using System.Collections;
using System.Collections.Generic;

namespace g3
{
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
        public IReadOnlyCollection<int> GetVtxEdges(int vID) {
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
                new Vector2i(edges[4 * eID+2], edges[4 * eID + 3]) : InvalidEdge;
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
            return AppendTriangle(new Vector3i(v0,v1,v2));
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
        void add_tri_edge(int tid, int v0, int v1, int j, int eid )
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






        // internal
        bool edge_is_boundary(int eid) {
            return edges[4 * eid + 3] == InvalidID;
        }
        bool edge_has_v(int eid, int vid) {
            return (edges[4 * eid] == vid) || (edges[4 * eid + 1] == vid);
        }

        int add_edge(int vA, int vB, int tA, int tB = InvalidID)
        {
            if ( vB < vA ) {
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




        public bool CheckValidity() {
            // [TODO] port this (but lots of code to add to do it)
            return false;
        }
        void check_or_fail(bool bCondition) {
            Util.gDevAssert(bCondition);
        }
	}
}

