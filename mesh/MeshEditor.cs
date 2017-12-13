using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;


namespace g3
{
    // This class implements various ways to do low-level edits to a mesh, in
    // a robust/reliable way.
    //   - if operations fail in-progress, we try to back them out
    //   - (?)
    //
    // 
    public class MeshEditor
    {
        public DMesh3 Mesh;


        public MeshEditor(DMesh3 mesh)
        {
            Mesh = mesh;
        }



        public virtual int[] AddTriangleFan_OrderedVertexLoop(int center, int[] vertex_loop, int group_id = -1)
        {
            int N = vertex_loop.Length;
            int[] new_tris = new int[N];

            int i = 0;
            for ( i = 0; i < N; ++i ) {
                int a = vertex_loop[i];
                int b = vertex_loop[(i + 1) % N];

                Index3i newT = new Index3i(center, b, a);
                int new_tid = Mesh.AppendTriangle(newT, group_id);
                if (new_tid == DMesh3.InvalidID)
                    goto operation_failed;

                new_tris[i] = new_tid;
            }

            return new_tris;


            operation_failed:
                // remove what we added so far
                if (i > 0) {
                    if (remove_triangles(new_tris, i) == false)
                        throw new Exception("MeshConstructor.AddTriangleFan_OrderedVertexLoop: failed to add fan, and also falied to back out changes.");
                }
                return null;
        }





        public virtual int[] AddTriangleFan_OrderedEdgeLoop(int center, int[] edge_loop, int group_id = -1)
        {
            int N = edge_loop.Length;
            int[] new_tris = new int[N];

            int i = 0;
            for ( i = 0; i < N; ++i ) {
                if (Mesh.IsBoundaryEdge(edge_loop[i]) == false)
                    goto operation_failed;

                Index2i ev = Mesh.GetOrientedBoundaryEdgeV(edge_loop[i]);
                int a = ev.a, b = ev.b;

                Index3i newT = new Index3i(center, b, a);
                int new_tid = Mesh.AppendTriangle(newT, group_id);
                if (new_tid == DMesh3.InvalidID)
                    goto operation_failed;

                new_tris[i] = new_tid;
            }

            return new_tris;


            operation_failed:
                // remove what we added so far
                if (i > 0) {
                    if (remove_triangles(new_tris, i-1) == false)
                        throw new Exception("MeshConstructor.AddTriangleFan_OrderedEdgeLoop: failed to add fan, and also failed to back out changes.");
                }
                return null;
        }




        /// <summary>
        /// Trivial back-and-forth stitch between two vertex loops with same length. 
        /// Loops must have appropriate orientation (which is...??)
        /// [TODO] check and fail on bad orientation
        /// </summary>
        public virtual int[] StitchLoop(int[] vloop1, int[] vloop2, int group_id = -1)
        {
            int N = vloop1.Length;
            if (N != vloop2.Length)
                throw new Exception("MeshEditor.StitchLoop: loops are not the same length!!");

            int[] new_tris = new int[N * 2];

            int i = 0;
            for ( ; i < N; ++i ) {
                int a = vloop1[i];
                int b = vloop1[(i + 1) % N];
                int c = vloop2[i];
                int d = vloop2[(i + 1) % N];

                Index3i t1 = new Index3i(b, a, d);
                Index3i t2 = new Index3i(a, c, d);

                int tid1 = Mesh.AppendTriangle(t1, group_id);
                int tid2 = Mesh.AppendTriangle(t2, group_id);

                if (tid1 == DMesh3.InvalidID || tid2 == DMesh3.InvalidID)
                    goto operation_failed;

                new_tris[2 * i] = tid1;
                new_tris[2 * i + 1] = tid2;
            }

            return new_tris;


            operation_failed:
                // remove what we added so far
                if (i > 0) {
                    if (remove_triangles(new_tris, 2*(i-1)) == false)
                        throw new Exception("MeshConstructor.StitchLoop: failed to add all triangles, and also failed to back out changes.");
                }
                return null;
        }





        /// <summary>
        /// Stitch two sets of boundary edges that are provided as unordered pairs of edges, by
        /// adding triangulated quads between each edge pair. 
        /// If a failure is encountered during stitching, the triangles added up to that point are removed.
        /// </summary>
        public virtual int[] StitchUnorderedEdges(List<Index2i> EdgePairs, int group_id = -1)
        {
            int N = EdgePairs.Count;
            int[] new_tris = new int[N * 2];

            int i = 0;
            for (; i < N; ++i) {
                Index2i edges = EdgePairs[i];

                // look up and orient the first edge
                Index4i edge_a = Mesh.GetEdge(edges.a);
                if ( edge_a.d != DMesh3.InvalidID )
                    goto operation_failed;
                Index3i edge_a_tri = Mesh.GetTriangle(edge_a.c);
                int a = edge_a.a, b = edge_a.b;
                IndexUtil.orient_tri_edge(ref a, ref b, edge_a_tri);

                // look up and orient the second edge
                Index4i edge_b = Mesh.GetEdge(edges.b);
                if (edge_b.d != DMesh3.InvalidID)
                    goto operation_failed;
                Index3i edge_b_tri = Mesh.GetTriangle(edge_b.c);
                int c = edge_b.a, d = edge_b.b;
                IndexUtil.orient_tri_edge(ref c, ref d, edge_b_tri);

                // swap second edge (right? should this be a parameter?)
                int tmp = c; c = d; d = tmp;

                Index3i t1 = new Index3i(b, a, d);
                Index3i t2 = new Index3i(a, c, d);

                int tid1 = Mesh.AppendTriangle(t1, group_id);
                int tid2 = Mesh.AppendTriangle(t2, group_id);

                if (tid1 == DMesh3.InvalidID || tid2 == DMesh3.InvalidID)
                    goto operation_failed;

                new_tris[2 * i] = tid1;
                new_tris[2 * i + 1] = tid2;
            }

            return new_tris;

            operation_failed:
            // remove what we added so far
            if (i > 0) {
                if (remove_triangles(new_tris, 2 * (i - 1)) == false)
                    throw new Exception("MeshConstructor.StitchLoop: failed to add all triangles, and also failed to back out changes.");
            }
            return null;
        }






        /// <summary>
        /// Trivial back-and-forth stitch between two vertex spans with same length. 
        /// vertex ordering must reslut in appropriate orientation (which is...??)
        /// [TODO] check and fail on bad orientation
        /// </summary>
        public virtual int[] StitchSpan(int[] vspan1, int[] vspan2, int group_id = -1)
        {
            int N = vspan1.Length;
            if (N != vspan2.Length)
                throw new Exception("MeshEditor.StitchSpan: spans are not the same length!!");
            N--;

            int[] new_tris = new int[N * 2];

            int i = 0;
            for ( ; i < N; ++i ) {
                int a = vspan1[i];
                int b = vspan1[i + 1];
                int c = vspan2[i];
                int d = vspan2[i + 1];

                Index3i t1 = new Index3i(b, a, d);
                Index3i t2 = new Index3i(a, c, d);

                int tid1 = Mesh.AppendTriangle(t1, group_id);
                int tid2 = Mesh.AppendTriangle(t2, group_id);

                if (tid1 == DMesh3.InvalidID || tid2 == DMesh3.InvalidID)
                    goto operation_failed;

                new_tris[2 * i] = tid1;
                new_tris[2 * i + 1] = tid2;
            }

            return new_tris;


            operation_failed:
                // remove what we added so far
                if (i > 0) {
                    if (remove_triangles(new_tris, 2*(i-1)) == false)
                        throw new Exception("MeshConstructor.StitchLoop: failed to add all triangles, and also failed to back out changes.");
                }
                return null;
        }







        // [TODO] cannot back-out this operation right now
        //
        // Remove list of triangles. Values of triangles[] set to InvalidID are ignored.
        public bool RemoveTriangles(int[] triangles, bool bRemoveIsolatedVerts)
        {
            bool bAllOK = true;
            for (int i = 0; i < triangles.Length; ++i ) {
                if (triangles[i] == DMesh3.InvalidID)
                    continue;

                MeshResult result = Mesh.RemoveTriangle(triangles[i], bRemoveIsolatedVerts, false);
                if (result != MeshResult.Ok)
                    bAllOK = false;
            }
            return bAllOK;
        }

        // [TODO] cannot back-out this operation right now
        //
        // Remove list of triangles. Values of triangles[] set to InvalidID are ignored.
        public bool RemoveTriangles(IEnumerable<int> triangles, bool bRemoveIsolatedVerts)
        {
            bool bAllOK = true;
            foreach ( int tid in triangles ) {
                if (! Mesh.IsTriangle(tid) ) {
                    bAllOK = false;
                    continue;
                }
                MeshResult result = Mesh.RemoveTriangle(tid, bRemoveIsolatedVerts, false);
                if (result != MeshResult.Ok)
                    bAllOK = false;
            }
            return bAllOK;
        }

        // [TODO] cannot back-out this operation right now
        //
        // Remove all triangles identified by selectorF returning true
        public bool RemoveTriangles(Func<int,bool> selectorF, bool bRemoveIsolatedVerts)
        {
            bool bAllOK = true;
            int NT = Mesh.MaxTriangleID;
            for ( int ti = 0; ti < NT; ++ti ) {
                if (Mesh.IsTriangle(ti) == false || selectorF(ti) == false)
                    continue;
                MeshResult result = Mesh.RemoveTriangle(ti, bRemoveIsolatedVerts, false);
                if (result != MeshResult.Ok)
                    bAllOK = false;
            }
            return bAllOK;
        }








        /// <summary>
        /// Disconnect the given triangles from their neighbours, by duplicating "boundary" vertices, ie
        /// vertices on edges for which one triangle is in-set and the other is not. 
        /// If bComputeEdgePairs is true, we return list of old/new edge pairs (useful for stitching)
        /// [TODO] currently boundary-edge behaviour is to *not* duplicate boundary verts
        /// </summary>
        public bool SeparateTriangles(IEnumerable<int> triangles, bool bComputeEdgePairs, out List<Index2i> EdgePairs)
        {
            HashSet<int> in_set = new HashSet<int>(triangles);
            Dictionary<int, int> VertexMap = new Dictionary<int, int>();
            EdgePairs = null;
            HashSet<int> edges = null;
            List<Index2i> OldEdgeVerts = null;
            if (bComputeEdgePairs) {
                EdgePairs = new List<Index2i>();
                edges = new HashSet<int>();
                OldEdgeVerts = new List<Index2i>();
            }

            // duplicate vertices on edges that are on boundary of triangles roi
            foreach ( int tid in triangles ) {
                Index3i te = Mesh.GetTriEdges(tid);

                for ( int j = 0; j < 3; ++j ) {
                    Index2i et = Mesh.GetEdgeT(te[j]);
                    // [TODO] what about behavior where we want to also duplicate boundary verts??
                    if (et.b == DMesh3.InvalidID ||  (et.a == tid && in_set.Contains(et.b)) || (et.b == tid && in_set.Contains(et.a)))
                        te[j] = -1;
                }

                for ( int j = 0; j < 3; ++j ) {
                    if (te[j] == -1)
                        continue;
                    Index2i ev = Mesh.GetEdgeV(te[j]);
                    if (VertexMap.ContainsKey(ev.a) == false)
                        VertexMap[ev.a] = Mesh.AppendVertex(Mesh, ev.a);
                    if (VertexMap.ContainsKey(ev.b) == false)
                        VertexMap[ev.b] = Mesh.AppendVertex(Mesh, ev.b);

                    if (bComputeEdgePairs && edges.Contains(te[j]) == false) {
                        edges.Add(te[j]);
                        OldEdgeVerts.Add(ev);
                        EdgePairs.Add(new Index2i(te[j], -1));
                    }
                }
            }

            // update triangles
            foreach ( int tid in triangles ) {
                Index3i tv = Mesh.GetTriangle(tid);
                Index3i tv_new = tv;
                for ( int j = 0; j < 3; ++j ) {
                    int newv;
                    if (VertexMap.TryGetValue(tv[j], out newv)) 
                        tv_new[j] = newv;
                }
                if ( tv_new != tv ) {
                    Mesh.SetTriangle(tid, tv_new);
                }
            }

            if ( bComputeEdgePairs ) {
                for ( int k = 0; k < EdgePairs.Count; ++k ) {
                    Index2i old_ev = OldEdgeVerts[k];
                    int new_a = VertexMap[old_ev.a];
                    int new_b = VertexMap[old_ev.b];
                    int new_eid = Mesh.FindEdge(new_a, new_b);
                    Util.gDevAssert(new_eid != DMesh3.InvalidID);
                    EdgePairs[k] = new Index2i(EdgePairs[k].a, new_eid);
                }
            }

            return true;
        }



        /// <summary>
        /// Make a copy of provided triangles, with new vertices. You provide MapV because
        /// you know if you are doing a small subset or a full-mesh-copy.
        /// </summary>
        public List<int> DuplicateTriangles(IEnumerable<int> triangles, ref IndexMap MapV, int group_id = -1)
        {
            List<int> new_triangles = new List<int>();
            foreach ( int tid in triangles ) {
                Index3i tri = Mesh.GetTriangle(tid);
                for (int j = 0; j < 3; ++j) {
                    int vid = tri[j];
                    if (MapV.Contains(vid) == false) {
                        int new_vid = Mesh.AppendVertex(Mesh, vid);
                        MapV[vid] = new_vid;
                        tri[j] = new_vid;
                    } else {
                        tri[j] = MapV[vid];
                    }
                }
                int new_tid = Mesh.AppendTriangle(tri, group_id);
                new_triangles.Add(new_tid);
            }
            return new_triangles;
        }



        /// <summary>
        /// Reverse face orientation on a subset of triangles
        /// </summary>
        public void ReverseTriangles(IEnumerable<int> triangles, bool bFlipVtxNormals = true)
        {
            if ( bFlipVtxNormals == false ) { 
                foreach (int tid in triangles) {
                    Mesh.ReverseTriOrientation(tid);
                }

            } else {
                BitArray donev = new BitArray(Mesh.MaxVertexID);

                foreach (int tid in triangles) {
                    Mesh.ReverseTriOrientation(tid);

                    Index3i tri = Mesh.GetTriangle(tid);
                    for (int j = 0; j < 3; ++j) {
                        int vid = tri[j];
                        if (donev[vid] == false) {
                            Mesh.SetVertexNormal(vid, -Mesh.GetVertexNormal(vid));
                            donev[vid] = true;
                        }
                    }
                }
            }

        }





        // in ReinsertSubmesh, a problem can arise where the mesh we are inserting has duplicate triangles of
        // the base mesh. This can lead to problematic behavior later. We can do various things, like delete
        // and replace that existing triangle, or just use it instead of adding a new one. Or fail, or ignore it.
        // This enum/argument controls the behavior. 
        // However, fundamentally this kind of problem should be handled upstream!! For example by not trying
        // to remesh areas that contain nonmanifold geometry...
        public enum DuplicateTriBehavior
        {
            AssertContinue,         // check will not be done in Release!
            AssertAbort, UseExisting, Replace
        }


        // Assumption here is that Submesh has been modified, but boundary loop has
        // been preserved, and that old submesh has already been removed from this mesh.
        // So, we just have to append new vertices and then rewrite triangles
        // If new_tris or new_verts is non-null, we will return this info.
        // new_tris should be set to TriangleCount (ie it is not necessarily a map)
        // For new_verts, if we used an existing bdry vtx instead, we set the value to -(existing_index+1),
        // otherwise the value is new_index (+1 is to handle 0)
        //
        // Returns true if submesh successfully inserted, false if any triangles failed
        // (which happens if triangle would result in non-manifold mesh)
        public bool ReinsertSubmesh(DSubmesh3 sub, ref int[] new_tris, out IndexMap SubToNewV,
            DuplicateTriBehavior eDuplicateBehavior = DuplicateTriBehavior.AssertAbort)
        {
            if (sub.BaseBorderV == null)
                throw new Exception("MeshEditor.ReinsertSubmesh: Submesh does not have required boundary info. Call ComputeBoundaryInfo()!");

            DMesh3 submesh = sub.SubMesh;
            bool bAllOK = true;

            IndexFlagSet done_v = new IndexFlagSet(submesh.MaxVertexID, submesh.TriangleCount/2);
            SubToNewV = new IndexMap(submesh.MaxVertexID, submesh.VertexCount);

            int nti = 0;
            int NT = submesh.MaxTriangleID;
            for (int ti = 0; ti < NT; ++ti ) {
                if (submesh.IsTriangle(ti) == false)
                    continue;

                Index3i sub_t = submesh.GetTriangle(ti);
                int gid = submesh.GetTriangleGroup(ti);

                Index3i new_t = Index3i.Zero;
                for ( int j = 0; j < 3; ++j ) {
                    int sub_v = sub_t[j];
                    int new_v = -1;
                    if (done_v[sub_v] == false) {

                        // first check if this is a boundary vtx on submesh and maps to a bdry vtx on base mesh
                        if (submesh.IsBoundaryVertex(sub_v)) {
                            int base_v = (sub_v < sub.SubToBaseV.size) ? sub.SubToBaseV[sub_v] : -1;
                            if ( base_v >= 0 && Mesh.IsVertex(base_v) && sub.BaseBorderV[base_v] == true ) { 
                                // [RMS] this should always be true, but assert in tests to find out
                                Debug.Assert(Mesh.IsBoundaryVertex(base_v));
                                if (Mesh.IsBoundaryVertex(base_v)) {
                                    new_v = base_v;
                                }
                            }
                        }

                        // if that didn't happen, append new vtx
                        if ( new_v == -1 ) {
                            new_v = Mesh.AppendVertex(submesh, sub_v);
                        }

                        SubToNewV[sub_v] = new_v;
                        done_v[sub_v] = true;

                    } else 
                        new_v = SubToNewV[sub_v];

                    new_t[j] = new_v;
                }

                // try to handle duplicate-tri case
                if (eDuplicateBehavior == DuplicateTriBehavior.AssertContinue) {
                    Debug.Assert(Mesh.FindTriangle(new_t.a, new_t.b, new_t.c) == DMesh3.InvalidID);
                } else {
                    int existing_tid = Mesh.FindTriangle(new_t.a, new_t.b, new_t.c);
                    if (existing_tid != DMesh3.InvalidID) {
                        if (eDuplicateBehavior == DuplicateTriBehavior.AssertAbort) {
                            Debug.Assert(existing_tid == DMesh3.InvalidID);
                            return false;
                        } else if (eDuplicateBehavior == DuplicateTriBehavior.UseExisting) {
                            if (new_tris != null)
                                new_tris[nti++] = existing_tid;
                            continue;
                        } else if (eDuplicateBehavior == DuplicateTriBehavior.Replace) {
                            Mesh.RemoveTriangle(existing_tid, false);
                        }
                    }
                }


                int new_tid = Mesh.AppendTriangle(new_t, gid);
                Debug.Assert(new_tid != DMesh3.InvalidID && new_tid != DMesh3.NonManifoldID);
                if ( ! Mesh.IsTriangle(new_tid) )
                    bAllOK = false;

                if (new_tris != null)
                    new_tris[nti++] = new_tid;
            }

            return bAllOK;
        }







        public bool AppendMesh(IMesh appendMesh, int appendGID = -1)
        {
            int[] mapV;
            return AppendMesh(appendMesh, out mapV, appendGID);
        }
        public bool AppendMesh(IMesh appendMesh, out int[] mapV, int appendGID = -1)
        {
            mapV = new int[appendMesh.MaxVertexID];
            foreach (int vid in appendMesh.VertexIndices() ) {
                
                NewVertexInfo vinfo = appendMesh.GetVertexAll(vid);
                int newvid = Mesh.AppendVertex(vinfo);
                mapV[vid] = newvid;
            }

            foreach (int tid in appendMesh.TriangleIndices()) {
                Index3i t = appendMesh.GetTriangle(tid);
                t.a = mapV[t.a];
                t.b = mapV[t.b];
                t.c = mapV[t.c];
                int gid = appendMesh.GetTriangleGroup(tid);
                if (appendGID >= 0)
                    gid = appendGID;
                Mesh.AppendTriangle(t, gid);
            }

            return true;
        }
        public static DMesh3 Combine(params IMesh[] appendMeshes )
        {
            DMesh3 m = new DMesh3();
            MeshEditor editor = new MeshEditor(m);
            foreach ( var mesh in appendMeshes ) {
                editor.AppendMesh(mesh, m.AllocateTriangleGroup());
            }
            return m;
        }


        public bool AppendMesh(IMesh appendMesh, IndexMap mergeMapV, out int[] mapV, int appendGID = -1)
        {
            mapV = new int[appendMesh.MaxVertexID];
            foreach (int vid in appendMesh.VertexIndices()) {
                if (mergeMapV.Contains(vid)) {
                    mapV[vid] = mergeMapV[vid];
                } else {
                    NewVertexInfo vinfo = appendMesh.GetVertexAll(vid);
                    int newvid = Mesh.AppendVertex(vinfo);
                    mapV[vid] = newvid;
                }
            }

            foreach (int tid in appendMesh.TriangleIndices()) {
                Index3i t = appendMesh.GetTriangle(tid);
                t.a = mapV[t.a];
                t.b = mapV[t.b];
                t.c = mapV[t.c];
                int gid = appendMesh.GetTriangleGroup(tid);
                if (appendGID >= 0)
                    gid = appendGID;
                Mesh.AppendTriangle(t, gid);
            }

            return true;
        }




        public void AppendBox(Frame3f frame, float size)
        {
            AppendBox(frame, size * Vector3f.One);
        }
        public void AppendBox(Frame3f frame, Vector3f size)
        {
            TrivialBox3Generator boxgen = new TrivialBox3Generator() {
                Box = new Box3d(frame, size),
                NoSharedVertices = false
            };
            boxgen.Generate();
            DMesh3 mesh = new DMesh3();
            boxgen.MakeMesh(mesh);
            AppendMesh(mesh, Mesh.AllocateTriangleGroup());
        }




        /// <summary>
        /// Remove all bowtie vertices in mesh. Makes one pass unless
        ///   bRepeatUntilClean = true, in which case repeats until no more bowties found
        /// Returns true if any vertices were removed
        /// </summary>
        public bool RemoveAllBowtieVertices(bool bRepeatUntilClean)
        {
            int nRemoved = 0;

            while (true) {
                List<int> bowties = new List<int>();
                foreach (int vID in Mesh.VertexIndices()) {
                    if (Mesh.IsBowtieVertex(vID))
                        bowties.Add(vID);
                }
                if (bowties.Count == 0)
                    break;

                foreach (int vID in bowties) {
                    MeshResult result = Mesh.RemoveVertex(vID, true, false);
                    Debug.Assert(result == MeshResult.Ok);
                    nRemoved++;
                }
                if (bRepeatUntilClean == false)
                    break;
            }
            return (nRemoved > 0);
        }






        // this is for backing out changes we have made...
        bool remove_triangles(int[] tri_list, int count)
        {
            for (int i = 0; i < count; ++i) {
                MeshResult result = Mesh.RemoveTriangle(tri_list[i], false, false);
                if (result != MeshResult.Ok)
                    return false;
            }
            return true;
        }


    }
}
