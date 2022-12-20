using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace g3
{
    /// <summary>
    /// A Spherical Fibonacci Point Set is a set of points that are roughly evenly distributed on
    /// a sphere. Basically the points lie on a spiral, see pdf below.
    /// The i-th SF point of an N-point set can be calculated directly.
    /// For a given (normalized) point P, finding the nearest SF point (ie mapping back to i)
    /// can be done in constant time.
    /// 
    /// math from http://lgdv.cs.fau.de/uploads/publications/spherical_fibonacci_mapping_opt.pdf
    /// </summary>
    public class SphericalFibonacciPointSet
    {
        public int N = 64;

        public SphericalFibonacciPointSet(int n = 64) {
            N = n;
        }


        public int Count { get { return N; } }


        /// <summary>
        /// Compute i'th spherical point
        /// </summary>
        public Vector3d Point(int i)
        {
            Util.gDevAssert(i < N);
            double div = (double)i / PHI;
            double phi = MathUtil.TwoPI * (div - Math.Floor(div));
            double cos_phi = Math.Cos(phi), sin_phi = Math.Sin(phi);

            double z = 1.0 - (2.0 * (double)i + 1.0) / (double)N;
            double theta = Math.Acos(z);
            double sin_theta = Math.Sin(theta);

            return new Vector3d(cos_phi * sin_theta, sin_phi * sin_theta, z);
        }
        public Vector3d this[int i] {
            get { return Point(i); }
        }


        /// <summary>
        /// Find index of nearest point-set point for input arbitrary point
        /// </summary>
        public int NearestPoint(Vector3d p, bool bIsNormalized = false)
        {
            if (bIsNormalized)
                return inverseSF(ref p);
            p.Normalize();
            return inverseSF(ref p);
        }




        static readonly double PHI = (Math.Sqrt(5.0) + 1.0) / 2.0;

        double madfrac(double a, double b)
        {
            //#define madfrac(A,B) mad((A),(B),-floor((A)*(B)))
            return a * b + -Math.Floor(a * b);
        }

        /// <summary>
        /// This computes mapping from p to i. Note that the code in the original PDF is HLSL shader code.
        /// I have ported here to comparable C# functions. *However* the PDF also explains some assumptions
        /// made about what certain operators return in different cases (particularly NaN handling).
        /// I have not yet tested these cases to make sure C# behavior is the same (not sure when they happen).
        /// </summary>
        int inverseSF(ref Vector3d p)
        {
            double phi = Math.Min(Math.Atan2(p.y, p.x), Math.PI);
            double cosTheta = p.z;
            double k = Math.Max(2.0, Math.Floor(
                Math.Log(N * Math.PI * Math.Sqrt(5.0) * (1.0 - cosTheta*cosTheta)) / Math.Log(PHI*PHI)));
            double Fk = Math.Pow(PHI, k) / Math.Sqrt(5.0);

            //double F0 = round(Fk), F1 = round(Fk * PHI);
            double F0 = Math.Round(Fk), F1 = Math.Round(Fk * PHI);

            Matrix2d B = new Matrix2d(
                2 * Math.PI * madfrac(F0 + 1, PHI - 1) - 2 * Math.PI * (PHI - 1),
                2 * Math.PI * madfrac(F1 + 1, PHI - 1) - 2 * Math.PI * (PHI - 1),
                -2 * F0 / N, -2 * F1 / N);
            Matrix2d invB = B.Inverse();

            //Vector2d c = floor(mul(invB, double2(phi, cosTheta - (1 - 1.0/N))));
            Vector2d c = new Vector2d(phi, cosTheta - (1 - 1.0 / N));
            c = invB * c;
            c.x = Math.Floor(c.x); c.y = Math.Floor(c.y);

            double d = double.PositiveInfinity, j = 0;
            for (uint s = 0; s < 4; ++s) {
                Vector2d cosTheta_second = new Vector2d(s%2, s/2) + c;
                cosTheta = B.Row(1).Dot(cosTheta_second) + (1-1.0/N);
                cosTheta = MathUtil.Clamp(cosTheta, -1.0, +1.0)*2.0 - cosTheta;
                double i = Math.Floor(N*0.5 - cosTheta*N*0.5);
                phi = 2.0 * Math.PI * madfrac(i, PHI - 1);
                cosTheta = 1.0 - (2.0 * i + 1.0) * (1.0 / N); // rcp(n);
                double sinTheta = Math.Sqrt(1.0 - cosTheta * cosTheta);
                Vector3d q = new Vector3d(
                    Math.Cos(phi) * sinTheta,
                    Math.Sin(phi) * sinTheta,
                    cosTheta);
                double squaredDistance = Vector3d.Dot(q - p, q - p);
                if (squaredDistance < d) {
                    d = squaredDistance;
                    j = i;
                }
            }

            // [TODO] should we be clamping this??
            return (int)j;
        }




    }
}
