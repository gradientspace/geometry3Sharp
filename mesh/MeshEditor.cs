using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;


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



        public virtual int[] AddTriangleStrip(IList<Frame3f> frames, IList<Interval1d> spans, int group_id = -1)
        {
            int N = frames.Count;
            if (N != spans.Count)
                throw new Exception("MeshEditor.AddTriangleStrip: spans list is not the same size!");
            int[] new_tris = new int[2*(N-1)];

            int prev_a = -1, prev_b = -1;
            int i = 0, ti = 0;
            for (i = 0; i < N; ++i) {
                Frame3f f = frames[i];
                Interval1d span = spans[i];

                Vector3d va = f.Origin + (float)span.a * f.Y;
                Vector3d vb = f.Origin + (float)span.b * f.Y;

                // [TODO] could compute normals here...

                int a = Mesh.AppendVertex(va);
                int b = Mesh.AppendVertex(vb);

                if ( prev_a != -1 ) {
                    new_tris[ti++] = Mesh.AppendTriangle(prev_a, b, prev_b);
                    new_tris[ti++] = Mesh.AppendTriangle(prev_a, a, b);

                }
                prev_a = a; prev_b = b;
            }

            return new_tris;
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
                if (new_tid < 0)
                    goto operation_failed;

                new_tris[i] = new_tid;
            }

            return new_tris;


            operation_failed:
                // remove what we added so far
                if (i > 0) {
                    if (remove_triangles(new_tris, i) == false)
                        throw new Exception("MeshEditor.AddTriangleFan_OrderedVertexLoop: failed to add fan, and also falied to back out changes.");
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
                if (new_tid < 0)
                    goto operation_failed;

                new_tris[i] = new_tid;
            }

            return new_tris;


            operation_failed:
                // remove what we added so far
                if (i > 0) {
                    if (remove_triangles(new_tris, i-1) == false)
                        throw new Exception("MeshEditor.AddTriangleFan_OrderedEdgeLoop: failed to add fan, and also failed to back out changes.");
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
                new_tris[2 * i] = tid1;
                new_tris[2 * i + 1] = tid2;

                if (tid1 < 0 || tid2 < 0)
                    goto operation_failed;
            }

            return new_tris;


            operation_failed:
                // remove what we added so far
                if (i > 0) {
                    if (remove_triangles(new_tris, 2*i+1) == false)
                        throw new Exception("MeshEditor.StitchLoop: failed to add all triangles, and also failed to back out changes.");
                }
                return null;
        }







        /// <summary>
        /// Trivial back-and-forth stitch between two vertex loops with same length. 
        /// If nearest vertices of input loops would not be matched, cycles loops so
        /// that this is the case. 
        /// Loops must have appropriate orientation.
        /// </summary>
        public virtual int[] StitchVertexLoops_NearestV(int[] loop0, int[] loop1, int group_id = -1)
        {
            int N = loop0.Length;
            Index2i iBestPair = Index2i.Zero;
            double best_dist = double.MaxValue;
            for (int i = 0; i < N; ++i) {
                Vector3d v0 = Mesh.GetVertex(loop0[i]);
                for (int j = 0; j < N; ++j) {
                    double dist_sqr = v0.DistanceSquared(Mesh.GetVertex(loop1[j]));
                    if (dist_sqr < best_dist) {
                        best_dist = dist_sqr;
                        iBestPair = new Index2i(i, j);
                    }
                }
            }
            if (iBestPair.a != iBestPair.b) {
                int[] newLoop0 = new int[N];
                int[] newLoop1 = new int[N];
                for (int i = 0; i < N; ++i) {
                    newLoop0[i] = loop0[(iBestPair.a + i) % N];
                    newLoop1[i] = loop1[(iBestPair.b + i) % N];
                }
                return StitchLoop(newLoop0, newLoop1, group_id);
            } else {
                return StitchLoop(loop0, loop1, group_id);
            }

        }






        /// <summary>
        /// Stitch two sets of boundary edges that are provided as unordered pairs of edges, by
        /// adding triangulated quads between each edge pair. 
        /// If bAbortOnFailure==true and a failure is encountered during stitching, the triangles added up to that point are removed.
        /// If bAbortOnFailure==false, failures are ignored and the returned triangle list may contain invalid values!
        /// </summary>
        public virtual int[] StitchUnorderedEdges(List<Index2i> EdgePairs, int group_id, bool bAbortOnFailure, out bool stitch_incomplete)
        {
            int N = EdgePairs.Count;
            int[] new_tris = new int[N * 2];
            if (bAbortOnFailure == false) {
                for (int k = 0; k < new_tris.Length; ++k)
                    new_tris[k] = DMesh3.InvalidID;
            }
            stitch_incomplete = false;

            int i = 0;
            for (; i < N; ++i) {
                Index2i edges = EdgePairs[i];

                // look up and orient the first edge
                Index4i edge_a = Mesh.GetEdge(edges.a);
                if (edge_a.d != DMesh3.InvalidID) {
                    if (bAbortOnFailure) goto operation_failed;
                    else { stitch_incomplete = true; continue; }
                }
                Index3i edge_a_tri = Mesh.GetTriangle(edge_a.c);
                int a = edge_a.a, b = edge_a.b;
                IndexUtil.orient_tri_edge(ref a, ref b, edge_a_tri);

                // look up and orient the second edge
                Index4i edge_b = Mesh.GetEdge(edges.b);
                if (edge_b.d != DMesh3.InvalidID) {
                    if (bAbortOnFailure) goto operation_failed;
                    else { stitch_incomplete = true; continue; }
                }
                Index3i edge_b_tri = Mesh.GetTriangle(edge_b.c);
                int c = edge_b.a, d = edge_b.b;
                IndexUtil.orient_tri_edge(ref c, ref d, edge_b_tri);

                // swap second edge (right? should this be a parameter?)
                int tmp = c; c = d; d = tmp;

                Index3i t1 = new Index3i(b, a, d);
                Index3i t2 = new Index3i(a, c, d);

                int tid1 = Mesh.AppendTriangle(t1, group_id);
                int tid2 = Mesh.AppendTriangle(t2, group_id);

                if (tid1 < 0 || tid2 < 0) {
                    if (bAbortOnFailure) goto operation_failed;
                    else { stitch_incomplete = true; continue; }
                }

                new_tris[2 * i] = tid1;
                new_tris[2 * i + 1] = tid2;
            }

            return new_tris;

            operation_failed:
            // remove what we added so far
            if (i > 0) {
                if (remove_triangles(new_tris, 2 * (i - 1)) == false)
                    throw new Exception("MeshEditor.StitchLoop: failed to add all triangles, and also failed to back out changes.");
            }
            return null;
        }
        public virtual int[] StitchUnorderedEdges(List<Index2i> EdgePairs, int group_id = -1, bool bAbortOnFailure = true)
        {
            bool incomplete = false;
            return StitchUnorderedEdges(EdgePairs, group_id, bAbortOnFailure, out incomplete);
        }






        /// <summary>
        /// Trivial back-and-forth stitch between two vertex spans with same length. 
        /// vertex ordering must reslut in appropriate orientation (which is...??)
        /// [TODO] check and fail on bad orientation
        /// </summary>
        public virtual int[] StitchSpan(IList<int> vspan1, IList<int> vspan2, int group_id = -1)
        {
            int N = vspan1.Count;
            if (N != vspan2.Count)
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

                if (tid1 < 0 || tid2 < 0)
                    goto operation_failed;

                new_tris[2 * i] = tid1;
                new_tris[2 * i + 1] = tid2;
            }

            return new_tris;


            operation_failed:
                // remove what we added so far
                if (i > 0) {
                    if (remove_triangles(new_tris, 2*(i-1)) == false)
                        throw new Exception("MeshEditor.StitchLoop: failed to add all triangles, and also failed to back out changes.");
                }
                return null;
        }







        // [TODO] cannot back-out this operation right now
        //
        // Remove list of triangles. Values of triangles[] set to InvalidID are ignored.
        public bool RemoveTriangles(IList<int> triangles, bool bRemoveIsolatedVerts)
        {
            bool bAllOK = true;
            for (int i = 0; i < triangles.Count; ++i ) {
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

        public static bool RemoveTriangles(DMesh3 Mesh, IList<int> triangles, bool bRemoveIsolatedVerts = true) {
            MeshEditor editor = new MeshEditor(Mesh);
            return editor.RemoveTriangles(triangles, bRemoveIsolatedVerts);
        }
        public static bool RemoveTriangles(DMesh3 Mesh, IEnumerable<int> triangles, bool bRemoveIsolatedVerts = true) {
            MeshEditor editor = new MeshEditor(Mesh);
            return editor.RemoveTriangles(triangles, bRemoveIsolatedVerts);
        }


        /// <summary>
        /// Remove 'loner' triangles that have no connected neighbours. 
        /// </summary>
        public static bool RemoveIsolatedTriangles(DMesh3 mesh)
        {
            MeshEditor editor = new MeshEditor(mesh);
            return editor.RemoveTriangles((tid) => {
                Index3i tnbrs = mesh.GetTriNeighbourTris(tid);
                return (tnbrs.a == DMesh3.InvalidID && tnbrs.b == DMesh3.InvalidID && tnbrs.c == DMesh3.InvalidID);
            }, true);
        }



        /// <summary>
        /// Remove 'fin' triangles that have only one connected triangle.
        /// Removing one fin can create another, by default will keep iterating
        /// until all fins removed (in a not very efficient way!).
        /// Pass bRepeatToConvergence=false to only do one pass.
        /// [TODO] if we are repeating, construct face selection from nbrs of first list and iterate over that on future passes!
        /// </summary>
        public static int RemoveFinTriangles(DMesh3 mesh, Func<DMesh3, int, bool> removeF = null, bool bRepeatToConvergence = true)
        {
            MeshEditor editor = new MeshEditor(mesh);

            int nRemoved = 0;
            List<int> to_remove = new List<int>();
            repeat:
            foreach ( int tid in mesh.TriangleIndices()) {
                Index3i nbrs = mesh.GetTriNeighbourTris(tid);
                int c = ((nbrs.a != DMesh3.InvalidID)?1:0) + ((nbrs.b != DMesh3.InvalidID)?1:0) + ((nbrs.c != DMesh3.InvalidID)?1:0);
                if (c <= 1) {
                    if (removeF == null || removeF(mesh, tid) == true )
                        to_remove.Add(tid);
                }
            }
            if (to_remove.Count == 0)
                return nRemoved;
            nRemoved += to_remove.Count;
            RemoveTriangles(mesh, to_remove, true);
            to_remove.Clear();
            if (bRepeatToConvergence)
                goto repeat;
            return nRemoved;
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



        /// <summary>
        /// separate triangle one-ring at vertex into connected components, and
        /// then duplicate vertex once for each component
        /// </summary>
        public void DisconnectBowtie(int vid)
        {
            List<List<int>> sets = new List<List<int>>();
            foreach ( int tid in Mesh.VtxTrianglesItr(vid)) {
                Index3i nbrs = Mesh.GetTriNeighbourTris(tid);
                bool found = false;
                foreach ( List<int> set in sets ) {
                    if ( set.Contains(nbrs.a) || set.Contains(nbrs.b) || set.Contains(nbrs.c) ) {
                        set.Add(tid);
                        found = true;
                        break;
                    }
                }
                if ( found == false ) {
                    List<int> set = new List<int>() { tid };
                    sets.Add(set);
                }
            }
            if (sets.Count == 1)
                return;  // not a bowtie!
            sets.Sort(bowtie_sorter);
            for ( int k = 1; k < sets.Count; ++k ) {
                int copy_vid = Mesh.AppendVertex(Mesh, vid);
                List<int> tris = sets[k];
                foreach ( int tid in tris ) {
                    Index3i t = Mesh.GetTriangle(tid);
                    if (t.a == vid) t.a = copy_vid;
                    else if (t.b == vid) t.b = copy_vid;
                    else t.c = copy_vid;
                    Mesh.SetTriangle(tid, t, false);
                }
            }
        }
        static int bowtie_sorter(List<int> l1, List<int> l2) {
            if (l1.Count == l2.Count) return 0;
            return (l1.Count > l2.Count) ? -1 : 1;
        }



        /// <summary>
        /// Disconnect all bowtie vertices in mesh. Iterates because sometimes
		/// disconnecting a bowtie creates new bowties (how??).
		/// Returns number of remaining bowties after iterations.
        /// </summary>
        public int DisconnectAllBowties(int nMaxIters = 10)
        {
            List<int> bowties = new List<int>(MeshIterators.BowtieVertices(Mesh));
            int iter = 0;
            while (bowties.Count > 0 && iter++ < nMaxIters) {
                foreach (int vid in bowties) 
                    DisconnectBowtie(vid);
                bowties = new List<int>(MeshIterators.BowtieVertices(Mesh));
            }
			return bowties.Count;
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
        public static void Append(DMesh3 appendTo, DMesh3 append)
        {
            MeshEditor editor = new MeshEditor(appendTo);
            editor.AppendMesh(append, appendTo.AllocateTriangleGroup());
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
            AppendBox(frame, size, Colorf.White);
        }
        public void AppendBox(Frame3f frame, Vector3f size, Colorf color)
        {
            TrivialBox3Generator boxgen = new TrivialBox3Generator() {
                Box = new Box3d(frame, size),
                NoSharedVertices = false
            };
            boxgen.Generate();
            DMesh3 mesh = new DMesh3();
            boxgen.MakeMesh(mesh);
            if (Mesh.HasVertexColors)
                mesh.EnableVertexColors(color);
            AppendMesh(mesh, Mesh.AllocateTriangleGroup());
        }
        public void AppendLine(Segment3d seg, float size)
        {
            Frame3f f = new Frame3f(seg.Center);
            f.AlignAxis(2, (Vector3f)seg.Direction);
            AppendBox(f, new Vector3f(size, size, seg.Extent));
        }
        public void AppendLine(Segment3d seg, float size, Colorf color)
        {
            Frame3f f = new Frame3f(seg.Center);
            f.AlignAxis(2, (Vector3f)seg.Direction);
            AppendBox(f, new Vector3f(size, size, seg.Extent), color);
        }
        public static void AppendBox(DMesh3 mesh, Vector3d pos, float size)
        {
            MeshEditor editor = new MeshEditor(mesh);
            editor.AppendBox(new Frame3f(pos), size);
        }
        public static void AppendBox(DMesh3 mesh, Vector3d pos, float size, Colorf color)
        {
            MeshEditor editor = new MeshEditor(mesh);
            editor.AppendBox(new Frame3f(pos), size*Vector3f.One, color);
        }
        public static void AppendBox(DMesh3 mesh, Vector3d pos, Vector3d normal, float size)
        {
            MeshEditor editor = new MeshEditor(mesh);
            editor.AppendBox(new Frame3f(pos, normal), size);
        }
        public static void AppendBox(DMesh3 mesh, Vector3d pos, Vector3d normal, float size, Colorf color)
        {
            MeshEditor editor = new MeshEditor(mesh);
            editor.AppendBox(new Frame3f(pos, normal), size*Vector3f.One, color);
        }
        public static void AppendBox(DMesh3 mesh, Frame3f frame, Vector3f size, Colorf color)
        {
            MeshEditor editor = new MeshEditor(mesh);
            editor.AppendBox(frame, size, color);
        }

        public static void AppendLine(DMesh3 mesh, Segment3d seg, float size)
        {
            Frame3f f = new Frame3f(seg.Center);
            f.AlignAxis(2, (Vector3f)seg.Direction);
            MeshEditor editor = new MeshEditor(mesh);
            editor.AppendBox(f, new Vector3f(size, size, seg.Extent));
        }




        public void AppendPathSolid(IEnumerable<Vector3d> vertices, double radius, Colorf color)
        {
            TubeGenerator tubegen = new TubeGenerator() {
                Vertices = new List<Vector3d>(vertices),
                Polygon = Polygon2d.MakeCircle(radius, 6),
                NoSharedVertices = false
            };
            DMesh3 mesh = tubegen.Generate().MakeDMesh();
            if (Mesh.HasVertexColors)
                mesh.EnableVertexColors(color);
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







        /// <summary>
        /// Remove any unused vertices in mesh, ie vertices with no edges.
        /// Returns number of removed vertices.
        /// </summary>
        public int RemoveUnusedVertices()
        {
            int nRemoved = 0;
            int NV = Mesh.MaxVertexID;
            for ( int vid = 0; vid < NV; ++vid) {
                if (Mesh.IsVertex(vid) && Mesh.GetVtxEdgeCount(vid) == 0) {
                    Mesh.RemoveVertex(vid);
                    ++nRemoved;
                }
            }
            return nRemoved;
        }
        public static int RemoveUnusedVertices(DMesh3 mesh) {
            MeshEditor e = new MeshEditor(mesh); return e.RemoveUnusedVertices();
        }






        /// <summary>
        /// Remove any connected components with volume &lt; min_volume area lt; min_area
        /// </summary>
        public int RemoveSmallComponents(double min_volume, double min_area)
        {
            MeshConnectedComponents C = new MeshConnectedComponents(Mesh);
            C.FindConnectedT();
            if (C.Count == 1)
                return 0;
            int nRemoved = 0;
            foreach (var comp in C.Components) {
                Vector2d vol_area = MeshMeasurements.VolumeArea(Mesh, comp.Indices, Mesh.GetVertex);
                if (vol_area.x < min_volume || vol_area.y < min_area) {
                    MeshEditor.RemoveTriangles(Mesh, comp.Indices);
                    nRemoved++;
                }
            }
            return nRemoved;
        }
        public static int RemoveSmallComponents(DMesh3 mesh, double min_volume, double min_area) {
            MeshEditor e = new MeshEditor(mesh); return e.RemoveSmallComponents(min_volume, min_area);
        }







        // this is for backing out changes we have made...
        bool remove_triangles(int[] tri_list, int count)
        {
            for (int i = 0; i < count; ++i) {
                if (Mesh.IsTriangle(tri_list[i]) == false)
                    continue;
                MeshResult result = Mesh.RemoveTriangle(tri_list[i], false, false);
                if (result != MeshResult.Ok)
                    return false;
            }
            return true;
        }


    }
}
