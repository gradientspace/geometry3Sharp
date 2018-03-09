using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    /// <summary>
    /// Remove triangles from mesh and store necessary data to be able to reverse the change.
    /// Vertex and Triangle IDs will be restored on Revert()
    /// Currently does *not* restore the same EdgeIDs
    /// </summary>
    public class RemoveTrianglesMeshChange
    {
        protected DVector<int> RemovedV;
        protected DVector<Vector3d> Positions;
        protected DVector<Vector3f> Normals;
        protected DVector<Vector3f> Colors;
        protected DVector<Vector2f> UVs;

        protected DVector<int> RemovedT;
        protected DVector<Index4i> Triangles;
        
        public RemoveTrianglesMeshChange()
        {
        }


        public void Initialize(DMesh3 mesh, IEnumerable<int> triangles)
        {
            initialize_buffers(mesh);
            bool has_groups = mesh.HasTriangleGroups;


            foreach ( int tid in triangles ) { 
                if (!mesh.IsTriangle(tid))
                    continue;

                Index3i tv = mesh.GetTriangle(tid);
                bool va = save_vertex(mesh, tv.a);
                bool vb = save_vertex(mesh, tv.b);
                bool vc = save_vertex(mesh, tv.c);

                Index4i tri = new Index4i(tv.a, tv.b, tv.c,
                    has_groups ? mesh.GetTriangleGroup(tid) : DMesh3.InvalidID);
                RemovedT.Add(tid);
                Triangles.Add(tri);

                MeshResult result = mesh.RemoveTriangle(tid, true, false);
                if (result != MeshResult.Ok)
                    throw new Exception("RemoveTrianglesMeshChange.Initialize: exception in RemoveTriangle(" + tid.ToString() + "): " + result.ToString());
                Util.gDevAssert(mesh.IsVertex(tv.a) == va && mesh.IsVertex(tv.b) == vb && mesh.IsVertex(tv.c) == vc);
            }
        }


        public void Apply(DMesh3 mesh)
        {
            int N = RemovedT.size;
            for ( int i = 0; i< N; ++i) {
                int tid = RemovedT[i];
                MeshResult result = mesh.RemoveTriangle(RemovedT[i], true, false);
                if (result != MeshResult.Ok)
                    throw new Exception("RemoveTrianglesMeshChange.Apply: error in RemoveTriangle(" + tid.ToString() + "): " + result.ToString());
            }
        }


        public void Revert(DMesh3 mesh)
        {
            int NV = RemovedV.size;
            if (NV > 0) {
                NewVertexInfo vinfo = new NewVertexInfo(Positions[0]);
                mesh.BeginUnsafeVerticesInsert();
                for (int i = 0; i < NV; ++i) {
                    int vid = RemovedV[i];
                    vinfo.v = Positions[i];
                    if (Normals != null) { vinfo.bHaveN = true; vinfo.n = Normals[i]; }
                    if (Colors != null) { vinfo.bHaveC = true; vinfo.c = Colors[i]; }
                    if (UVs != null) { vinfo.bHaveUV = true; vinfo.uv = UVs[i]; }
                    MeshResult result = mesh.InsertVertex(vid, ref vinfo, true);
                    if (result != MeshResult.Ok)
                        throw new Exception("RemoveTrianglesMeshChange.Revert: error in InsertVertex(" + vid.ToString() + "): " + result.ToString());
                }
                mesh.EndUnsafeVerticesInsert();
            }

            int NT = RemovedT.size;
            if (NT > 0) {
                mesh.BeginUnsafeTrianglesInsert();
                for (int i = 0; i < NT; ++i) {
                    int tid = RemovedT[i];
                    Index4i tdata = Triangles[i];
                    Index3i tri = new Index3i(tdata.a, tdata.b, tdata.c);
                    MeshResult result = mesh.InsertTriangle(tid, tri, tdata.d, true);
                    if (result != MeshResult.Ok)
                        throw new Exception("RemoveTrianglesMeshChange.Revert: error in InsertTriangle(" + tid.ToString() + "): " + result.ToString());
                }
                mesh.EndUnsafeTrianglesInsert();
            }
        }




        bool save_vertex(DMesh3 mesh, int vid)
        {
            if ( mesh.VerticesRefCounts.refCount(vid) == 2 ) {
                RemovedV.Add(vid);
                Positions.Add(mesh.GetVertex(vid));
                if (Normals != null)
                    Normals.Add(mesh.GetVertexNormal(vid));
                if (Colors != null)
                    Colors.Add(mesh.GetVertexColor(vid));
                if (UVs != null)
                    UVs.Add(mesh.GetVertexUV(vid));
                return false;
            }
            return true;
        }


        void initialize_buffers(DMesh3 mesh)
        {
            RemovedV = new DVector<int>();
            Positions = new DVector<Vector3d>();
            if (mesh.HasVertexNormals)
                Normals = new DVector<Vector3f>();
            if (mesh.HasVertexColors)
                Colors = new DVector<Vector3f>();
            if (mesh.HasVertexUVs)
                UVs = new DVector<Vector2f>();

            RemovedT = new DVector<int>();
            Triangles = new DVector<Index4i>();
        }

    }

}
