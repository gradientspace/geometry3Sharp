using System;
using System.Collections;
using System.Collections.Generic;

namespace g3
{




    public class DSubmesh3
    {
        public DMesh3 BaseMesh;
        public DMesh3 SubMesh;
        public MeshComponents WantComponents = MeshComponents.All;

        public IndexFlagSet BaseSubmeshV;       // we compute this anyway, might as well hang onto it
        public IndexMap BaseToSubV;
        public DVector<int> SubToBaseV;


        // boundary info
        public IndexFlagSet BaseBorderV;


        public DSubmesh3(DMesh3 mesh, int[] subTriangles)
        {
            BaseMesh = mesh;
            compute(subTriangles, subTriangles.Length);
        }


        public void ComputeBoundaryInfo(int[] subTriangles) {
            ComputeBoundaryInfo(subTriangles, subTriangles.Length);
        }
        public void ComputeBoundaryInfo(IEnumerable<int> triangles, int tri_count)
        {
            IndexFlagSet sub_tris = new IndexFlagSet(BaseMesh.MaxTriangleID, tri_count);
            foreach (int ti in triangles)
                sub_tris[ti] = true;

            BaseBorderV = new IndexFlagSet(true, BaseMesh.MaxVertexID);

            // this processes each edge internal edge twice...but it is cheap
            foreach (int ti in triangles) {
                Index3i tedges = BaseMesh.GetTriEdges(ti);
                for ( int j = 0; j < 3; ++j ) {
                    int eid = tedges[j];
                    Index2i tris = BaseMesh.GetEdgeT(eid);
                    if (tris.b == DMesh3.InvalidID || sub_tris[tris.a] != sub_tris[tris.b]) { 
                        Index2i ve = BaseMesh.GetEdgeV(eid);
                        BaseBorderV[ve.a] = true;
                        BaseBorderV[ve.b] = true;
                    } 
                }
            }
            
        }




        void compute(IEnumerable<int> triangles, int tri_count )
        {
            int est_verts = tri_count / 2;

            SubMesh = new DMesh3( BaseMesh.Components & WantComponents );

            BaseSubmeshV = new IndexFlagSet(BaseMesh.MaxVertexID, est_verts);
            BaseToSubV = new IndexMap(BaseMesh.MaxVertexID, est_verts);
            SubToBaseV = new DVector<int>();

            foreach ( int ti in triangles ) {
                Index3i base_t = BaseMesh.GetTriangle(ti);
                Index3i new_t = Index3i.Zero;
                int gid = BaseMesh.GetTriangleGroup(ti);

                for ( int j = 0; j < 3; ++j ) {
                    int base_v = base_t[j];
                    int sub_v = -1;
                    if (BaseSubmeshV[base_v] == false) {
                        sub_v = SubMesh.AppendVertex(BaseMesh, base_v);
                        BaseSubmeshV[base_v] = true;
                        BaseToSubV[base_v] = sub_v;
                        SubToBaseV.insert(base_v, sub_v);
                    } else
                        sub_v = BaseToSubV[base_v];
                    new_t[j] = sub_v;
                }

                SubMesh.AppendTriangle(new_t, gid);
            }




        }




    }
}
