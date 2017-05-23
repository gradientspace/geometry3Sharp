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

            sumXX *= invNumPoints;
            sumXY *= invNumPoints;
            sumXZ *= invNumPoints;
            sumYY *= invNumPoints;
            sumYZ *= invNumPoints;
            sumZZ *= invNumPoints;

            double[] matrix = new double[] {
                sumXX, sumXY, sumXZ,
                sumXY, sumYY, sumYZ,
                sumXZ, sumYZ, sumZZ
            };

            // Setup the eigensolver.
            SymmetricEigenSolver solver = new SymmetricEigenSolver(3, 4096);
            int iters = solver.Solve(matrix, SymmetricEigenSolver.SortType.Increasing);
            ResultValid = (iters > 0 && iters < SymmetricEigenSolver.NO_CONVERGENCE);

            Box.Extent = new Vector3d(solver.GetEigenvalues());
            double[] evectors = solver.GetEigenvectors();
            Box.AxisX = new Vector3d(evectors[0], evectors[1], evectors[2]);
            Box.AxisY = new Vector3d(evectors[3], evectors[4], evectors[5]);
            Box.AxisZ = new Vector3d(evectors[6], evectors[7], evectors[8]);
        }


    }
}
