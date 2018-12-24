using System;
using System.Collections.Generic;

namespace g3
{
 

    /// <summary>
    /// Formulas for triangle winding number approximation
    /// </summary>
    public static class FastTriWinding
    {
        /// <summary>
        /// precompute constant coefficients of triangle winding number approximation
        /// p: 'center' of expansion for triangles (area-weighted centroid avg)
        /// r: max distance from p to triangles
        /// order1: first-order vector coeff
        /// order2: second-order matrix coeff
        /// triCache: optional precomputed triangle centroid/normal/area
        /// </summary>
        public static void ComputeCoeffs(DMesh3 mesh, IEnumerable<int> triangles, 
            ref Vector3d p, ref double r, 
            ref Vector3d order1, ref Matrix3d order2,
            MeshTriInfoCache triCache = null )
        {
            p = Vector3d.Zero;
            order1 = Vector3d.Zero;
            order2 = Matrix3d.Zero;
            r = 0;

            // compute area-weighted centroid of triangles, we use this as the expansion point
            Vector3d P0 = Vector3d.Zero, P1 = Vector3d.Zero, P2 = Vector3d.Zero;
            double sum_area = 0;
            foreach (int tid in triangles) {
                if (triCache != null) {
                    double area = triCache.Areas[tid];
                    sum_area += area;
                    p += area * triCache.Centroids[tid];
                } else {
                    mesh.GetTriVertices(tid, ref P0, ref P1, ref P2);
                    double area = MathUtil.Area(ref P0, ref P1, ref P2);
                    sum_area += area;
                    p += area * ((P0 + P1 + P2) / 3.0);
                }
            }
            p /= sum_area;

            // compute first and second-order coefficients of FWN taylor expansion, as well as
            // 'radius' value r, which is max dist from any tri vertex to p  
            Vector3d n = Vector3d.Zero, c = Vector3d.Zero; double a = 0;
            foreach ( int tid in triangles ) {
                mesh.GetTriVertices(tid, ref P0, ref P1, ref P2);

                if (triCache == null) {
                    c = (1.0 / 3.0) * (P0 + P1 + P2);
                    n = MathUtil.FastNormalArea(ref P0, ref P1, ref P2, out a);
                } else {
                    triCache.GetTriInfo(tid, ref n, ref a, ref c);
                }

                order1 += a * n;

                Vector3d dcp = c - p;
                order2 += a * new Matrix3d(ref dcp, ref n);

                // this is just for return value...
                double maxdist = MathUtil.Max(P0.DistanceSquared(ref p), P1.DistanceSquared(ref p), P2.DistanceSquared(ref p));
                r = Math.Max(r, Math.Sqrt(maxdist));
            }
        }


        /// <summary>
        /// Evaluate first-order FWN approximation at point q, relative to center c
        /// </summary>
        public static double EvaluateOrder1Approx(ref Vector3d center, ref Vector3d order1Coeff, ref Vector3d q)
        {
            Vector3d dpq = (center - q);
            double len = dpq.Length;

            return (1.0 / MathUtil.FourPI) * order1Coeff.Dot(dpq / (len * len * len));
        }


        /// <summary>
        /// Evaluate second-order FWN approximation at point q, relative to center c
        /// </summary>
        public static double EvaluateOrder2Approx(ref Vector3d center, ref Vector3d order1Coeff, ref Matrix3d order2Coeff, ref Vector3d q)
        {
            Vector3d dpq = (center - q);
            double len = dpq.Length;
            double len3 = len * len * len;
            double fourPi_len3 = 1.0 / (MathUtil.FourPI * len3);

            double order1 = fourPi_len3 * order1Coeff.Dot(ref dpq);

            // second-order hessian \grad^2(G)
            double c = - 3.0 / (MathUtil.FourPI * len3 * len * len);

            // expanded-out version below avoids extra constructors
            //Matrix3d xqxq = new Matrix3d(ref dpq, ref dpq);
            //Matrix3d hessian = new Matrix3d(fourPi_len3, fourPi_len3, fourPi_len3) - c * xqxq;
            Matrix3d hessian = new Matrix3d(
                fourPi_len3 + c*dpq.x*dpq.x, c*dpq.x*dpq.y, c*dpq.x*dpq.z,
                c*dpq.y*dpq.x, fourPi_len3 + c*dpq.y*dpq.y, c*dpq.y*dpq.z,
                c*dpq.z*dpq.x, c*dpq.z*dpq.y, fourPi_len3 + c*dpq.z*dpq.z);

            double order2 = order2Coeff.InnerProduct(ref hessian);

            return order1 + order2;
        }




        // triangle-winding-number first-order approximation. 
        // t is triangle, p is 'center' of cluster of dipoles, q is evaluation point
        // (This is really just for testing)
        public static double Order1Approx(ref Triangle3d t, ref Vector3d p, ref Vector3d xn, ref double xA, ref Vector3d q)
        {
            Vector3d at0 = xA * xn;

            Vector3d dpq = (p - q);
            double len = dpq.Length;
            double len3 = len * len * len;

            return (1.0 / MathUtil.FourPI) * at0.Dot(dpq / (len * len * len));
        }


        // triangle-winding-number second-order approximation
        // t is triangle, p is 'center' of cluster of dipoles, q is evaluation point
        // (This is really just for testing)
        public static double Order2Approx(ref Triangle3d t, ref Vector3d p, ref Vector3d xn, ref double xA, ref Vector3d q)
        {
            Vector3d dpq = (p - q);

            double len = dpq.Length;
            double len3 = len * len * len;

            // first-order approximation - integrated_normal_area * \grad(G)
            double order1 = (xA / MathUtil.FourPI) * xn.Dot(dpq / len3);

            // second-order hessian \grad^2(G)
            Matrix3d xqxq = new Matrix3d(ref dpq, ref dpq);
            xqxq *= 3.0 / (MathUtil.FourPI * len3 * len * len);
            double diag = 1 / (MathUtil.FourPI * len3);
            Matrix3d hessian = new Matrix3d(diag, diag, diag) - xqxq;

            // second-order LHS - integrated second-order area matrix (formula 26)
            Vector3d centroid = new Vector3d(
                (t.V0.x + t.V1.x + t.V2.x) / 3.0, (t.V0.y + t.V1.y + t.V2.y) / 3.0, (t.V0.z + t.V1.z + t.V2.z) / 3.0);
            Vector3d dcp = centroid - p;
            Matrix3d o2_lhs = new Matrix3d(ref dcp, ref xn);
            double order2 = xA * o2_lhs.InnerProduct(ref hessian);

            return order1 + order2;
        }
    }




    /// <summary>
    /// Formulas for point-set winding number approximation
    /// </summary>
    public static class FastPointWinding
    {
        /// <summary>
        /// precompute constant coefficients of point winding number approximation
        /// pointAreas must be provided, and pointSet must have vertex normals!
        /// p: 'center' of expansion for points (area-weighted point avg)
        /// r: max distance from p to points
        /// order1: first-order vector coeff
        /// order2: second-order matrix coeff
        /// </summary>
        public static void ComputeCoeffs(
            IPointSet pointSet, IEnumerable<int> points, double[] pointAreas,
            ref Vector3d p, ref double r,
            ref Vector3d order1, ref Matrix3d order2 )
        {
            if (pointSet.HasVertexNormals == false)
                throw new Exception("FastPointWinding.ComputeCoeffs: point set does not have normals!");

            p = Vector3d.Zero;
            order1 = Vector3d.Zero;
            order2 = Matrix3d.Zero;
            r = 0;

            // compute area-weighted centroid of points, we use this as the expansion point
            double sum_area = 0;
            foreach (int vid in points) {
                sum_area += pointAreas[vid];
                p += pointAreas[vid] * pointSet.GetVertex(vid);
            }
            p /= sum_area;

            // compute first and second-order coefficients of FWN taylor expansion, as well as
            // 'radius' value r, which is max dist from any tri vertex to p  
            foreach (int vid in points) {
                Vector3d p_i = pointSet.GetVertex(vid);
                Vector3d n_i = pointSet.GetVertexNormal(vid);
                double a_i = pointAreas[vid];

                order1 += a_i * n_i;

                Vector3d dcp = p_i - p;
                order2 += a_i * new Matrix3d(ref dcp, ref n_i);

                // this is just for return value...
                r = Math.Max(r, p_i.Distance(p));
            }
        }


        /// <summary>
        /// Evaluate first-order FWN approximation at point q, relative to center c
        /// </summary>
        public static double EvaluateOrder1Approx(ref Vector3d center, ref Vector3d order1Coeff, ref Vector3d q)
        {
            Vector3d dpq = (center - q);
            double len = dpq.Length;

            return (1.0 / MathUtil.FourPI) * order1Coeff.Dot(dpq / (len * len * len));
        }



        /// <summary>
        /// Evaluate second-order FWN approximation at point q, relative to center c
        /// </summary>
        public static double EvaluateOrder2Approx(ref Vector3d center, ref Vector3d order1Coeff, ref Matrix3d order2Coeff, ref Vector3d q)
        {
            Vector3d dpq = (center - q);
            double len = dpq.Length;
            double len3 = len * len * len;
            double fourPi_len3 = 1.0 / (MathUtil.FourPI * len3);

            double order1 = fourPi_len3 * order1Coeff.Dot(ref dpq);

            // second-order hessian \grad^2(G)
            double c = -3.0 / (MathUtil.FourPI * len3 * len * len);

            // expanded-out version below avoids extra constructors
            //Matrix3d xqxq = new Matrix3d(ref dpq, ref dpq);
            //Matrix3d hessian = new Matrix3d(fourPi_len3, fourPi_len3, fourPi_len3) - c * xqxq;
            Matrix3d hessian = new Matrix3d(
                fourPi_len3 + c * dpq.x * dpq.x, c * dpq.x * dpq.y, c * dpq.x * dpq.z,
                c * dpq.y * dpq.x, fourPi_len3 + c * dpq.y * dpq.y, c * dpq.y * dpq.z,
                c * dpq.z * dpq.x, c * dpq.z * dpq.y, fourPi_len3 + c * dpq.z * dpq.z);

            double order2 = order2Coeff.InnerProduct(ref hessian);

            return order1 + order2;
        }



        public static double ExactEval(ref Vector3d x, ref Vector3d xn, double xA, ref Vector3d q)
        {
            Vector3d dv = (x - q);
            double len = dv.Length;
            return (xA / MathUtil.FourPI) * xn.Dot(dv / (len * len * len));
        }

        // point-winding-number first-order approximation. 
        // x is dipole point, p is 'center' of cluster of dipoles, q is evaluation point
        public static double Order1Approx(ref Vector3d x, ref Vector3d p, ref Vector3d xn, double xA, ref Vector3d q)
        {
            Vector3d dpq = (p - q);
            double len = dpq.Length;
            double len3 = len * len * len;

            return (xA / MathUtil.FourPI) * xn.Dot(dpq / (len * len * len));
        }


        // point-winding-number second-order approximation
        // x is dipole point, p is 'center' of cluster of dipoles, q is evaluation point
        public static double Order2Approx(ref Vector3d x, ref Vector3d p, ref Vector3d xn, double xA, ref Vector3d q)
        {
            Vector3d dpq = (p - q);
            Vector3d dxp = (x - p);

            double len = dpq.Length;
            double len3 = len * len * len;

            // first-order approximation - area*normal*\grad(G)
            double order1 = (xA / MathUtil.FourPI) * xn.Dot(dpq / len3);

            // second-order hessian \grad^2(G)
            Matrix3d xqxq = new Matrix3d(ref dpq, ref dpq);
            xqxq *= 3.0 / (MathUtil.FourPI * len3 * len * len);
            double diag = 1 / (MathUtil.FourPI * len3);
            Matrix3d hessian = new Matrix3d(diag, diag, diag) - xqxq;

            // second-order LHS area * \outer(x-p, normal)
            Matrix3d o2_lhs = new Matrix3d(ref dxp, ref xn);
            double order2 = xA * o2_lhs.InnerProduct(ref hessian);

            return order1 + order2;
        }
    }


}
