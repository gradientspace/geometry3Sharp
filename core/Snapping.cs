using System;

namespace g3
{
    public class Snapping
    {

        public static double SnapToIncrement(double fValue, double fIncrement, double offset = 0)
        {
            if (!MathUtil.IsFinite(fValue))
                return 0;
            fValue -= offset;
            double sign = Math.Sign(fValue);
            fValue = Math.Abs(fValue);
            int nInc = (int)(fValue / fIncrement);
            double fRem = fValue % fIncrement;
            if (fRem > fIncrement / 2)
                ++nInc;
            return sign * (double)nInc * fIncrement + offset;
        }




        public static double SnapToNearbyIncrement(double fValue, double fIncrement, double fTolerance)
        {
            double snapped = SnapToIncrement(fValue, fIncrement);
            if (Math.Abs(snapped - fValue) < fTolerance)
                return snapped;
            return fValue;
        }

        private static double SnapToIncrementSigned(double fValue, double fIncrement, bool low)
        {
            if (!MathUtil.IsFinite(fValue))
                return 0;
            double sign = Math.Sign(fValue);
            fValue = Math.Abs(fValue);
            int nInc = (int)(fValue / fIncrement);

            if (low && sign < 0)
                ++nInc;
            else if (!low && sign > 0)
                ++nInc;

            return sign * (double)nInc * fIncrement;

        }

        public static double SnapToIncrementLow(double fValue, double fIncrement, double offset=0)
        {
            return SnapToIncrementSigned(fValue - offset, fIncrement, true) + offset;
        }

        public static double SnapToIncrementHigh(double fValue, double fIncrement, double offset = 0)
        {
            return SnapToIncrementSigned(fValue - offset, fIncrement, false) + offset;
        }
    }
}
