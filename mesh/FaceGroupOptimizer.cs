using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    /// <summary>
    /// Given input mesh with a set of face groups, optimize the face group boundaries.
    /// This involves flipping triangles between groups, and/or assigning to "background" group.
    /// Also has Dilate/Contract functions to grow/shrink groups in various ways.
    /// </summary>
    public class FaceGroupOptimizer
    {
        // cannot change this w/o updating GetEnumeratorF
        DMesh3 mesh;
        public DMesh3 Mesh { get { return mesh; } }

        // defaults to Mesh.TriangleIndices(), override to use a sub-region
        public Func<IEnumerable<int>> GetEnumeratorF = null;

        // this is "no-group" background ID
        public int BackgroundGroupID = 0;

        // a group might have a fin triangle that, when we clip it, will
        // introduce a 'hole' to the background. This may be desirable in some
        // cases and not others. Default is to prevent this.
        public bool DontClipEnclosedFins = true;

        // an 'ear' is a tri w/ 2 same-group nbrs, that is either in a different
        // group, or in the background. This flag prevents flipping ear tris between groups,
        // which will result in more-jagged boundaries
        public bool NoEarGroupSwaps = false;

        // internal buffer
        List<Index2i> temp = new List<Index2i>();

        public FaceGroupOptimizer(DMesh3 meshIn)
        {
            mesh = meshIn;
            GetEnumeratorF = () => { return mesh.TriangleIndices(); };
        }


        // return true if we clipped something
        public int ClipFins(bool bClipLoners)
        {
            temp.Clear();
            var triangles = GetEnumeratorF();
            foreach ( int tid in triangles ) {
                if (is_fin(tid, bClipLoners))
                    temp.Add(new Index2i(tid,BackgroundGroupID) );
            }
            if (temp.Count == 0)
                return 0;
            foreach (Index2i update in temp)
                mesh.SetTriangleGroup(update.a, update.b);
            return temp.Count;
        }


        // return true if we filled any ears.
        public int FillEars(bool bFillTinyHoles)
        {
            // [TODO] not efficient! checks each nbr 3 times !! ugh!!
            int filled = 0;
            var triangles = GetEnumeratorF();
            foreach (int tid in triangles ) {
                int swap_to_g = is_ear(tid, bFillTinyHoles, NoEarGroupSwaps);
                if (swap_to_g >= 0) {
                    // should we push on list and defer to end??
                    mesh.SetTriangleGroup(tid, swap_to_g);
                    filled++;
                }
            }
            return filled;
        }


        /// <summary>
        /// Do rounds of ear-filling and fin-clipping until we can't anymore
        /// Returns true if group assignments were modified
        /// </summary>
        public bool LocalOptimize(bool bClipFins, bool bFillEars, bool bFillTinyHoles = true, bool bClipLoners = true, int max_iters = 100)
        {
            bool bModified = false;
            bool done = false;
            int iters = 0;
            while (!done && iters++ < max_iters) {
                done = true;
                int clipped = 0, filled = 0;
                if (bClipFins)
                    clipped = ClipFins(bClipLoners);
                if (bFillEars)
                    filled = FillEars(bFillTinyHoles);
                if (clipped > 0 || filled > 0) { 
                    done = false;
                    bModified = true;
                }
                //System.Console.WriteLine("Clipped {0}   Filled {1}", clipped, filled);
            }
            return bModified;
        }



        /// <summary>
        /// Simultaneously grow all groups into areas with background group id, expanding
        /// by N rings. does not expand across group borders.
        /// </summary>
        public int DilateAllGroups(int nRings)
        {
            // [TODO] [OPTIMIZE] on rings > 0, we only need to check around tris we added in first pass, right??

            int tCount = 0;
            for (int k = 0; k < nRings; ++k) {
                temp.Clear();
                var triangles = GetEnumeratorF();
                foreach (int tid in triangles) {
                    int gid = mesh.GetTriangleGroup(tid);
                    if (gid != BackgroundGroupID)
                        continue;

                    // find nearest nbr??
                    Index3i nbr_tris = mesh.GetTriNeighbourTris(tid);
                    for (int j = 0; j < 3; ++j) {
                        if (nbr_tris[j] != DMesh3.InvalidID) {
                            int nbr_g = mesh.GetTriangleGroup(nbr_tris[j]);
                            if (nbr_g != BackgroundGroupID) {
                                temp.Add(new Index2i(tid, nbr_g));
                                break;
                            }
                        }
                    }
                }
                if (temp.Count == 0)
                    return tCount;          // do not have to do further rings in this case

                foreach (Index2i update in temp) {
                    if (mesh.GetTriangleGroup(update.a) == BackgroundGroupID) {
                        mesh.SetTriangleGroup(update.a, update.b);
                        ++tCount;
                    }
                }
            }

            return tCount;
        }



        /// <summary>
        /// Simultaneously contract all groups by N rings. 
        /// if bBackroundOnly=true, then non-background group borders stay connected,
        /// otherwise they pull apart.
        /// </summary>
        public int ContractAllGroups(int nRings, bool bBackgroundOnly)
        {
            int tCount = 0;
            for (int k = 0; k < nRings; ++k) {
                temp.Clear();
                var triangles = GetEnumeratorF();
                foreach (int tid in triangles) {
                    int gid = mesh.GetTriangleGroup(tid);
                    Index3i nbr_tris = mesh.GetTriNeighbourTris(tid);

                    bool bIsBorder = false;
                    if (bBackgroundOnly) {
                        for (int j = 0; j < 3 && bIsBorder == false; ++j) {
                            if (nbr_tris[j] != DMesh3.InvalidID && mesh.GetTriangleGroup(nbr_tris[j]) == BackgroundGroupID)
                                bIsBorder = true;
                        }
                    } else {
                        for (int j = 0; j < 3 && bIsBorder == false; ++j) {
                            if (nbr_tris[j] != DMesh3.InvalidID && mesh.GetTriangleGroup(nbr_tris[j]) != gid)
                                bIsBorder = true;
                        }
                    }
                    if (bIsBorder)
                        temp.Add(new Index2i(tid, BackgroundGroupID));
                }
                if (temp.Count == 0)
                    return tCount;          // do not have to do further rings in this case

                foreach (Index2i update in temp) {
                    mesh.SetTriangleGroup(update.a, update.b);
                    ++tCount;
                }
            }

            return tCount;
        }






        private int find_max_nbr(int tid, out int nbr_same, out int nbr_diff, out int bdry_e)
        {
            Index3i nbr_tris = mesh.GetTriNeighbourTris(tid);

            Index3i nbr_g = Index3i.Max;
            for (int j = 0; j < 3; ++j) {
                int nbr_t = nbr_tris[j];
                nbr_g[j] = (nbr_t == DMesh3.InvalidID) ? -1 : mesh.GetTriangleGroup(nbr_tris[j]);
            }
            int max_idx = -1;
            for ( int j = 0; j < 3; ++j ) {
                if (nbr_g[j] != -1 && (nbr_g[j] == nbr_g[(j + 1) % 3] || nbr_g[j] == nbr_g[(j + 2) % 3]))
                    max_idx = j;
            }
            nbr_same = 1; nbr_diff = 0; bdry_e = 0;
            if (max_idx == -1)
                return -1;
            int max_g = nbr_g[max_idx];
            for ( int k = 1; k < 3; ++k ) {
                int j = (max_idx + k) % 3;
                if (nbr_g[j] == -1)
                    bdry_e++;
                else if (nbr_g[j] == max_g)
                    nbr_same++;
                else
                    nbr_diff++;
            }
            return max_g;
        }
        private int is_ear(int tid, bool include_tiny_holes, bool bBackgroundOnly)
        {
            int gid = mesh.GetTriangleGroup(tid);
            if (bBackgroundOnly && gid != BackgroundGroupID)
                return -1;

            int nbr_same, nbr_diff, bdry_e;
            int best_g = find_max_nbr(tid, out nbr_same, out nbr_diff, out bdry_e);
            if (best_g == -1 || best_g == gid)
                return -1;
            if (bdry_e == 2 && nbr_same == 1) {
                return best_g;        // 2 boundary edges, swap to only group nbr
            } else if (nbr_same == 2) {
                if (bdry_e == 1 || nbr_diff == 1)
                    return best_g;        // 2 nbrs w/ same group, swap to that group

            } else if (include_tiny_holes && nbr_same == 3) {
                return best_g;            // swap to surrounding group
            }
            return -1;
        }



        private void count_same_nbrs(int tid, out int nbr_same, out int nbr_diff, out int nbr_bg, out int bdry_e)
        {
            int gid = mesh.GetTriangleGroup(tid);
            Index3i nbr_tris = mesh.GetTriNeighbourTris(tid);
            nbr_same = 0; nbr_diff = 0; bdry_e = 0; nbr_bg = 0;
            for (int j = 0; j < 3; ++j) {
                int nbr_t = nbr_tris[j];
                if (nbr_t == DMesh3.InvalidID) {
                    bdry_e++;
                    continue;
                }
                int nbr_g = mesh.GetTriangleGroup(nbr_t);
                if (nbr_g == BackgroundGroupID)
                    ++nbr_bg;
                if ( nbr_g == gid ) 
                    nbr_same++;
                else
                    nbr_diff++;
            }
        }
        private bool is_fin(int tid, bool include_loners)
        {
            int gid = mesh.GetTriangleGroup(tid);
            if (gid == BackgroundGroupID)
                return false;

            int nbr_same, nbr_diff, bdry_e, nbr_bg;
            count_same_nbrs(tid, out nbr_same, out nbr_diff, out nbr_bg, out bdry_e);

            bool bClip = (nbr_same == 1 && nbr_diff == 2) ||
                         (include_loners == true && nbr_same == 0 && nbr_diff == 3);

            if (DontClipEnclosedFins && bClip & nbr_bg == 0)
                bClip = false;

            return bClip;
        }




    }
}
