// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Distributed under the Boost Software License, Version 1.0. http://www.boost.org/LICENSE_1_0.txt
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using g3;

namespace gs
{
    /// <summary>
    /// Extension to Remesher that is smarter about which edges/vertices to touch:
    ///  - queue tracks edges that were affected on last pass, and hence might need to be updated
    ///  - FastSplitIteration() just does splits, to reach target edge length as quickly as possible
    ///  - RemeshIteration() applies remesh pass for modified edges
    ///  - TrackedSmoothPass() smooths all vertices but only adds to queue if edge changes enough
    ///  - TrackedProjectionPass() same
    /// 
    /// </summary>
    public class RemesherPro : Remesher
    {

        public bool UseFaceAlignedProjection = false;
        public int FaceProjectionPassesPerIteration = 1;



        public RemesherPro(DMesh3 m) : base(m)
        {
        }


        HashSet<int> modified_edges;
        SpinLock modified_edges_lock = new SpinLock();


        protected IEnumerable<int> EdgesIterator()
        {
            int cur_eid = start_edges();
            bool done = false;
            do {
                yield return cur_eid;
                cur_eid = next_edge(cur_eid, out done);
            } while (done == false);
        }


        void queue_one_ring_safe(int vid) {
            if ( mesh.IsVertex(vid) ) {
                bool taken = false;
                modified_edges_lock.Enter(ref taken);

                foreach (int eid in mesh.VtxEdgesItr(vid))
                    modified_edges.Add(eid);

                modified_edges_lock.Exit();
            }
        }

        void queue_one_ring(int vid) {
            if ( mesh.IsVertex(vid) ) {
                foreach (int eid in mesh.VtxEdgesItr(vid))
                    modified_edges.Add(eid);
            }
        }

        void queue_edge_safe(int eid) {
            bool taken = false;
            modified_edges_lock.Enter(ref taken);

            modified_edges.Add(eid);

            modified_edges_lock.Exit();
        }

        void queue_edge(int eid) {
            modified_edges.Add(eid);
        }



        Action<int, int, int, int> SplitF = null;


        protected override void OnEdgeSplit(int edgeID, int va, int vb, DMesh3.EdgeSplitInfo splitInfo)
        {
            if (SplitF != null)
                SplitF(edgeID, va, vb, splitInfo.vNew);
        }



        /// <summary>
        /// Converge on remeshed result as quickly as possible
        /// </summary>
        public void FastestRemesh(int nMaxIterations = 25, bool bDoFastSplits = true)
        {
            ResetQueue();

            // first we do fast splits to hit edge length target
            // ?? should we do project in fastsplit? will result in more splits, and
            //    we are going to project in first remesh pass anyway...
            //    (but that might result in larger queue in first remesh pass?)
            int fastsplit_i = 0;
            int max_fastsplits = nMaxIterations;
            if (bDoFastSplits) {
                if (Cancelled())
                    return;

                bool bContinue = true;
                while ( bContinue ) {
                    int nSplits = FastSplitIteration();
                    if (fastsplit_i++ > max_fastsplits)
                        bContinue = false;
                    if ((double)nSplits / (double)mesh.EdgeCount < 0.01)
                        bContinue = false;
                    if (Cancelled())
                        return;
                };
                ResetQueue();
            }

            // should we do a fast collapse pass? more dangerous...

            // now do queued remesh iterations. 
            // disable projection every other iteration to improve speed
            var saveMode = this.ProjectionMode;
            for (int k = 0; k < nMaxIterations - 1; ++k) {
                if (Cancelled())
                    break;
                ProjectionMode = (k % 2 == 0) ? TargetProjectionMode.NoProjection : saveMode;
                RemeshIteration();
            }

            // final pass w/ full projection
            ProjectionMode = saveMode;

            if (Cancelled())
                return;

            RemeshIteration();
        }






        /// <summary>
        /// This is a remesh that tries to recover sharp edges by aligning triangles to face normals
        /// of our projection target (similar to Ohtake RZN-flow). 
        /// </summary>
        public void SharpEdgeReprojectionRemesh(int nRemeshIterations, int nTuneIterations, bool bDoFastSplits = true)
        {
            if (ProjectionTarget == null || ProjectionTarget is IOrientedProjectionTarget == false)
                throw new Exception("RemesherPro.SharpEdgeReprojectionRemesh: cannot call this without a ProjectionTarget that has normals");

            ResetQueue();

            // first we do fast splits to hit edge length target
            // ?? should we do project in fastsplit? will result in more splits, and
            //    we are going to project in first remesh pass anyway...
            //    (but that might result in larger queue in first remesh pass?)
            int fastsplit_i = 0;
            int max_fastsplits = nRemeshIterations;
            if (bDoFastSplits) {
                if (Cancelled())
                    return;

                bool bContinue = true;
                while (bContinue) {
                    int nSplits = FastSplitIteration();
                    if (fastsplit_i++ > max_fastsplits)
                        bContinue = false;
                    if ((double)nSplits / (double)mesh.EdgeCount < 0.01)
                        bContinue = false;
                    if (Cancelled())
                        return;
                };
                ResetQueue();
            }

            bool save_use_face_aligned = UseFaceAlignedProjection;
            UseFaceAlignedProjection = true;
            FaceProjectionPassesPerIteration = 1;

            // should we do a fast collapse pass? more dangerous but would get rid of all the tiny
            // edges we might have just created, and/or get us closer to target resolution

            // now do queued remesh iterations. As we proceed we slowly step
            // down the smoothing factor, this helps us get triangles closer
            // to where they will ultimately want to go
            double smooth_speed = SmoothSpeedT;
            for (int k = 0; k < nRemeshIterations; ++k) {
                if (Cancelled())
                    break;
                RemeshIteration();
                if ( k > nRemeshIterations/2 )
                    SmoothSpeedT *= 0.9f;
            }

            // [TODO] would like to still do splits and maybe sometimes flips here. 
            // Perhaps this could be something more combinatorial? Like, test all the
            // edges we queued in the projection pass, if we can get better alignment
            // after a with flip or split, do it
            //SmoothSpeedT = 0;
            //MinEdgeLength = MinEdgeLength * 0.1;
            //EnableFlips = false;
            for (int k = 0; k < nTuneIterations; ++k) {
                if (Cancelled())
                    break;
                TrackedFaceProjectionPass();
                //RemeshIteration();
            }

            SmoothSpeedT = smooth_speed;
            UseFaceAlignedProjection = save_use_face_aligned;

            //TrackedProjectionPass(true);
        }











        /// <summary>
        /// Reset tracked-edges queue. Should be called if mesh is modified by external functions
        /// between passes, and also between different types of passes (eg FastSplitIteration vs RemeshIteration)
        /// </summary>
        public void ResetQueue()
        {
            if (modified_edges != null) {
                modified_edges.Clear();
                modified_edges = null;
            }
        }

        List<int> edges_buffer = new List<int>();


        /// <summary>
        /// This pass only does edge splits. Returns number of split edges.
        /// Tracks previously-split 
        /// </summary>
        public int FastSplitIteration()
        {
            if (mesh.TriangleCount == 0)    // badness if we don't catch this...
                return 0;

            PushState();
            EnableFlips = EnableCollapses = EnableSmoothing = false;
            ProjectionMode = TargetProjectionMode.NoProjection;

            begin_pass();

            // Iterate over all edges in the mesh at start of pass.
            // Some may be removed, so we skip those.
            // However, some old eid's may also be re-used, so we will touch
            // some new edges. Can't see how we could efficiently prevent this.
            //
            begin_ops();

            IEnumerable<int> edgesItr = EdgesIterator();
            if (modified_edges == null) {
                modified_edges = new HashSet<int>();
            } else {
                edges_buffer.Clear(); edges_buffer.AddRange(modified_edges);
                edgesItr = edges_buffer;
                modified_edges.Clear();
            }

            int startEdges = Mesh.EdgeCount;
            int splitEdges = 0;
            
            // When we split an edge, we need to check it and the adjacent ones we added.
            // Because of overhead in ProcessEdge, it is worth it to do a distance-check here
            double max_edge_len_sqr = MaxEdgeLength * MaxEdgeLength;
            SplitF = (edgeID, a, b, vNew) => {
                Vector3d v = Mesh.GetVertex(vNew);
                foreach (int eid in Mesh.VtxEdgesItr(vNew)) {
                    Index2i ev = Mesh.GetEdgeV(eid);
                    int othervid = (ev.a == vNew) ? ev.b : ev.a;
                    if (mesh.GetVertex(othervid).DistanceSquared(ref v) > max_edge_len_sqr)
                        queue_edge(eid);
                }
                //queue_one_ring(vNew);
            };


            ModifiedEdgesLastPass = 0;
            int processedLastPass = 0;
            foreach (int cur_eid in edgesItr) {
                if (Cancelled())
                    goto abort_compute;

                if (mesh.IsEdge(cur_eid)) {
                    Index2i ev = mesh.GetEdgeV(cur_eid);
                    Index2i ov = mesh.GetEdgeOpposingV(cur_eid);

                    processedLastPass++;
                    ProcessResult result = ProcessEdge(cur_eid);
                    if (result == ProcessResult.Ok_Split) {
                        // new edges queued by SplitF
                        ModifiedEdgesLastPass++;
                        splitEdges++;
                    } 
                }
            }
            end_ops();

            //System.Console.WriteLine("FastSplitIteration: start {0}  end {1}  processed: {2}   modified: {3}  queue: {4}",
            //    startEdges, Mesh.EdgeCount, processedLastPass, ModifiedEdgesLastPass, modified_edges.Count);

            abort_compute:
            SplitF = null;
            PopState();

            end_pass();

            return splitEdges;
        }






        public virtual void RemeshIteration()
        {
            if (mesh.TriangleCount == 0)    // badness if we don't catch this...
                return;

            begin_pass();

            // Iterate over all edges in the mesh at start of pass.
            // Some may be removed, so we skip those.
            // However, some old eid's may also be re-used, so we will touch
            // some new edges. Can't see how we could efficiently prevent this.
            //
            begin_ops();

            IEnumerable<int> edgesItr = EdgesIterator();
            if (modified_edges == null) {
                modified_edges = new HashSet<int>();
            } else {
                edges_buffer.Clear(); edges_buffer.AddRange(modified_edges);
                edgesItr = edges_buffer;
                modified_edges.Clear();
            }

            int startEdges = Mesh.EdgeCount;
            int flips = 0, splits = 0, collapes = 0;

            ModifiedEdgesLastPass = 0;
            int processedLastPass = 0;
            foreach (int cur_eid in edgesItr) {
                if (Cancelled())
                    return;

                if (mesh.IsEdge(cur_eid)) {
                    Index2i ev = mesh.GetEdgeV(cur_eid);
                    Index2i ov = mesh.GetEdgeOpposingV(cur_eid);

                    // TODO: optimize the queuing here, are over-doing it!
                    // TODO: be able to queue w/o flip (eg queue from smooth never requires flip check)

                    processedLastPass++;
                    ProcessResult result = ProcessEdge(cur_eid);
                    if (result == ProcessResult.Ok_Collapsed) {
                        queue_one_ring(ev.a); queue_one_ring(ev.b);
                        queue_one_ring(ov.a); queue_one_ring(ov.b);
                        ModifiedEdgesLastPass++;
                        collapes++;
                    } else if (result == ProcessResult.Ok_Split) {
                        queue_one_ring(ev.a); queue_one_ring(ev.b);
                        queue_one_ring(ov.a); queue_one_ring(ov.b);
                        ModifiedEdgesLastPass++;
                        splits++;
                    } else if (result == ProcessResult.Ok_Flipped) {
                        queue_one_ring(ev.a); queue_one_ring(ev.b);
                        queue_one_ring(ov.a); queue_one_ring(ov.b);
                        ModifiedEdgesLastPass++;
                        flips++;
                    }
                }
            }
            end_ops();

            //System.Console.WriteLine("RemeshIteration: start {0}  end {1}  processed: {2}   modified: {3}  queue: {4}",
            //    startEdges, Mesh.EdgeCount, processedLastPass, ModifiedEdgesLastPass, modified_edges.Count);
            //System.Console.WriteLine("   flips {0}  splits {1}  collapses {2}", flips, splits, collapes);

            if (Cancelled())
                return;

            begin_smooth();
            if (EnableSmoothing && SmoothSpeedT > 0) {
                TrackedSmoothPass(EnableParallelSmooth);
                DoDebugChecks();
            }
            end_smooth();

            if (Cancelled())
                return;

            begin_project();
            if (ProjectionTarget != null && ProjectionMode == TargetProjectionMode.AfterRefinement) {
                //FullProjectionPass();

                if (UseFaceAlignedProjection) {
                    for ( int i = 0; i < FaceProjectionPassesPerIteration; ++i )
                        TrackedFaceProjectionPass();
                } else {
                    TrackedProjectionPass(EnableParallelProjection);
                }
                DoDebugChecks();
            }
            end_project();

            end_pass();
        }


        protected virtual void TrackedSmoothPass(bool bParallel)
        {
            InitializeVertexBufferForPass();

            Func<DMesh3, int, double, Vector3d> smoothFunc = MeshUtil.UniformSmooth;
            if (CustomSmoothF != null) {
                smoothFunc = CustomSmoothF;
            } else {
                if (SmoothType == SmoothTypes.MeanValue)
                    smoothFunc = MeshUtil.MeanValueSmooth;
                else if (SmoothType == SmoothTypes.Cotan)
                    smoothFunc = MeshUtil.CotanSmooth;
            }

            Action<int> smooth = (vID) => {
                Vector3d vCur = Mesh.GetVertex(vID);
                bool bModified = false;
                Vector3d vSmoothed = ComputeSmoothedVertexPos(vID, smoothFunc, out bModified);
                //if (vCur.EpsilonEqual(vSmoothed, MathUtil.ZeroTolerancef))
                //    bModified = false;
                if (bModified) {
                    vModifiedV[vID] = true;
                    vBufferV[vID] = vSmoothed;

                    foreach (int eid in mesh.VtxEdgesItr(vID)) {
                        Index2i ev = Mesh.GetEdgeV(eid);
                        int othervid = (ev.a == vID) ? ev.b : ev.a;
                        Vector3d otherv = mesh.GetVertex(othervid);
                        double old_len = vCur.Distance(otherv);
                        double new_len = vSmoothed.Distance(otherv);
                        if (new_len < MinEdgeLength || new_len > MaxEdgeLength)
                            queue_edge_safe(eid);
                    }
                }
            };


            if (bParallel) {
                gParallel.ForEach<int>(smooth_vertices(), smooth);
            } else {
                foreach (int vID in smooth_vertices())
                    smooth(vID);
            }

            ApplyVertexBuffer(bParallel);
            //System.Console.WriteLine("Smooth Pass: queue: {0}", modified_edges.Count);
        }





        // [TODO] projection pass
        //   - only project vertices modified by smooth pass?
        //   - and/or verts in set of modified edges? 
        protected virtual void TrackedProjectionPass(bool bParallel)
        {
            InitializeVertexBufferForPass();

            Action<int> project = (vID) => {
                Vector3d vCur = Mesh.GetVertex(vID);
                bool bModified = false;
                Vector3d vProjected = ComputeProjectedVertexPos(vID, out bModified);
                if (vCur.EpsilonEqual(vProjected, MathUtil.ZeroTolerancef))
                    bModified = false;
                if (bModified) {
                    vModifiedV[vID] = true;
                    vBufferV[vID] = vProjected;

                    foreach (int eid in mesh.VtxEdgesItr(vID)) {
                        Index2i ev = Mesh.GetEdgeV(eid);
                        int othervid = (ev.a == vID) ? ev.b : ev.a;
                        Vector3d otherv = mesh.GetVertex(othervid);
                        double old_len = vCur.Distance(otherv);
                        double new_len = vProjected.Distance(otherv);
                        if (new_len < MinEdgeLength || new_len > MaxEdgeLength)
                            queue_edge_safe(eid);
                    }
                }
            };


            if (bParallel) {
                gParallel.ForEach<int>(smooth_vertices(), project);
            } else {
                foreach (int vID in smooth_vertices())
                    project(vID);
            }

            ApplyVertexBuffer(bParallel);
            //System.Console.WriteLine("Projection Pass: queue: {0}", modified_edges.Count);
        }




        /// <summary>
        /// This computes projected position w/ proper constraints/etc.
        /// Does not modify mesh.
        /// </summary>
        protected virtual Vector3d ComputeProjectedVertexPos(int vID, out bool bModified)
        {
            bModified = false;

            if (vertex_is_constrained(vID))
                return Mesh.GetVertex(vID);
            if (VertexControlF != null && (VertexControlF(vID) & VertexControl.NoProject) != 0)
                return Mesh.GetVertex(vID);

            Vector3d curpos = mesh.GetVertex(vID);
            Vector3d projected = ProjectionTarget.Project(curpos, vID);
            bModified = true;
            return projected;
        }







        /*
         *  Implementation of face-aligned projection. Combined with rest of remesh
         *  this is basically an RZN-flow-type algorithm. 
         */



        protected DVector<double> vBufferVWeights = new DVector<double>();

        protected virtual void InitializeBuffersForFacePass()
        {
            base.InitializeVertexBufferForPass();
            if (vBufferVWeights.size < vBufferV.size)
                vBufferVWeights.resize(vBufferV.size);

            int NV = mesh.MaxVertexID;
            for (int i = 0; i < NV; ++i) {
                vBufferV[i] = Vector3d.Zero;
                vBufferVWeights[i] = 0;
            }
        }



        // [TODO] projection pass
        //   - only project vertices modified by smooth pass?
        //   - and/or verts in set of modified edges? 
        protected virtual void TrackedFaceProjectionPass()
        {
            IOrientedProjectionTarget normalTarget = ProjectionTarget as IOrientedProjectionTarget;
            if (normalTarget == null)
                throw new Exception("RemesherPro.TrackedFaceProjectionPass: projection target does not have normals!");

            InitializeBuffersForFacePass();

            SpinLock buffer_lock = new SpinLock();

            // this function computes rotated position of triangle, such that it
            // aligns with face normal on target surface. We accumulate weighted-average 
            // of vertex positions, which we will then use further down where possible.
            Action<int> process_triangle = (tid) => {
                Vector3d normal; double area; Vector3d centroid;
                mesh.GetTriInfo(tid, out normal, out area, out centroid);

                Vector3d projNormal;
                Vector3d projPos = normalTarget.Project(centroid, out projNormal);

                Index3i tv = mesh.GetTriangle(tid);
                Vector3d v0 = mesh.GetVertex(tv.a), v1 = mesh.GetVertex(tv.b), v2 = mesh.GetVertex(tv.c);

                // ugh could probably do this more efficiently...
                Frame3f triF = new Frame3f(centroid, normal);
                v0 = triF.ToFrameP(ref v0); v1 = triF.ToFrameP(ref v1); v2 = triF.ToFrameP(ref v2);
                triF.AlignAxis(2, (Vector3f)projNormal);
                triF.Origin = (Vector3f)projPos;
                v0 = triF.FromFrameP(ref v0); v1 = triF.FromFrameP(ref v1); v2 = triF.FromFrameP(ref v2);

                double dot = normal.Dot(projNormal);
                dot = MathUtil.Clamp(dot, 0, 1.0);
                double w = area * (dot * dot * dot);

                bool taken = false;
                buffer_lock.Enter(ref taken);
                vBufferV[tv.a] += w * v0; vBufferVWeights[tv.a] += w;
                vBufferV[tv.b] += w * v1; vBufferVWeights[tv.b] += w;
                vBufferV[tv.c] += w * v2; vBufferVWeights[tv.c] += w;
                buffer_lock.Exit();
            };
            
            // compute face-aligned vertex positions
            gParallel.ForEach(mesh.TriangleIndices(), process_triangle);


            // ok now we filter out all the positions we can't change, as well as vertices that
            // did not actually move. We also queue any edges that moved far enough to fall
            // under min/max edge length thresholds
            gParallel.ForEach(mesh.VertexIndices(), (vID) => {
                vModifiedV[vID] = false;
                if (vBufferVWeights[vID] < MathUtil.ZeroTolerance)
                    return;
                if (vertex_is_constrained(vID))
                    return;
                if (VertexControlF != null && (VertexControlF(vID) & VertexControl.NoProject) != 0)
                    return;

                Vector3d curpos = mesh.GetVertex(vID);
                Vector3d projPos = vBufferV[vID] / vBufferVWeights[vID];
                if (curpos.EpsilonEqual(projPos, MathUtil.ZeroTolerancef))
                    return;

                vModifiedV[vID] = true;
                vBufferV[vID] = projPos;

                foreach (int eid in mesh.VtxEdgesItr(vID)) {
                    Index2i ev = Mesh.GetEdgeV(eid);
                    int othervid = (ev.a == vID) ? ev.b : ev.a;
                    Vector3d otherv = mesh.GetVertex(othervid);
                    double old_len = curpos.Distance(otherv);
                    double new_len = projPos.Distance(otherv);
                    if (new_len < MinEdgeLength || new_len > MaxEdgeLength)
                        queue_edge_safe(eid);
                }

            });


            // update vertices
            ApplyVertexBuffer(true);
        }















        struct SettingState
        {
            public bool EnableFlips;
            public bool EnableCollapses;
            public bool EnableSplits;
            public bool EnableSmoothing;

            public double MinEdgeLength;
            public double MaxEdgeLength;

            public double SmoothSpeedT;
            public SmoothTypes SmoothType;
            public TargetProjectionMode ProjectionMode;
        }
        List<SettingState> stateStack = new List<SettingState>();

        public void PushState()
        {
            SettingState s = new SettingState() {
                EnableFlips = this.EnableFlips,
                EnableCollapses = this.EnableCollapses,
                EnableSplits = this.EnableSplits,
                EnableSmoothing = this.EnableSmoothing,
                MinEdgeLength = this.MinEdgeLength,
                MaxEdgeLength = this.MaxEdgeLength,
                SmoothSpeedT = this.SmoothSpeedT,
                SmoothType = this.SmoothType,
                ProjectionMode = this.ProjectionMode
            };
            stateStack.Add(s);
        }

        public void PopState()
        {
            SettingState s = stateStack.Last();
            stateStack.RemoveAt(stateStack.Count - 1);

            this.EnableFlips = s.EnableFlips;
            this.EnableCollapses = s.EnableCollapses;
            this.EnableSplits = s.EnableSplits;
            this.EnableSmoothing = s.EnableSmoothing;
            this.MinEdgeLength = s.MinEdgeLength;
            this.MaxEdgeLength = s.MaxEdgeLength;
            this.SmoothSpeedT = s.SmoothSpeedT;
            this.SmoothType = s.SmoothType;
            this.ProjectionMode = s.ProjectionMode;
        }

        



    }
}
