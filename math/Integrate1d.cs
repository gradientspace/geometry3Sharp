using System;

namespace g3
{
    // ported from WildMagic5 Integrate1
    public static class Integrate1d
    {
        public static double RombergIntegral(int order, double a, double b, Func<double,object,double> function, object userData)
        {
            if (order <= 0)
                throw new Exception("Integrate1d.RombergIntegral: Integration order must be positive\n");
            double[,] rom = new double[2, order];

            double h = b - a;

            rom[0, 0] = ((double)0.5) * h * (function(a, userData) + function(b, userData));
            for (int i0 = 2, p0 = 1; i0 <= order; ++i0, p0 *= 2, h *= (double)0.5) {
                // Approximations via the trapezoid rule.
                double sum = (double)0;
                int i1;
                for (i1 = 1; i1 <= p0; ++i1)
                    sum += function(a + h * (i1 - ((double)0.5)), userData);

                // Richardson extrapolation.
                rom[1, 0] = ((double)0.5) * (rom[0, 0] + h * sum);
                for (int i2 = 1, p2 = 4; i2 < i0; ++i2, p2 *= 4) {
                    rom[1, i2] = (p2 * rom[1, i2 - 1] - rom[0, i2 - 1]) / (p2 - 1);
                }

                for (i1 = 0; i1 < i0; ++i1)
                    rom[0, i1] = rom[1, i1];
            }
            double result = rom[0, order - 1];
            return result;
        }


        // these are for GaussianQuadrature, because C# cannot have a static const array inside a method
        private static readonly double[] root = {
            (double)-0.9061798459, (double)-0.5384693101, (double) 0.0, (double)+0.5384693101, (double)+0.9061798459 };
        private static readonly double[] coeff = {
            (double)0.2369268850, (double)0.4786286705, (double)0.5688888889, (double)0.4786286705, (double)0.2369268850 };

        public static double GaussianQuadrature(double a, double b, Func<double,object,double> function, object userData)
        {
            // Legendre polynomials:
            // P_0(x) = 1
            // P_1(x) = x
            // P_2(x) = (3x^2-1)/2
            // P_3(x) = x(5x^2-3)/2
            // P_4(x) = (35x^4-30x^2+3)/8
            // P_5(x) = x(63x^4-70x^2+15)/8

            // Generation of polynomials:
            //   d/dx[ (1-x^2) dP_n(x)/dx ] + n(n+1) P_n(x) = 0
            //   P_n(x) = sum_{k=0}^{floor(n/2)} c_k x^{n-2k}
            //     c_k = (-1)^k (2n-2k)! / [ 2^n k! (n-k)! (n-2k)! ]
            //   P_n(x) = ((-1)^n/(2^n n!)) d^n/dx^n[ (1-x^2)^n ]
            //   (n+1)P_{n+1}(x) = (2n+1) x P_n(x) - n P_{n-1}(x)
            //   (1-x^2) dP_n(x)/dx = -n x P_n(x) + n P_{n-1}(x)

            // Roots of the Legendre polynomial of specified degree.
            const int degree = 5;
            // (moved static array defns to above)

            // Need to transform domain [a,b] to [-1,1].  If a <= x <= b and
            // -1 <= t <= 1, then x = ((b-a)*t+(b+a))/2.
            double radius = ((double)0.5) * (b - a);
            double center = ((double)0.5) * (b + a);

            double result = (double)0;
            for (int i = 0; i < degree; ++i) {
                result += coeff[i] * function(radius * root[i] + center, userData);
            }
            result *= radius;

            return result;
        }



        static public double TrapezoidRule(int numSamples, double a, double b, Func<double, object, double> function, object userData)
        {
            if (numSamples < 2)
                throw new Exception("Integrate1d.TrapezoidRule: Must have more than two samples\n");


            double h = (b - a) / (double)(numSamples - 1);
            double result = ((double)0.5) * (function(a, userData) +
                function(b, userData));

            for (int i = 1; i <= numSamples - 2; ++i) {
                result += function(a + i * h, userData);
            }
            result *= h;
            return result;
        }


    }
}
