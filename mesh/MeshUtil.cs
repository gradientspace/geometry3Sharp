using System;
using System.Collections.Generic;

namespace g3 {
	
	public static class MeshUtil {





		// t in range [0,1]
		public static Vector3d UniformSmooth(DMesh3 mesh, int vID, double t) 
		{
			Vector3d v = mesh.GetVertex(vID);
            //Vector3d c = MeshWeights.OneRingCentroid(mesh, vID);
            //return (1 - t) * v + (t) * c;
            Vector3d c = Vector3d.Zero;
            mesh.VtxOneRingCentroid(vID, ref c);
            double s = 1.0 - t;
            v.x = s * v.x + t * c.x;
            v.y = s * v.y + t * c.y;
            v.z = s * v.z + t * c.z;
            return v;
        }

		// t in range [0,1]
		public static Vector3d MeanValueSmooth(DMesh3 mesh, int vID, double t) 
		{
			Vector3d v = mesh.GetVertex(vID);
			Vector3d c = MeshWeights.MeanValueCentroid(mesh, vID);
			return (1-t)*v + (t)*c;
		}

		// t in range [0,1]
		public static Vector3d CotanSmooth(DMesh3 mesh, int vID, double t) 
		{
			Vector3d v = mesh.GetVertex(vID);
			Vector3d c = MeshWeights.CotanCentroid(mesh, vID);
			return (1-t)*v + (t)*c;
		}


		public static void ScaleMesh(DMesh3 mesh, Frame3f f, Vector3f vScale) {
			foreach ( int vid in mesh.VertexIndices() ) {
				Vector3f v = (Vector3f)mesh.GetVertex(vid);
				Vector3f vScaledInF = f.ToFrameP(ref v) * vScale;
				Vector3d vNew = f.FromFrameP(ref vScaledInF);
				mesh.SetVertex(vid, vNew);

				// TODO: normals
			}
		}



        /// <summary>
        /// computes opening angle between the two triangles connected to edge
        /// </summary>
        public static double OpeningAngleD(DMesh3 mesh, int eid)
        {
            Index2i et = mesh.GetEdgeT(eid);
            if (et[1] == DMesh3.InvalidID)
                return double.MaxValue;     // boundary edge!!

            Vector3d n0 = mesh.GetTriNormal(et[0]);
            Vector3d n1 = mesh.GetTriNormal(et[1]);
            return Vector3d.AngleD(n0, n1);
        }


        /// <summary>
        /// computes sum of opening-angles in triangles around vid, minus 2pi.
        /// This is zero on flat areas.
        /// </summary>
        public static double DiscreteGaussCurvature(DMesh3 mesh, int vid)
        {
            double angle_sum = 0;
            foreach (int tid in mesh.VtxTrianglesItr(vid)) {
                Index3i et = mesh.GetTriangle(tid);
                int idx = IndexUtil.find_tri_index(vid, ref et);
                angle_sum += mesh.GetTriInternalAngleR(tid, idx);
            }
            return angle_sum - MathUtil.TwoPI;
        }




        /// <summary>
        /// Check if collapsing edge edgeID to point newv will flip normal of any attached face
        /// </summary>
        public static bool CheckIfCollapseCreatesFlip(DMesh3 mesh, int edgeID, Vector3d newv)
        {
            Index4i edge_info = mesh.GetEdge(edgeID);
            int tc = edge_info.c, td = edge_info.d;

            for (int j = 0; j < 2; ++j) {
                int vid = edge_info[j];
                int vother = edge_info[(j + 1) % 2];

                foreach (int tid in mesh.VtxTrianglesItr(vid)) {
                    if (tid == tc || tid == td)
                        continue;
                    Index3i curt = mesh.GetTriangle(tid);
                    if (curt.a == vother || curt.b == vother || curt.c == vother)
                        return true;        // invalid nbrhood for collapse
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
                    if (sign <= 0.0)
                        return true;
                }
            }
            return false;
        }




        /// <summary>
        /// if before a flip we have normals (n1,n2) and after we have (m1,m2), check if
        /// the dot between any of the 4 pairs changes sign after the flip, or is
        /// less than the dot-product tolerance (ie angle tolerance)
        /// </summary>
        public static bool CheckIfEdgeFlipCreatesFlip(DMesh3 mesh, int eID, double flip_dot_tol = 0.0)
        {
            Util.gDevAssert(mesh.IsBoundaryEdge(eID) == false);
            Index4i einfo = mesh.GetEdge(eID);
            Index2i ov = mesh.GetEdgeOpposingV(eID);

            int a = einfo.a, b = einfo.b, c = ov.a, d = ov.b;
            int t0 = einfo.c;

            Vector3d vC = mesh.GetVertex(c), vD = mesh.GetVertex(d);
            Index3i tri_v = mesh.GetTriangle(t0);
            int oa = a, ob = b;
            IndexUtil.orient_tri_edge(ref oa, ref ob, ref tri_v);
            Vector3d vOA = mesh.GetVertex(oa), vOB = mesh.GetVertex(ob);
            Vector3d n0 = MathUtil.FastNormalDirection(ref vOA, ref vOB, ref vC);
            Vector3d n1 = MathUtil.FastNormalDirection(ref vOB, ref vOA, ref vD);
            Vector3d f0 = MathUtil.FastNormalDirection(ref vC, ref vD, ref vOB);
            if (edge_flip_metric(ref n0, ref f0, flip_dot_tol) <= flip_dot_tol 
                || edge_flip_metric(ref n1, ref f0, flip_dot_tol) <= flip_dot_tol)
                return true;
            Vector3d f1 = MathUtil.FastNormalDirection(ref vD, ref vC, ref vOA);
            if (edge_flip_metric(ref n0, ref f1, flip_dot_tol) <= flip_dot_tol 
                || edge_flip_metric(ref n1, ref f1, flip_dot_tol) <= flip_dot_tol)
                return true;
            return false;
        }
        static double edge_flip_metric(ref Vector3d n0, ref Vector3d n1, double flip_dot_tol) {
            return (flip_dot_tol == 0) ? n0.Dot(n1) : n0.Normalized.Dot(n1.Normalized);
        }



        /// <summary>
        /// For given edge, return it's triangles and the triangles that would
        /// be created if it was flipped (used in edge-flip optimizers)
        /// </summary>
        public static void GetEdgeFlipTris(DMesh3 mesh, int eID,
            out Index3i orig_t0, out Index3i orig_t1,
            out Index3i flip_t0, out Index3i flip_t1)
        {
            Index4i einfo = mesh.GetEdge(eID);
            Index2i ov = mesh.GetEdgeOpposingV(eID);
            int a = einfo.a, b = einfo.b, c = ov.a, d = ov.b;
            int t0 = einfo.c;
            Index3i tri_v = mesh.GetTriangle(t0);
            int oa = a, ob = b;
            IndexUtil.orient_tri_edge(ref oa, ref ob, ref tri_v);
            orig_t0 = new Index3i(oa, ob, c);
            orig_t1 = new Index3i(ob, oa, d);
            flip_t0 = new Index3i(c, d, ob);
            flip_t1 = new Index3i(d, c, oa);
        }


        /// <summary>
        /// For given edge, return normals of it's two triangles, and normals
        /// of the triangles created if edge is flipped (used in edge-flip optimizers)
        /// </summary>
        public static void GetEdgeFlipNormals(DMesh3 mesh, int eID, 
            out Vector3d n1, out Vector3d n2,
            out Vector3d on1, out Vector3d on2)
        {
            Index4i einfo = mesh.GetEdge(eID);
            Index2i ov = mesh.GetEdgeOpposingV(eID);
            int a = einfo.a, b = einfo.b, c = ov.a, d = ov.b;
            int t0 = einfo.c;
            Vector3d vC = mesh.GetVertex(c), vD = mesh.GetVertex(d);
            Index3i tri_v = mesh.GetTriangle(t0);
            int oa = a, ob = b;
            IndexUtil.orient_tri_edge(ref oa, ref ob, ref tri_v);
            Vector3d vOA = mesh.GetVertex(oa), vOB = mesh.GetVertex(ob);
            n1 = MathUtil.Normal(ref vOA, ref vOB, ref vC);
            n2 = MathUtil.Normal(ref vOB, ref vOA, ref vD);
            on1 = MathUtil.Normal(ref vC, ref vD, ref vOB);
            on2 = MathUtil.Normal(ref vD, ref vC, ref vOA);
        }




        public static DCurve3 ExtractLoopV(IMesh mesh, IEnumerable<int> vertices) {
            DCurve3 curve = new DCurve3();
            foreach (int vid in vertices)
                curve.AppendVertex(mesh.GetVertex(vid));
            curve.Closed = true;
            return curve;
        }
        public static DCurve3 ExtractLoopV(IMesh mesh, int[] vertices) {
            DCurve3 curve = new DCurve3();
            for (int i = 0; i < vertices.Length; ++i)
                curve.AppendVertex(mesh.GetVertex(vertices[i]));
            curve.Closed = true;
            return curve;
        }

	}
}
