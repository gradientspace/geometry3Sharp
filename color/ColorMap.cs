using System;
using System.Collections.Generic;

namespace g3
{
    public class ColorMap
    {
        struct ColorPoint
        {
            public float t;
            public Colorf c;
        }

        List<ColorPoint> points = new List<ColorPoint>();
        Interval1d validRange;

        public ColorMap()
        {
            validRange = Interval1d.Empty;
        }

        public ColorMap(float[] t, Colorf[] c)
        {
            validRange = Interval1d.Empty;
            for (int i = 0; i < t.Length; ++i)
                AddPoint(t[i], c[i]);
        }

        public void AddPoint(float t, Colorf c)
        {
            ColorPoint cp = new ColorPoint() { t = t, c = c };
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




        public Colorf Linear(float t)
        {
            if (t <= points[0].t)
                return points[0].c;
            int N = points.Count;
            if (t >= points[N - 1].t)
                return points[N - 1].c;
            for ( int k = 1; k < points.Count; ++k ) {
                if ( points[k].t > t ) {
                    ColorPoint prev = points[k - 1], next = points[k];
                    float a = (t - prev.t) / (next.t - prev.t);
                    return (1.0f - a) * prev.c + (a) * next.c;
                }
            }
            return points[N - 1].c;  // should never get here...
        }


    }
}
