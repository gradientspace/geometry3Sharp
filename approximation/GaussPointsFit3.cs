using System;
using System.Collections.Generic;

namespace g3
{
    // ported from WildMagic5 Wm5ApprGaussPointsFit3
    // Fit points with a Gaussian distribution.  The center is the mean of the
    // points, the axes are the eigenvectors of the covariance matrix, and the
    // extents are the eigenvalues of the covariance matrix and are returned in
    // increasing order.  The quantites are stored in a Box3<Real> just to have a
    // single container.
    public class GaussPointsFit3
    {
        public Box3d Box;
        public bool ResultValid = false;

        public GaussPointsFit3(IEnumerable<Vector3d> points)
        {
            Box = new Box3d(Vector3d.Zero, Vector3d.One);

            // Compute the mean of the points.
            int numPoints = 0;
            foreach (Vector3d v in points) {
                Box.Center += v;
                numPoints++;
            }
            double invNumPoints = (1.0) / numPoints;
            Box.Center *= invNumPoints;

            // Compute the covariance matrix of the points.
            double sumXX = (double)0, sumXY = (double)0, sumXZ = (double)0;
            double sumYY = (double)0, sumYZ = (double)0, sumZZ = (double)0;
            foreach (Vector3d p in points) { 
                Vector3d diff = p - Box.Center;
                sumXX += diff[0] * diff[0];
                sumXY += diff[0] * diff[1];
                sumXZ += diff[0] * diff[2];
                sumYY += diff[1] * diff[1];
                sumYZ += diff[1] * diff[2];
                sumZZ += diff[2] * diff[2];
            }

            do_solve(sumXX, sumXY, sumXZ, sumYY, sumYZ, sumZZ, invNumPoints);
        }





        public GaussPointsFit3(IEnumerable<Vector3d> points, IEnumerable<double> weights)
        {
            Box = new Box3d(Vector3d.Zero, Vector3d.One);

            // Compute the mean of the points.
            int numPoints = 0;
            double weightSum = 0;
            IEnumerator<double> weights_itr = weights.GetEnumerator();
            foreach (Vector3d v in points) {
                weights_itr.MoveNext();
                double w = weights_itr.Current;
                Box.Center += w * v;
                weightSum += w;
                numPoints++;
            }
            double invWeightDivide = (1.0) / weightSum;
            Box.Center *= invWeightDivide;

            // Compute the covariance matrix of the points.
            double sumXX = (double)0, sumXY = (double)0, sumXZ = (double)0;
            double sumYY = (double)0, sumYZ = (double)0, sumZZ = (double)0;
            weights_itr = weights.GetEnumerator();
            foreach (Vector3d p in points) {
                weights_itr.MoveNext();
                double w = weights_itr.Current;
                w *= w;
                Vector3d diff = p - Box.Center;
                sumXX += w * diff[0] * diff[0];
                sumXY += w * diff[0] * diff[1];
                sumXZ += w * diff[0] * diff[2];
                sumYY += w * diff[1] * diff[1];
                sumYZ += w * diff[1] * diff[2];
                sumZZ += w * diff[2] * diff[2];
            }

            do_solve(sumXX, sumXY, sumXZ, sumYY, sumYZ, sumZZ, invWeightDivide * invWeightDivide);
        }



        void do_solve(double sumXX, double sumXY, double sumXZ, double sumYY, double sumYZ, double sumZZ, double invSumMultiplier)
        {
            sumXX *= invSumMultiplier;
            sumXY *= invSumMultiplier;
            sumXZ *= invSumMultiplier;
            sumYY *= invSumMultiplier;
            sumYZ *= invSumMultiplier;
            sumZZ *= invSumMultiplier;

            double[] matrix = new double[] {
                sumXX, sumXY, sumXZ,
                sumXY, sumYY, sumYZ,
                sumXZ, sumYZ, sumZZ
            };

            // Setup the eigensolver.
            SymmetricEigenSolver solver = new SymmetricEigenSolver(3, 4096);
            int iters = solver.Solve(matrix, SymmetricEigenSolver.SortType.Increasing);
            ResultValid = (iters > 0 && iters < SymmetricEigenSolver.NO_CONVERGENCE);
            if (ResultValid) {
                Box.Extent = new Vector3d(solver.GetEigenvalues());
                double[] evectors = solver.GetEigenvectors();
                Box.AxisX = new Vector3d(evectors[0], evectors[1], evectors[2]);
                Box.AxisY = new Vector3d(evectors[3], evectors[4], evectors[5]);
                Box.AxisZ = new Vector3d(evectors[6], evectors[7], evectors[8]);
            }
        }


    }
}
