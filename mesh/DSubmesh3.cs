using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace g3
{




    public class DSubmesh3
    {
        public DMesh3 BaseMesh;
        public DMesh3 SubMesh;
        public MeshComponents WantComponents = MeshComponents.All;
        public bool ComputeTriMaps = false;
        public int OverrideGroupID = -1;
        
        public IndexFlagSet BaseSubmeshV;       // vertices in base mesh that are in submesh
                                                // (we compute this anyway, might as well hang onto it)

        public IndexMap BaseToSubV;             // vertex index map from base to submesh 
        public DVector<int> SubToBaseV;         // vertex index map from submesh to base mesh

        public IndexMap BaseToSubT;             // triangle index map from base to submesh. Only computed if ComputeTriMaps = true.
        public DVector<int> SubToBaseT;         // triangle index map from submesh to base mesh. Only computed if ComputeTriMaps = true.

        // boundary info
        public IndexHashSet BaseBorderE;        // list of internal border edge indices on base mesh. Does not include mesh boundary edges.
        public IndexHashSet BaseBoundaryE;      // list of mesh-boundary edges on base mesh that are in submesh
        public IndexHashSet BaseBorderV;        // list of border vertex indices on base mesh (ie verts of BaseBorderE - does not include mesh boundary vertices)


        public DSubmesh3(DMesh3 mesh, int[] subTriangles)
        {
            BaseMesh = mesh;
            compute(subTriangles, subTriangles.Length);
        }
        public DSubmesh3(DMesh3 mesh, IEnumerable<int> subTriangles, int nTriEstimate = 0)
        {
            BaseMesh = mesh;
            compute(subTriangles, nTriEstimate);
        }

        public DSubmesh3(DMesh3 mesh)
        {
            BaseMesh = mesh;
        }
        public void Compute(int[] subTriangles) {
            compute(subTriangles, subTriangles.Length);
        }
        public void Compute(IEnumerable<int> subTriangles, int nTriEstimate = 0) {
            compute(subTriangles, nTriEstimate);
        }

        public int MapVertexToSubmesh(int base_vID) {
            return BaseToSubV[base_vID];
        }
        public int MapVertexToBaseMesh(int sub_vID) {
            if (sub_vID < SubToBaseV.Length)
                return SubToBaseV[sub_vID];
            return DMesh3.InvalidID;
        }

        public Index2i MapVerticesToSubmesh(Index2i v) {
            return new Index2i(BaseToSubV[v.a], BaseToSubV[v.b]);
        }
        public Index2i MapVerticesToBaseMesh(Index2i v) {
            return new Index2i(MapVertexToBaseMesh(v.a), MapVertexToBaseMesh(v.b));
        }

        public void MapVerticesToSubmesh(int[] vertices)
        {
            for (int i = 0; i < vertices.Length; ++i)
                vertices[i] = BaseToSubV[vertices[i]];
        }


        public int MapEdgeToSubmesh(int base_eid)
        {
            Index2i base_ev = BaseMesh.GetEdgeV(base_eid);
            Index2i sub_ev = MapVerticesToSubmesh(base_ev);
            return SubMesh.FindEdge(sub_ev.a, sub_ev.b);
        }
        public void MapEdgesToSubmesh(int[] edges)
        {
            for (int i = 0; i < edges.Length; ++i)
                edges[i] = MapEdgeToSubmesh(edges[i]);
        }

        public int MapEdgeToBaseMesh(int sub_eid)
        {
            Index2i sub_ev = SubMesh.GetEdgeV(sub_eid);
            Index2i base_ev = MapVerticesToBaseMesh(sub_ev);
            return BaseMesh.FindEdge(base_ev.a, base_ev.b);
        }


        public int MapTriangleToSubmesh(int base_tID)
        {
            if (ComputeTriMaps == false)
                throw new InvalidOperationException("DSubmesh3.MapTriangleToSubmesh: must set ComputeTriMaps = true!");
            return BaseToSubT[base_tID];
        }
        public int MapTriangleToBaseMesh(int sub_tID)
        {
            if (ComputeTriMaps == false)
                throw new InvalidOperationException("DSubmesh3.MapTriangleToBaseMesh: must set ComputeTriMaps = true!");
            if (sub_tID < SubToBaseT.Length)
                return SubToBaseT[sub_tID];
            return DMesh3.InvalidID;
        }

        public void MapTrianglesToSubmesh(int[] triangles)
        {
            if (ComputeTriMaps == false)
                throw new InvalidOperationException("DSubmesh3.MapTrianglesToSubmesh: must set ComputeTriMaps = true!");
            for (int i = 0; i < triangles.Length; ++i)
                triangles[i] = BaseToSubT[triangles[i]];
        }




        public void ComputeBoundaryInfo(int[] subTriangles) {
            ComputeBoundaryInfo(subTriangles, subTriangles.Length);
        }
        public void ComputeBoundaryInfo(IEnumerable<int> triangles, int tri_count_est)
        {
            // set of base-mesh triangles that are in submesh
            IndexFlagSet sub_tris = new IndexFlagSet(BaseMesh.MaxTriangleID, tri_count_est);
            foreach (int ti in triangles)
                sub_tris[ti] = true;

            BaseBorderV = new IndexHashSet();
            BaseBorderE = new IndexHashSet();
            BaseBoundaryE = new IndexHashSet();

            // Iterate through edges in submesh roi on base mesh. If
            // one of the tris of the edge is not in submesh roi, then this
            // is a boundary edge.
            //
            // (edge iteration via triangle iteration processes each internal edge twice...)
            foreach (int ti in triangles) {
                Index3i tedges = BaseMesh.GetTriEdges(ti);
                for ( int j = 0; j < 3; ++j ) {
                    int eid = tedges[j];
                    Index2i tris = BaseMesh.GetEdgeT(eid);
                    if ( tris.b == DMesh3.InvalidID ) {     // this is a boundary edge
                        BaseBoundaryE[eid] = true;

                    } else if (sub_tris[tris.a] != sub_tris[tris.b]) {  // this is a border edge
                        BaseBorderE[eid] = true;
                        Index2i ve = BaseMesh.GetEdgeV(eid);
                        BaseBorderV[ve.a] = true;
                        BaseBorderV[ve.b] = true;
                    } 
                }
            }
            
        }



        // [RMS] estimate can be zero
        void compute(IEnumerable<int> triangles, int tri_count_est )
        {
            int est_verts = tri_count_est / 2;

            SubMesh = new DMesh3( BaseMesh.Components & WantComponents );

            BaseSubmeshV = new IndexFlagSet(BaseMesh.MaxVertexID, est_verts);
            BaseToSubV = new IndexMap(BaseMesh.MaxVertexID, est_verts);
            SubToBaseV = new DVector<int>();

            if ( ComputeTriMaps ) {
                BaseToSubT = new IndexMap(BaseMesh.MaxTriangleID, tri_count_est);
                SubToBaseT = new DVector<int>();
            }

            foreach ( int tid in triangles ) {
                if ( ! BaseMesh.IsTriangle(tid) )
                    throw new Exception("DSubmesh3.compute: triangle " + tid + " does not exist in BaseMesh!");
                Index3i base_t = BaseMesh.GetTriangle(tid);
                Index3i new_t = Index3i.Zero;
                int gid = BaseMesh.GetTriangleGroup(tid);

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

                if (OverrideGroupID >= 0)
                    gid = OverrideGroupID;
                int sub_tid = SubMesh.AppendTriangle(new_t, gid);

                if ( ComputeTriMaps ) {
                    BaseToSubT[tid] = sub_tid;
                    SubToBaseT.insert(tid, sub_tid);
                }
            }




        }





        public static DMesh3 QuickSubmesh(DMesh3 mesh, int[] triangles) {
            DSubmesh3 submesh = new DSubmesh3(mesh, triangles);
            return submesh.SubMesh;
        }
        public static DMesh3 QuickSubmesh(DMesh3 mesh, IEnumerable<int> triangles) {
            return QuickSubmesh(mesh, triangles.ToArray());
        }

    }
}
