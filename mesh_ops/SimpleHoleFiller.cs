using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    public class SimpleHoleFiller
    {
        public DMesh3 Mesh;
        public EdgeLoop Loop;

        int NewVertex;
        int[] NewTriangles;


        public SimpleHoleFiller(DMesh3 mesh, EdgeLoop loop)
        {
            Mesh = mesh;
            Loop = loop;

            NewVertex = DMesh3.InvalidID;
            NewTriangles = null;
        }


        public bool Fill(int group_id = -1)
        {
            if (Loop.Vertices.Length < 3)
                return false;

            // compute centroid
            Vector3d c = Vector3d.Zero;
            for (int i = 0; i < Loop.Vertices.Length; ++i)
                c += Mesh.GetVertex(Loop.Vertices[i]);
            c *= 1.0 / Loop.Vertices.Length;

            // add centroid vtx
            NewVertex = Mesh.AppendVertex(c);

            // stitch triangles
            MeshEditor editor = new MeshEditor(Mesh);
            try {
                NewTriangles = editor.AddTriangleFan_OrderedVertexLoop(NewVertex, Loop.Vertices, group_id);
            } catch {
                NewTriangles = null;
            }

            // if fill failed, back out vertex-add
            if ( NewTriangles == null ) {
                Mesh.RemoveVertex(NewVertex);
                NewVertex = DMesh3.InvalidID;
            }

            return true;

        }


    }
}
