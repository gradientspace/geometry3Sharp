using System;

namespace g3
{
    public class Snapping
    {

        public static double SnapToIncrement(double fValue, double fIncrement)
        {
            double sign = Math.Sign(fValue);
            fValue = Math.Abs(fValue);
            int nInc = (int)(fValue / fIncrement);
            double fRem = Math.IEEERemainder(fValue, fIncrement);
            if (fRem > fIncrement / 2)
                ++nInc;
            return sign * (double)nInc * fIncrement;
        }


    }
}
