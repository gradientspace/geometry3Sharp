using System;
using System.Collections.Generic;
using System.Diagnostics;


namespace g3
{

    /// <summary>
    /// Mesh Simplication - implementation of Garland & Heckbert Quadric Error Metric (QEM) Simplification
    /// 
    /// </summary>
	public class Reducer : MeshRefinerBase
	{
        protected IProjectionTarget target = null;

		// other options

		// if true, we try to find position for collapsed vertices that
		// minimizes quadrice error. If false we just use midpoints.
		// Note: using midpoints is *significantly* slower, because it results
		// in may more points that would cause a triangle flip, which are then rejected.
		// (Also results in more invalid collapses, not sure why though...)
		public bool MinimizeQuadricPositionError = true;

        // if true, we try to keep boundary vertices on boundary. You probably want this.
        public bool PreserveBoundaryShape = true;

		// [RMS] this is a debugging aid, will break to debugger if these edges are touched, in debug builds
		public List<int> DebugEdges = new List<int>();

		// if Target is set, we can project onto it in different ways
		public enum TargetProjectionMode
		{
			NoProjection,           // disable projection
			AfterRefinement,        // do all projection after reducing
			Inline                  // projection is computed before evaluating quadrics, so
                                    // we are properly evaluating QEM error of result
		}
		public TargetProjectionMode ProjectionMode = TargetProjectionMode.AfterRefinement;

		// this just lets us write more concise tests below
		bool EnableInlineProjection { get { return ProjectionMode == TargetProjectionMode.Inline; } }

		// set to true to print profiling info to console
		public bool ENABLE_PROFILING = false;


		public Reducer(DMesh3 m) : base(m)
		{
		}
		protected Reducer()        // for subclasses that extend our behavior
		{
		}


		public void SetProjectionTarget(IProjectionTarget target)
		{
			this.target = target;
		}




        protected double MinEdgeLength = double.MaxValue;
        protected int TargetCount = int.MaxValue;
        protected enum TargetModes
        {
            TriangleCount, VertexCount, MinEdgeLength
        }
        protected TargetModes ReduceMode = TargetModes.TriangleCount;



        public virtual void DoReduce()
        {
            if (mesh.TriangleCount == 0)    // badness if we don't catch this...
                return;

            begin_pass();

            begin_setup();
            Precompute();
            if (Cancelled())
                return;
            InitializeVertexQuadrics();
            if (Cancelled())
                return;
            InitializeQueue();
            if (Cancelled())
                return;
            end_setup();

            begin_ops();

            begin_collapse();
            while (EdgeQueue.Count > 0) {

                // termination criteria
                if ( ReduceMode == TargetModes.VertexCount ) {
                    if (mesh.VertexCount <= TargetCount)
                        break;
                } else {
                    if (mesh.TriangleCount <= TargetCount)
                        break;
                }

                COUNT_ITERATIONS++;
                int eid = EdgeQueue.Dequeue();
                if (!mesh.IsEdge(eid))
                    continue;
                if (Cancelled())
                    return;

                int vKept;
                ProcessResult result = CollapseEdge(eid, EdgeQuadrics[eid].collapse_pt, out vKept);
                if (result == ProcessResult.Ok_Collapsed) {
                    vertQuadrics[vKept] = EdgeQuadrics[eid].q;
                    UpdateNeighbours(vKept);
                }
            }
            end_collapse();
            end_ops();

            if (Cancelled())
                return;

            Reproject();

            end_pass();
        }



        public virtual void ReduceToTriangleCount(int nCount)
        {
            ReduceMode = TargetModes.TriangleCount;
            TargetCount = Math.Max(1,nCount);
            MinEdgeLength = double.MaxValue;
            DoReduce();
        }

        public virtual void ReduceToVertexCount(int nCount)
        {
            ReduceMode = TargetModes.VertexCount;
            TargetCount = Math.Max(3,nCount);
            MinEdgeLength = double.MaxValue;
            DoReduce();
        }

        public virtual void ReduceToEdgeLength(double minEdgeLen)
        {
            ReduceMode = TargetModes.MinEdgeLength;
            TargetCount = 1;
            MinEdgeLength = minEdgeLen;
            DoReduce();
        }











        public virtual void FastCollapsePass(double fMinEdgeLength, int nRounds = 1, bool MeshIsClosedHint = false)
        {
            if (mesh.TriangleCount == 0)    // badness if we don't catch this...
                return;

            MinEdgeLength = fMinEdgeLength;
            double min_sqr = MinEdgeLength * MinEdgeLength;

            // we don't collapse on the boundary
            HaveBoundary = false;

            begin_pass();

            begin_setup();
            Precompute(MeshIsClosedHint);
            if (Cancelled())
                return;
            end_setup();

            begin_ops();

            begin_collapse();

            int N = mesh.MaxEdgeID;
            int num_last_pass = 0;
            for (int ri = 0; ri < nRounds; ++ri) {
                num_last_pass = 0;

                Vector3d va = Vector3d.Zero, vb = Vector3d.Zero;
                for (int eid = 0; eid < N; ++eid) {
                    if (!mesh.IsEdge(eid))
                        continue;
                    if (mesh.IsBoundaryEdge(eid))
                        continue;
                    if (Cancelled())
                        return;

                    mesh.GetEdgeV(eid, ref va, ref vb);
                    if (va.DistanceSquared(ref vb) > min_sqr)
                        continue;

                    COUNT_ITERATIONS++;

                    Vector3d midpoint = (va + vb) * 0.5;
                    int vKept;
                    ProcessResult result = CollapseEdge(eid, midpoint, out vKept);
                    if (result == ProcessResult.Ok_Collapsed) {
                        ++num_last_pass;
                    }
                }

                if (num_last_pass == 0)     // converged
                    break;
            }
            end_collapse();
            end_ops();

            if (Cancelled())
                return;

            Reproject();

            end_pass();
        }











        protected QuadricError[] vertQuadrics;
		protected virtual void InitializeVertexQuadrics()
		{

			int NT = mesh.MaxTriangleID;
			QuadricError[] triQuadrics = new QuadricError[NT];
			double[] triAreas = new double[NT];
            gParallel.BlockStartEnd(0, mesh.MaxTriangleID-1, (start_tid, end_tid) => {
                Vector3d c, n;
                for (int tid = start_tid; tid <= end_tid; tid++) {
                    if (mesh.IsTriangle(tid)) {
                        mesh.GetTriInfo(tid, out n, out triAreas[tid], out c);
                        triQuadrics[tid] = new QuadricError(ref n, ref c);
                    }
                }
			});


			int NV = mesh.MaxVertexID;
			vertQuadrics = new QuadricError[NV];
            gParallel.BlockStartEnd(0, mesh.MaxVertexID-1, (start_vid, end_vid) => {
                for (int vid = start_vid; vid <= end_vid; vid++) {
                    vertQuadrics[vid] = QuadricError.Zero;
                    if (mesh.IsVertex(vid)) {
                        foreach (int tid in mesh.VtxTrianglesItr(vid)) {
                            vertQuadrics[vid].Add(triAreas[tid], ref triQuadrics[tid]);
                        }
                        //Util.gDevAssert(MathUtil.EpsilonEqual(0, vertQuadrics[i].Evaluate(mesh.GetVertex(i)), MathUtil.Epsilon * 10));
                    }
                }
			});

		}


        // internal class for priority queue
        protected struct QEdge { 
			public int eid;
			public QuadricError q;
			public Vector3d collapse_pt;

			public QEdge(int edge_id, ref QuadricError qin, ref Vector3d pt) {
				eid = edge_id;
				q = qin;
				collapse_pt = pt;
			}
		}

        protected QEdge[] EdgeQuadrics;
        protected IndexPriorityQueue EdgeQueue;

		protected virtual void InitializeQueue()
		{
			int NE = mesh.EdgeCount;
            int MaxEID = mesh.MaxEdgeID;

            EdgeQuadrics = new QEdge[MaxEID];
            EdgeQueue = new IndexPriorityQueue(MaxEID);
            float[] edgeErrors = new float[MaxEID];

            // vertex quadrics can be computed in parallel
            gParallel.BlockStartEnd(0, MaxEID-1, (start_eid, end_eid) => {
                for (int eid = start_eid; eid <= end_eid; eid++) {
                    if (mesh.IsEdge(eid)) {
                        Index2i ev = mesh.GetEdgeV(eid);
                        QuadricError Q = new QuadricError(ref vertQuadrics[ev.a], ref vertQuadrics[ev.b]);
                        Vector3d opt = OptimalPoint(eid, ref Q, ev.a, ev.b);
                        edgeErrors[eid] = (float)Q.Evaluate(ref opt);
                        EdgeQuadrics[eid] = new QEdge(eid, ref Q, ref opt);
                    }
                }
            });

            // sorted pq insert is faster, so sort edge errors array and index map
            int[] indices = new int[MaxEID];
            for (int i = 0; i < MaxEID; ++i)
                indices[i] = i;
            Array.Sort(edgeErrors, indices);

            // now do inserts
            for ( int i = 0; i < edgeErrors.Length; ++i ) {
                int eid = indices[i];
                if ( mesh.IsEdge(eid) ) {
                    QEdge edge = EdgeQuadrics[eid];
                    EdgeQueue.Insert(edge.eid, edgeErrors[i]);
                }
            }

            /* 
            // previous code that does unsorted insert. This is marginally slower, but
            // might get even slower on larger meshes? have only tried up to about 350k.
            // (still, this function is not the bottleneck...)
            int cur_eid = start_edges();
            bool done = false;
            do {
                if (mesh.IsEdge(cur_eid)) {
                    QEdge edge = EdgeQuadrics[cur_eid];
                    double err = errList[cur_eid];
                    EdgeQueue.Enqueue(cur_eid, (float)err);
                }
                cur_eid = next_edge(cur_eid, out done);
            } while (done == false);
            */
        }


        // return point that minimizes quadric error for edge [ea,eb]
        protected Vector3d OptimalPoint(int eid, ref QuadricError q, int ea, int eb) {

            // if we would like to preserve boundary, we need to know that here
            // so that we properly score these edges
            if (HaveBoundary && PreserveBoundaryShape) {
                if (mesh.IsBoundaryEdge(eid)) {
                    return (mesh.GetVertex(ea) + mesh.GetVertex(eb)) * 0.5;
                } else {
                    if (IsBoundaryV(ea))
                        return mesh.GetVertex(ea);
                    else if (IsBoundaryV(eb))
                        return mesh.GetVertex(eb);
                }
            }

            // [TODO] if we have constraints, we should apply them here, for same reason as bdry above...

			if (MinimizeQuadricPositionError == false) {
				return project( (mesh.GetVertex(ea) + mesh.GetVertex(eb)) * 0.5 );
			} else {
                Vector3d result = Vector3d.Zero;
                if (q.OptimalPoint(ref result))
                    return project(result);

                // degenerate matrix, evaluate quadric at edge end and midpoints
				// (could do line search here...)
				Vector3d va = mesh.GetVertex(ea);
				Vector3d vb = mesh.GetVertex(eb);
				Vector3d c = project((va + vb) * 0.5);
				double fa = q.Evaluate(ref va);
				double fb = q.Evaluate(ref vb);
				double fc = q.Evaluate(ref c);
				double m = MathUtil.Min(fa, fb, fc);
				if (m == fa) return va;
				else if (m == fb) return vb;
				return c;
			}
		}
        Vector3d project(Vector3d pos) {
            if (EnableInlineProjection && target != null) {
                return target.Project(pos);
            }
            return pos;
        }





		// update queue weight for each edge in vertex one-ring
		protected virtual void UpdateNeighbours(int vid) 
		{
			foreach (int eid in mesh.VtxEdgesItr(vid)) {
				Index2i nev = mesh.GetEdgeV(eid);
				QuadricError Q = new QuadricError(ref vertQuadrics[nev.a], ref vertQuadrics[nev.b]);
				Vector3d opt = OptimalPoint(eid, ref Q, nev.a, nev.b);
				double err = Q.Evaluate(ref opt);
                EdgeQuadrics[eid] = new QEdge(eid, ref Q, ref opt);
				if ( EdgeQueue.Contains(eid) ) {
					EdgeQueue.Update(eid, (float)err);
				} else {
					EdgeQueue.Insert(eid, (float)err);
				}
			}			
		}


		protected virtual void Reproject() {
			begin_project();
			if (target != null && ProjectionMode == TargetProjectionMode.AfterRefinement) {
				FullProjectionPass();
				DoDebugChecks();
			}
			end_project();			
		}



        protected bool HaveBoundary;
        protected bool[] IsBoundaryVtxCache;
        protected virtual void Precompute(bool bMeshIsClosed = false)
        {
            HaveBoundary = false;
            IsBoundaryVtxCache = new bool[mesh.MaxVertexID];
            if (bMeshIsClosed == false) {
                foreach (int eid in mesh.BoundaryEdgeIndices()) {
                    Index2i ev = mesh.GetEdgeV(eid);
                    IsBoundaryVtxCache[ev.a] = true;
                    IsBoundaryVtxCache[ev.b] = true;
                    HaveBoundary = true;
                }
            }
        }
        protected bool IsBoundaryV(int vid)
        {
            return IsBoundaryVtxCache[vid];
        }





        // subclasses can override these to implement custom behavior...

        protected virtual void OnEdgeCollapse(int edgeID, int va, int vb, DMesh3.EdgeCollapseInfo collapseInfo)
        {
            // this is for subclasses...
        }




        // start_edges() and next_edge() control the iteration over edges that will be refined.
        // Default here is to iterate over entire mesh.
        // Subclasses can override these two functions to restrict the affected edges (eg EdgeLoopRemesher)


        // We are using a modulo-index loop to break symmetry/pathological conditions. 
        const int nPrime = 31337;     // any prime will do...
        int nMaxEdgeID;
        protected virtual int start_edges() {
            nMaxEdgeID = mesh.MaxEdgeID;
            return 0;
        }

        protected virtual int next_edge(int cur_eid, out bool bDone) {
            int new_eid = (cur_eid + nPrime) % nMaxEdgeID;
            bDone = (new_eid == 0);
            return new_eid;
        }



        protected virtual IEnumerable<int> project_vertices()
        {
            return mesh.VertexIndices();
        }




		protected enum ProcessResult {
			Ok_Collapsed = 0,
			Ignored_CannotCollapse = 1,
            Ignored_EdgeIsFullyConstrained = 2,
			Ignored_EdgeTooLong = 3,
			Ignored_Constrained = 4,
			Ignored_CreatesFlip = 5,
			Failed_OpNotSuccessful = 6,
			Failed_NotAnEdge = 7
		};

		protected virtual ProcessResult CollapseEdge(int edgeID, Vector3d vNewPos, out int collapseToV) 
		{
			collapseToV = DMesh3.InvalidID;
            RuntimeDebugCheck(edgeID);

            EdgeConstraint constraint =
                (constraints == null) ? EdgeConstraint.Unconstrained : constraints.GetEdgeConstraint(edgeID);
            if (constraint.NoModifications)
                return ProcessResult.Ignored_EdgeIsFullyConstrained;
			if (constraint.CanCollapse == false)
				return ProcessResult.Ignored_EdgeIsFullyConstrained;
			

			// look up verts and tris for this edge
			int a = 0, b = 0, t0 = 0, t1 = 0;
			if ( mesh.GetEdge(edgeID, ref a, ref b, ref t0, ref t1) == false )
				return ProcessResult.Failed_NotAnEdge;
			bool bIsBoundaryEdge = (t1 == DMesh3.InvalidID);

			// look up 'other' verts c (from t0) and d (from t1, if it exists)
			Index3i T0tv = mesh.GetTriangle(t0);
			int c = IndexUtil.find_tri_other_vtx(a, b, T0tv);
			Index3i T1tv = (bIsBoundaryEdge) ? DMesh3.InvalidTriangle : mesh.GetTriangle(t1);
			int d = (bIsBoundaryEdge) ? DMesh3.InvalidID : IndexUtil.find_tri_other_vtx( a, b, T1tv );

			Vector3d vA = mesh.GetVertex(a);
			Vector3d vB = mesh.GetVertex(b);
			double edge_len_sqr = (vA-vB).LengthSquared;
			if (edge_len_sqr > MinEdgeLength * MinEdgeLength)
				return ProcessResult.Ignored_EdgeTooLong;

            begin_collapse();

            // check if we should collapse, and also find which vertex we should collapse to,
            // in cases where we have constraints/etc
            int collapse_to = -1;
            bool bCanCollapse = can_collapse_constraints(edgeID, a, b, c, d, t0, t1, out collapse_to);
			if (bCanCollapse == false)
				return ProcessResult.Ignored_Constrained;

            // if we have a boundary, we want to collapse to boundary
            if (PreserveBoundaryShape && HaveBoundary) {
                if (collapse_to != -1) {
                    if (( IsBoundaryV(b) && collapse_to != b) ||
                         ( IsBoundaryV(a) && collapse_to != a))
                        return ProcessResult.Ignored_Constrained;
                }
                if (IsBoundaryV(b))
                    collapse_to = b;
                else if (IsBoundaryV(a))
                    collapse_to = a;
            }

            // optimization: if edge cd exists, we cannot collapse or flip. look that up here?
            //  funcs will do it internally...
            //  (or maybe we can collapse if cd exists? edge-collapse doesn't check for it explicitly...)
            ProcessResult retVal = ProcessResult.Failed_OpNotSuccessful;

            int iKeep = b, iCollapse = a;

            // if either vtx is fixed, collapse to that position
            if ( collapse_to == b ) {
                vNewPos = vB;
            } else if ( collapse_to == a ) {
                iKeep = a; iCollapse = b;
                vNewPos = vA;
            } else
                vNewPos = get_projected_collapse_position(iKeep, vNewPos);

            // check if this collapse will create a normal flip. Also checks
            // for invalid collapse nbrhood, since we are doing one-ring iter anyway.
            // [TODO] could we skip this one-ring check in CollapseEdge? pass in hints?
			if ( collapse_creates_flip_or_invalid(a, b, ref vNewPos, t0, t1) ||  collapse_creates_flip_or_invalid(b, a, ref vNewPos, t0,t1) ) {
				retVal = ProcessResult.Ignored_CreatesFlip;
				goto skip_to_end;
			}

            // lots of cases where we cannot collapse, but we should just let
            // mesh sort that out, right?
            COUNT_COLLAPSES++;
			DMesh3.EdgeCollapseInfo collapseInfo;
			MeshResult result = mesh.CollapseEdge(iKeep, iCollapse, out collapseInfo);
			if ( result == MeshResult.Ok ) {
				collapseToV = iKeep;
				mesh.SetVertex(iKeep, vNewPos);
                if (constraints != null) {
                    constraints.ClearEdgeConstraint(edgeID);
                    constraints.ClearEdgeConstraint(collapseInfo.eRemoved0);
                    if ( collapseInfo.eRemoved1 != DMesh3.InvalidID )
                        constraints.ClearEdgeConstraint(collapseInfo.eRemoved1);
                    constraints.ClearVertexConstraint(iCollapse);
                }
                OnEdgeCollapse(edgeID, iKeep, iCollapse, collapseInfo);
                DoDebugChecks();

				retVal = ProcessResult.Ok_Collapsed;
			}

skip_to_end:
            end_collapse();
			return retVal;
		}





        protected void project_vertex(int vID, IProjectionTarget targetIn)
        {
            Vector3d curpos = mesh.GetVertex(vID);
            Vector3d projected = targetIn.Project(curpos, vID);
            mesh.SetVertex(vID, projected);
        }

        // used by collapse-edge to get projected position for new vertex
        protected Vector3d get_projected_collapse_position(int vid, Vector3d vNewPos)
        {
            if (constraints != null) {
                VertexConstraint vc = constraints.GetVertexConstraint(vid);
                if (vc.Target != null) 
                    return vc.Target.Project(vNewPos, vid);
                if (vc.Fixed)
                    return vNewPos;
            }

            // we don't need to do inline projection to target surface here because we 
            // already did it in OptimalPoint()

            return vNewPos;
        }




        // we can do projection in parallel if we have .net 
        protected virtual void FullProjectionPass()
        {
            Action<int> project = (vID) => {
                if (vertex_is_constrained(vID))
                    return;
                Vector3d curpos = mesh.GetVertex(vID);
                Vector3d projected = target.Project(curpos, vID);
                mesh.SetVertex(vID, projected);
            };

            gParallel.ForEach<int>(project_vertices(), project);
        }




        [Conditional("DEBUG")]
        protected virtual void RuntimeDebugCheck(int eid)
        {
            if (DebugEdges.Contains(eid))
                System.Diagnostics.Debugger.Break();
        }


        public bool ENABLE_DEBUG_CHECKS = false;
        protected virtual void DoDebugChecks()
        {
            if (ENABLE_DEBUG_CHECKS == false)
                return;

            DebugCheckVertexConstraints();

            // [RMS] keeping this for now, is useful in testing that we are preserving group boundaries
            //foreach ( int eid in mesh.EdgeIndices() ) {
            //    if (mesh.IsGroupBoundaryEdge(eid))
            //        if (constraints.GetEdgeConstraint(eid).CanFlip) {
            //            Util.gBreakToDebugger();
            //            throw new Exception("fuck");
            //        }
            //}
            //foreach ( int vid in mesh.VertexIndices() ) {
            //    if (mesh.IsGroupBoundaryVertex(vid))
            //        if (constraints.GetVertexConstraint(vid).Target == null)
            //            Util.gBreakToDebugger();
            //}
        }

        protected virtual void DebugCheckVertexConstraints()
        {
            if (constraints == null)
                return;

            foreach ( KeyValuePair<int,VertexConstraint> vc in constraints.VertexConstraintsItr() ) {
                int vid = vc.Key;
                if (vc.Value.Target != null) {
                    Vector3d curpos = mesh.GetVertex(vid);
                    Vector3d projected = vc.Value.Target.Project(curpos, vid);
                    if (curpos.DistanceSquared(projected) > 0.0001f)
                        Util.gBreakToDebugger();
                }
            }
        }




        //
        // profiling functions, turn on ENABLE_PROFILING to see output in console
        // 
        int COUNT_COLLAPSES;
		int COUNT_ITERATIONS;
        Stopwatch AllOpsW, SetupW, ProjectW, CollapseW;

        protected virtual void begin_pass() {
            if ( ENABLE_PROFILING ) {
                COUNT_COLLAPSES = 0;
				COUNT_ITERATIONS = 0;
                AllOpsW = new Stopwatch();
				SetupW = new Stopwatch();
                ProjectW = new Stopwatch();
                CollapseW = new Stopwatch();
            }
        }

        protected virtual void end_pass() {
            if ( ENABLE_PROFILING ) {
                System.Console.WriteLine(string.Format(
					"ReducePass: T {0} V {1} collapses {2}  iterations {3}", mesh.TriangleCount, mesh.VertexCount, COUNT_COLLAPSES, COUNT_ITERATIONS
                    ));
                System.Console.WriteLine(string.Format(
					"           Timing1: setup {0} ops {1} project {2}", Util.ToSecMilli(SetupW.Elapsed), Util.ToSecMilli(AllOpsW.Elapsed), Util.ToSecMilli(ProjectW.Elapsed)
                    ));
            }
        }

        protected virtual void begin_ops() {
            if ( ENABLE_PROFILING ) AllOpsW.Start();
        }
        protected virtual void end_ops() {
            if ( ENABLE_PROFILING ) AllOpsW.Stop();
        }
		protected virtual void begin_setup() {
			if (ENABLE_PROFILING) SetupW.Start();
		}
		protected virtual void end_setup() {
			if (ENABLE_PROFILING) SetupW.Stop();
		}

        protected virtual void begin_project() {
            if ( ENABLE_PROFILING ) ProjectW.Start();
        }
        protected virtual void end_project() {
            if ( ENABLE_PROFILING ) ProjectW.Stop();
        }

        protected virtual void begin_collapse() {
            if ( ENABLE_PROFILING ) CollapseW.Start();
        }
        protected virtual void end_collapse() {
            if ( ENABLE_PROFILING ) CollapseW.Stop();
        }


	}





	/// <summary>
	/// Stores quadratic function that evaluates distance to plane,
	/// in minimal 10-coefficient form, following http://mgarland.org/files/papers/qtheory.pdf
	/// - symmetric matrix A
	/// - vector b
	/// - constant c
	/// </summary>
	public struct QuadricError {
		public double Axx, Axy, Axz, Ayy, Ayz, Azz;
		public double bx, by, bz;
		public double c;

		public static readonly QuadricError Zero = new QuadricError() {
			Axx = 0, Axy = 0, Axz = 0, Ayy = 0, Ayz = 0, Azz = 0, bx = 0, by = 0, bz = 0, c = 0
		};

		public QuadricError(ref Vector3d n, ref Vector3d p) {
			Axx = n.x * n.x;
			Axy = n.x * n.y;
			Axz = n.x * n.z;
			Ayy = n.y * n.y;
			Ayz = n.y * n.z;
			Azz = n.z * n.z;
			bx = by = bz = c = 0;
			Vector3d v = multiplyA(ref p);
			bx = -v.x; by = -v.y; bz = -v.z;
			c = p.Dot(ref v);
		}
		public QuadricError(ref QuadricError a, ref QuadricError b) {
			Axx = a.Axx + b.Axx;
			Axy = a.Axy + b.Axy;
			Axz = a.Axz + b.Axz;
			Ayy = a.Ayy + b.Ayy;
			Ayz = a.Ayz + b.Ayz;
			Azz = a.Azz + b.Azz;
			bx = a.bx + b.bx;
			by = a.by + b.by;
			bz = a.bz + b.bz;
			c = a.c + b.c;
		}


		public void Add(double w, ref QuadricError b) {
			Axx += w * b.Axx;
			Axy += w * b.Axy;
			Axz += w * b.Axz;
			Ayy += w * b.Ayy;
			Ayz += w * b.Ayz;
			Azz += w * b.Azz;
			bx += w * b.bx;
			by += w * b.by;
			bz += w * b.bz;
			c += w * b.c;
		}


		/// <summary>
		/// returns pAp + 2*dot(p,b) + c
		/// </summary>
		public double Evaluate(ref Vector3d pt) {
			double x = Axx * pt.x + Axy * pt.y + Axz * pt.z;
			double y = Axy * pt.x + Ayy * pt.y + Ayz * pt.z;
			double z = Axz * pt.x + Ayz * pt.y + Azz * pt.z;
			return (pt.x * x + pt.y * y + pt.z * z) +
					 2.0 * (pt.x * bx + pt.y * by + pt.z * bz) + c;
		}


		Vector3d multiplyA(ref Vector3d pt) {
			double x = Axx * pt.x + Axy * pt.y + Axz * pt.z;
			double y = Axy * pt.x + Ayy * pt.y + Ayz * pt.z;
			double z = Axz * pt.x + Ayz * pt.y + Azz * pt.z;
			return new Vector3d(x, y, z);
		}



		public bool OptimalPoint(ref Vector3d result) {
			double a11 = Azz * Ayy - Ayz * Ayz;
			double a12 = Axz * Ayz - Azz * Axy;
			double a13 = Axy * Ayz - Axz * Ayy;
			double a22 = Azz * Axx - Axz * Axz;
			double a23 = Axy * Axz - Axx * Ayz;
			double a33 = Axx * Ayy - Axy * Axy;
			double det = (Axx * a11) + (Axy * a12) + (Axz * a13);
            // [RMS] not sure what we should be using for this threshold...have seen
            //  det less than 10^-9 on "normal" meshes.
			if (Math.Abs(det) > 1000.0*MathUtil.Epsilon) {
				det = 1.0 / det;
				a11 *= det; a12 *= det; a13 *= det;
				a22 *= det; a23 *= det; a33 *= det;				
				double x = a11 * bx + a12 * by + a13 * bz;
				double y = a12 * bx + a22 * by + a23 * bz;
				double z = a13 * bx + a23 * by + a33 * bz;
                result = new Vector3d(-x, -y, -z);
                return true;
			} else {
                return false;
			}

		}

	}




}
