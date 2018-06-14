using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace g3
{
    public class MeshEdgeSelection : IEnumerable<int>
    {
        public DMesh3 Mesh;

        HashSet<int> Selected;

        List<int> temp;
        BitArray tempBits;

        public MeshEdgeSelection(DMesh3 mesh)
        {
            Mesh = mesh;
            Selected = new HashSet<int>();
            temp = new List<int>();
        }
        public MeshEdgeSelection(MeshEdgeSelection copy)
        {
            Mesh = copy.Mesh;
            Selected = new HashSet<int>(copy.Selected);
            temp = new List<int>();
        }

        protected BitArray Bitmap {
            get {
                if (tempBits == null)
                    tempBits = new BitArray(Mesh.MaxEdgeID);
                return tempBits;
            }
        }


        // convert vertex selection to edge selection. Require at least minCount verts of edge to be selected
        public MeshEdgeSelection(DMesh3 mesh, MeshVertexSelection convertV, int minCount = 2) : this(mesh)
        {
            minCount = MathUtil.Clamp(minCount, 1, 2);

            // [TODO] if minCount == 1, and convertV is small, it is faster to iterate over convertV!!

            foreach ( int eid in mesh.EdgeIndices() ) {
                Index2i ev = mesh.GetEdgeV(eid);
                int n = (convertV.IsSelected(ev.a) ? 1 : 0) +
                        (convertV.IsSelected(ev.b) ? 1 : 0);
                if (n >= minCount)
                    add(eid);
            }
        }

        // convert face selection to edge selection. Require at least minCount tris of edge to be selected
        public MeshEdgeSelection(DMesh3 mesh, MeshFaceSelection convertT, int minCount = 1) : this(mesh)
        {
            minCount = MathUtil.Clamp(minCount, 1, 2);

            if (minCount == 1) {
                foreach (int tid in convertT) {
                    Index3i te = mesh.GetTriEdges(tid);
                    add(te.a); add(te.b); add(te.c);
                }
            } else {
                foreach (int eid in mesh.EdgeIndices()) {
                    Index2i et = mesh.GetEdgeT(eid);
                    if (convertT.IsSelected(et.a) && convertT.IsSelected(et.b))
                        add(eid);
                }
            }
        }



        public IEnumerator<int> GetEnumerator() {
            return Selected.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator() {
            return Selected.GetEnumerator();
        }


        private void add(int eid)
        {
            Selected.Add(eid);
        }
        private void remove(int eid)
        {
            Selected.Remove(eid);
        }


        public int Count
        {
            get { return Selected.Count; }
        }



        public bool IsSelected(int eid)
        {
            return Selected.Contains(eid);
        }


        public void Select(int eid)
        {
            Debug.Assert(Mesh.IsEdge(eid));
            if (Mesh.IsEdge(eid))
                add(eid);
        }
        public void Select(int[] edges)
        {
            for ( int i = 0; i < edges.Length; ++i ) {
                if (Mesh.IsEdge(edges[i]))
                    add(edges[i]);
            }
        }
        public void Select(List<int> edges)
        {
            for ( int i = 0; i < edges.Count; ++i ) {
                if (Mesh.IsEdge(edges[i]))
                    add(edges[i]);
            }
        }
        public void Select(IEnumerable<int> edges)
        {
            foreach ( int eid in edges) { 
                if (Mesh.IsEdge(eid))
                    add(eid);
            }
        }
        public void Select(Func<int,bool> selectF)
        {
            temp.Clear();
            int NT = Mesh.MaxEdgeID;
            for (int eid = 0; eid < NT; ++eid) { 
                if (Mesh.IsEdge(eid) && selectF(eid) )
                    temp.Add(eid);
            }
            Select(temp);
        }

        public void SelectVertexEdges(int[] vertices)
        {
            for ( int i = 0; i < vertices.Length; ++i ) {
                int vid = vertices[i];
                foreach (int eid in Mesh.VtxEdgesItr(vid))
                    add(eid);
            }
        }
        public void SelectVertexEdges(IEnumerable<int> vertices)
        {
            foreach ( int vid in vertices ) { 
                foreach (int eid in Mesh.VtxEdgesItr(vid))
                    add(eid);
            }
        }

        public void SelectTriangleEdges(IEnumerable<int> triangles)
        {
            foreach ( int tid in triangles ) {
                Index3i et = Mesh.GetTriEdges(tid);
                add(et.a); add(et.b); add(et.c);
            }
        }


        public void SelectBoundaryTriEdges(MeshFaceSelection triangles)
        {
            foreach ( int tid in triangles ) {
                Index3i te = Mesh.GetTriEdges(tid);
                for ( int j = 0; j < 3; ++j ) {
                    Index2i et = Mesh.GetEdgeT(te[j]);
                    int other_tid = (et.a == tid) ? et.b : et.a;
                    if (triangles.IsSelected(other_tid) == false)
                        add(te[j]);
                }
            }
        }


        public void Deselect(int tid) {
            remove(tid);
        }
        public void Deselect(int[] edges) {
            for ( int i = 0; i < edges.Length; ++i ) 
                remove(edges[i]);
        }
        public void Deselect(IEnumerable<int> edges) {
            foreach ( int tid in edges )
                remove(tid);
        }
        public void DeselectAll()
        {
            Selected.Clear();
        }



        public int[] ToArray()
        {
            return Selected.ToArray();
        }


    }
}
