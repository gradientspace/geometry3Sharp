using System;
using System.Collections.Generic;

namespace g3
{
	// ported from WildMagic5 Wm5ApprQuadraticFit2
	public static class QuadraticFit2
	{
		// The quadratic fit is
		//
		//   0 = C[0] + C[1]*X + C[2]*Y + C[3]*X^2 + C[4]*Y^2 + C[5]*X*Y
		//
		// subject to Length(C) = 1.  Minimize E(C) = C^t M C with Length(C) = 1
		// and M = (sum_i V_i)(sum_i V_i)^t where
		//
		//   V = (1, X, Y, X^2, Y^2, X*Y)
		//         
		// The minimum value is the smallest eigenvalue of M and C is a corresponding
		// unit length eigenvector.
		//
		// Input:
		//   n = number of points to fit
		//   p[0..n-1] = array of points to fit
		//
		// Output:
		//   c[0..5] = coefficients of quadratic fit (the eigenvector)
		//   return value of function is nonnegative and a measure of the fit
		//   (the minimum eigenvalue; 0 = exact fit, positive otherwise)

		// Canonical forms.  The quadratic equation can be factored into
		// P^T A P + B^T P + K = 0 where P = (X,Y,Z), K = C[0], B = (C[1],C[2],C[3]),
		// and A is a 3x3 symmetric matrix with A00 = C[4], A11 = C[5], A22 = C[6],
		// A01 = C[7]/2, A02 = C[8]/2, and A12 = C[9]/2.  Matrix A = R^T D R where
		// R is orthogonal and D is diagonal (using an eigendecomposition).  Define
		// V = R P = (v0,v1,v2), E = R B = (e0,e1,e2), D = diag(d0,d1,d2), and f = K
		// to obtain
		//
		//   d0 v0^2 + d1 v1^2 + d2 v^2 + e0 v0 + e1 v1 + e2 v2 + f = 0
		//
		// The characterization depends on the signs of the d_i.
		public static double Fit(Vector2d[] points, double[] coefficients) {
			DenseMatrix A = new DenseMatrix(6, 6);
			int numPoints = points.Length;
			for (int i = 0; i < numPoints; ++i) {
				double x = points[i].x;
				double y = points[i].y;
				double x2 = x * x;
				double y2 = y * y;
				double xy = x * y;
				double x3 = x * x2;
				double xy2 = x * y2;
				double x2y = x * xy;
				double y3 = y * y2;
				double x4 = x * x3;
				double x2y2 = x * xy2;
				double x3y = x * x2y;
				double y4 = y * y3;
				double xy3 = x * y3;

				A[0, 1] += x;
				A[0, 2] += y;
				A[0, 3] += x2;
				A[0, 4] += y2;
				A[0, 5] += xy;
				A[1, 3] += x3;
				A[1, 4] += xy2;
				A[1, 5] += x2y;
				A[2, 4] += y3;
				A[3, 3] += x4;
				A[3, 4] += x2y2;
				A[3, 5] += x3y;
				A[4, 4] += y4;
				A[4, 5] += xy3;
			}

			A[0, 0] = (double)numPoints;
			A[1, 1] = A[0, 3];
			A[1, 2] = A[0, 5];
			A[2, 2] = A[0, 4];
			A[2, 3] = A[1, 5];
			A[2, 5] = A[1, 4];
			A[5, 5] = A[3, 4];

			for (int row = 0; row < 6; ++row) {
				for (int col = 0; col < row; ++col) {
					A[row, col] = A[col, row];
				}
			}

			double invNumPoints = 1.0 / (double)numPoints;
			for (int row = 0; row < 6; ++row) {
				for (int col = 0; col < 6; ++col) {
					A[row, col] *= invNumPoints;
				}
			}

			SymmetricEigenSolver es = new SymmetricEigenSolver(6, 1024);
			es.Solve(A.Buffer, SymmetricEigenSolver.SortType.Increasing);
			es.GetEigenvector(0, coefficients);

			// For an exact fit, numeric round-off errors might make the minimum
			// eigenvalue just slightly negative.  Return the absolute value since
			// the application might rely on the return value being nonnegative.
			return Math.Abs(es.GetEigenvalue(0));
		}



		// If you think your points are nearly circular, use this.  The circle is of
		// the form C'[0]+C'[1]*X+C'[2]*Y+C'[3]*(X^2+Y^2), where Length(C') = 1.  The
		// function returns C = (C'[0]/C'[3],C'[1]/C'[3],C'[2]/C'[3]), so the fitted
		// circle is C[0]+C[1]*X+C[2]*Y+X^2+Y^2.  The center is (xc,yc) =
		// -0.5*(C[1],C[2]) and the radius is r = sqrt(xc*xc+yc*yc-C[0]).
		public static double FitCircle2(Vector2d[] points, out Circle2d circle )
		{
			DenseMatrix A = new DenseMatrix(4, 4);
			int numPoints = points.Length;
			for (int i = 0; i < numPoints; ++i) {
				double x = points[i].x;
				double y = points[i].y;
				double x2 = x * x;
				double y2 = y * y;
				double xy = x * y;
				double r2 = x2 + y2;
				double xr2 = x * r2;
				double yr2 = y * r2;
				double r4 = r2 * r2;

				A[0, 1] += x;
				A[0, 2] += y;
				A[0, 3] += r2;
				A[1, 1] += x2;
				A[1, 2] += xy;
				A[1, 3] += xr2;
				A[2, 2] += y2;
				A[2, 3] += yr2;
				A[3, 3] += r4;
			}

			A[0, 0] = (double)numPoints;

			for (int row = 0; row < 4; ++row) {
				for (int col = 0; col < row; ++col) {
					A[row, col] = A[col, row];
				}
			}

			double invNumPoints = 1.0 / (double)numPoints;
			for (int row = 0; row < 4; ++row) {
				for (int col = 0; col < 4; ++col) {
					A[row, col] *= invNumPoints;
				}
			}

			SymmetricEigenSolver es = new SymmetricEigenSolver(4, 1024);
			es.Solve(A.Buffer, SymmetricEigenSolver.SortType.Increasing);
			double[] evector = new double[4];
			es.GetEigenvector(0, evector);

			double inv = 1.0 / evector[3];  // TODO: Guard against zero divide?
			Vector3d coefficients = Vector3d.Zero;
			for (int row = 0; row < 3; ++row) {
				coefficients[row] = inv * evector[row];
			}

			Vector2d center = new Vector2d(-0.5 * coefficients[1], -0.5 * coefficients[2]);
			double r = Math.Sqrt(Math.Abs(center.LengthSquared - coefficients[0]));
			circle = new Circle2d(center, r);

			// For an exact fit, numeric round-off errors might make the minimum
			// eigenvalue just slightly negative.  Return the absolute value since
			// the application might rely on the return value being nonnegative.
			return Math.Abs(es.GetEigenvalue(0));
		}

	}
}
