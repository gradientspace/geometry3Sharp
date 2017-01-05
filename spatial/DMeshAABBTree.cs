using System;
using System.Collections.Generic;
using System.Linq;

namespace g3
{
    public class DMeshAABBTree3
    {
        DMesh3 mesh;

        public DMeshAABBTree3(DMesh3 m)
        {
            mesh = m;
        }


        // storage for box nodes. 
        //   - box_to_index is a pointer into index_list
        //   - box_centers and box_extents are the centers/extents of the bounding boxes
        DVector<int> box_to_index;
        DVector<Vector3f> box_centers;
        DVector<Vector3f> box_extents;

        // list of indices for a given box. There is *no* marker/sentinel between
        // boxes, you have to get the starting index from box_to_index[]
        //
        // There are three kinds of records:
        //   - if i < triangles_end, then the list is a number of triangles,
        //       stored as [N t1 t2 t3 ... tN]
        //   - if i > triangles_end and index_list[i] < 0, this is a single-child
        //       internal box, with index (-index_list[i])-1     (shift-by-one in case actual value is 0!)
        //   - if i > triangles_end and index_list[i] > 0, this is a two-child
        //       internal box, with indices index_list[i]-1 and index_list[i+1]-1
        DVector<int> index_list;

        // index_list[i] for i < triangles_end is a triangle-index list, otherwise box-index pair/single
        int triangles_end = -1;

        // box_to_index[root_index] is the root node of the tree
        int root_index = -1;








        // strategy here is:
        //  1) partition triangles by vertex one-rings into leaf boxes
        //      1a) 
        public void BuildByOneRings()
        {
            box_to_index = new DVector<int>();
            box_centers = new DVector<Vector3f>();
            box_extents = new DVector<Vector3f>();
            int iBoxCur = 0;

            index_list = new DVector<int>();
            int iIndicesCur = 0;

            // replace w/ BitArray ?
            byte[] used_triangles = new byte[mesh.MaxTriangleID];
            Array.Clear(used_triangles, 0, used_triangles.Length);

            int[] temp_tris = new int[1024];

            // first pass: cluster by one-ring, but if # of free tris
            //  in a ring is small (< 3), push onto spill list to try again,
            //  because those tris might be picked up by a bigger cluster
            DVector<int> spill = new DVector<int>();
            foreach ( int vid in mesh.VertexIndices() ) {
                // collect free triangles
                int tti = 0;
                foreach ( int tid in mesh.VtxTrianglesItr(vid) ) {
                    if ( used_triangles[tid] == 0 ) 
                        temp_tris[tti++] = tid;
                }
                if (tti == 0)
                    continue;
                // if we only had a couple free triangles, wait and see if
                // they get picked up by another vert
                if (tti < 3) {
                    spill.Add(vid);
                    continue;
                }

                // append new box
                AxisAlignedBox3f box = AxisAlignedBox3f.Empty;
                int iBox = iBoxCur++;
                box_to_index.insert(iIndicesCur, iBox);

                index_list.insert(tti, iIndicesCur++);
                for (int i = 0; i < tti; ++i) {
                    index_list.insert(temp_tris[i], iIndicesCur++);
                    used_triangles[temp_tris[i]]++;     // incrementing for sanity check below, just need to set to 1
                    box.Contain(mesh.GetTriBounds(temp_tris[i]));
                }

                box_centers.insert(box.Center, iBox);
                box_extents.insert(box.Extents, iBox);
            }


            // OK, check any spill vertices. most are probably gone now, but
            // a few stray triangles might still exist
            //  todo: nearly same code as above, can move to function??
            int N = spill.Length;
            for ( int si = 0; si < N; ++si ) {
                int vid = spill[si];

                 // collect free triangles
                int tti = 0;
                foreach ( int tid in mesh.VtxTrianglesItr(vid) ) {
                    if ( used_triangles[tid] == 0 ) 
                        temp_tris[tti++] = tid;
                }
                if (tti == 0)
                    continue;
                
                // append new box
                AxisAlignedBox3f box = AxisAlignedBox3f.Empty;
                int iBox = iBoxCur++;
                box_to_index.insert(iIndicesCur, iBox);

                index_list.insert(tti, iIndicesCur++);
                for (int i = 0; i < tti; ++i) {
                    index_list.insert(temp_tris[i], iIndicesCur++);
                    used_triangles[temp_tris[i]]++;     // incrementing for sanity check below, just need to set to 1
                    box.Contain(mesh.GetTriBounds(temp_tris[i]));
                }

                box_centers.insert(box.Center, iBox);
                box_extents.insert(box.Extents, iBox);
            }


            // SANITY CHECK - REMOVE!!
            foreach ( int tid in mesh.TriangleIndices() ) {
                int n = used_triangles[tid];
                if (n != 1)
                    Util.gBreakToDebugger();
            }

            // keep track of where triangle lists end
            triangles_end = iIndicesCur;

            // ok, now repeatedly cluster current layer of N boxes into N/2 (or N/2+1) boxes,
            // until we hit a 1-box layer, which is root of the tree
            int nPrevEnd = iBoxCur;
            int nLayerSize = cluster_boxes(0, iBoxCur, ref iBoxCur, ref iIndicesCur);
            int iStart = nPrevEnd;
            int iCount = iBoxCur - nPrevEnd;
            while ( nLayerSize > 1 ) {
                nPrevEnd = iBoxCur;
                nLayerSize = cluster_boxes(iStart, iCount, ref iBoxCur, ref iIndicesCur);
                iStart = nPrevEnd;
                iCount = iBoxCur - nPrevEnd;
            }

            root_index = iBoxCur - 1;
        }




        public int box_triangles(int vid, int[] used_triangles, int[] temp_tris, 
            ref int iBoxCur, ref int iIndicesCur,
            DVector<int> spill, int nSpillThresh )
        {
            // collect free triangles
            int tti = 0;
            foreach ( int tid in mesh.VtxTrianglesItr(vid) ) {
                if ( used_triangles[tid] == 0 ) 
                    temp_tris[tti++] = tid;
            }

            // none free, get out
            if (tti == 0)
                return 0;

            // if we only had a couple free triangles, wait and see if
            // they get picked up by another vert
            if (tti < nSpillThresh) {
                spill.Add(vid);
                return tti;
            }

            // append new box
            AxisAlignedBox3f box = AxisAlignedBox3f.Empty;
            int iBox = iBoxCur++;
            box_to_index.insert(iIndicesCur, iBox);

            index_list.insert(tti, iIndicesCur++);
            for (int i = 0; i < tti; ++i) {
                index_list.insert(temp_tris[i], iIndicesCur++);
                used_triangles[temp_tris[i]]++;     // incrementing for sanity check below, just need to set to 1
                box.Contain(mesh.GetTriBounds(temp_tris[i]));
            }

            box_centers.insert(box.Center, iBox);
            box_extents.insert(box.Extents, iBox);
            return tti;
        }





        // Turn a span of N boxes into N/2 boxes, by pairing boxes
        // Except, of course, if N is odd, then we get N/2+1, where the +1
        // box has a single child box (ie just a copy).
        // [TODO] instead merge that extra box into on of parents? Reduces tree depth by 1
        public int cluster_boxes(int iStart, int iCount, ref int iBoxCur, ref int iIndicesCur)
        {
            int[] indices = new int[iCount];
            for (int i = 0; i < iCount; ++i)
                indices[i] = iStart + i;

            int nDim = 0;
            Array.Sort(indices, (a, b) => {
                float axis_min_a = box_centers[a][nDim] - box_extents[a][nDim];
                float axis_min_b = box_centers[b][nDim] - box_extents[b][nDim];
                return (axis_min_a == axis_min_b) ? 0 :
                            (axis_min_a < axis_min_b) ? -1 : 1;
            });

            int nPairs = iCount / 2;
            int nLeft = iCount - 2 * nPairs;

            // this is dumb! but lets us test the rest...
            for ( int pi = 0; pi < nPairs; pi++ ) {
                int i0 = indices[2*pi];
                int i1 = indices[2*pi + 1];

                Vector3f center, extent;
                get_combined_box(i0, i1, out center, out extent);

                // append new box
                int iBox = iBoxCur++;
                box_to_index.insert(iIndicesCur, iBox);

                index_list.insert(i0+1, iIndicesCur++);
                index_list.insert(i1+1, iIndicesCur++);

                box_centers.insert(center, iBox);
                box_extents.insert(center, iBox);
            }

            // [todo] could we merge with last other box? need a way to tell
            //   that there are 3 children though...could use negative index for that?
            if ( nLeft > 0 ) {
                if (nLeft > 1)
                    Util.gBreakToDebugger();

                int iLeft = indices[2*nPairs];

                // duplicate box at this level... ?
                int iBox = iBoxCur++;
                box_to_index.insert(iIndicesCur, iBox);

                // negative index means only one child
                index_list.insert(-(iLeft+1), iIndicesCur++);
                
                box_centers.insert(box_centers[iLeft], iBox);
                box_extents.insert(box_extents[iLeft], iBox);
            }

            return nPairs + nLeft;
        }






        // construct box that contains two boxes
        public void get_combined_box(int b0, int b1, out Vector3f center, out Vector3f extent)
        {
            Vector3f c0 = box_centers[b0];
            Vector3f e0 = box_extents[b0];
            Vector3f c1 = box_centers[b1];
            Vector3f e1 = box_extents[b1];

            float minx = Math.Min(c0.x - e0.x, c1.x - e1.x);
            float maxx = Math.Max(c0.x + e0.x, c1.x + e1.x);
            float miny = Math.Min(c0.y - e0.y, c1.y - e1.y);
            float maxy = Math.Max(c0.y + e0.y, c1.y + e1.y);
            float minz = Math.Min(c0.z - e0.z, c1.z - e1.z);
            float maxz = Math.Max(c0.z + e0.z, c1.z + e1.z);

            center = new Vector3f(0.5f * (minx + maxx), 0.5f * (miny + maxy), 0.5f * (minz + maxz));
            extent = new Vector3f(0.5f * (maxx - minx), 0.5f * (maxy - miny), 0.5f * (maxz - minz));
        }


        public AxisAlignedBox3f get_box(int iBox)
        {
            Vector3f c = box_centers[iBox];
            Vector3f e = box_extents[iBox];
            e += 10.0f*MathUtil.Epsilonf;      // because of float/double casts, box may drift to the point
                                               // where mesh vertex will be slightly outside box
            return new AxisAlignedBox3f(c - e, c + e);
        }






        // make sure we can reach every tri in mesh through tree (also demo of how to traverse tree...)
        public void TestCoverage()
        {
            int[] tri_counts = new int[mesh.MaxTriangleID];
            Array.Clear(tri_counts, 0, tri_counts.Length);
            int[] parent_indices = new int[box_to_index.Length];
            Array.Clear(parent_indices, 0, parent_indices.Length);

            test_coverage(tri_counts, parent_indices, root_index);

            foreach (int ti in mesh.TriangleIndices())
                if (tri_counts[ti] != 1)
                    Util.gBreakToDebugger();
        }
        private void test_coverage(int[] tri_counts, int[] parent_indices, int iCur)
        {
            int idx = box_to_index[iCur];

            if ( idx < triangles_end ) {
                // triange-list case, array is [N t1 t2 ... tN]
                int n = index_list[idx];
                AxisAlignedBox3f box = get_box(iCur);
                for ( int i = 1; i <= n; ++i ) {
                    int ti = index_list[idx + i];
                    tri_counts[ti]++;

                    Index3i tv = mesh.GetTriangle(ti);
                    for ( int j = 0; j < 3; ++j ) {
                        Vector3f v = (Vector3f)mesh.GetVertex(tv[j]);
                        if (!box.Contains(v))
                            Util.gBreakToDebugger();
                    }
                }

            } else {
                int i0 = index_list[idx];
                if ( i0 < 0 ) {
                    // negative index means we only have one 'child' box to descend into
                    i0 = (-i0) - 1;
                    parent_indices[i0] = iCur;
                    test_coverage(tri_counts, parent_indices, i0);
                } else {
                    // positive index, two sequential child box indices to descend into
                    i0 = i0 - 1;
                    parent_indices[i0] = iCur;
                    test_coverage(tri_counts, parent_indices, i0);
                    int i1 = index_list[idx + 1];
                    i1 = i1 - 1;
                    parent_indices[i1] = iCur;
                    test_coverage(tri_counts, parent_indices, i1);
                }
            }
        }




    }
}
