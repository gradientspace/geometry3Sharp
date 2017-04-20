using System;


namespace g3
{

    public static class MathUtil
    {

        public const double Deg2Rad = (Math.PI / 180.0);
        public const double Rad2Deg = (180.0 / Math.PI);
        public const double TwoPI = 2.0 * Math.PI;
        public const double HalfPI = 0.5 * Math.PI;
        public const double ZeroTolerance = 1e-08;
        public const double Epsilon = 2.2204460492503131e-016;
        public const double SqrtTwo = 1.41421356237309504880168872420969807;

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
            return Math.Abs(a - b) < epsilon;
        }
        public static bool EpsilonEqual(float a, float b, float epsilon = MathUtil.Epsilonf) {
            return (float)Math.Abs(a - b) < epsilon;
        }

        public static bool PrecisionEqual(double a, double b, int nDigits) {
            return Math.Round(a, nDigits) == Math.Round(b, nDigits);
        }
        public static bool PrecisionEqual(float a, float b, int nDigits) {
            return Math.Round(a, nDigits) == Math.Round(b, nDigits);
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
            float fSign = Math.Sign(vFrom.Cross(vTo)[nPlaneNormalIdx]);
            float fAngle = fSign * Vector3f.AngleD(vFrom, vTo);
            return fAngle;
        }
        public static double PlaneAngleSignedD(Vector3d vFrom, Vector3d vTo, int nPlaneNormalIdx = 1)
        {
            vFrom[nPlaneNormalIdx] = vTo[nPlaneNormalIdx] = 0.0;
            vFrom.Normalize();
            vTo.Normalize();
            double fSign = Math.Sign(vFrom.Cross(vTo)[nPlaneNormalIdx]);
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
            return (1 - t) * a + (t) * b;
        }
        public static double Lerp(double a, double b, double t) {
            return (1 - t) * a + (t) * b;
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
            float d = 1 - fX * fX;
            return (d > 0) ? 1 - (d * d * d) : 0;
        }
        public static double WyvillRise01(double fX) {
            double d = 1 - fX * fX;
            return (d > 0) ? 1 - (d * d * d) : 0;
        }

        public static float WyvillFalloff01(float fX) {
            float d = 1 - fX * fX;
            return (d > 0) ? (d * d * d) : 0;
        }
        public static double WyvillFalloff01(double fX) {
            double d = 1 - fX * fX;
            return (d > 0) ? (d * d * d) : 0;
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



		public static double Area(Vector3d v1, Vector3d v2, Vector3d v3) {
			return 0.5 * (v2 - v1).Cross(v3 - v1).Length;
		}



        public static Vector3d Normal(Vector3d v1, Vector3d v2, Vector3d v3) {
            Vector3d edge1 = v2 - v1;
            Vector3d edge2 = v3 - v2;
            edge1.Normalize();
            edge2.Normalize();
            Vector3d vCross = edge1.Cross(edge2);
            vCross.Normalize();
            return vCross;
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




        static readonly int[] powers_of_10 = { 1, 10, 100, 1000, 10000, 100000, 1000000, 10000000, 100000000, 1000000000 };
        public static int PowerOf10(int n) {
            return powers_of_10[n];
        }


    }
}
