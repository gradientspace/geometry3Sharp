using System;

namespace g3
{
    // ported from WildMagic5 Curve2/SingleCurve2
    // Abstract base class for 2D parametric curves
    public abstract class BaseCurve2
    {

        // Curve parameter is t where tmin <= t <= tmax.
        protected double mTMin, mTMax;

        public BaseCurve2 (double tmin, double tmax) {
            mTMin = tmin;
            mTMax = tmax;
        }

        // Interval on which curve parameter is defined.  If you are interested
        // in only a subinterval of the actual domain of the curve, you may set
        // that subinterval with SetTimeInterval.  This function requires that
        // tmin < tmax.
        public double GetMinTime () {
            return mTMax;
        }
        public double GetMaxTime () {
            return mTMax;
        }
        public void SetTimeInterval (double tmin, double tmax) {
            if (tmin >= tmax)
                throw new Exception("Curve2.SetTimeInterval: invalid min/max");
            mTMin = tmin;
            mTMax = tmax;
        }

        // Position and derivatives.
        abstract public Vector2d GetPosition (double t);
        abstract public Vector2d GetFirstDerivative (double t);
        abstract public Vector2d GetSecondDerivative (double t);
        abstract public Vector2d GetThirdDerivative (double t);

        // Differential geometric quantities.
        public double GetSpeed (double t) {
			Vector2d d1 = GetFirstDerivative(t);
			return d1.Length;
        }


        double GetSpeedWithData(double t, object data) {
            return (data as BaseCurve2).GetSpeed(t);
        }

        virtual public double GetLength (double t0, double t1)
        {
            if (t0 < mTMin || t0 > mTMax)
                throw new Exception("BaseCurve2.GetLength: min t out of bounds: " + t0);
            if (t1 < mTMin || t1 > mTMax)
                throw new Exception("BaseCurve2.GetLength: max t out of bounds: " + t1);
            if (t0 > t1)
                throw new Exception("BaseCurve2.GetLength: inverted t-range\n " + t0.ToString() + " " + t1.ToString());
            return Integrate1d.RombergIntegral(8, t0, t1, GetSpeedWithData, this);
        }


        public double GetTotalLength ()
        {
            return GetLength(mTMin, mTMax);
        }

        public Vector2d GetTangent (double t)
        {
            return GetFirstDerivative(t).Normalized;
        }
        public Vector2d GetNormal (double t)
        {
            return GetFirstDerivative(t).Normalized.Perp;
        }
        public void GetFrame (double t, ref Vector2d position, ref Vector2d tangent, ref Vector2d normal)
        {
            position = GetPosition(t);
            tangent = GetFirstDerivative(t).Normalized;
            normal = tangent.Perp;
        }
        public double GetCurvature (double t)
        {
            Vector2d der1 = GetFirstDerivative(t);
            Vector2d der2 = GetSecondDerivative(t);
            double speedSqr = der1.LengthSquared;

            if (speedSqr >= MathUtil.ZeroTolerance) {
                double numer = der1.DotPerp(der2);
                double denom = Math.Pow(speedSqr, (double)1.5);
                return numer/denom;
            } else {
                // Curvature is indeterminate, just return 0.
                return (double)0;
            }
        }

        // Inverse mapping of s = Length(t) given by t = Length^{-1}(s).
        virtual public double GetTime (double length, int iterations = 32, double tolerance = (double)1e-06)
        {
            if (length <= 0)
                return mTMin;
            if (length >= GetTotalLength())
                return mTMax;

            // If L(t) is the length function for t in [tmin,tmax], the derivative is
            // L'(t) = |x'(t)| >= 0 (the magnitude of speed).  Therefore, L(t) is a
            // nondecreasing function (and it is assumed that x'(t) is zero only at
            // isolated points; that is, no degenerate curves allowed).  The second
            // derivative is L"(t).  If L"(t) >= 0 for all t, L(t) is a convex
            // function and Newton's method for root finding is guaranteed to
            // converge.  However, L"(t) can be negative, which can lead to Newton
            // iterates outside the domain [tmin,tmax].  The algorithm here avoids
            // this problem by using a hybrid of Newton's method and bisection.

            // Initial guess for Newton's method.
            double ratio = length / GetTotalLength();
            double oneMinusRatio = 1 - ratio;
            double t = oneMinusRatio * mTMin + ratio * mTMax;

            // Initial root-bounding interval for bisection.
            double lower = mTMin, upper = mTMax;

            for (int i = 0; i < iterations; ++i) {
                double difference = GetLength(mTMin, t) - length;
                if ( Math.Abs(difference) < tolerance) {
                    // |L(t)-length| is close enough to zero, report t as the time
                    // at which 'length' is attained.
                    return t;
                }

                // Generate a candidate for Newton's method.
                double tCandidate = t - difference / GetSpeed(t);

                // Update the root-bounding interval and test for containment of the
                // candidate.
                if (difference > 0) {
                    upper = t;
                    if (tCandidate <= lower) {
                        // Candidate is outside the root-bounding interval.  Use
                        // bisection instead.
                        t = (0.5) * (upper + lower);
                    } else {
                        // There is no need to compare to 'upper' because the tangent
                        // line has positive slope, guaranteeing that the t-axis
                        // intercept is smaller than 'upper'.
                        t = tCandidate;
                    }
                } else {
                    lower = t;
                    if (tCandidate >= upper) {
                        // Candidate is outside the root-bounding interval.  Use
                        // bisection instead.
                        t = (0.5) * (upper + lower);
                    } else {
                        // There is no need to compare to 'lower' because the tangent
                        // line has positive slope, guaranteeing that the t-axis
                        // intercept is larger than 'lower'.
                        t = tCandidate;
                    }
                }
            }

            // A root was not found according to the specified number of iterations
            // and tolerance.  You might want to increase iterations or tolerance or
            // integration accuracy.  However, in this application it is likely that
            // the time values are oscillating, due to the limited numerical
            // precision of 32-bit floats.  It is safe to use the last computed time.
            return t;
        }

        // Subdivision.
        Vector2d[] SubdivideByTime (int numPoints)
        {
            if (numPoints < 2)
                throw new Exception("BaseCurve2.SubdivideByTime: Subdivision requires at least two points, requested " + numPoints);
            Vector2d[] points = new Vector2d[numPoints];
            double delta = (mTMax - mTMin)/(numPoints - 1);
            for (int i = 0; i < numPoints; ++i) {
                double t = mTMin + delta*i;
                points[i] = GetPosition(t);
            }
            return points;
        }
        Vector2d[] SubdivieByLength (int numPoints)
        {
            if (numPoints < 2)
                throw new Exception("BaseCurve2.SubdivideByTime: Subdivision requires at least two points, requested " + numPoints);
            Vector2d[] points = new Vector2d[numPoints];
            double delta = GetTotalLength()/(numPoints - 1);
            for (int i = 0; i < numPoints; ++i) {
                double length = delta*i;
                double t = GetTime(length);
                points[i] = GetPosition(t);
            }
            return points;
        }

    }
}
