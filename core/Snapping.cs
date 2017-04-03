using System;

namespace g3
{
    public class Snapping
    {

        public static double SnapToIncrement(double fValue, double fIncrement)
        {
            if (!MathUtil.IsFinite(fValue))
                return 0;
            double sign = Math.Sign(fValue);
            fValue = Math.Abs(fValue);
            int nInc = (int)(fValue / fIncrement);
            double fRem = fValue % fIncrement;
            if (fRem > fIncrement / 2)
                ++nInc;
            return sign * (double)nInc * fIncrement;
        }


    }
}
