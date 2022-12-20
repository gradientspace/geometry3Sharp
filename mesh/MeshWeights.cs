using System;

namespace g3 
{
	public static class MeshWeights {

		public static Vector3d OneRingCentroid(DMesh3 mesh, int vID) 
		{
			Vector3d vSum = Vector3d.Zero;
			int nCount = 0;
			foreach ( int nbr in mesh.VtxVerticesItr(vID) ) {
				vSum += mesh.GetVertex(nbr);
				nCount++;
			}
            if (nCount == 0)
                return mesh.GetVertex(vID);
			double m = 1.0 / nCount;
			vSum.x *= m; vSum.y *= m; vSum.z *= m;
			return vSum;
		}


		// Compute cotan-weighted neighbour sum around a vertex.
		// These weights are numerically unstable if any of the triangles are degenerate.
		// We catch these problems and return input vertex as centroid
		// http://www.geometry.caltech.edu/pubs/DMSB_III.pdf
		public static Vector3d CotanCentroid(DMesh3 mesh, int v_i)
		{
			Vector3d vSum = Vector3d.Zero;
			double wSum = 0;
			Vector3d Vi = mesh.GetVertex(v_i);

			int v_j = DMesh3.InvalidID, opp_v1 = DMesh3.InvalidID, opp_v2 = DMesh3.InvalidID;
			int t1 = DMesh3.InvalidID, t2 = DMesh3.InvalidID;
			bool bAborted = false;
			foreach ( int eid in mesh.VtxEdgesItr(v_i) ) {
				opp_v2 = DMesh3.InvalidID;
				mesh.GetVtxNbrhood(eid, v_i, ref v_j, ref opp_v1, ref opp_v2, ref t1, ref t2);
				Vector3d Vj = mesh.GetVertex(v_j);

				Vector3d Vo1 = mesh.GetVertex(opp_v1);
				double cot_alpha_ij = MathUtil.VectorCot(
					(Vi-Vo1).Normalized, (Vj-Vo1).Normalized );
				if ( cot_alpha_ij == 0 ) {
					bAborted = true;
					break;
				}
				double w_ij = cot_alpha_ij;

				if ( opp_v2 != DMesh3.InvalidID ) {
					Vector3d Vo2 = mesh.GetVertex(opp_v2);
					double cot_beta_ij = MathUtil.VectorCot(
						(Vi-Vo2).Normalized, (Vj-Vo2).Normalized );
					if ( cot_beta_ij == 0 ) {
						bAborted = true;
						break;
					}
					w_ij += cot_beta_ij;
				}

				vSum += w_ij * Vj;
				wSum += w_ij;
			}
			if ( bAborted || Math.Abs(wSum) < MathUtil.ZeroTolerance )
				return Vi;
			return vSum / wSum;
		}


		// from http://www.geometry.caltech.edu/pubs/DMSB_III.pdf
		public static double VoronoiArea(DMesh3 mesh, int v_i)
		{
			double areaSum = 0;
			Vector3d Vi = mesh.GetVertex(v_i);

			foreach ( int tid in mesh.VtxTrianglesItr(v_i) ) {
				Index3i t = mesh.GetTriangle(tid);
				int ti = (t[0] == v_i) ? 0 : ( (t[1] == v_i) ? 1 : 2 );
				Vector3d Vj = mesh.GetVertex( t[ (ti+1)%3 ] );
				Vector3d Vk = mesh.GetVertex( t[ (ti+2)%3 ] );

				if ( MathUtil.IsObtuse(Vi, Vj, Vk) ) {
					Vector3d Vij = Vj-Vi;
					Vector3d Vik = Vk-Vi;
					Vij.Normalize(); Vik.Normalize();
					double areaT = 0.5 * Vij.Cross(Vik).Length;
					if ( Vector3d.AngleR(Vij, Vik) > MathUtil.HalfPI )
						areaSum += (areaT * 0.5);   // obtuse at v_i
					else
						areaSum += (areaT * 0.25);	// not obtuse

				} else {

					// voronoi area
					Vector3d Vji = Vi-Vj;
					double dist_ji = Vji.Normalize();
					Vector3d Vki = Vi-Vk;
					double dist_ki = Vki.Normalize();
					Vector3d Vkj = (Vj-Vk).Normalized;

					double cot_alpha_ij = MathUtil.VectorCot(Vki, Vkj);
					double cot_alpha_ik = MathUtil.VectorCot(Vji,-Vkj);
					areaSum += dist_ji * dist_ji * cot_alpha_ij * 0.125;
					areaSum += dist_ki * dist_ki * cot_alpha_ik * 0.125; 
				}
			}
			return areaSum;
		}


		// http://128.148.32.110/courses/cs224/papers/mean_value.pdf
		public static Vector3d MeanValueCentroid(DMesh3 mesh, int v_i)
		{
			Vector3d vSum = Vector3d.Zero;
			double wSum = 0;
			Vector3d Vi = mesh.GetVertex(v_i);

			int v_j = DMesh3.InvalidID, opp_v1 = DMesh3.InvalidID, opp_v2 = DMesh3.InvalidID;
			int t1 = DMesh3.InvalidID, t2 = DMesh3.InvalidID;	
			foreach ( int eid in mesh.VtxEdgesItr(v_i) ) {
				opp_v2 = DMesh3.InvalidID;
				mesh.GetVtxNbrhood(eid, v_i, ref v_j, ref opp_v1, ref opp_v2, ref t1, ref t2);

				Vector3d Vj = mesh.GetVertex(v_j);
				Vector3d vVj = (Vj - Vi);
				double len_vVj = vVj.Normalize();
				// [RMS] is this the right thing to do? if vertices are coincident,
				//   weight of this vertex should be very high!
				if ( len_vVj < MathUtil.ZeroTolerance ) 
					continue;
				Vector3d vVdelta = (mesh.GetVertex(opp_v1) - Vi).Normalized;
				double w_ij = VectorTanHalfAngle(vVj, vVdelta);

				if ( opp_v2 != DMesh3.InvalidID ) {
					Vector3d vVgamma = (mesh.GetVertex(opp_v2) - Vi).Normalized;
					w_ij += VectorTanHalfAngle(vVj, vVgamma);
				}

				w_ij /= len_vVj;

				vSum += w_ij * Vj;
				wSum += w_ij;
			}
            if ( wSum < MathUtil.ZeroTolerance )
                return Vi;
			return vSum / wSum;
		}
		// tan(theta/2) = +/- sqrt( (1-cos(theta)) / (1+cos(theta)) )
		// (in context above we never want negative value!)
		public static double VectorTanHalfAngle(Vector3d a, Vector3d b) {
			double cosAngle = a.Dot(b);
			double sqr = (1-cosAngle) / (1 + cosAngle);
			sqr = MathUtil.Clamp(sqr, 0, Double.MaxValue);
			return Math.Sqrt(sqr);
		}


	}
}
