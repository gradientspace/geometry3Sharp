using System;
using System.Collections.Generic;
using System.Diagnostics;


namespace g3
{


    public interface IMeshComponentManager
    {
        void AddComponent(MeshDecomposition.Component C);
        void ClearAllComponents();
    }



    // [TODO] 
    //    - build strategy using AABB tree traversal
    //    - option to store components here (currently if client does not hold, we are discarding)
    public class MeshDecomposition
    {
        DMesh3 mesh;

        public int MaxComponentSize { set; get; }
        public bool TrackVertexMapping = true;

        Index2i[] mapTo;
        DVector<int> mapToMulti;

        public IMeshComponentManager Manager { set; get; }


        public struct Component
        {
            public int id;              // probably linear index

            public int[] triangles;     // new triangles (ie for submesh)
            public int tri_count;

    
            public int[] source_vertices;   // source vertices by index in submes
                                            // eg if we have vertex a in triangles[0]
                                            // then the vertex is mesh.vertex[source_vertex[a]]
        }


        public MeshDecomposition(DMesh3 mesh, IMeshComponentManager manager)
        {
            MaxComponentSize = 62000;      // max for unity is 64

            this.mesh = mesh;
            this.Manager = manager;
        }



        public void BuildLinear()
        {
            int NV = mesh.MaxVertexID;

            if (TrackVertexMapping) {
                mapTo = new Index2i[NV];
                for (int i = 0; i < NV; ++i)
                    mapTo[i] = Index2i.Zero;
                mapToMulti = new DVector<int>();
            }

            // temporary map from orig vertices to submesh vertices
            int[] mapToCur = new int[NV];
            Array.Clear(mapToCur, 0, mapToCur.Length);

            int nT = mesh.MaxTriangleID;

            // by eulers formula we generally have NT =~ 2*NV, so
            // as long as we stay within bounds for T we are probably ok
            // (Although if mesh is all disconnected triangles, we're fucked...)
            int[] cur_subt = new int[MaxComponentSize];
            int[] cur_subv = new int[MaxComponentSize];
            int subti = 0;
            int subi = 1;


            Action add_component = () => {
                Index2i mapRange;
                int max_subv;
                Component new_comp = extract_submesh(subi++, cur_subt, subti, mapToCur, cur_subv, out mapRange, out max_subv);

                // [TODO] perhaps manager can request smaller chunks?
                Manager.AddComponent(new_comp);

                Array.Clear(cur_subt, 0, subti);
                subti = 0;
                Array.Clear(mapToCur, mapRange.a, mapRange.b - mapRange.a + 1);
                Array.Clear(cur_subv, 0, max_subv);
            };



            int[] tri_order = get_tri_order_by_axis_sort();
            int tri_count = tri_order.Length;

            for (int ii = 0; ii < tri_count; ++ii) {
                int ti = tri_order[ii];

                cur_subt[subti++] = ti;

                if (subti == MaxComponentSize)
                    add_component();
            }
            if (subti > 0 )
                add_component();

        }



        int[] get_tri_order_by_axis_sort()
        {
            int i = 0;
            int[] tri_order = new int[mesh.TriangleCount];

            int nT = mesh.MaxTriangleID;
            for ( int ti = 0; ti < nT; ++ti ) {
                if (mesh.IsTriangle(ti))
                    tri_order[i++] = ti;
            }

            Array.Sort(tri_order, (t0, t1) => {
                double f0 = mesh.GetTriCentroid(t0).x;
                double f1 = mesh.GetTriCentroid(t1).x;
                return (f0 == f1) ? 0 : (f0 < f1) ? -1 : 1;
            });

            return tri_order;
        }



        // create Component from triangles
        Component extract_submesh(int submesh_index, int[] subt, int Nt, int[] mapToCur, int[] subv, 
            out Index2i mapRange, out int max_subv )
        {
            int subvi = 0;

            Component C = new Component();
            C.id = submesh_index;
            C.triangles = new int[Nt * 3];
            C.tri_count = Nt;

            mapRange = new Index2i(int.MaxValue, int.MinValue);

            // construct list of triangles and vertex map
            for (int ti = 0; ti < Nt; ++ti) {
                int tid = subt[ti];
                Index3i tri = mesh.GetTriangle(tid);
                for (int j = 0; j < 3; ++j) {
                    int vid = tri[j];
                    if (mapToCur[vid] == 0) {
                        mapToCur[vid] = (subvi + 1);
                        subv[subvi] = vid;

                        if (vid < mapRange.a) mapRange.a = vid;
                        else if (vid > mapRange.b) mapRange.b = vid;

                        if ( TrackVertexMapping )
                            add_submesh_mapv(vid, C.id, subvi);

                        subvi++;
                    }
                    C.triangles[3*ti+j] = mapToCur[vid] - 1;
                }
            }

            C.source_vertices = new int[subvi];
            Array.Copy(subv, C.source_vertices, subvi);
            max_subv = subvi;

            return C;
        }




        // optimizations:
        //    - no need to store negative-count. that means we could
        //      save one integer per list by using .a or .b
        //      (but code would be horrible...)
        void add_submesh_mapv(int orig_vid, int submesh_i, int submesh_vid)
        {
            // if we have not used this vertex at all, we can store one 
            // (submesh,vid) pair in mapTo
            if (mapTo[orig_vid].a == 0) {
                mapTo[orig_vid].a = submesh_i;
                mapTo[orig_vid].b = submesh_vid;
            } else { 
                // collision. Need to handle

                if ( mapTo[orig_vid].a > 0 ) {
                    // if this is the first collision, we push the original
                    // index pair onto the multi-list, then this new one

                    int idx0 = mapToMulti.size;
                    mapToMulti.push_back(mapTo[orig_vid].a);
                    mapToMulti.push_back(mapTo[orig_vid].b);
                    mapToMulti.push_back(-1);       // end of list

                    int idx1 = mapToMulti.size;
                    mapToMulti.push_back(submesh_i);
                    mapToMulti.push_back(submesh_vid);
                    mapToMulti.push_back(idx0);       // point to first element

                    mapTo[orig_vid].a = -2;     // negative element count
                    mapTo[orig_vid].b = idx1;   // point to second element

                } else {
                    // we're already using the multi-list for this index,
                    // push this new index pair onto head of list

                    mapTo[orig_vid].a--;        // increment negative-counter
                    int cur_front = mapTo[orig_vid].b;

                    int idx = mapToMulti.size;
                    mapToMulti.push_back(submesh_i);
                    mapToMulti.push_back(submesh_vid);
                    mapToMulti.push_back(cur_front);    // point to current front of list

                    mapTo[orig_vid].b = idx;            // point to new element
                }



            }
        }



    }
}
