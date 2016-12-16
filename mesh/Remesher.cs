using System;
using System.Collections.Generic;

namespace g3 {
	
	public class Remesher {

		DMesh3 mesh;

		public double MinEdgeLength = 0.001f;
		public double MaxEdgeLength = 0.1f;

		public double SmoothSpeedT = 0.1f;

		public bool EnableFlips = true;
		public bool EnableCollapses = true;
		public bool EnableSplits = true;
		public bool EnableSmoothing = true;

		public Remesher(DMesh3 m) {
			mesh = m;
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

		}





		enum ProcessResult {
			Ok_Collapsed,
			Ok_Flipped,
			Ok_Split,
			Ignored_EdgeIsFine,
			Failed_OpNotSuccessful,
			Failed_NotAnEdge
		};

		ProcessResult ProcessEdge(int edgeID) 
		{
			// look up verts and tris for this edge
			int a = 0, b = 0, t0 = 0, t1 = 0;
			if ( mesh.GetEdge(edgeID, ref a, ref b, ref t0, ref t1) == false )
				return ProcessResult.Failed_NotAnEdge;
			bool bIsBoundaryEdge = (t1 == DMesh3.InvalidID);

			// look up 'other' verts c (from t0) and d (from t1, if it exists)
			Vector3i T0tv = mesh.GetTriangle(t0);
			int c = IndexUtil.find_tri_other_vtx(a, b, T0tv);
			Vector3i T1tv = (bIsBoundaryEdge) ? DMesh3.InvalidTriangle : mesh.GetTriangle(t1);
			int d = (bIsBoundaryEdge) ? DMesh3.InvalidID : IndexUtil.find_tri_other_vtx( a, b, T1tv );

			Vector3d vA = mesh.GetVertex(a);
			Vector3d vB = mesh.GetVertex(b);
			double edge_len_sqr = (vA-vB).LengthSquared;

			// optimization: if edge cd exists, we cannot collapse or flip. look that up here?
			//  funcs will do it internally...
			//  (or maybe we can collapse if cd exists? edge-collapse doesn't check for it explicitly...)

			// if edge length is too short, we want to collapse it
			bool bTriedCollapse = false;
			if ( EnableCollapses && edge_len_sqr < MinEdgeLength*MinEdgeLength ) {

				// TODO be smart about picking b (keep vtx). 
				//    - swap if one is bdry vtx, for example?

				// lots of cases where we cannot collapse, but we should just let
				// mesh sort that out, right?

				DMesh3.EdgeCollapseInfo collapseInfo;
				MeshResult result = mesh.CollapseEdge(b, a, out collapseInfo);
				if ( result == MeshResult.Ok ) {

					Vector3d vNewPos = (vA + vB) * 0.5f;
					mesh.SetVertex(b, vNewPos);

					return ProcessResult.Ok_Collapsed;
				} else 
					bTriedCollapse = true;

			}

			// if this is not a boundary edge, maybe we want to flip
			bool bTriedFlip = false;
			if ( EnableFlips && bIsBoundaryEdge == false ) {

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
			if ( EnableSplits && edge_len_sqr > MaxEdgeLength*MaxEdgeLength ) {

				DMesh3.EdgeSplitInfo splitInfo;
				MeshResult result = mesh.SplitEdge(edgeID, out splitInfo);
				if ( result == MeshResult.Ok ) {
					return ProcessResult.Ok_Split;
				} else
					bTriedSplit = true;
			}


			if ( bTriedFlip || bTriedSplit || bTriedCollapse )
				return ProcessResult.Failed_OpNotSuccessful;
			else
				return ProcessResult.Ignored_EdgeIsFine;
		}




		void FullSmoothPass_InPlace() {
			foreach ( int vID in mesh.VertexIndices() ) {
				Vector3d vSmoothed = MeshUtil.UniformSmooth(mesh, vID, SmoothSpeedT);
				mesh.SetVertex( vID, vSmoothed);
			}

		}



	}
}
