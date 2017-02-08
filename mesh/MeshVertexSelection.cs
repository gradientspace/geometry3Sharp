using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace g3
{
    public class MeshVertexSelection : IEnumerable<int>
    {
        public DMesh3 Mesh;

        HashSet<int> Selected;
        //List<int> temp;

        public MeshVertexSelection(DMesh3 mesh)
        {
            Mesh = mesh;
            Selected = new HashSet<int>();
            //temp = new List<int>();
        }


        public IEnumerator<int> GetEnumerator() {
            return Selected.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator() {
            return Selected.GetEnumerator();
        }


        private bool is_selected(int tid)
        {
            return Selected.Contains(tid);
        }
        private void add(int tid)
        {
            Selected.Add(tid);
        }
        private void remove(int tid)
        {
            Selected.Remove(tid);
        }



        public void Select(int tid)
        {
            Debug.Assert(Mesh.IsVertex(tid));
            if (Mesh.IsVertex(tid))
                add(tid);
        }
        public void Select(int[] vertices)
        {
            for ( int i = 0; i < vertices.Length; ++i ) {
                if (Mesh.IsVertex(vertices[i]))
                    add(vertices[i]);
            }
        }
        public void SelectTriangleVertices(int[] triangles)
        {
            for ( int i = 0; i < triangles.Length; ++i ) {
                Index3i tri = Mesh.GetTriangle(triangles[i]);
                add(tri.a); add(tri.b); add(tri.c);
            }
        }
        public void SelectTriangleVertices(MeshFaceSelection triangles)
        {
            foreach ( int tid in triangles ) {
                Index3i tri = Mesh.GetTriangle(tid);
                add(tri.a); add(tri.b); add(tri.c);
            }
        }



        public void Deselect(int vid) {
            remove(vid);
        }
        public void Deselect(int[] vertices) {
            for ( int i = 0; i < vertices.Length; ++i ) 
                remove(vertices[i]);
        }


        public int[] ToArray()
        {
            int nVerts = Selected.Count;
            int[] verts = new int[nVerts];
            int i = 0;
            foreach (int vid in Selected)
                verts[i++] = vid;
            return verts;
        }

       

    }
}
