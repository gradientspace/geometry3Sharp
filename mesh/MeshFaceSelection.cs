using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace g3
{
    public class MeshFaceSelection : IEnumerable<int>
    {
        public DMesh3 Mesh;

        HashSet<int> Selected;
        List<int> temp;

        public MeshFaceSelection(DMesh3 mesh)
        {
            Mesh = mesh;
            Selected = new HashSet<int>();
            temp = new List<int>();
        }

        // convert vertex selection to face selection. Require at least minCount verts of
        // tri to be selected (valid values are 1,2,3)
        public MeshFaceSelection(DMesh3 mesh, MeshVertexSelection convertV, int minCount = 3) : this(mesh)
        {
            minCount = MathUtil.Clamp(minCount, 1, 3);

            foreach ( int tid in mesh.TriangleIndices() ) {
                Index3i tri = mesh.GetTriangle(tid);

                if (minCount == 1) {
                    if (convertV.IsSelected(tri.a) || convertV.IsSelected(tri.b) || convertV.IsSelected(tri.c))
                        add(tid);
                } else if (minCount == 3) {
                    if (convertV.IsSelected(tri.a) && convertV.IsSelected(tri.b) && convertV.IsSelected(tri.c))
                        add(tid);
                } else {
                    int n = (convertV.IsSelected(tri.a) ? 1 : 0) +
                            (convertV.IsSelected(tri.b) ? 1 : 0) +
                            (convertV.IsSelected(tri.c) ? 1 : 0);
                    if (n >= minCount)
                        add(tid);
                }
            }
        }


        public IEnumerator<int> GetEnumerator() {
            return Selected.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator() {
            return Selected.GetEnumerator();
        }


        private void add(int tid)
        {
            Selected.Add(tid);
        }
        private void remove(int tid)
        {
            Selected.Remove(tid);
        }


        public int Count
        {
            get { return Selected.Count; }
        }



        public bool IsSelected(int tid)
        {
            return Selected.Contains(tid);
        }


        public void Select(int tid)
        {
            Debug.Assert(Mesh.IsTriangle(tid));
            if (Mesh.IsTriangle(tid))
                add(tid);
        }
        public void Select(int[] triangles)
        {
            for ( int i = 0; i < triangles.Length; ++i ) {
                if (Mesh.IsTriangle(triangles[i]))
                    add(triangles[i]);
            }
        }
        public void Select(IEnumerable<int> triangles)
        {
            foreach ( int tID in triangles ) { 
                if (Mesh.IsTriangle(tID))
                    add(tID);
            }
        }

        public void SelectVertexOneRings(int[] vertices)
        {
            for ( int i = 0; i < vertices.Length; ++i ) {
                int vid = vertices[i];
                foreach (int tid in Mesh.VtxTrianglesItr(vid))
                    add(tid);
            }
        }
        public void SelectVertexOneRings(IEnumerable<int> vertices)
        {
            foreach ( int vid in vertices ) { 
                foreach (int tid in Mesh.VtxTrianglesItr(vid))
                    add(tid);
            }
        }


        public void Deselect(int tid) {
            remove(tid);
        }
        public void Deselect(int[] triangles) {
            for ( int i = 0; i < triangles.Length; ++i ) 
                remove(triangles[i]);
        }
        public void Deselect(IEnumerable<int> triangles) {
            foreach ( int tid in triangles )
                remove(tid);
        }



        public int[] ToArray()
        {
            int nTris = Selected.Count;
            int[] tris = new int[nTris];
            int i = 0;
            foreach (int tid in Selected)
                tris[i++] = tid;
            return tris;
        }



        public void ExpandToFaceNeighbours(Func<int, bool> FilterF = null)
        {
            temp.Clear();

            foreach ( int tid in Selected ) { 
                Index3i nbr_tris = Mesh.GetTriNeighbourTris(tid);
                for (int j = 0; j < 3; ++j) {
                    if (FilterF != null && FilterF(nbr_tris[j]) == false)
                        continue;
                    if (nbr_tris[j] != DMesh3.InvalidID && IsSelected(nbr_tris[j]) == false)
                        temp.Add(nbr_tris[j]);
                }
            }

            for (int i = 0; i < temp.Count; ++i)
                add(temp[i]);
        }


        // this may process vertices multiple times...
        public void ExpandToOneRingNeighbours(Func<int, bool> FilterF = null)
        {
            temp.Clear();

            foreach ( int tid in Selected ) { 
                Index3i tri_v = Mesh.GetTriangle(tid);
                for (int j = 0; j < 3; ++j) {
                    int vid = tri_v[j];
                    foreach (int nbr_t in Mesh.VtxTrianglesItr(vid)) {
                        if (FilterF != null && FilterF(nbr_t) == false)
                            continue;
                        if (IsSelected(nbr_t) == false)
                            temp.Add(nbr_t);
                    }
                }
            }

            for (int i = 0; i < temp.Count; ++i)
                add(temp[i]);
        }



        public void FloodFill(int tSeed, Func<int,bool> FilterF = null)
        {
            FloodFill(new int[] { tSeed }, FilterF);
        }
        public void FloodFill(int[] Seeds, Func<int,bool> FilterF = null)
        {
            // why does dvector version of this hang??
            DVector<int> stack = new DVector<int>(Seeds);
            while ( stack.size > 0 ) {

                int tID = stack.back;
                stack.pop_back();

                Index3i nbrs = Mesh.GetTriNeighbourTris(tID);
                for ( int j = 0; j < 3; ++j ) {
                    int nbr_tid = nbrs[j];
                    if (nbr_tid == DMesh3.InvalidID || IsSelected(nbr_tid))
                        continue;
                    if (FilterF != null && FilterF(nbr_tid) == false)
                        continue;
                    add(nbr_tid);

                    stack.push_back(nbr_tid);
                }
            }
        }



        // return true if we clipped something
        public bool ClipFins()
        {
            temp.Clear();
            foreach (int tid in Selected) {
                if (is_fin(tid))
                    temp.Add(tid);
            }
            if (temp.Count == 0)
                return false;
            foreach (int tid in temp)
                remove(tid);
            return true;
        }


        // return true if we filled any ears.
        public bool FillEars()
        {
            // [TODO] not efficient! checks each nbr 3 times !! ugh!!
            temp.Clear();
            foreach (int tid in Selected) {
                Index3i nbr_tris = Mesh.GetTriNeighbourTris(tid);
                for (int j = 0; j < 3; ++j) {
                    int nbr_t = nbr_tris[j];
                    if (IsSelected(nbr_t))
                        continue;
                    if (is_ear(nbr_t))
                        temp.Add(nbr_t);
                }
            }
            if (temp.Count == 0)
                return false;
            foreach (int tid in temp)
                add(tid);
            return true;
        }

        // returns true if selection was modified
        public bool LocalOptimize(bool bClipFins, bool bFillEars)
        {
            bool bModified = false;
            bool done = false;
            while ( ! done ) {
                done = true;
                if (bClipFins && ClipFins())
                    done = false;
                if (bFillEars && FillEars())
                    done = false;
                if (done == false)
                    bModified = true;
            }
            return bModified;
        }








        private void count_nbrs(int tid, out int nbr_in, out int nbr_out, out int bdry_e)
        {
            Index3i nbr_tris = Mesh.GetTriNeighbourTris(tid);
            nbr_in = 0; nbr_out = 0; bdry_e = 0;
            for ( int j = 0; j < 3; ++j ) {
                int nbr_t = nbr_tris[j];
                if (nbr_t == DMesh3.InvalidID)
                    bdry_e++;
                else if (IsSelected(nbr_t) == true)
                    nbr_in++;
                else
                    nbr_out++;
            }
        }
        private bool is_ear(int tid)
        {
            if (IsSelected(tid) == true)
                return false;
            int nbr_in, nbr_out, bdry_e;
            count_nbrs(tid, out nbr_in, out nbr_out, out bdry_e);
            if (bdry_e == 2 && nbr_in == 1) {
                return true;        // unselected w/ 2 boundary edges, nbr is  in
            } else if (nbr_in == 2) {
                if (bdry_e == 1 || nbr_out == 1)
                    return true;        // unselected w/ 2 selected nbrs
            }
            return false;
        }
        private bool is_fin(int tid)
        {
            if (IsSelected(tid) == false)
                return false;
            int nbr_in, nbr_out, bdry_e;
            count_nbrs(tid, out nbr_in, out nbr_out, out bdry_e);
            return (nbr_in == 1 && nbr_out == 2);
        }


    }
}
