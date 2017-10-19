using System;
using System.Collections.Generic;
using System.Linq;

namespace g3
{
    /// <summary>
    /// Extrude all faces of a mesh, and stitch together any boundary loops. Steps are:
    /// 1) make a copy of all triangles in mesh
    /// 2) offset copy vertices
    /// 3) connect up loops with triangle strips
    /// </summary>
    public class MeshExtrudeMesh
    {
        public DMesh3 Mesh;

        // arguments

        public SetGroupBehavior OffsetGroup = SetGroupBehavior.AutoGenerate;
        public SetGroupBehavior StitchGroups = SetGroupBehavior.AutoGenerate;

        // set new position based on original loop vertex position, normal, and index
        public Func<Vector3d, Vector3f, int, Vector3d> ExtrudedPositionF;

        // If you extrude "in", ie w/ a negative distance, then you need to set IsOutward = false
        public bool IsPositiveOffset = true;

        // outputs
        public MeshBoundaryLoops InitialLoops;      // initial boundary loops on input mesh (may be empty)
        public int[] InitialTriangles;              // initial set of triangles
        public int[] InitialVertices;               // initial set of vertices
        public IndexMap InitialToOffsetMapV;        // map from initial vertices to copy vertices
        List<int> OffsetTriangles;                  // triangles of offset surface  (note: can get vertices via MapV(InitialVertices)
        public int OffsetGroupID;                   // group ID of offset-surface triangles
        public EdgeLoop[] NewLoops;                 // New loops on offset surface (1-1 correspondence w/ InitialLoops)
        public int[][] StitchTriangles;             // triangle strip 'tubes' that connect each loop-pair
        public int[] StitchGroupIDs;                // group ID for each triangle-strip-tube



        public MeshExtrudeMesh(DMesh3 mesh)
        {
            Mesh = mesh;

            ExtrudedPositionF = (pos, normal, idx) => {
                return pos + normal;
            };
        }


        public virtual ValidationStatus Validate()
        {
            // is there any reason we couldn't do this??

            return ValidationStatus.Ok;
        }


        public virtual bool Extrude()
        {
            MeshNormals normals = null;
            bool bHaveNormals = Mesh.HasVertexNormals;
            if (!bHaveNormals) {
                normals = new MeshNormals(Mesh);
                normals.Compute();
            }

            InitialLoops = new MeshBoundaryLoops(Mesh);
            InitialTriangles = Mesh.TriangleIndices().ToArray();
            InitialVertices = Mesh.VertexIndices().ToArray();

            // duplicate triangles of mesh
            InitialToOffsetMapV = new IndexMap(Mesh.MaxVertexID, Mesh.MaxVertexID);
            OffsetGroupID = OffsetGroup.GetGroupID(Mesh);
            MeshEditor editor = new MeshEditor(Mesh);
            OffsetTriangles = editor.DuplicateTriangles(InitialTriangles, ref InitialToOffsetMapV, OffsetGroupID);

            // set vertices to new positions
            foreach (int vid in InitialVertices) {
                int newvid = InitialToOffsetMapV[vid];
                if (! Mesh.IsVertex(newvid))
                    continue;

                Vector3d v = Mesh.GetVertex(vid);
                Vector3f n = (bHaveNormals) ? Mesh.GetVertexNormal(vid) : (Vector3f)normals.Normals[vid];
                Vector3d newv = ExtrudedPositionF(v, n, vid);

                Mesh.SetVertex(newvid, newv);
            }

            // we need to reverse one side
            if (IsPositiveOffset)
                editor.ReverseTriangles(InitialTriangles);
            else
                editor.ReverseTriangles(OffsetTriangles);

            // stitch each loop
            NewLoops = new EdgeLoop[InitialLoops.Count];
            StitchTriangles = new int[InitialLoops.Count][];
            StitchGroupIDs = new int[InitialLoops.Count];
            int li = 0;
            foreach (var loop in InitialLoops) {
                int[] loop2 = new int[loop.VertexCount];
                for (int k = 0; k < loop2.Length; ++k)
                    loop2[k] = InitialToOffsetMapV[loop.Vertices[k]];

                
                StitchGroupIDs[li] = StitchGroups.GetGroupID(Mesh);
                if (IsPositiveOffset) {
                    StitchTriangles[li] = editor.StitchLoop(loop2, loop.Vertices, StitchGroupIDs[li]);
                } else {
                    StitchTriangles[li] = editor.StitchLoop(loop.Vertices, loop2, StitchGroupIDs[li]);
                }
                NewLoops[li] = EdgeLoop.FromVertices(Mesh, loop2);
                li++;
            }

            return true;
        }


    }
}
