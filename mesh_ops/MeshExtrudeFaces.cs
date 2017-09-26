using System;
using System.Collections.Generic;
using System.Linq;

namespace g3
{
    public class MeshExtrudeFaces
    {
        public DMesh3 Mesh;
        public int[] Triangles;

        // arguments

        public SetGroupBehavior Group = SetGroupBehavior.AutoGenerate;

        // set new position based on original loop vertex position, normal, and index
        public Func<Vector3d, Vector3f, int, Vector3d> ExtrudedPositionF;

        // outputs
        public List<Index2i> EdgePairs;
        public MeshVertexSelection ExtrudeVertices;
        public int[] JoinTriangles;
        public int SetGroupID;


        public MeshExtrudeFaces(DMesh3 mesh, int[] triangles, bool bForceCopyArray = false)
        {
            Mesh = mesh;
            if (bForceCopyArray) 
                Triangles = (int[])triangles.Clone();
            else
                Triangles = triangles;

            ExtrudedPositionF = (pos, normal, idx) => {
                return pos + Vector3d.AxisY;
            };
        }

        public MeshExtrudeFaces(DMesh3 mesh, IEnumerable<int> triangles)
        {
            Mesh = mesh;
            Triangles = triangles.ToArray();

            ExtrudedPositionF = (pos, normal, idx) => {
                return pos + Vector3d.AxisY;
            };
        }



        public virtual ValidationStatus Validate()
        {
            // [todo] check that boundary is ok


            return ValidationStatus.Ok;
        }


        public virtual bool Extrude()
        {
            MeshEditor editor = new MeshEditor(Mesh);


            editor.SeparateTriangles(Triangles, true, out EdgePairs);

            MeshNormals normals = null;
            bool bHaveNormals = Mesh.HasVertexNormals;
            if (!bHaveNormals) {
                normals = new MeshNormals(Mesh);
                normals.Compute();
            }

            ExtrudeVertices = new MeshVertexSelection(Mesh);
            ExtrudeVertices.SelectTriangleVertices(Triangles);

            Vector3d[] NewVertices = new Vector3d[ExtrudeVertices.Count];
            int k = 0;
            foreach (int vid in ExtrudeVertices) {
                Vector3d v = Mesh.GetVertex(vid);
                Vector3f n = (bHaveNormals) ? Mesh.GetVertexNormal(vid) : (Vector3f)normals.Normals[vid];
                NewVertices[k++] = ExtrudedPositionF(v, n, vid);
            }
            k = 0;
            foreach (int vid in ExtrudeVertices)
                Mesh.SetVertex(vid, NewVertices[k++]);

            SetGroupID = Group.GetGroupID(Mesh);
            JoinTriangles = editor.StitchUnorderedEdges(EdgePairs, SetGroupID);

            return true;
        }


    }
}
