using System;


namespace g3
{

    public class MathUtil
    {

        public const double Deg2Rad = (180.0 / Math.PI);
        public const double Rad2Deg = (Math.PI / 180.0);
        public const double TwoPI = 2.0 * Math.PI;
        public const double HalfPI = 0.5 * Math.PI;
        public const double ZeroTolerance = 1e-08;
        public const double Epsilon = 2.2204460492503131e-016;

        public const float Rad2Degf = (float)(180.0 / Math.PI);
        public const float Deg2Radf = (float)(Math.PI / 180.0);
        public const float PIf = (float)(Math.PI);
        public const float TwoPIf = 2.0f * PIf;
        public const float HalfPIf = 0.5f * PIf;

        public const float ZeroTolerancef = 1e-06f;
        public const float Epsilonf = 1.192092896e-07F;


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


        // fMinMaxValue may be signed
        public static float RangeClamp(float fValue, float fMinMaxValue)
        {
            return Clamp(fValue, -Math.Abs(fMinMaxValue), Math.Abs(fMinMaxValue));
        }
        public static double RangeClamp(double fValue, double fMinMaxValue)
        {
            return Clamp(fValue, -Math.Abs(fMinMaxValue), Math.Abs(fMinMaxValue));
        }







        public static float PlaneAngleD(Vector3f a, Vector3f b, int nPlaneNormalIdx = 1)
        {
            a[nPlaneNormalIdx] = b[nPlaneNormalIdx] = 0.0f;
            a.Normalize();
            b.Normalize();
            return Vector3f.AngleD(a, b);
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


    }
}
