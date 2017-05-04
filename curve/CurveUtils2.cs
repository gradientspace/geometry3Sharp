using System;
using System.Collections.Generic;

namespace g3 
{
	public static class CurveUtils2 
	{


        public static IParametricCurve2d Convert(Polygon2d poly)
        {
            ParametricCurveSequence2 seq = new ParametricCurveSequence2();
            int N = poly.VertexCount;
            for (int i = 0; i < N; ++i)
                seq.Append(new Segment2d(poly[i], poly[(i + 1)%N]));
            seq.IsClosed = true;
            return seq;
        }


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


        /// <summary>
        /// if the children of C are a tree, iterate through all the leaves
        /// </summary>
        public static IEnumerable<IParametricCurve2d> LeafCurvesIteration(IParametricCurve2d c)
        {
            if (c is IMultiCurve2d) {
                var multiC = c as IMultiCurve2d;
                foreach ( IParametricCurve2d c2 in multiC.Curves ) {
                    foreach (var c3 in LeafCurvesIteration(c2))
                        yield return c3;
                }
            } else
                yield return c;
        }


        public static List<IParametricCurve2d> Flatten(List<IParametricCurve2d> curves)
        {
            List<IParametricCurve2d> l = new List<IParametricCurve2d>();
            foreach ( IParametricCurve2d sourceC in curves) {
                foreach (var c in LeafCurvesIteration(sourceC))
                    l.Add(c);
            }
            return l;
        }

	}
}
