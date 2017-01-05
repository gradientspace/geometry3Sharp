using System;
using System.Collections.Generic;

namespace g3 {
	
	public class Remesher {

		DMesh3 mesh;
        MeshConstraints constraints = null;
        IProjectionTarget target = null;

		public bool EnableFlips = true;
		public bool EnableCollapses = true;
		public bool EnableSplits = true;
		public bool EnableSmoothing = true;


		public double MinEdgeLength = 0.001f;
		public double MaxEdgeLength = 0.1f;

		public double SmoothSpeedT = 0.1f;
		public enum SmoothTypes {
			Uniform, Cotan, MeanValue
		};
		public SmoothTypes SmoothType = SmoothTypes.Uniform;


		public Remesher(DMesh3 m) {
			mesh = m;
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


		public void BasicRemeshPass() {

			int nMaxEdgeID = mesh.MaxEdgeID;
			for ( int eid = 0; eid < nMaxEdgeID; ++eid ) {
				if ( ! mesh.IsEdge(eid) )
					continue;

				/*ProcessResult result = */ProcessEdge(eid);
				// do what with result??
			}

			if ( EnableSmoothing && SmoothSpeedT > 0)
				FullSmoothPass_InPlace();

            if (target != null)
                FullProjectionPass();
		}




		enum ProcessResult {
			Ok_Collapsed,
			Ok_Flipped,
			Ok_Split,
			Ignored_EdgeIsFine,
            Ignored_EdgeIsFullyConstrained,
			Failed_OpNotSuccessful,
			Failed_NotAnEdge
		};

		ProcessResult ProcessEdge(int edgeID) 
		{
            EdgeConstraint constraint =
                (constraints == null) ? EdgeConstraint.Unconstrained : constraints.GetEdgeConstraint(edgeID);
            if (constraint.NoModifications)
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

            bool aFixed = vertex_is_fixed(a);
            bool bFixed = vertex_is_fixed(b);
            bool bothFixed = (aFixed && bFixed);

			// optimization: if edge cd exists, we cannot collapse or flip. look that up here?
			//  funcs will do it internally...
			//  (or maybe we can collapse if cd exists? edge-collapse doesn't check for it explicitly...)

			// if edge length is too short, we want to collapse it
			bool bTriedCollapse = false;
			if ( EnableCollapses && constraint.CanCollapse && bothFixed == false && edge_len_sqr < MinEdgeLength*MinEdgeLength ) {

                int iKeep = b, iCollapse = a;
                Vector3d vNewPos = (vA + vB) * 0.5;

                // if either vtx is fixed, collapse to that position
                if ( bFixed ) {
                    vNewPos = vB;
                } else if ( aFixed ) {
                    iKeep = a; iCollapse = b;
                    vNewPos = vA;
                }

				// TODO be smart about picking b (keep vtx). 
				//    - swap if one is bdry vtx, for example?

				// lots of cases where we cannot collapse, but we should just let
				// mesh sort that out, right?

				DMesh3.EdgeCollapseInfo collapseInfo;
				MeshResult result = mesh.CollapseEdge(iKeep, iCollapse, out collapseInfo);
				if ( result == MeshResult.Ok ) {
					mesh.SetVertex(b, vNewPos);
                    if (constraints != null) {
                        constraints.ClearEdgeConstraint(edgeID);
                        constraints.ClearVertexConstraint(iCollapse);
                    }

					return ProcessResult.Ok_Collapsed;
				} else 
					bTriedCollapse = true;

			}

			// if this is not a boundary edge, maybe we want to flip
			bool bTriedFlip = false;
			if ( EnableFlips && constraint.CanFlip && bIsBoundaryEdge == false ) {

				// don't want to flip if it will invert triangle...tetrahedron sign??

				// can we do this more efficiently somehow?
				bool a_is_boundary_vtx = bIsBoundaryEdge || mesh.vertex_is_boundary(a);
				bool b_is_boundary_vtx = bIsBoundaryEdge || mesh.vertex_is_boundary(b);
				bool c_is_boundary_vtx = mesh.vertex_is_boundary(c);
				bool d_is_boundary_vtx = mesh.vertex_is_boundary(d);
				int valence_a = mesh.GetVtxEdgeValence(a), valence_b = mesh.GetVtxEdgeValence(b);
				int valence_c = mesh.GetVtxEdgeValence(c), valence_d = mesh.GetVtxEdgeValence(d);
				int valence_a_target = (a_is_boundary_vtx) ? valence_a : 6;
				int valence_b_target = (b_is_boundary_vtx) ? valence_b : 6;
				int valence_c_target = (c_is_boundary_vtx) ? valence_c : 6;
				int valence_d_target = (d_is_boundary_vtx) ? valence_d : 6;


				// if total valence error improves by flip, we want to do it
				int curr_err = Math.Abs(valence_a-valence_a_target) + Math.Abs(valence_b-valence_b_target)
				                   + Math.Abs(valence_c-valence_c_target) + Math.Abs(valence_d-valence_d_target);
				int flip_err = Math.Abs((valence_a-1)-valence_a_target) + Math.Abs((valence_b-1)-valence_b_target)
				                   + Math.Abs((valence_c+1)-valence_c_target) + Math.Abs((valence_d+1)-valence_d_target);

				if ( flip_err < curr_err ) {
					// try flip
					DMesh3.EdgeFlipInfo flipInfo;
					MeshResult result = mesh.FlipEdge(edgeID, out flipInfo);
					if ( result == MeshResult.Ok ) {

						return ProcessResult.Ok_Flipped;
					} else 
						bTriedFlip = true;

				}

			}


			// if edge length is too long, we want to split it
			bool bTriedSplit = false;
			if ( EnableSplits && constraint.CanSplit && edge_len_sqr > MaxEdgeLength*MaxEdgeLength ) {

				DMesh3.EdgeSplitInfo splitInfo;
				MeshResult result = mesh.SplitEdge(edgeID, out splitInfo);
				if ( result == MeshResult.Ok ) {
                    if (constraints != null)
                        update_constraints_after_split(edgeID, a, b, splitInfo);

					return ProcessResult.Ok_Split;
				} else
					bTriedSplit = true;
			}


			if ( bTriedFlip || bTriedSplit || bTriedCollapse )
				return ProcessResult.Failed_OpNotSuccessful;
			else
				return ProcessResult.Ignored_EdgeIsFine;
		}



        void update_constraints_after_split(int edgeID, int va, int vb, DMesh3.EdgeSplitInfo splitInfo)
        {
            if (constraints.HasEdgeConstraint(edgeID)) {
                constraints.SetOrUpdateEdgeConstraint(splitInfo.eNew, constraints.GetEdgeConstraint(edgeID));

                // [RMS] not clear this is the right thing to do. note that we
                //   cannot do outside loop because then pair of fixed verts connected
                //   by unconstrained edge will produce a new fixed vert, which is bad
                //   (eg on minimal triangulation of capped cylinder)
                if (vertex_is_fixed(va) && vertex_is_fixed(vb))
                    constraints.SetOrUpdateVertexConstraint(splitInfo.vNew,
                        new VertexConstraint(true));
            }
        }




        bool vertex_is_fixed(int vid)
        {
            if (constraints != null && constraints.GetVertexConstraint(vid).Fixed)
                return true;
            return false;
        }




		void FullSmoothPass_InPlace() {
            Func<DMesh3, int, double, Vector3d> smoothFunc = MeshUtil.UniformSmooth;
            if (SmoothType == SmoothTypes.MeanValue)
                smoothFunc = MeshUtil.MeanValueSmooth;
            else if (SmoothType == SmoothTypes.Cotan)
                smoothFunc = MeshUtil.CotanSmooth;

			foreach ( int vID in mesh.VertexIndices() ) {

                if (vertex_is_fixed(vID))
                    continue;

				Vector3d vSmoothed = smoothFunc(mesh, vID, SmoothSpeedT);
				mesh.SetVertex( vID, vSmoothed);
			}
		}



        void FullProjectionPass()
        {
            foreach ( int vID in mesh.VertexIndices() ) {
                if (vertex_is_fixed(vID))
                    continue;
                Vector3d curpos = mesh.GetVertex(vID);
                Vector3d projected = target.Project(curpos);
                mesh.SetVertex(vID, projected);
            }
        }



	}
}
