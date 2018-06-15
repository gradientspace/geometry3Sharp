using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    /// <summary>
    /// Mesh change for vertex deformations. Currently minimal support for initializing buffers.
    /// AppendNewVertex() can be used to accumulate modified vertices and their initial positions.
    /// </summary>
    public class ModifyVerticesMeshChange
    {
        public DVector<int> ModifiedV;
        public DVector<Vector3d> OldPositions, NewPositions;
        public DVector<Vector3f> OldNormals, NewNormals;
        public DVector<Vector3f> OldColors, NewColors;
        public DVector<Vector2f> OldUVs, NewUVs;

        public Action<ModifyVerticesMeshChange> OnApplyF;
        public Action<ModifyVerticesMeshChange> OnRevertF;

        public ModifyVerticesMeshChange(DMesh3 mesh, MeshComponents wantComponents = MeshComponents.All)
        {
            initialize_buffers(mesh, wantComponents);
        }


        public int AppendNewVertex(DMesh3 mesh, int vid)
        {
            int idx = ModifiedV.Length;
            ModifiedV.Add(vid);
            OldPositions.Add(mesh.GetVertex(vid));
            NewPositions.Add(OldPositions[idx]);
            if (NewNormals != null) {
                OldNormals.Add(mesh.GetVertexNormal(vid));
                NewNormals.Add(OldNormals[idx]);
            }
            if (NewColors != null) {
                OldColors.Add(mesh.GetVertexColor(vid));
                NewColors.Add(OldColors[idx]);
            }
            if (NewUVs != null) {
                OldUVs.Add(mesh.GetVertexUV(vid));
                NewUVs.Add(OldUVs[idx]);
            }
            return idx;
        }

        public void Apply(DMesh3 mesh)
        {
            int N = ModifiedV.size;
            for (int i = 0; i < N; ++i) {
                int vid = ModifiedV[i];
                mesh.SetVertex(vid, NewPositions[i]);
                if (NewNormals != null)
                    mesh.SetVertexNormal(vid, NewNormals[i]);
                if (NewColors != null)
                    mesh.SetVertexColor(vid, NewColors[i]);
                if (NewUVs != null)
                    mesh.SetVertexUV(vid, NewUVs[i]);
            }
            if (OnApplyF != null)
                OnApplyF(this);
        }


        public void Revert(DMesh3 mesh)
        {
            int N = ModifiedV.size;
            for (int i = 0; i < N; ++i) {
                int vid = ModifiedV[i];
                mesh.SetVertex(vid, OldPositions[i]);
                if (NewNormals != null)
                    mesh.SetVertexNormal(vid, OldNormals[i]);
                if (NewColors != null)
                    mesh.SetVertexColor(vid, OldColors[i]);
                if (NewUVs != null)
                    mesh.SetVertexUV(vid, OldUVs[i]);
            }
            if (OnRevertF != null)
                OnRevertF(this);
        }


        void initialize_buffers(DMesh3 mesh, MeshComponents components)
        {
            ModifiedV = new DVector<int>();
            NewPositions = new DVector<Vector3d>();
            OldPositions = new DVector<Vector3d>();
            if (mesh.HasVertexNormals && (components & MeshComponents.VertexNormals) != 0) {
                NewNormals = new DVector<Vector3f>();
                OldNormals = new DVector<Vector3f>();
            }
            if (mesh.HasVertexColors && (components & MeshComponents.VertexColors) != 0) {
                NewColors = new DVector<Vector3f>();
                OldColors = new DVector<Vector3f>();
            }
            if (mesh.HasVertexUVs && (components & MeshComponents.VertexUVs) != 0) {
                NewUVs = new DVector<Vector2f>();
                OldUVs = new DVector<Vector2f>();
            }
        }

    }








    /// <summary>
    /// Mesh change for full-mesh vertex deformations - more efficient than ModifyVerticesMeshChange.
    /// Note that this does not enforce that vertex count does not change!
    /// </summary>
    public class SetVerticesMeshChange
    {
        public DVector<double> OldPositions, NewPositions;
        public DVector<float> OldNormals, NewNormals;
        public DVector<float> OldColors, NewColors;
        public DVector<float> OldUVs, NewUVs;

        public Action<SetVerticesMeshChange> OnApplyF;
        public Action<SetVerticesMeshChange> OnRevertF;

        public SetVerticesMeshChange()
        {
        }

        public void Apply(DMesh3 mesh)
        {
            if ( NewPositions != null )
                mesh.VerticesBuffer.copy(NewPositions);
            if (mesh.HasVertexNormals && NewNormals != null)
                mesh.NormalsBuffer.copy(NewNormals);
            if (mesh.HasVertexColors&& NewColors != null)
                mesh.ColorsBuffer.copy(NewColors);
            if (mesh.HasVertexUVs && NewUVs != null)
                mesh.UVBuffer.copy(NewUVs);
            if (OnApplyF != null)
                OnApplyF(this);
        }


        public void Revert(DMesh3 mesh)
        {
            if ( OldPositions != null )
                mesh.VerticesBuffer.copy(OldPositions);
            if (mesh.HasVertexNormals && OldNormals != null)
                mesh.NormalsBuffer.copy(OldNormals);
            if (mesh.HasVertexColors && OldColors != null)
                mesh.ColorsBuffer.copy(OldColors);
            if (mesh.HasVertexUVs && OldUVs != null)
                mesh.UVBuffer.copy(OldUVs);
            if (OnRevertF != null)
                OnRevertF(this);
        }
    }












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

        public Action<IEnumerable<int>,IEnumerable<int>> OnApplyF;
        public Action<IEnumerable<int>, IEnumerable<int>> OnRevertF;

        public RemoveTrianglesMeshChange()
        {
        }


        public void InitializeFromApply(DMesh3 mesh, IEnumerable<int> triangles)
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



        public void InitializeFromExisting(DMesh3 mesh, IEnumerable<int> remove_t)
        {
            initialize_buffers(mesh);
            bool has_groups = mesh.HasTriangleGroups;

            HashSet<int> triangles = new HashSet<int>(remove_t);
            HashSet<int> vertices = new HashSet<int>();
            IndexUtil.TrianglesToVertices(mesh, remove_t, vertices);
            List<int> save_v = new List<int>();
            foreach ( int vid in vertices ) {
                bool all_contained = true;
                foreach ( int tid in mesh.VtxTrianglesItr(vid) ) {
                    if (triangles.Contains(tid) == false) {
                        all_contained = false;
                        break;
                    }
                }
                if (all_contained)
                    save_v.Add(vid);
            }

            foreach (int vid in save_v) {
                save_vertex(mesh, vid, true);
            }

            foreach (int tid in remove_t) {
                Util.gDevAssert(mesh.IsTriangle(tid));
                Index3i tv = mesh.GetTriangle(tid);
                Index4i tri = new Index4i(tv.a, tv.b, tv.c,
                    has_groups ? mesh.GetTriangleGroup(tid) : DMesh3.InvalidID);
                RemovedT.Add(tid);
                Triangles.Add(tri);
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

            if ( OnApplyF != null )
                OnApplyF(RemovedV, RemovedT);
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

            if (OnRevertF != null)
                OnRevertF(RemovedV, RemovedT);
        }




        bool save_vertex(DMesh3 mesh, int vid, bool force = false)
        {
            if ( force || mesh.VerticesRefCounts.refCount(vid) == 2 ) {
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








    /// <summary>
    /// Add triangles from mesh and store necessary data to be able to reverse the change.
    /// Vertex and Triangle IDs will be restored on Revert()
    /// Currently does *not* restore the same EdgeIDs
    /// </summary>
    public class AddTrianglesMeshChange
    {
        protected DVector<int> AddedV;
        protected DVector<Vector3d> Positions;
        protected DVector<Vector3f> Normals;
        protected DVector<Vector3f> Colors;
        protected DVector<Vector2f> UVs;

        protected DVector<int> AddedT;
        protected DVector<Index4i> Triangles;

        public Action<IEnumerable<int>, IEnumerable<int>> OnApplyF;
        public Action<IEnumerable<int>, IEnumerable<int>> OnRevertF;


        public AddTrianglesMeshChange()
        {
        }


        public void InitializeFromExisting(DMesh3 mesh, IEnumerable<int> added_v, IEnumerable<int> added_t)
        {
            initialize_buffers(mesh);
            bool has_groups = mesh.HasTriangleGroups;

            if (added_v != null) {
                foreach (int vid in added_v) {
                    Util.gDevAssert(mesh.IsVertex(vid));
                    append_vertex(mesh, vid);
                }
            }

            foreach (int tid in added_t) {
                Util.gDevAssert(mesh.IsTriangle(tid));

                Index3i tv = mesh.GetTriangle(tid);
                Index4i tri = new Index4i(tv.a, tv.b, tv.c,
                    has_groups ? mesh.GetTriangleGroup(tid) : DMesh3.InvalidID);
                AddedT.Add(tid);
                Triangles.Add(tri);
            }
        }


        public void Apply(DMesh3 mesh)
        {
            int NV = AddedV.size;
            if (NV > 0) {
                NewVertexInfo vinfo = new NewVertexInfo(Positions[0]);
                mesh.BeginUnsafeVerticesInsert();
                for (int i = 0; i < NV; ++i) {
                    int vid = AddedV[i];
                    vinfo.v = Positions[i];
                    if (Normals != null) { vinfo.bHaveN = true; vinfo.n = Normals[i]; }
                    if (Colors != null) { vinfo.bHaveC = true; vinfo.c = Colors[i]; }
                    if (UVs != null) { vinfo.bHaveUV = true; vinfo.uv = UVs[i]; }
                    MeshResult result = mesh.InsertVertex(vid, ref vinfo, true);
                    if (result != MeshResult.Ok)
                        throw new Exception("AddTrianglesMeshChange.Revert: error in InsertVertex(" + vid.ToString() + "): " + result.ToString());
                }
                mesh.EndUnsafeVerticesInsert();
            }

            int NT = AddedT.size;
            if (NT > 0) {
                mesh.BeginUnsafeTrianglesInsert();
                for (int i = 0; i < NT; ++i) {
                    int tid = AddedT[i];
                    Index4i tdata = Triangles[i];
                    Index3i tri = new Index3i(tdata.a, tdata.b, tdata.c);
                    MeshResult result = mesh.InsertTriangle(tid, tri, tdata.d, true);
                    if (result != MeshResult.Ok)
                        throw new Exception("AddTrianglesMeshChange.Revert: error in InsertTriangle(" + tid.ToString() + "): " + result.ToString());
                }
                mesh.EndUnsafeTrianglesInsert();
            }

            if (OnApplyF != null)
                OnApplyF(AddedV, AddedT);
        }


        public void Revert(DMesh3 mesh)
        {
            int N = AddedT.size;
            for (int i = 0; i < N; ++i) {
                int tid = AddedT[i];
                MeshResult result = mesh.RemoveTriangle(AddedT[i], true, false);
                if (result != MeshResult.Ok)
                    throw new Exception("AddTrianglesMeshChange.Apply: error in RemoveTriangle(" + tid.ToString() + "): " + result.ToString());
            }

            if ( OnRevertF != null )
                OnRevertF(AddedV, AddedT);
        }




        void append_vertex(DMesh3 mesh, int vid)
        {
            AddedV.Add(vid);
            Positions.Add(mesh.GetVertex(vid));
            if (Normals != null)
                Normals.Add(mesh.GetVertexNormal(vid));
            if (Colors != null)
                Colors.Add(mesh.GetVertexColor(vid));
            if (UVs != null)
                UVs.Add(mesh.GetVertexUV(vid));
        }


        void initialize_buffers(DMesh3 mesh)
        {
            AddedV = new DVector<int>();
            Positions = new DVector<Vector3d>();
            if (mesh.HasVertexNormals)
                Normals = new DVector<Vector3f>();
            if (mesh.HasVertexColors)
                Colors = new DVector<Vector3f>();
            if (mesh.HasVertexUVs)
                UVs = new DVector<Vector2f>();

            AddedT = new DVector<int>();
            Triangles = new DVector<Index4i>();
        }

    }




}
