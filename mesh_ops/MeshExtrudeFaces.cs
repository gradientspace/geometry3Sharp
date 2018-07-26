using System;
using System.Collections.Generic;
using System.Linq;

namespace g3
{
    /// <summary>
    /// Extrude a subset of faces of Mesh. Steps are:
    /// 1) separate subset from neighbouring triangles
    /// 2) offset them
    /// 3) connect original and offset edges (now boundary edges) with a triangle strip
    /// 
    /// Caveats:
    ///    - not sure it works for multiple regions?
    ///    - boundary vertices are currently attached to offset region, rather than also duplicated
    ///      and then connected w/ strip
    ///      [TODO] implement this behavior
    /// </summary>
    public class MeshExtrudeFaces
    {
        public DMesh3 Mesh;
        public int[] Triangles;

        public SetGroupBehavior Group = SetGroupBehavior.AutoGenerate;

        // set new position based on original loop vertex position, normal, and index
        public Func<Vector3d, Vector3f, int, Vector3d> ExtrudedPositionF;

        // outputs
        public List<Index2i> EdgePairs;                 // pairs of edges (original, extruded) that were stitched together
        public MeshVertexSelection ExtrudeVertices;     // vertices of extruded region
        public int[] JoinTriangles;                     // triangles generated to connect original end extruded edges together
                                                        // may contain invalid triangle IDs if JoinIncomplete=true
        public int JoinGroupID;                         // group ID of connection triangles
        public bool JoinIncomplete = false;             // if true, errors were encountered during the join operation


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


        /// <summary>
        /// Apply the extrustion operation to input Mesh.
        /// Will return false if operation is not completed.
        /// However changes are not backed out, so if false is returned, input Mesh is in 
        /// undefined state (generally means there are some holes)
        /// </summary>
        public virtual bool Extrude()
        {
            MeshEditor editor = new MeshEditor(Mesh);

            bool bOK = editor.SeparateTriangles(Triangles, true, out EdgePairs);

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

            JoinGroupID = Group.GetGroupID(Mesh);
            JoinTriangles = editor.StitchUnorderedEdges(EdgePairs, JoinGroupID, false, out JoinIncomplete);

            return JoinTriangles != null && JoinIncomplete == false;
        }


    }
}
