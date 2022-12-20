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
        List<int> temp;

        public MeshVertexSelection(DMesh3 mesh)
        {
            Mesh = mesh;
            Selected = new HashSet<int>();
            temp = new List<int>();
        }

        // convert face selection to vertex selection. 
        public MeshVertexSelection(DMesh3 mesh, MeshFaceSelection convertT) : this(mesh)
        {
            foreach (int tid in convertT) {
                Index3i tv = mesh.GetTriangle(tid);
                add(tv.a); add(tv.b); add(tv.c);
            }
        }

        // convert edge selection to vertex selection. 
        public MeshVertexSelection(DMesh3 mesh, MeshEdgeSelection convertE) : this(mesh)
        {
            foreach (int eid in convertE) {
                Index2i ev = mesh.GetEdgeV(eid);
                add(ev.a); add(ev.b);
            }
        }


        public HashSet<int> ExtractSelected()
        {
            var ret = Selected;
            Selected = new HashSet<int>();
            return ret;
        }


        public IEnumerator<int> GetEnumerator() {
            return Selected.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator() {
            return Selected.GetEnumerator();
        }


        private void add(int vID)
        {
            Selected.Add(vID);
        }
        private void remove(int vID)
        {
            Selected.Remove(vID);
        }

        public int Count {
            get { return Selected.Count; }
        }

        public bool IsSelected(int vID) {
            return Selected.Contains(vID);
        }


        public void Select(int vID)
        {
            Debug.Assert(Mesh.IsVertex(vID));
            if (Mesh.IsVertex(vID))
                add(vID);
        }
        public void Select(int[] vertices)
        {
            for ( int i = 0; i < vertices.Length; ++i ) {
                if (Mesh.IsVertex(vertices[i]))
                    add(vertices[i]);
            }
        }
        public void Select(IEnumerable<int> vertices)
        {
            foreach ( int vID in vertices ) { 
                if (Mesh.IsVertex(vID))
                    add(vID);
            }
        }

        public void SelectTriangleVertices(int[] triangles)
        {
            for ( int i = 0; i < triangles.Length; ++i ) {
                Index3i tri = Mesh.GetTriangle(triangles[i]);
                add(tri.a); add(tri.b); add(tri.c);
            }
        }
        public void SelectTriangleVertices(IEnumerable<int> triangles)
        {
            foreach (int tid in triangles) { 
                Index3i tri = Mesh.GetTriangle(tid);
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


        /// <summary>
        /// for each vertex of input triangle set, select vertex if all
        /// one-ring triangles are contained in triangle set (ie vertex is not on boundary of triangle set).
        /// </summary>
        public void SelectInteriorVertices(MeshFaceSelection triangles)
        {
            HashSet<int> borderv = new HashSet<int>();
            foreach ( int tid in triangles ) {
                Index3i tv = Mesh.GetTriangle(tid);
                for ( int j = 0; j < 3; ++j ) {
                    int vid = tv[j];
                    if (Selected.Contains(vid) || borderv.Contains(vid))
                        continue;
                    bool full_ring = true;
                    foreach (int ring_tid in Mesh.VtxTrianglesItr(vid)) {
                        if (triangles.IsSelected(ring_tid) == false) {
                            full_ring = false;
                            break;
                        }
                    }
                    if (full_ring)
                        add(vid);
                    else
                        borderv.Add(vid);
                }
            }
        }




        /// <summary>
        /// Select set of boundary vertices connected to vSeed.
        /// </summary>
        public void SelectConnectedBoundaryV(int vSeed)
        {
            if ( ! Mesh.IsBoundaryVertex(vSeed))
                throw new Exception("MeshConnectedComponents.FindConnectedBoundaryV: vSeed is not a boundary vertex");

            HashSet<int> found = (Selected.Count == 0) ? Selected : new HashSet<int>();
            found.Add(vSeed);
            List<int> queue = temp; queue.Clear();
            queue.Add(vSeed);
            while (queue.Count > 0) {
                int vid = queue[queue.Count - 1];
                queue.RemoveAt(queue.Count - 1);
                foreach (int nbrid in Mesh.VtxVerticesItr(vid)) {
                    if (Mesh.IsBoundaryVertex(nbrid) && found.Contains(nbrid) == false) {
                        found.Add(nbrid);
                        queue.Add(nbrid);
                    }
                }
            }
            if ( found != Selected ) {
                foreach (int vid in found)
                    add(vid);
            }
            temp.Clear();
        }




        public void SelectEdgeVertices(int[] edges)
        {
            for (int i = 0; i < edges.Length; ++i) {
                Index2i ev = Mesh.GetEdgeV(edges[i]);
                add(ev.a); add(ev.b);
            }
        }
        public void SelectEdgeVertices(IEnumerable<int> edges) {
            foreach (int eid in edges) {
                Index2i ev = Mesh.GetEdgeV(eid);
                add(ev.a); add(ev.b);
            }
        }



        public void Deselect(int vID) {
            remove(vID);
        }
        public void Deselect(int[] vertices) {
            for ( int i = 0; i < vertices.Length; ++i ) 
                remove(vertices[i]);
        }
        public void Deselect(IEnumerable<int> vertices) {
            foreach ( int vid in vertices )
                remove(vid);
        }
        public void DeselectEdge(int eid) {
            Index2i ev = Mesh.GetEdgeV(eid);
            remove(ev.a); remove(ev.b);
        }
        public void DeselectEdges(IEnumerable<int> edges) {
            foreach ( int eid in edges ) {
                Index2i ev = Mesh.GetEdgeV(eid);
                remove(ev.a); remove(ev.b);
            }
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

       


        /// <summary>
        /// Add all one-ring neighbours of current selection to set.
        /// On a large mesh this is quite expensive as we don't know the boundary,
        /// so we have to iterate over all triangles.
        /// 
        /// Return false from FilterF to prevent vertices from being included.
        /// </summary>
        public void ExpandToOneRingNeighbours(Func<int, bool> FilterF = null)
        {
            temp.Clear();

            foreach ( int vid in Selected ) {
                foreach (int nbr_vid in Mesh.VtxVerticesItr(vid)) {
                    if (FilterF != null && FilterF(nbr_vid) == false)
                        continue;
                    if (IsSelected(nbr_vid) == false)
                        temp.Add(nbr_vid);
                }
            }

            for (int i = 0; i < temp.Count; ++i)
                add(temp[i]);
        }


        // [TODO] should do this more efficiently, like MeshFaceSelection
        public void ExpandToOneRingNeighbours(int nRings, Func<int, bool> FilterF = null)
        {
            for (int k = 0; k < nRings; ++k)
                ExpandToOneRingNeighbours(FilterF);
        }





        /// <summary>
        /// Grow selection outwards from seed vertex, until it hits boundaries defined by vertex filter.
        /// </summary>
        public void FloodFill(int vSeed, Func<int, bool> VertIncludedF = null)
        {
            FloodFill(new int[] { vSeed }, VertIncludedF);
        }
        /// <summary>
        /// Grow selection outwards from seed vertex, until it hits boundaries defined by vertex filter.
        /// </summary>
        public void FloodFill(int[] Seeds, Func<int, bool> VertIncludedF = null)
        {
            DVector<int> stack = new DVector<int>(Seeds);
            for (int k = 0; k < Seeds.Length; ++k)
                add(Seeds[k]);
            while (stack.size > 0) {
                int vID = stack.back;
                stack.pop_back();

                foreach ( int nbr_vid in Mesh.VtxVerticesItr(vID) ) {
                    if ( IsSelected(nbr_vid) == true || VertIncludedF(nbr_vid) == false)
                        continue;
                    add(nbr_vid);
                    stack.push_back(nbr_vid);
                }
            }
        }


    }
}
