using System;

namespace g3
{
    // ported from WildMagic5 Ellipse2
    public class Ellipse2d : IParametricCurve2d
    {
        // An ellipse has center K, axis directions U[0] and U[1] (both
        // unit-length vectors), and extents e[0] and e[1] (both positive
        // numbers).  A point X = K+y[0]*U[0]+y[1]*U[1] is on the ellipse whenever
        // (y[0]/e[0])^2+(y[1]/e[1])^2 = 1.  The test for a point inside the
        // ellipse uses "<=" instead of "=" in the previous expression.  An
        // algebraic representation for the ellipse is
        //   1 = (X-K)^T * (U[0]*U[0]^T/e[0]^2 + U[1]*U[1]^T/e[1]^2) * (X-K)
        //     = (X-K)^T * M * (X-K)
        // where the superscript T denotes transpose.  Observe that U[i]*U[i]^T
        // is a matrix, not a scalar dot product.  The matrix M is symmetric.
        // The ellipse is also represented by a quadratic equation
        //   0 = a0 + a1*x[0] + a2*x[1] + a3*x[0]^2 + a4*x[0]*x[1] + a5*x[1]^2
        //     = a0 + [a1 a2]*X + X^T*[a3   a4/2]*X
        //                            [a4/2 a5  ]
        //     = C + B^T*X + X^T*A*X
        // where X = (x[0],x[1]).  This equation can be factored to the form
        // (X-K)^T*M*(X-K) = 1, where K = -A^{-1}*B/2, M = A/(B^T*A^{-1}*B/4-C).
        // To be an ellipse, M must have all positive eigenvalues.


        public Vector2d Center;
        public Vector2d Axis0, Axis1;
        public Vector2d Extent;
        public bool IsReversed;		// use ccw orientation instead of cw


        public Ellipse2d(Vector2d center, Vector2d axis0, Vector2d axis1, Vector2d extent) {
            Center = center;
            Axis0 = axis0;
            Axis1 = axis1;
            Extent.x = extent.x;
            Extent.y = extent.y;
            IsReversed = false;
        }

		public Ellipse2d(Vector2d center, Vector2d axis0, Vector2d axis1, double extent0, double extent1) {
            Center = center;
            Axis0 = axis0;
            Axis1 = axis1;
            Extent.x = extent0;
            Extent.y = extent1;
			IsReversed = false;
        }

		public Ellipse2d(Vector2d center, double rotationAngleDeg, double extent0, double extent1) {
			Center = center;
			Matrix2d m = new Matrix2d(rotationAngleDeg * MathUtil.Deg2Rad);
			Axis0 = m * Vector2d.AxisX;
			Axis1 = m * Vector2d.AxisY;
			Extent = new Vector2d(extent0, extent1);
			IsReversed = false;
		}

        // Compute M = sum_{i=0}^1 U[i]*U[i]^T/e[i]^2.
        public Matrix2d GetM() {
            Vector2d ratio0 = Axis0 / Extent[0];
            Vector2d ratio1 = Axis1 / Extent[1];
            return  new Matrix2d(ratio0, ratio0) + new Matrix2d(ratio1, ratio1);
        }

        // Compute M^{-1} = sum_{i=0}^1 U[i]*U[i]^T*e[i]^2.
        public Matrix2d GetMInverse()
        {
            Vector2d ratio0 = Axis0 * Extent[0];
            Vector2d ratio1 = Axis1 * Extent[1];
            return new Matrix2d(ratio0, ratio0) + new Matrix2d(ratio1, ratio1);
        }

        // construct the coefficients in the quadratic equation that represents
        // the ellipse.  'coeff' stores a0 through a5.  'A', 'B', and 'C' are as
        // described in the comments before the constructors.
        public double[] ToCoefficients()
        {
            Matrix2d A = Matrix2d.Zero;
            Vector2d B = Vector2d.Zero;
            double C = 0;
            ToCoefficients(ref A, ref B, ref C);
            double[] coeff = Convert(A, B, C);

            // Arrange for one of the x0^2 or x1^2 coefficients to be 1.
            double maxValue = Math.Abs(coeff[3]);
            int maxIndex = 3;
            double absValue = Math.Abs(coeff[5]);
            if (absValue > maxValue) {
                maxValue = absValue;
                maxIndex = 5;
            }

            double invMaxValue = ((double)1) / maxValue;
            for (int i = 0; i < 6; ++i) {
                if (i != maxIndex) {
                    coeff[i] *= invMaxValue;
                } else {
                    coeff[i] = (double)1;
                }
            }

            return coeff;
        }

        public void ToCoefficients(ref Matrix2d A, ref Vector2d B, ref double C)
        {
            Vector2d ratio0 = Axis0 / Extent[0];
            Vector2d ratio1 = Axis1 / Extent[1];
            A = new Matrix2d(ratio0, ratio0) + new Matrix2d(ratio1, ratio1);
            B = ((double)-2) * (A * Center);
            C = A.QForm(Center, Center) - (double)1;
        }

        // construct C, U[i], and e[i] from the quadratic equation.  The return
        // value is 'true' if and only if the input coefficients represent an
        // ellipse.  If the function returns 'false', the ellipse data members
        // are undefined.  'coeff' stores a0 through a5.  'A', 'B', and 'C' are as
        // described in the comments before the constructors.
        public bool FromCoefficients(double[] coeff)
        {
            Matrix2d A = Matrix2d.Zero;
            Vector2d B = Vector2d.Zero;
            double C = 0;
            Convert(coeff, ref A, ref B, ref C);
            return FromCoefficients(A, B, C);
        }

        public bool FromCoefficients(Matrix2d A, Vector2d B, double C)
        {
            throw new NotImplementedException("Ellipse2.FromCoefficients: need EigenDecomposition");
/*
            // Compute the center K = -A^{-1}*B/2.
            Matrix2d invA = A.Inverse();
            if (invA == Matrix2d.Zero) {
                return false;
            }

            Center = ((double)-0.5) * (invA * B);

            // Compute B^T*A^{-1}*B/4 - C = K^T*A*K - C = -K^T*B/2 - C.
            double rightSide = -((double)0.5) * (Center.Dot(B)) - C;
            if (Math.Abs(rightSide) < Math < double >::ZERO_TOLERANCE) {
                return false;
            }

            // Compute M = A/(K^T*A*K - C).
            double invRightSide = ((double)1) / rightSide;
            Matrix2d M = invRightSide * A;

            // Factor into M = R*D*R^T.
            EigenDecomposition<double> eigensystem(M);
            eigensystem.Solve(true);
            for (int i = 0; i < 2; ++i) {
                if (eigensystem.GetEigenvalue(i) <= (double)0) {
                    return false;
                }

                Extent[i] = Math < double >::InvSqrt(eigensystem.GetEigenvalue(i));
                Axis[i] = eigensystem.GetEigenvector2(i);
            }

            return true;
*/
        }

        // Evaluate the quadratic function Q(X) = (X-K)^T * M * (X-K) - 1.
        public double Evaluate(Vector2d point)
        {
            Vector2d diff = point - Center;
            double ratio0 = Axis0.Dot(diff) / Extent[0];
            double ratio1 = Axis1.Dot(diff) / Extent[1];
            double value = ratio0 * ratio0 + ratio1 * ratio1 - (double)1;
            return value;
        }


        // Test whether the input point is inside or on the ellipse.  The point
        // is contained when Q(X) <= 0, where Q(X) is the function in the comment
        // before the function Evaluate().
        public bool Contains(Vector2d point)
        {
            return (Evaluate(point) <= (double)0);
        }


        static void Convert(double[] coeff, ref Matrix2d A, ref Vector2d B, ref double C)
        {
            C = coeff[0];
            B.x = coeff[1];
            B.y = coeff[2];
            A.m00 = coeff[3];
            A.m01 = ((double)0.5) * coeff[4];
            A.m10 = A.m01;
            A.m11 = coeff[5];
        }

        static double[] Convert(Matrix2d A, Vector2d B, double C)
        {
            double[] coeff = new double[6];
            coeff[0] = C;
            coeff[1] = B.x;
            coeff[2] = B.y;
            coeff[3] = A.m00;
            coeff[4] = 2.0 * A.m01;
            coeff[5] = A.m11;
            return coeff;
        }





		public bool IsClosed {
			get { return true; }
		}

		public void Reverse() {
			IsReversed = ! IsReversed;
		}

        public IParametricCurve2d Clone() {
            return new Ellipse2d(this.Center, this.Axis0, this.Axis1, this.Extent)
                { IsReversed = this.IsReversed };
        }


        public bool IsTransformable { get { return true; } }
        public void Transform(ITransform2 xform)
        {
            Center = xform.TransformP(Center);
            Axis0 = xform.TransformN(Axis0);
            Axis1 = xform.TransformN(Axis1);
            Extent.x = xform.TransformScalar(Extent.x);
            Extent.y = xform.TransformScalar(Extent.y);
        }



        // angle in range [-2pi,2pi]
        public Vector2d SampleDeg(double degrees)
        {
            double theta = degrees * MathUtil.Deg2Rad;
            double c = Math.Cos(theta), s = Math.Sin(theta);
            return Center + (Extent.x * c * Axis0) + (Extent.y * s * Axis1);
        }

        // angle in range [-2pi,2pi]
        public Vector2d SampleRad(double radians)
        {
            double c = Math.Cos(radians), s = Math.Sin(radians);
            return Center + (Extent.x * c * Axis0) + (Extent.y * s * Axis1);
        }


		public double ParamLength {
			get { return 1.0f; }
		}

		// t in range[0,1] spans ellipse
		public Vector2d SampleT(double t) {
			double theta = (IsReversed) ? -t*MathUtil.TwoPI : t*MathUtil.TwoPI;
			double c = Math.Cos(theta), s = Math.Sin(theta);
            return Center + (Extent.x * c * Axis0) + (Extent.y * s * Axis1);
		}

		// t in range[0,1] spans ellipse
		public Vector2d TangentT(double t) {
			double theta = (IsReversed) ? -t*MathUtil.TwoPI : t*MathUtil.TwoPI;
			double c = Math.Cos(theta), s = Math.Sin(theta);
            Vector2d tangent = -Extent.x * s * Axis0 + Extent.y * c * Axis1;
            if (IsReversed)
                tangent = -tangent;
            tangent.Normalize();
            return tangent;
		}




        // [TODO] could use RombergIntegral like BaseCurve2, but need
        // first-derivative function

		public bool HasArcLength { get {return false;} }

		public double ArcLength {
			get { throw new NotImplementedException("Ellipse2.ArcLength"); }
		}

		public Vector2d SampleArcLength(double a) {
            throw new NotImplementedException("Ellipse2.SampleArcLength");
		}



        public double Area {
            get { return Math.PI * Extent.x * Extent.y; }
        }
		public double ApproxArcLen {
			get {
				// [RMS] from http://mathforum.org/dr.math/faq/formulas/faq.ellipse.html, 
				//   apparently due to Ramanujan
				double a = Math.Max(Extent.x, Extent.y);
				double b = Math.Min(Extent.x, Extent.y);
				double x = (a-b) / (a+b);
				double tx2 = 3*x*x;
				double denom = 10.0 + Math.Sqrt(4-tx2);
				return Math.PI * (a+b) * (1 + tx2 / denom );
			}
		}


    }
}
