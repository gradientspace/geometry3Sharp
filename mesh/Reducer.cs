using System;
using System.Collections.Generic;
using System.Diagnostics;


namespace g3 {

	public class Reducer
	{

		protected DMesh3 mesh;
		MeshConstraints constraints = null;
		IProjectionTarget target = null;

		public double MaxEdgeLength = 0.1f;

		// Sometimes we need to have very granular control over what happens to
		// specific vertices. This function allows client to specify such behavior.
		// Somewhat redundant w/ VertexConstraints, but simpler to code.
		[Flags]
		public enum VertexControl
		{
			AllowAll = 0,
			NoProject = 2,
			NoMovement = NoProject
		}
		public Func<int, VertexControl> VertexControlF;


		// other options

		// if true, we try to find position for collapsed vertices that
		// minimizes quadrice error. If false we just use midpoints.
		// Note: using midpoints is *significantly* slower, because it results
		// in may more points that would cause a triangle flip, which are then rejected.
		// (Also results in more invalid collapses, not sure why though...)
		public bool MinimizeQuadricPositionError = true;

		// if true, then when two Fixed vertices have the same non-invalid SetID,
		// we treat them as not fixed and allow collapse
		public bool AllowCollapseFixedVertsWithSameSetID = true;

		// [RMS] this is a debugging aid, will break to debugger if these edges are touched, in debug builds
		public List<int> DebugEdges = new List<int>();

		// if Target is set, we can project onto it in different ways
		enum TargetProjectionMode
		{
			NoProjection,           // disable projection
			AfterRefinement,        // do all projection after the refine/smooth pass
			Inline                  // project after each vertex update. Better results but more
									// expensive because eg we might create a vertex with
									// split, then project, then smooth, then project again.
		}
		TargetProjectionMode ProjectionMode = TargetProjectionMode.AfterRefinement;

		// this just lets us write more concise tests below
		bool EnableInlineProjection { get { return ProjectionMode == TargetProjectionMode.Inline; } }

		// set to true to print profiling info to console
		public bool ENABLE_PROFILING = false;


		public Reducer(DMesh3 m)
		{
			mesh = m;
		}
		protected Reducer()        // for subclasses that extend our behavior
		{
		}


		public DMesh3 Mesh {
			get { return mesh; }
		}
		public MeshConstraints Constraints {
			get { return constraints; }
		}


		//! This object will be modified !!!
		public void SetExternalConstraints(MeshConstraints cons)
		{
			constraints = cons;
		}


		public void SetProjectionTarget(IProjectionTarget target)
		{
			this.target = target;
		}





		QuadricError[] vertQuadrics;
		protected virtual void InitializeVertexQuadrics()
		{

			int NT = mesh.MaxTriangleID;
			QuadricError[] triQuadrics = new QuadricError[NT];
			double[] triAreas = new double[NT];
			gParallel.ForEach(mesh.TriangleIndices(), (tid) => {
				Vector3d c, n;
				mesh.GetTriInfo(tid, out n, out triAreas[tid], out c);
				triQuadrics[tid] = new QuadricError(n, c);
			});


			int NV = mesh.MaxVertexID;
			vertQuadrics = new QuadricError[NV];
			gParallel.ForEach(mesh.VertexIndices(), (vid) => {
				vertQuadrics[vid] = QuadricError.Zero;
				foreach (int tid in mesh.VtxTrianglesItr(vid)) {
					vertQuadrics[vid].Add(triAreas[tid], ref triQuadrics[tid]);
				}
				//Util.gDevAssert(MathUtil.EpsilonEqual(0, vertQuadrics[i].Evaluate(mesh.GetVertex(i)), MathUtil.Epsilon * 10));
			});

		}



		// internal class for priority queue
		class QEdge : g3ext.FastPriorityQueueNode, IEquatable<QEdge>
		{
			public int eid;
			public QuadricError q;
			public Vector3d collapse_pt;
			public QEdge() {
				eid = DMesh3.InvalidID;
			}
			public QEdge(int edge_id, QuadricError qin, Vector3d pt) {
				Initialize(edge_id, qin, pt);
			}

			public void Initialize(int edge_id, QuadricError qin, Vector3d pt) {
				eid = edge_id;
				q = qin;
				collapse_pt = pt;				
			}

			public bool Equals(QEdge other) {
				return eid == other.eid;
			}
		}

		g3ext.FastPriorityQueue<QEdge> EdgeQueue;
		QEdge[] Nodes;
		MemoryPool<QEdge> NodePool;

		protected virtual void InitializeQueue()
		{
			int NE = mesh.EdgeCount;

			Nodes = new QEdge[2*NE];		// [RMS] do we need this many?
			NodePool = new MemoryPool<QEdge>(NE);
			EdgeQueue = new g3ext.FastPriorityQueue<QEdge>(NE);

			int cur_eid = start_edges();
			bool done = false;
			do {
				if (mesh.IsEdge(cur_eid)) {
					Index2i ev = mesh.GetEdgeV(cur_eid);

					QuadricError Q = new QuadricError(ref vertQuadrics[ev.a], ref vertQuadrics[ev.b]);
					Vector3d opt = OptimalPoint(Q, ev.a, ev.b);
					double err = Q.Evaluate(opt);

					QEdge ee = NodePool.Allocate();
					ee.Initialize(cur_eid, Q, opt);
					Nodes[cur_eid] = ee;
					EdgeQueue.Enqueue(ee, (float)err);

				}
				cur_eid = next_edge(cur_eid, out done);
			} while (done == false);


		}


		// return point that minimizes quadric error for edge [ea,eb]
		Vector3d OptimalPoint(QuadricError q, int ea, int eb) {
			if (MinimizeQuadricPositionError == false) {
				return (mesh.GetVertex(ea) + mesh.GetVertex(eb)) * 0.5;
			} else {
				try {
					return q.OptimalPoint();
				} catch {
					// degenerate matrix, evaluate quadric at edge end and midpoints
					// (could do line search here...)
					Vector3d va = mesh.GetVertex(ea);
					Vector3d vb = mesh.GetVertex(eb);
					Vector3d c = (va + vb) * 0.5;
					double fa = q.Evaluate(va);
					double fb = q.Evaluate(vb);
					double fc = q.Evaluate(c);
					double m = MathUtil.Min(fa, fb, fc);
					if (m == fa) return va;
					else if (m == fb) return vb;
					return c;
				}
			}
		}


		// update queue weight for each edge in vertex one-ring
		protected virtual void UpdateNeighbours(int vid) 
		{
			foreach (int eid in mesh.VtxEdgesItr(vid)) {
				Index2i nev = mesh.GetEdgeV(eid);
				QuadricError Q = new QuadricError(ref vertQuadrics[nev.a], ref vertQuadrics[nev.b]);
				Vector3d opt = OptimalPoint(Q, nev.a, nev.b);
				double err = Q.Evaluate(opt);
				QEdge eid_node = Nodes[eid];
				if (eid_node != null) {
					eid_node.q = Q;
					eid_node.collapse_pt = opt;
					EdgeQueue.UpdatePriority(eid_node, (float)err);
				} else {
					QEdge ee = NodePool.Allocate();
					ee.Initialize(eid, Q, opt);
					Nodes[eid] = ee;
					EdgeQueue.Enqueue(ee, (float)err);
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




		protected double MinEdgeLength = double.MaxValue;
		protected int TargetTriangleCount = int.MaxValue;



		public virtual void DoReduce()
		{
			if (mesh.TriangleCount == 0)    // badness if we don't catch this...
				return;

			begin_pass();

			begin_setup();
			InitializeVertexQuadrics();
			InitializeQueue();
			end_setup();

			begin_ops();

			begin_collapse();
			while (EdgeQueue.Count > 0 && mesh.TriangleCount > TargetTriangleCount) {
				COUNT_ITERATIONS++;
				QEdge cur = EdgeQueue.Dequeue();
				Nodes[cur.eid] = null;
				NodePool.Return(cur);
				if (!mesh.IsEdge(cur.eid))
					continue;

				int vKept;
				ProcessResult result = CollapseEdge(cur.eid, cur.collapse_pt, out vKept);
				if (result == ProcessResult.Ok_Collapsed) {
					vertQuadrics[vKept] = cur.q;
					UpdateNeighbours(vKept);
				}
			}
			end_collapse();
			end_ops();

			Reproject();

			end_pass();
		}




		public virtual void ReduceToTriangleCount(int nCount) {
			TargetTriangleCount = nCount;
			MinEdgeLength = double.MaxValue;
			DoReduce();
		}



		public virtual void ReduceToEdgeLength(double minEdgeLen) {
			TargetTriangleCount = 1;
			MinEdgeLength = minEdgeLen;
			DoReduce();
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

			if (creates_flip_or_invalid(a, b, vNewPos, t0, t1)) {
				retVal = ProcessResult.Ignored_CreatesFlip;
				goto skip_to_end;
			}
				

            // TODO be smart about picking b (keep vtx). 
            //    - swap if one is bdry vtx, for example?
            // lots of cases where we cannot collapse, but we should just let
            // mesh sort that out, right?
            COUNT_COLLAPSES++;
			DMesh3.EdgeCollapseInfo collapseInfo;
			MeshResult result = mesh.CollapseEdge(iKeep, iCollapse, out collapseInfo);
			if ( result == MeshResult.Ok ) {
				collapseToV = b;
				mesh.SetVertex(b, vNewPos);
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



		bool creates_flip_or_invalid(int vid, int vother, Vector3d newv, int tc, int td) {
			foreach ( int tid in mesh.VtxTrianglesItr(vid)) {
				if (tid == tc || tid == td)
					continue;
				Index3i curt = mesh.GetTriangle(tid);
				if (curt.a == vother || curt.b == vother || curt.c == vother)
					return true;		// invalid nbrhood for collapse
				Vector3d va = mesh.GetVertex(curt.a);
				Vector3d vb = mesh.GetVertex(curt.b);
				Vector3d vc = mesh.GetVertex(curt.c);
				Vector3d ncur = (vb - va).Cross(vc - va);
				double sign = 0;
				if (curt.a == vid) {
					Vector3d nnew = (vb - newv).Cross(vc - newv);
					sign = ncur.Dot(nnew);
				} else if (curt.b == vid) {
					Vector3d nnew = (newv - va).Cross(vc - va);
					sign = ncur.Dot(nnew);
				} else if (curt.c == vid) {
					Vector3d nnew = (vb - va).Cross(newv - va);
					sign = ncur.Dot(nnew);
				} else
					throw new Exception("should never be here!");
				if (sign <= 0)
					return true;
			}
			return false;
		}


        // Figure out if we can collapse edge eid=[a,b] under current constraint set.
        // First we resolve vertex constraints using can_collapse_vtx(). However this
        // does not catch some topological cases at the edge-constraint level, which 
        // which we will only be able to detect once we know if we are losing a or b.
        // See comments on can_collapse_vtx() for what collapse_to is for.
        bool can_collapse_constraints(int eid, int a, int b, int c, int d, int tc, int td, out int collapse_to)
        {
            collapse_to = -1;
            if (constraints == null)
                return true;
            bool bVtx = can_collapse_vtx(eid, a, b, out collapse_to);
            if (bVtx == false)
                return false;

            // when we lose a vtx in a collapse, we also lose two edges [iCollapse,c] and [iCollapse,d].
            // If either of those edges is constrained, we would lose that constraint.
            // This would be bad.
            int iCollapse = (collapse_to == a) ? b : a;
            if (c != DMesh3.InvalidID) {
                int ec = mesh.FindEdgeFromTri(iCollapse, c, tc);
                if (constraints.GetEdgeConstraint(ec).IsUnconstrained == false)
                    return false;
            }
            if (d != DMesh3.InvalidID) {
                int ed = mesh.FindEdgeFromTri(iCollapse, d, td);
                if (constraints.GetEdgeConstraint(ed).IsUnconstrained == false)
                    return false;
            }

            return true;
        }


        // resolve vertex constraints for collapsing edge eid=[a,b]. Generally we would
        // collapse a to b, and set the new position as 0.5*(v_a+v_b). However if a *or* b
        // are constrained, then we want to keep that vertex and collapse to its position.
        // This vertex (a or b) will be returned in collapse_to, which is -1 otherwise.
        // If a *and* b are constrained, then things are complicated (and documented below).
        bool can_collapse_vtx(int eid, int a, int b, out int collapse_to)
        {
            collapse_to = -1;
            if (constraints == null)
                return true;
            VertexConstraint ca = constraints.GetVertexConstraint(a);
            VertexConstraint cb = constraints.GetVertexConstraint(b);

            // no constraint at all
            if (ca.Fixed == false && cb.Fixed == false && ca.Target == null && cb.Target == null)
                return true;

            // handle a or b fixed
            if ( ca.Fixed == true && cb.Fixed == false ) {
                collapse_to = a;
                return true;
            }
            if ( cb.Fixed == true && ca.Fixed == false) {
                collapse_to = b;
                return true;
            }
            // if both fixed, and options allow, treat this edge as unconstrained (eg collapse to midpoint)
            // [RMS] tried picking a or b here, but something weird happens, where
            //   eg cylinder cap will entirely erode away. Somehow edge lengths stay below threshold??
            if ( AllowCollapseFixedVertsWithSameSetID 
                    && ca.FixedSetID >= 0 
                    && ca.FixedSetID == cb.FixedSetID) {
                return true;
            }

            // handle a or b w/ target
            if ( ca.Target != null && cb.Target == null ) {
                collapse_to = a;
                return true;
            }
            if ( cb.Target != null && ca.Target == null ) {
                collapse_to = b;
                return true;
            }
            // if both vertices are on the same target, and the edge is on that target,
            // then we can collapse to either and use the midpoint (which will be projected
            // to the target). *However*, if the edge is not on the same target, then we 
            // cannot collapse because we would be changing the constraint topology!
            if ( cb.Target != null && ca.Target != null && ca.Target == cb.Target ) {
                if ( constraints.GetEdgeConstraint(eid).Target == ca.Target )
                    return true;
            }

            return false;            
        }


        bool vertex_is_fixed(int vid)
        {
            if (constraints != null && constraints.GetVertexConstraint(vid).Fixed)
                return true;
            return false;
        }
        bool vertex_is_constrained(int vid)
        {
            if ( constraints != null ) {
                VertexConstraint vc = constraints.GetVertexConstraint(vid);
                if (vc.Fixed || vc.Target != null)
                    return true;
            }
            return false;
        }

        VertexConstraint get_vertex_constraint(int vid)
        {
            if (constraints != null)
                return constraints.GetVertexConstraint(vid);
            return VertexConstraint.Unconstrained;
        }

        void project_vertex(int vID, IProjectionTarget targetIn)
        {
            Vector3d curpos = mesh.GetVertex(vID);
            Vector3d projected = targetIn.Project(curpos, vID);
            mesh.SetVertex(vID, projected);
        }

        // used by collapse-edge to get projected position for new vertex
        Vector3d get_projected_collapse_position(int vid, Vector3d vNewPos)
        {
            if (constraints != null) {
                VertexConstraint vc = constraints.GetVertexConstraint(vid);
                if (vc.Target != null) 
                    return vc.Target.Project(vNewPos, vid);
                if (vc.Fixed)
                    return vNewPos;
            }
            // no constraint applied, so if we have a target surface, project to that
            if ( EnableInlineProjection && target != null ) {
                if (VertexControlF == null || (VertexControlF(vid) & VertexControl.NoProject) == 0)
                    return target.Project(vNewPos, vid);
            }
            return vNewPos;
        }






        // we can do projection in parallel if we have .net 
        void FullProjectionPass()
        {
            Action<int> project = (vID) => {
                if (vertex_is_constrained(vID))
                    return;
                if (VertexControlF != null && (VertexControlF(vID) & VertexControl.NoProject) != 0)
                    return;
                Vector3d curpos = mesh.GetVertex(vID);
                Vector3d projected = target.Project(curpos, vID);
                mesh.SetVertex(vID, projected);
            };

            gParallel.ForEach<int>(project_vertices(), project);
        }




        [Conditional("DEBUG")] 
        void RuntimeDebugCheck(int eid)
        {
            if (DebugEdges.Contains(eid))
                System.Diagnostics.Debugger.Break();
        }


        public bool ENABLE_DEBUG_CHECKS = false;
        void DoDebugChecks()
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

        void DebugCheckVertexConstraints()
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

		public QuadricError(Vector3d n, Vector3d p) {
			Axx = n.x * n.x;
			Axy = n.x * n.y;
			Axz = n.x * n.z;
			Ayy = n.y * n.y;
			Ayz = n.y * n.z;
			Azz = n.z * n.z;
			bx = by = bz = c = 0;
			Vector3d v = multiplyA(p);
			bx = -v.x; by = -v.y; bz = -v.z;
			c = p.Dot(v);
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
		public double Evaluate(Vector3d pt) {
			double x = Axx * pt.x + Axy * pt.y + Axz * pt.z;
			double y = Axy * pt.x + Ayy * pt.y + Ayz * pt.z;
			double z = Axz * pt.x + Ayz * pt.y + Azz * pt.z;
			return (pt.x * x + pt.y * y + pt.z * z) +
					 2.0 * (pt.x * bx + pt.y * by + pt.z * bz) + c;
		}


		Vector3d multiplyA(Vector3d pt) {
			double x = Axx * pt.x + Axy * pt.y + Axz * pt.z;
			double y = Axy * pt.x + Ayy * pt.y + Ayz * pt.z;
			double z = Axz * pt.x + Ayz * pt.y + Azz * pt.z;
			return new Vector3d(x, y, z);
		}




		public Vector3d OptimalPoint() {
			double a11 = Azz * Ayy - Ayz * Ayz;
			double a12 = Axz * Ayz - Azz * Axy;
			double a13 = Axy * Ayz - Axz * Ayy;
			double a22 = Azz * Axx - Axz * Axz;
			double a23 = Axy * Axz - Axx * Ayz;
			double a33 = Axx * Ayy - Axy * Axy;
			double det = (Axx * a11) + (Axy * a12) + (Axz * a13);
			if (Math.Abs(det) > MathUtil.Epsilonf) {
				det = 1.0 / det;
				a11 *= det; a12 *= det; a13 *= det;
				a22 *= det; a23 *= det; a33 *= det;				
				double x = a11 * bx + a12 * by + a13 * bz;
				double y = a12 * bx + a22 * by + a23 * bz;
				double z = a13 * bx + a23 * by + a33 * bz;
				return new Vector3d(-x, -y, -z);
			} else {
				throw new Exception("now what?");
			}

		}

	}




}
