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

        List<int> temp, temp2;
        BitArray tempBits;

        public MeshFaceSelection(DMesh3 mesh)
        {
            Mesh = mesh;
            Selected = new HashSet<int>();
            temp = new List<int>();
            temp2 = new List<int>();
        }
        public MeshFaceSelection(MeshFaceSelection copy)
        {
            Mesh = copy.Mesh;
            Selected = new HashSet<int>(copy.Selected);
            temp = new List<int>();
            temp2 = new List<int>();
        }

        protected BitArray Bitmap {
            get {
                if (tempBits == null)
                    tempBits = new BitArray(Mesh.MaxTriangleID);
                return tempBits;
            }
        }


        // convert vertex selection to face selection. Require at least minCount verts of
        // tri to be selected (valid values are 1,2,3)
        public MeshFaceSelection(DMesh3 mesh, MeshVertexSelection convertV, int minCount = 3) : this(mesh)
        {
            minCount = MathUtil.Clamp(minCount, 1, 3);

            if (minCount == 1) {
                foreach ( int vid in convertV ) {
                    foreach (int tid in mesh.VtxTrianglesItr(vid))
                        add(tid);
                }
            } else {
                foreach (int tid in mesh.TriangleIndices()) {
                    Index3i tri = mesh.GetTriangle(tid);
                    if (minCount == 3) {
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
        }


        // select a group
        public MeshFaceSelection(DMesh3 mesh, int group_id) : this(mesh)
        {
            SelectGroup(group_id);
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
        public void Select(List<int> triangles)
        {
            for ( int i = 0; i < triangles.Count; ++i ) {
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
        public void Select(Func<int,bool> selectF)
        {
            temp.Clear();
            int NT = Mesh.MaxTriangleID;
            for (int tID = 0; tID < NT; ++tID) { 
                if (Mesh.IsTriangle(tID) && selectF(tID) )
                    temp.Add(tID);
            }
            Select(temp);
        }


        public void SelectVertexOneRing(int vid) {
            foreach (int tid in Mesh.VtxTrianglesItr(vid))
                add(tid);
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


        public void SelectEdgeTris(int eid)
        {
            Index2i et = Mesh.GetEdgeT(eid);
            add(et.a);
            if (et.b != DMesh3.InvalidID)
                add(et.b);
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
        public void DeselectAll()
        {
            Selected.Clear();
        }



        public void SelectGroup(int gid)
        {
            int NT = Mesh.MaxTriangleID;
            for (int tid = 0; tid < NT; ++tid) {
                if (Mesh.IsTriangle(tid) && Mesh.GetTriangleGroup(tid) == gid)
                    add(tid);
            }
        }
        public void SelectGroupInverse(int gid)
        {
            int NT = Mesh.MaxTriangleID;
            for (int tid = 0; tid < NT; ++tid) {
                if (Mesh.IsTriangle(tid) && Mesh.GetTriangleGroup(tid) != gid)
                    add(tid);
            }
        }
        public void DeselectGroup(int gid)
        {
            // cannot just iterate over selected tris because remove() will change them...
            int NT = Mesh.MaxTriangleID;
            for (int tid = 0; tid < NT; ++tid) {
                if (Mesh.IsTriangle(tid) && Mesh.GetTriangleGroup(tid) == gid)
                    remove(tid);
            }
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



        /// <summary>
        /// find set of tris just outside border of selection
        /// </summary>
        public List<int> FindNeighbourTris()
        {
            List<int> result = new List<int>();
            foreach (int tid in Selected) {
                Index3i nbr_tris = Mesh.GetTriNeighbourTris(tid);
                for (int j = 0; j < 3; ++j) {
                    if (nbr_tris[j] != DMesh3.InvalidID && IsSelected(nbr_tris[j]) == false)
                        result.Add(nbr_tris[j]);
                }
            }
            return result;
        }


        /// <summary>
        /// find set of tris just inside border of selection
        /// </summary>
        public List<int> FindBorderTris()
        {
            List<int> result = new List<int>();
            foreach (int tid in Selected) {
                Index3i nbr_tris = Mesh.GetTriNeighbourTris(tid);
                if (IsSelected(nbr_tris.a) == false || IsSelected(nbr_tris.b) == false || IsSelected(nbr_tris.c) == false)
                    result.Add(tid);
            }
            return result;
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
        public void ExpandToFaceNeighbours(int rounds, Func<int, bool> FilterF = null)
        {
            for (int k = 0; k < rounds; ++k)
                ExpandToFaceNeighbours(FilterF);
        }


        /// <summary>
        /// Add all triangles in vertex one-rings of current selection to set.
        /// On a large mesh this is quite expensive as we don't know the boundary,
        /// so we have to iterate over all triangles.
        /// 
        /// Return false from FilterF to prevent triangles from being included.
        /// </summary>
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


        /// <summary>
        /// Expand selection by N vertex one-rings. This is *significantly* faster
        /// than calling ExpandToOnering() multiple times, because we can track
        /// the growing front and only check the new triangles.
        /// 
        /// Return false from FilterF to prevent triangles from being included.
        /// </summary>
        public void ExpandToOneRingNeighbours(int nRings, Func<int, bool> FilterF = null)
        {
            if ( nRings == 1 ) {
                ExpandToOneRingNeighbours(FilterF);
                return;
            }

            var addTris = temp;
            var checkTris = temp2;
            checkTris.Clear();
            checkTris.AddRange(Selected);

            Bitmap.SetAll(false);
            foreach (int tid in Selected)
                Bitmap.Set(tid, true);

            for (int ri = 0; ri < nRings; ++ri) {
                addTris.Clear();

                foreach (int tid in checkTris) {
                    Index3i tri_v = Mesh.GetTriangle(tid);
                    for (int j = 0; j < 3; ++j) {
                        int vid = tri_v[j];
                        foreach (int nbr_t in Mesh.VtxTrianglesItr(vid)) {
                            if (FilterF != null && FilterF(nbr_t) == false)
                                continue;
                            if (Bitmap.Get(nbr_t) == false) {
                                addTris.Add(nbr_t);
                                Bitmap.Set(nbr_t, true);
                            }
                        }
                    }
                }

                for (int i = 0; i < addTris.Count; ++i)
                    add(addTris[i]);

                var t = checkTris; checkTris = addTris; addTris = t;   // swap
            }
        }





        /// <summary>
        /// remove all triangles in vertex one-rings of current selection to set.
        /// On a large mesh this is quite expensive as we don't know the boundary,
        /// so we have to iterate over all triangles.
        /// 
        /// Return false from FilterF to prevent triangles from being deselected.
        /// </summary>
        public void ContractBorderByOneRingNeighbours()
        {
            temp.Clear();   // border vertices

            // [TODO] border vertices are pushed onto the temp list multiple times.
            // minor inefficiency, but maybe we could improve it?

            // find set of vertices on border
            foreach (int tid in Selected) {
                Index3i tri_v = Mesh.GetTriangle(tid);
                for (int j = 0; j < 3; ++j) {
                    int vid = tri_v[j];
                    foreach (int nbr_t in Mesh.VtxTrianglesItr(vid)) {
                        if ( IsSelected(nbr_t) == false ) {
                            temp.Add(vid);
                            break;
                        }
                    }
                }
            }

            foreach ( int vid in temp) {
                foreach (int nbr_t in Mesh.VtxTrianglesItr(vid)) 
                    Deselect(nbr_t);
            }

        }




        /// <summary>
        /// Grow selection outwards from seed triangle, until it hits boundaries defined by triangle and edge filters.
        /// Edge filter is not effective unless it (possibly combined w/ triangle filter) defines closed loops.
        /// </summary>
        public void FloodFill(int tSeed, Func<int,bool> TriFilterF = null, Func<int, bool> EdgeFilterF = null)
        {
            FloodFill(new int[] { tSeed }, TriFilterF, EdgeFilterF);
        }
        /// <summary>
        /// Grow selection outwards from seed triangles, until it hits boundaries defined by triangle and edge filters.
        /// Edge filter is not effective unless it (possibly combined w/ triangle filter) defines closed loops.
        /// </summary>
        public void FloodFill(int[] Seeds, Func<int,bool> TriFilterF = null, Func<int,bool> EdgeFilterF = null)
        {
            DVector<int> stack = new DVector<int>(Seeds);
            for (int k = 0; k < Seeds.Length; ++k)
                add(Seeds[k]);
            while ( stack.size > 0 ) {
                int tID = stack.back;
                stack.pop_back();

                Index3i nbrs = Mesh.GetTriNeighbourTris(tID);
                for ( int j = 0; j < 3; ++j ) {
                    int nbr_tid = nbrs[j];
                    if (nbr_tid == DMesh3.InvalidID || IsSelected(nbr_tid))
                        continue;
                    if (TriFilterF != null && TriFilterF(nbr_tid) == false)
                        continue;
                    if (EdgeFilterF != null && EdgeFilterF(Mesh.GetTriEdge(tID,j)) == false)
                        continue;
                    add(nbr_tid);

                    stack.push_back(nbr_tid);
                }
            }
        }



        // return true if we clipped something
        public bool ClipFins(bool bClipLoners)
        {
            temp.Clear();
            foreach (int tid in Selected) {
                if (is_fin(tid, bClipLoners))
                    temp.Add(tid);
            }
            if (temp.Count == 0)
                return false;
            foreach (int tid in temp)
                remove(tid);
            return true;
        }


        // return true if we filled any ears.
        public bool FillEars(bool bFillTinyHoles)
        {
            // [TODO] not efficient! checks each nbr 3 times !! ugh!!
            temp.Clear();
            foreach (int tid in Selected) {
                Index3i nbr_tris = Mesh.GetTriNeighbourTris(tid);
                for (int j = 0; j < 3; ++j) {
                    int nbr_t = nbr_tris[j];
                    if (IsSelected(nbr_t))
                        continue;
                    if (is_ear(nbr_t, bFillTinyHoles))
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
        public bool LocalOptimize(bool bClipFins, bool bFillEars, bool bFillTinyHoles = true, bool bClipLoners = true, bool bRemoveBowties = false)
        {
            bool bModified = false;
            bool done = false;
            int count = 0;
            HashSet<int> temp_hash = new HashSet<int>();
            while ( ! done ) {
                done = true;
                if (count++ == 25)      // terminate in case we get stuck
                    break;
                if (bClipFins && ClipFins(bClipLoners))
                    done = false;
                if (bFillEars && FillEars(bFillTinyHoles))
                    done = false;
                if (bRemoveBowties && remove_bowties(temp_hash))
                    done = false;
                if (done == false)
                    bModified = true;
            }
            if (bRemoveBowties)
                remove_bowties(temp_hash);        // do a final pass of this because it is usually the most problematic...
            return bModified;
        }
        public bool LocalOptimize(bool bRemoveBowties = true) {
            return LocalOptimize(true, true, true, true, bRemoveBowties);
        }




        /// <summary>
        /// Find any "bowtie" vertices - ie vertex v such taht there is multiple spans of triangles
        /// selected in v's triangle one-ring - and deselect those one-rings.
        /// Returns true if selection was modified.
        /// </summary>
        public bool RemoveBowties() {
            return remove_bowties(null);
        }
        public bool remove_bowties(HashSet<int> tempHash)
        {
            bool bModified = false;
            bool done = false;
            HashSet<int> vertices = (tempHash == null) ? new HashSet<int>() : tempHash;
            while (!done) {
                done = true;
                vertices.Clear();
                foreach (int tid in Selected) {
                    Index3i tv = Mesh.GetTriangle(tid);
                    vertices.Add(tv.a); vertices.Add(tv.b); vertices.Add(tv.c);
                }

                foreach (int vid in vertices) {
                    if (is_bowtie_vtx(vid)) {
                        Deselect(Mesh.VtxTrianglesItr(vid));
                        done = false;
                    }
                }
                if (done == false)
                    bModified = true;
            }
            return bModified;
        }
        private bool is_bowtie_vtx(int vid)
        {
            int border_edges = 0;
            foreach ( int eid in Mesh.VtxEdgesItr(vid) ) {
                Index2i et = Mesh.GetEdgeT(eid);
                if (et.b != DMesh3.InvalidID) {
                    bool in_a = IsSelected(et.a);
                    bool in_b = IsSelected(et.b);
                    if (in_a != in_b)
                        border_edges++;
                } else {
                    if (IsSelected(et.a))
                        border_edges++;
                }
            }
            return border_edges > 2;
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
        private bool is_ear(int tid, bool include_tiny_holes)
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

            } else if ( include_tiny_holes && nbr_in == 3 ) {
                return true;
            }
            return false;
        }
        private bool is_fin(int tid, bool include_loners)
        {
            if (IsSelected(tid) == false)
                return false;
            int nbr_in, nbr_out, bdry_e;
            count_nbrs(tid, out nbr_in, out nbr_out, out bdry_e);
            return (nbr_in == 1 && nbr_out == 2) ||
                (include_loners == true && nbr_in == 0 && nbr_out == 3);
        }


    }
}
