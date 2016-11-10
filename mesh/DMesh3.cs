using System;

namespace g3
{
    public class DMesh3
	{
        public static readonly int InvalidID = int.MaxValue;

        RefCountVector vertices_refcount;
        DVector<double> vertices;

        RefCountVector triangles_refcount;
        DVector<int> triangles;


        RefCountVector edges_refcount;
        DVector<int> edges;

            

        public DMesh3()
        {
            vertices = new DVector<double>();
            vertices_refcount = new RefCountVector();

            triangles = new DVector<int>();
            triangles_refcount = new RefCountVector();

            edges = new DVector<int>();
            edges_refcount = new RefCountVector();
        }




        public int AppendVertex(Vector3d v) {
            int vid = vertices_refcount.allocate();
            vertices.insert(v[2], 3 * vid + 2);
            vertices.insert(v[1], 3 * vid + 1);
            vertices.insert(v[0], 3 * vid);

            // TODO normals, colors, vtx-edges

            return vid;
        }


        public int AppendTriangle(int v0, int v1, int v2) {
            return AppendTriangle(new Vector3i(v0,v1,v2));
        }
        public int AppendTriangle(Vector3i tv) {
            // [TODO] check vertices existence

            // check for duplicate vertices
            if (tv[0] == tv[1] || tv[0] == tv[2] || tv[1] == tv[2]) {
                Util.gDevAssert(false);
                return InvalidID;
            }

            // TODO check for non-manifold edges

            // now safe to insert triangle
            int tid = triangles_refcount.allocate();
            triangles.insert(tv[2], 3 * tid + 2);
            triangles.insert(tv[1], 3 * tid + 1);
            triangles.insert(tv[0], 3 * tid);

            // increment ref counts and update/create edges
            for (int j = 0; j < 3; ++j) {
                vertices_refcount.increment(tv[j]);

                // TODO edges
            }

            return tid;

                
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

