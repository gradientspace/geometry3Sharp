using System;
using System.Collections.Generic;


namespace g3
{

    public static class MathUtil
    {

        public const double Deg2Rad = (Math.PI / 180.0);
        public const double Rad2Deg = (180.0 / Math.PI);
        public const double TwoPI = 2.0 * Math.PI;
        public const double FourPI = 4.0 * Math.PI;
        public const double HalfPI = 0.5 * Math.PI;
        public const double ZeroTolerance = 1e-08;
        public const double Epsilon = 2.2204460492503131e-016;
        public const double SqrtTwo = 1.41421356237309504880168872420969807;
        public const double SqrtTwoInv = 1.0 / SqrtTwo;
        public const double SqrtThree = 1.73205080756887729352744634150587236;

        public const float Deg2Radf = (float)(Math.PI / 180.0);
        public const float Rad2Degf = (float)(180.0 / Math.PI);
        public const float PIf = (float)(Math.PI);
        public const float TwoPIf = 2.0f * PIf;
        public const float HalfPIf = 0.5f * PIf;
        public const float SqrtTwof = 1.41421356237f;

        public const float ZeroTolerancef = 1e-06f;
        public const float Epsilonf = 1.192092896e-07F;


        public static bool IsFinite(double d) {
            return double.IsInfinity(d) == false && double.IsNaN(d) == false;
        }
        public static bool IsFinite(float d) {
            return float.IsInfinity(d) == false && float.IsNaN(d) == false;
        }


        public static bool EpsilonEqual(double a, double b, double epsilon = MathUtil.Epsilon) {
            return Math.Abs(a - b) <= epsilon;
        }
        public static bool EpsilonEqual(float a, float b, float epsilon = MathUtil.Epsilonf) {
            return (float)Math.Abs(a - b) <= epsilon;
        }

        // ugh C# generics so limiting...
        public static T Clamp<T>(T f, T low, T high) where T : IComparable
        {
            if (f.CompareTo(low) < 0) return low;
            else if (f.CompareTo(high) > 0) return high;
            else return f;
        }
        public static float Clamp(float f, float low, float high) {
            return (f < low) ? low : (f > high) ? high : f;
        }
        public static double Clamp(double f, double low, double high) {
            return (f < low) ? low : (f > high) ? high : f;
        }
        public static int Clamp(int f, int low, int high) {
            return (f < low) ? low : (f > high) ? high : f;
        }

        public static int ModuloClamp(int f, int N) {
            while (f < 0)
                f += N;
            return f % N;
        }

        // fMinMaxValue may be signed
        public static float RangeClamp(float fValue, float fMinMaxValue)
        {
            return Clamp(fValue, -Math.Abs(fMinMaxValue), Math.Abs(fMinMaxValue));
        }
        public static double RangeClamp(double fValue, double fMinMaxValue)
        {
            return Clamp(fValue, -Math.Abs(fMinMaxValue), Math.Abs(fMinMaxValue));
        }


        public static float SignedClamp(float f, float fMax) {
            return Clamp(Math.Abs(f), 0, fMax) * Math.Sign(f);
        }
        public static double SignedClamp(double f, double fMax) {
            return Clamp(Math.Abs(f), 0, fMax) * Math.Sign(f);
        }

        public static float SignedClamp(float f, float fMin, float fMax) {
            return Clamp(Math.Abs(f), fMin, fMax) * Math.Sign(f);
        }
        public static double SignedClamp(double f, double fMin, double fMax) {
            return Clamp(Math.Abs(f), fMin, fMax) * Math.Sign(f);
        }


        public static bool InRange(float f, float low, float high) {
            return f >= low && f <= high;
        }
        public static bool InRange(double f, double low, double high) {
            return f >= low && f <= high;
        }
        public static bool InRange(int f, int low, int high) {
            return f >= low && f <= high;
        }


        // clamps theta to angle interval [min,max]. should work for any theta,
        // regardless of cycles, however min & max values should be in range
        // [-360,360] and min < max
        public static double ClampAngleDeg(double theta, double min, double max)
        {
            // convert interval to center/extent - [c-e,c+e]
            double c = (min+max)*0.5;
            double e = max-c;

            // get rid of extra rotations
            theta = theta % 360;

            // shift to origin, then convert theta to +- 180
            theta -= c;
            if ( theta < -180 )
                theta += 360;
            else if ( theta > 180 )
                theta -= 360;

            // clamp to extent
            if ( theta < -e )
                theta = -e;
            else if ( theta > e )
                theta = e;

            // shift back
            return theta + c;
        }



         // clamps theta to angle interval [min,max]. should work for any theta,
        // regardless of cycles, however min & max values should be in range
        // [-2_PI,2_PI] and min < max
        public static double ClampAngleRad(double theta, double min, double max)
        {
            // convert interval to center/extent - [c-e,c+e]
            double c = (min+max)*0.5;
            double e = max-c;

            // get rid of extra rotations
            theta = theta % TwoPI;

            // shift to origin, then convert theta to +- 180
            theta -= c;
            if ( theta < -Math.PI )
                theta += TwoPI;
            else if ( theta > Math.PI )
                theta -= TwoPI;

            // clamp to extent
            if ( theta < -e )
                theta = -e;
            else if ( theta > e )
                theta = e;

            // shift back
            return theta + c;
        }



        // for ((i++) % N)-type loops, but where we might be using (i--)
        public static int WrapSignedIndex(int val, int mod)
        {
            while (val < 0)
                val += mod;
            return val % mod;
        }


        // compute min and max of a,b,c with max 3 comparisons (sometimes 2)
        public static void MinMax(double a, double b, double c, out double min, out double max)
        {
            if ( a < b ) {
                if ( a < c ) {
                    min = a; max = Math.Max(b, c);
                } else {
                    min = c; max = b;
                }
            } else {
                if ( a > c ) {
                    max = a; min = Math.Min(b, c);
                } else {
                    min = b; max = c;
                }
            }
        }


        public static double Min(double a, double b, double c) {
            return Math.Min(a, Math.Min(b, c));
        }
        public static float Min(float a, float b, float c) {
            return Math.Min(a, Math.Min(b, c));
        }
        public static int Min(int a, int b, int c) {
            return Math.Min(a, Math.Min(b, c));
        }
        public static double Max(double a, double b, double c) {
            return Math.Max(a, Math.Max(b, c));
        }
        public static float Max(float a, float b, float c) {
            return Math.Max(a, Math.Max(b, c));
        }
        public static int Max(int a, int b, int c) {
            return Math.Max(a, Math.Max(b, c));
        }



        // there are fast approximations to this...
        public static double InvSqrt(double f)
        {
            return f / Math.Sqrt(f);
        }


        // normal Atan2 returns in range [-pi,pi], this shifts to [0,2pi]
        public static double Atan2Positive(double y, double x)
        {
            double theta = Math.Atan2(y, x);
            if (theta < 0)
                theta = (2 * Math.PI) + theta;
            return theta;
        }


        public static float PlaneAngleD(Vector3f a, Vector3f b, int nPlaneNormalIdx = 1)
        {
            a[nPlaneNormalIdx] = b[nPlaneNormalIdx] = 0.0f;
            a.Normalize();
            b.Normalize();
            return Vector3f.AngleD(a, b);
        }
        public static double PlaneAngleD(Vector3d a, Vector3d b, int nPlaneNormalIdx = 1)
        {
            a[nPlaneNormalIdx] = b[nPlaneNormalIdx] = 0.0;
            a.Normalize();
            b.Normalize();
            return Vector3d.AngleD(a, b);
        }


        public static float PlaneAngleSignedD(Vector3f vFrom, Vector3f vTo, int nPlaneNormalIdx = 1)
        {
            vFrom[nPlaneNormalIdx] = vTo[nPlaneNormalIdx] = 0.0f;
            vFrom.Normalize();
            vTo.Normalize();
            Vector3f c = vFrom.Cross(vTo);
            if (c.LengthSquared < MathUtil.ZeroTolerancef) {        // vectors are parallel
                return vFrom.Dot(vTo) < 0 ? 180.0f : 0;
            }
            float fSign = Math.Sign(c[nPlaneNormalIdx]);
            float fAngle = fSign * Vector3f.AngleD(vFrom, vTo);
            return fAngle;
        }
        public static double PlaneAngleSignedD(Vector3d vFrom, Vector3d vTo, int nPlaneNormalIdx = 1)
        {
            vFrom[nPlaneNormalIdx] = vTo[nPlaneNormalIdx] = 0.0;
            vFrom.Normalize();
            vTo.Normalize();
            Vector3d c = vFrom.Cross(vTo);
            if (c.LengthSquared < MathUtil.ZeroTolerance) {        // vectors are parallel
                return vFrom.Dot(vTo) < 0 ? 180.0 : 0;
            }
            double fSign = Math.Sign(c[nPlaneNormalIdx]);
            double fAngle = fSign * Vector3d.AngleD(vFrom, vTo);
            return fAngle;
        }

        public static float PlaneAngleSignedD(Vector3f vFrom, Vector3f vTo, Vector3f planeN)
        {
            vFrom = vFrom - Vector3f.Dot(vFrom, planeN) * planeN;
            vTo = vTo - Vector3f.Dot(vTo, planeN) * planeN;
            vFrom.Normalize();
            vTo.Normalize();
            Vector3f c = Vector3f.Cross(vFrom, vTo);
            if (c.LengthSquared < MathUtil.ZeroTolerancef) {        // vectors are parallel
                return vFrom.Dot(vTo) < 0 ? 180.0f : 0;
            }
            float fSign = Math.Sign(Vector3f.Dot(c, planeN));
            float fAngle = fSign * Vector3f.AngleD(vFrom, vTo);
            return fAngle;
        }
        public static double PlaneAngleSignedD(Vector3d vFrom, Vector3d vTo, Vector3d planeN)
        {
            vFrom = vFrom - Vector3d.Dot(vFrom, planeN) * planeN;
            vTo = vTo - Vector3d.Dot(vTo, planeN) * planeN;
            vFrom.Normalize();
            vTo.Normalize();
            Vector3d c = Vector3d.Cross(vFrom, vTo);
            if (c.LengthSquared < MathUtil.ZeroTolerance) {        // vectors are parallel
                return vFrom.Dot(vTo) < 0 ? 180.0 : 0;
            }
            double fSign = Math.Sign(Vector3d.Dot(c, planeN));
            double fAngle = fSign * Vector3d.AngleD(vFrom, vTo);
            return fAngle;
        }


        public static float PlaneAngleSignedD(Vector2f vFrom, Vector2f vTo)
        {
            vFrom.Normalize();
            vTo.Normalize();
            float fSign = Math.Sign(vFrom.Cross(vTo));
            float fAngle = fSign * Vector2f.AngleD(vFrom, vTo);
            return fAngle;
        }
        public static double PlaneAngleSignedD(Vector2d vFrom, Vector2d vTo)
        {
            vFrom.Normalize();
            vTo.Normalize();
            double fSign = Math.Sign(vFrom.Cross(vTo));
            double fAngle = fSign * Vector2d.AngleD(vFrom, vTo);
            return fAngle;
        }



        public static int MostParallelAxis(Frame3f f, Vector3f vDir) {
            double dot0 = Math.Abs(f.X.Dot(vDir));
            double dot1 = Math.Abs(f.Y.Dot(vDir));
            double dot2 = Math.Abs(f.Z.Dot(vDir));
            double m = Math.Max(dot0, Math.Max(dot1, dot2));
            return (m == dot0) ? 0 : (m == dot1) ? 1 : 2;
        }



        public static float Lerp(float a, float b, float t) {
            return (1.0f - t) * a + (t) * b;
        }
        public static double Lerp(double a, double b, double t) {
            return (1.0 - t) * a + (t) * b;
        }

        public static float SmoothStep(float a, float b, float t) {
            t = t * t * (3.0f - 2.0f * t);
            return (1.0f - t) * a + (t) * b;
        }
        public static double SmoothStep(double a, double b, double t) {
            t = t * t * (3.0 - 2.0 * t);
            return (1.0-t) * a + (t) * b;
        }


        public static float SmoothInterp(float a, float b, float t) {
            float tt = WyvillRise01(t);
            return (1.0f - tt) * a + (tt) * b;
        }
        public static double SmoothInterp(double a, double b, double t) {
            double tt = WyvillRise01(t);
            return (1.0 - tt) * a + (tt) * b;
        }

        //! if yshift is 0, function approaches y=1 at xZero from y=0. 
        //! speed (> 0) controls how fast it gets there
        //! yshift pushes the whole graph upwards (so that it actually crosses y=1 at some point)
        public static float SmoothRise0To1(float fX, float yshift, float xZero, float speed)
        {
            double denom = Math.Pow((fX - (xZero - 1)), speed);
            float fY = (float)((1 + yshift) + (1 / -denom));
            return Clamp(fY, 0, 1);
        }

        public static float WyvillRise01(float fX) {
            float d = MathUtil.Clamp(1.0f - fX*fX, 0.0f, 1.0f);
            return 1 - (d * d * d);
        }
        public static double WyvillRise01(double fX) {
            double d = MathUtil.Clamp(1.0 - fX*fX, 0.0, 1.0);
            return 1 - (d * d * d);
        }

        public static float WyvillFalloff01(float fX) {
            float d = 1 - fX * fX;
            return (d >= 0) ? (d * d * d) : 0;
        }
        public static double WyvillFalloff01(double fX) {
            double d = 1 - fX * fX;
            return (d >= 0) ? (d * d * d) : 0;
        }


        public static float WyvillFalloff(float fD, float fInnerRad, float fOuterRad)
        {
            if (fD > fOuterRad) {
                return 0;
            } else if (fD > fInnerRad) {
                fD -= fInnerRad;
                fD /= (fOuterRad - fInnerRad);
                fD = Math.Max(0, Math.Min(1, fD));
                float fVal = (1.0f - fD * fD);
                return fVal * fVal * fVal;
            } else
                return 1.0f;
        }
        public static double WyvillFalloff(double fD, double fInnerRad, double fOuterRad)
        {
            if (fD > fOuterRad) {
                return 0;
            } else if (fD > fInnerRad) {
                fD -= fInnerRad;
                fD /= (fOuterRad - fInnerRad);
                fD = Math.Max(0, Math.Min(1, fD));
                double fVal = (1.0f - fD * fD);
                return fVal * fVal * fVal;
            } else
                return 1.0;
        }



        // lerps from [0,1] for x in range [deadzone,R]
        public static float LinearRampT(float R, float deadzoneR, float x)
        {
            float sign = Math.Sign(x);
            x = Math.Abs(x);
            if (x < deadzoneR)
                return 0.0f;
            else if (x > R)
                return sign * 1.0f;
            else {
                x = Math.Min(x, R);
                float d = (x - deadzoneR) / (R - deadzoneR);
                return sign * d;
            }
        }



		public static double Area(ref Vector3d v1, ref Vector3d v2, ref Vector3d v3) {
			return 0.5 * (v2 - v1).Cross(v3 - v1).Length;
		}
        public static double Area(Vector3d v1, Vector3d v2, Vector3d v3) {
            return 0.5 * (v2 - v1).Cross(v3 - v1).Length;
        }


        public static Vector3d Normal(ref Vector3d v1, ref Vector3d v2, ref Vector3d v3) {
            Vector3d edge1 = v2 - v1;
            Vector3d edge2 = v3 - v2;
            edge1.Normalize();
            edge2.Normalize();
            Vector3d vCross = edge1.Cross(edge2);
            vCross.Normalize();
            return vCross;
        }
        public static Vector3d Normal(Vector3d v1, Vector3d v2, Vector3d v3) {
            return Normal(ref v1, ref v2, ref v3);
        }


		/// <summary>
		/// compute vector in direction of triangle normal (cross-product). No normalization.
		/// </summary>
		/// <returns>The normal direction.</returns>
		public static Vector3d FastNormalDirection(ref Vector3d v1, ref Vector3d v2, ref Vector3d v3)
		{
			Vector3d edge1 = v2 - v1;
			Vector3d edge2 = v3 - v1;
			return edge1.Cross(edge2);
		}


		/// <summary>
		/// simultaneously compute triangle normal and area, and only normalize after
		/// cross-product, not before (so, fewer normalizes then Normal())
		/// </summary>
		public static Vector3d FastNormalArea(ref Vector3d v1, ref Vector3d v2, ref Vector3d v3, out double area)
		{
			Vector3d edge1 = v2 - v1;
			Vector3d edge2 = v3 - v1;
			Vector3d vCross = edge1.Cross(edge2);
			area = 0.5 * vCross.Normalize();
			return vCross;
		}


        /// <summary>
        /// aspect ratio of triangle 
        /// </summary>
        public static double AspectRatio(ref Vector3d v1, ref Vector3d v2, ref Vector3d v3)
        {
            double a = v1.Distance(ref v2), b = v2.Distance(ref v3), c = v3.Distance(ref v1);
            double s = (a + b + c) / 2.0;
            return (a * b * c) / (8.0 * (s - a) * (s - b) * (s - c));
        }
        public static double AspectRatio(Vector3d v1, Vector3d v2, Vector3d v3) {
            return AspectRatio(ref v1, ref v2, ref v3);
        }


		//! fast cotangent between two normalized vectors 
		//! cot = cos/sin, both of which can be computed from vector identities
		//! returns zero if result would be unstable (eg infinity)
		// formula from http://www.geometry.caltech.edu/pubs/DMSB_III.pdf
		public static double VectorCot( Vector3d v1, Vector3d v2 )
		{
			double fDot = v1.Dot(v2);
			double lensqr1 = v1.LengthSquared;
			double lensqr2 = v2.LengthSquared;
			double d = MathUtil.Clamp(lensqr1 * lensqr2 - fDot*fDot, 0.0f, Double.MaxValue);
			if ( d < MathUtil.ZeroTolerance )
				return 0;
			else
				return fDot / Math.Sqrt( d );
		}

		public static double VectorTan( Vector3d v1, Vector3d v2 )
		{
			double fDot = v1.Dot(v2);
			double lensqr1 = v1.LengthSquared;
			double lensqr2 = v2.LengthSquared;
			double d = MathUtil.Clamp(lensqr1 * lensqr2 - fDot*fDot, 0.0f, Double.MaxValue);
			if ( d == 0 )
				return 0;
			return Math.Sqrt(d) / fDot;
		}


		public static bool IsObtuse(Vector3d v1, Vector3d v2, Vector3d v3) {
			double a2 = v1.DistanceSquared(v2);
			double b2 = v1.DistanceSquared(v3);
			double c2 = v2.DistanceSquared(v3);
			return (a2+b2 < c2) || (b2+c2 < a2) || (c2+a2 < b2);
		}


		// code adapted from http://softsurfer.com/Archive/algorithm_0103/algorithm_0103.htm
		//    Return: >0 for P2 left of the line through P0 and P1
		//            =0 for P2 on the line
		//            <0 for P2 right of the line
		public static double IsLeft( Vector2d P0, Vector2d P1, Vector2d P2 )
		{
			return Math.Sign( ( (P1.x - P0.x) * (P2.y - P0.y) - (P2.x - P0.x) * (P1.y - P0.y) ) );
		}
		public static double IsLeft(ref Vector2d P0, ref Vector2d P1, ref Vector2d P2)
		{
			return Math.Sign(((P1.x - P0.x) * (P2.y - P0.y) - (P2.x - P0.x) * (P1.y - P0.y)));
		}


        /// <summary>
        /// Compute barycentric coordinates/weights of vPoint inside triangle (V0,V1,V2). 
        /// If point is in triangle plane and inside triangle, coords will be positive and sum to 1.
        /// ie if result is a, then vPoint = a.x*V0 + a.y*V1 + a.z*V2.
        /// </summary>
        public static Vector3d BarycentricCoords(ref Vector3d vPoint, ref Vector3d V0, ref Vector3d V1, ref Vector3d V2)
        {
            Vector3d kV02 = V0 - V2;
            Vector3d kV12 = V1 - V2;
            Vector3d kPV2 = vPoint - V2;
            double fM00 = kV02.Dot(kV02);
            double fM01 = kV02.Dot(kV12);
            double fM11 = kV12.Dot(kV12);
            double fR0 = kV02.Dot(kPV2);
            double fR1 = kV12.Dot(kPV2);
            double fDet = fM00 * fM11 - fM01 * fM01;
            double fInvDet = 1.0 / fDet;
            double fBary1 = (fM11 * fR0 - fM01 * fR1) * fInvDet;
            double fBary2 = (fM00 * fR1 - fM01 * fR0) * fInvDet;
            double fBary3 = 1.0 - fBary1 - fBary2;
            return new Vector3d(fBary1, fBary2, fBary3);
        }
        public static Vector3d BarycentricCoords(Vector3d vPoint, Vector3d V0, Vector3d V1, Vector3d V2) {
            return BarycentricCoords(ref vPoint, ref V0, ref V1, ref V2);
        }

        /// <summary>
        /// Compute barycentric coordinates/weights of vPoint inside triangle (V0,V1,V2). 
        /// If point is inside triangle, coords will pe positive and sum to 1.
        /// ie if result is a, then vPoint = a.x*V0 + a.y*V1 + a.z*V2.
        /// </summary>
        public static Vector3d BarycentricCoords(Vector2d vPoint, Vector2d V0, Vector2d V1, Vector2d V2)
        {
            Vector2d kV02 = V0 - V2;
            Vector2d kV12 = V1 - V2;
            Vector2d kPV2 = vPoint - V2;
            double fM00 = kV02.Dot(kV02);
            double fM01 = kV02.Dot(kV12);
            double fM11 = kV12.Dot(kV12);
            double fR0 = kV02.Dot(kPV2);
            double fR1 = kV12.Dot(kPV2);
            double fDet = fM00 * fM11 - fM01 * fM01;
            double fInvDet = 1.0 / fDet;
            double fBary1 = (fM11 * fR0 - fM01 * fR1) * fInvDet;
            double fBary2 = (fM00 * fR1 - fM01 * fR0) * fInvDet;
            double fBary3 = 1.0 - fBary1 - fBary2;
            return new Vector3d(fBary1, fBary2, fBary3);
        }


        /// <summary>
        /// signed winding angle of oriented triangle [a,b,c] wrt point p
        /// formula from Jacobson et al 13 http://igl.ethz.ch/projects/winding-number/
        /// </summary>
        public static double TriSolidAngle(Vector3d a, Vector3d b, Vector3d c, ref Vector3d p)
        {
            a -= p; b -= p; c -= p;
            double la = a.Length, lb = b.Length, lc = c.Length;
            double bottom = (la * lb * lc) + a.Dot(ref b) * lc + b.Dot(ref c) * la + c.Dot(ref a) * lb;
            double top = a.x * (b.y * c.z - c.y * b.z) - a.y * (b.x * c.z - c.x * b.z) + a.z * (b.x * c.y - c.x * b.y);
            return 2.0 * Math.Atan2(top, bottom);
        }



        public static bool SolveQuadratic(double a, double b, double c, out double minT, out double maxT)
        {
            minT = maxT = 0;
            if (a == 0 && b == 0)   // function is constant...
                return true;

            double discrim = b*b - 4.0*a*c;
            if (discrim < 0)
                return false;    // no solution

            // a bit odd but numerically better (says NRIC)
            double t = -0.5 * (b + Math.Sign(b)*Math.Sqrt(discrim));  
            minT = t / a;
            maxT = c / t;
            if ( minT > maxT ) {
                a = minT; minT = maxT; maxT = a;   // swap
            }

            return true;
        }




        static readonly int[] powers_of_10 = { 1, 10, 100, 1000, 10000, 100000, 1000000, 10000000, 100000000, 1000000000 };
        public static int PowerOf10(int n) {
            return powers_of_10[n];
        }


        /// <summary>
        /// Iterate from 0 to (nMax-1) using prime-modulo, so we see every index once, but not in-order
        /// </summary>
        public static IEnumerable<int> ModuloIteration(int nMaxExclusive, int nPrime = 31337)
        {
            int i = 0;
            bool done = false;
            while (done == false) {
                yield return i;
                i = (i + nPrime) % nMaxExclusive;
                done = (i == 0);
            }
        }


    }
}
