using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    public class MeshRefinerBase
    {
        protected DMesh3 mesh;
        protected MeshConstraints constraints = null;


        // if true, then when two Fixed vertices have the same non-invalid SetID,
        // we treat them as not fixed and allow collapse
        public bool AllowCollapseFixedVertsWithSameSetID = true;


        /// <summary>
        /// If normals dot product is less than this, we consider it a normal flip. default = 0
        /// </summary>
        public double EdgeFlipTolerance {
            get { return edge_flip_tol; }
            set { edge_flip_tol = MathUtil.Clamp(value, -1.0, 1.0); }
        }
        protected double edge_flip_tol = 0.0f;


        public MeshRefinerBase(DMesh3 mesh) {
            this.mesh = mesh;
        }

        protected MeshRefinerBase() {
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


        /// <summary>
        /// Set this to be able to cancel running remesher
        /// </summary>
        public ProgressCancel Progress = null;

        /// <summary>
        /// if this returns true, abort computation. 
        /// </summary>
        protected virtual bool Cancelled() {
            return (Progress == null) ? false : Progress.Cancelled();
        }


        protected double edge_flip_metric(ref Vector3d n0, ref Vector3d n1)
        {
            if (edge_flip_tol == 0) {
                return n0.Dot(n1);
            } else {
                return n0.Normalized.Dot(n1.Normalized);
            }
        }


        /// <summary>
        /// check if edge collapse will create a face-normal flip. 
        /// Also checks if collapse would violate link condition, since
        /// we are iterating over one-ring anyway.
        /// 
        /// This only checks one-ring of vid, so you have to call it twice,
        /// with vid and vother reversed, to check both one-rings
        /// </summary>
        protected bool collapse_creates_flip_or_invalid(int vid, int vother, ref Vector3d newv, int tc, int td)
        {
            Vector3d va = Vector3d.Zero, vb = Vector3d.Zero, vc = Vector3d.Zero;
            foreach (int tid in mesh.VtxTrianglesItr(vid)) {
                if (tid == tc || tid == td)
                    continue;
                Index3i curt = mesh.GetTriangle(tid);
                if (curt.a == vother || curt.b == vother || curt.c == vother)
                    return true;		// invalid nbrhood for collapse
                mesh.GetTriVertices(tid, ref va, ref vb, ref vc);
                Vector3d ncur = (vb - va).Cross(vc - va);
                double sign = 0;
                if (curt.a == vid) {
                    Vector3d nnew = (vb - newv).Cross(vc - newv);
                    sign = edge_flip_metric(ref ncur, ref nnew);
                } else if (curt.b == vid) {
                    Vector3d nnew = (newv - va).Cross(vc - va);
                    sign = edge_flip_metric(ref ncur, ref nnew);
                } else if (curt.c == vid) {
                    Vector3d nnew = (vb - va).Cross(newv - va);
                    sign = edge_flip_metric(ref ncur, ref nnew);
                } else
                    throw new Exception("should never be here!");
                if (sign <= edge_flip_tol)
                    return true;
            }
            return false;
        }



        /// <summary>
        /// Check if edge flip might reverse normal direction. 
        /// 
        /// Not entirely clear on how to implement this test. 
        /// Currently checking if any normal-pairs are reversed.
        /// </summary>
        protected bool flip_inverts_normals(int a, int b, int c, int d, int t0)
        {
            Vector3d vC = mesh.GetVertex(c), vD = mesh.GetVertex(d);
            Index3i tri_v = mesh.GetTriangle(t0);
            int oa = a, ob = b;
            IndexUtil.orient_tri_edge(ref oa, ref ob, ref tri_v);
            Vector3d vOA = mesh.GetVertex(oa), vOB = mesh.GetVertex(ob);
            Vector3d n0 = MathUtil.FastNormalDirection(ref vOA, ref vOB, ref vC);
            Vector3d n1 = MathUtil.FastNormalDirection(ref vOB, ref vOA, ref vD);
            Vector3d f0 = MathUtil.FastNormalDirection(ref vC, ref vD, ref vOB);
            if ( edge_flip_metric(ref n0, ref f0) <= edge_flip_tol || edge_flip_metric(ref n1, ref f0) <= edge_flip_tol)
                return true;
            Vector3d f1 = MathUtil.FastNormalDirection(ref vD, ref vC, ref vOA);
            if (edge_flip_metric(ref n0, ref f1) <= edge_flip_tol || edge_flip_metric(ref n1, ref f1) <= edge_flip_tol)
                return true;

            // this only checks if output faces are pointing towards eachother, which seems 
            // to still result in normal-flips in some cases
            //if (f0.Dot(f1) < 0)
            //    return true;

            return false;
        }






        // Figure out if we can collapse edge eid=[a,b] under current constraint set.
        // First we resolve vertex constraints using can_collapse_vtx(). However this
        // does not catch some topological cases at the edge-constraint level, which 
        // which we will only be able to detect once we know if we are losing a or b.
        // See comments on can_collapse_vtx() for what collapse_to is for.
        protected bool can_collapse_constraints(int eid, int a, int b, int c, int d, int tc, int td, out int collapse_to)
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
        protected bool can_collapse_vtx(int eid, int a, int b, out int collapse_to)
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
            if (ca.Fixed == true && cb.Fixed == false) {
                // if b is fixed to a target, and it is different than a's target, we can't collapse
                if (cb.Target != null && cb.Target != ca.Target)
                    return false;
                collapse_to = a;
                return true;
            }
            if (cb.Fixed == true && ca.Fixed == false) {
                if (ca.Target != null && ca.Target != cb.Target)
                    return false;
                collapse_to = b;
                return true;
            }
            // if both fixed, and options allow, treat this edge as unconstrained (eg collapse to midpoint)
            // [RMS] tried picking a or b here, but something weird happens, where
            //   eg cylinder cap will entirely erode away. Somehow edge lengths stay below threshold??
            if (AllowCollapseFixedVertsWithSameSetID
                    && ca.FixedSetID >= 0
                    && ca.FixedSetID == cb.FixedSetID) {
                return true;
            }

            // handle a or b w/ target
            if (ca.Target != null && cb.Target == null) {
                collapse_to = a;
                return true;
            }
            if (cb.Target != null && ca.Target == null) {
                collapse_to = b;
                return true;
            }
            // if both vertices are on the same target, and the edge is on that target,
            // then we can collapse to either and use the midpoint (which will be projected
            // to the target). *However*, if the edge is not on the same target, then we 
            // cannot collapse because we would be changing the constraint topology!
            if (cb.Target != null && ca.Target != null && ca.Target == cb.Target) {
                if (constraints.GetEdgeConstraint(eid).Target == ca.Target)
                    return true;
            }

            return false;
        }




        protected bool vertex_is_fixed(int vid)
        {
            if (constraints != null && constraints.GetVertexConstraint(vid).Fixed)
                return true;
            return false;
        }
        protected bool vertex_is_constrained(int vid)
        {
            if (constraints != null) {
                VertexConstraint vc = constraints.GetVertexConstraint(vid);
                if (vc.Fixed || vc.Target != null)
                    return true;
            }
            return false;
        }

        protected VertexConstraint get_vertex_constraint(int vid)
        {
            if (constraints != null)
                return constraints.GetVertexConstraint(vid);
            return VertexConstraint.Unconstrained;
        }
        protected bool get_vertex_constraint(int vid, ref VertexConstraint  vc)
        {
            return (constraints == null) ? false :
                constraints.GetVertexConstraint(vid, ref vc);
        }

    }
}
