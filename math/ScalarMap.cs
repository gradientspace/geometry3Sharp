using System;
using System.Collections.Generic;

namespace g3
{
    /// <summary>
    /// Scalar version of a ColorMap (ie interpolate between sample points)
    /// [TODO] could we make this a template?
    /// </summary>
    public class ScalarMap
    {
        struct Sample
        {
            public double t;
            public double value;
        }

        List<Sample> points = new List<Sample>();
        Interval1d validRange;

        public ScalarMap()
        {
            validRange = Interval1d.Empty;
        }



        public void AddPoint(double t, double value)
        {
            Sample cp = new Sample() { t = t, value = value };
            if ( points.Count == 0 ) {
                points.Add(cp);
                validRange.Contain(t);
            } else if ( t < points[0].t ) {
                points.Insert(0, cp);
                validRange.Contain(t);
            } else {
                for ( int k = 0; k < points.Count; ++k ) {
                    if ( points[k].t == t ) {
                        points[k] = cp;
                        return;
                    } else if ( points[k].t > t ) {
                        points.Insert(k, cp);
                        return;
                    }
                }
                points.Add(cp);
                validRange.Contain(t);
            }
        }




        public double Linear(double t)
        {
            if (t <= points[0].t)
                return points[0].value;
            int N = points.Count;
            if (t >= points[N - 1].t)
                return points[N - 1].value;
            for ( int k = 1; k < points.Count; ++k ) {
                if ( points[k].t > t ) {
                    Sample prev = points[k - 1], next = points[k];
                    double a = (t - prev.t) / (next.t - prev.t);
                    return (1.0f - a) * prev.value + (a) * next.value;
                }
            }
            return points[N - 1].value;  // should never get here...
        }


    }
}
