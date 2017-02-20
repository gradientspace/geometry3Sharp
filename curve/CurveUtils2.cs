using System;
namespace g3 
{
	public static class CurveUtils2 
	{


		// 2D curve utils?
		public static double SampledDistance(IParametricCurve2d c, Vector2d point, int N = 100)
		{
			double tMax = c.ParamLength;
			double min_dist = double.MaxValue;
			for ( int i = 0; i <= N; ++i ) {
				double fT = (double)i / (double)N;
				fT *= tMax;
				Vector2d p = c.SampleT(fT);
				double d = p.DistanceSquared(point);
				if ( d < min_dist )
					min_dist = d;
			}
			return Math.Sqrt(min_dist);
		}


	}
}
