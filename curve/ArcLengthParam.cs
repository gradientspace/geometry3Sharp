using System;
using System.Collections.Generic;
using System.Linq;

namespace g3
{
    public struct CurveSample
    {
        public Vector3d position;
        public Vector3d tangent;
        public CurveSample(Vector3d p, Vector3d t) {
            position = p; tangent = t;
        }
    }

    public interface IArcLengthParam
    {
        double ArcLength { get; }
        CurveSample Sample(double fArcLen);
    }



    public class SampledArcLengthParam : IArcLengthParam
    {
        double[] arc_len;
        Vector3d[] positions;

        public SampledArcLengthParam( IEnumerable<Vector3d> samples, int nCountHint = -1 )
        {
            int N = (nCountHint == -1) ? samples.Count() : nCountHint;
            arc_len = new double[N];
            arc_len[0] = 0;
            positions = new Vector3d[N];

            int i = 0;
            Vector3d prev = Vector3f.Zero;
            foreach ( Vector3d v in samples ) {
                positions[i] = v;
                if (i > 0) { 
                    double d = (v - prev).Length;
                    arc_len[i] = arc_len[i - 1] + d;
                }
                i++;
                prev = v;
            }
        }



        public double ArcLength
        {
            get { return arc_len[arc_len.Length - 1]; }
        }


        public CurveSample Sample(double f)
        {
            if (f <= 0)
                return new CurveSample(new Vector3d(positions[0]), tangent(0));

            int N = arc_len.Length;
            if (f >= arc_len[N - 1])
                return new CurveSample(new Vector3d(positions[N-1]), tangent(N-1));

            for (int k = 0; k < N; ++k) {
                if (f < arc_len[k]) {
                    int a = k - 1;
                    int b = k;
                    if (arc_len[a] == arc_len[b])
                        return new CurveSample(new Vector3d(positions[a]), tangent(a));
                    double t = (f - arc_len[a]) / (arc_len[b] - arc_len[a]);
                    return new CurveSample(
                        Vector3d.Lerp(positions[a], positions[b], t),
                        Vector3d.Lerp(tangent(a), tangent(b), t));
                }
            }

            throw new ArgumentException("SampledArcLengthParam.Sample: somehow arc len is outside any possible range");
        }


        protected Vector3d tangent(int i)
        {
            int N = arc_len.Length;
            if (i == 0)
                return (positions[1] - positions[0]).Normalized;
            else if (i == N-1)
                return (positions[N-1] - positions[N-2]).Normalized;
            else
                return (positions[i + 1] - positions[i - 1]).Normalized;
        }
    }

}
