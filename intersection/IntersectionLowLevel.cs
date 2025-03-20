using System;

namespace g3
{
    internal static class IntersectionLowLevel
    {

		// this function is from various WildMagic/GTEngine intersection tests
		// clips segment [t0,t1] against line/plane direction/origin (in plane equation)
		static public bool Clip(double denom, double numer, ref double t0, ref double t1)
		{
			// Return value is 'true' if line segment intersects the current test
			// plane.  Otherwise 'false' is returned in which case the line segment
			// is entirely clipped.

			if (denom > (double)0)
			{
				if (numer > denom*t1)
					return false;

				if (numer > denom*t0)
					t0 = numer/denom;

				return true;
			}
			else if (denom < (double)0)
			{
				if (numer > denom*t0)
					return false;

				if (numer > denom*t1)
					t1 = numer/denom;

				return true;
			}
			else
			{
				return numer <= (double)0;
			}
		}
    }
}
