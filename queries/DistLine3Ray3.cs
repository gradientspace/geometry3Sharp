using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    // ported from WildMagic 5 
    // https://www.geometrictools.com/Downloads/Downloads.html

    public class DistLine3Ray3
    {
        Line3d line;
        public Line3d Line
        {
            get { return line; }
            set { line = value; DistanceSquared = -1.0; }
        }

        Ray3d ray;
        public Ray3d Ray
        {
            get { return ray; }
            set { ray = value; DistanceSquared = -1.0; }
        }

        public double DistanceSquared = -1.0;

        public Vector3d LineClosest;
        public double LineParameter;
        public Vector3d RayClosest;
        public double RayParameter;


        public DistLine3Ray3(Ray3d rayIn, Line3d LineIn)
        {
            this.ray = rayIn; this.line = LineIn;
        }

        static public double MinDistance(Ray3d r, Line3d s)
        {
            return new DistLine3Ray3(r, s).Get();
        }
        static public double MinDistanceLineParam(Ray3d r, Line3d s)
        {
            return new DistLine3Ray3(r, s).Compute().LineParameter;
        }


        public DistLine3Ray3 Compute()
        {
            GetSquared();
            return this;
        }

        public double Get()
        {
            return Math.Sqrt(GetSquared());
        }


        public double GetSquared ()
        {
            if (DistanceSquared >= 0)
                return DistanceSquared;

            Vector3d kDiff = line.Origin - ray.Origin;
            double a01 = -line.Direction.Dot(ray.Direction);
            double b0 = kDiff.Dot(line.Direction);
            double c = kDiff.LengthSquared;
            double det = Math.Abs(1.0 - a01 * a01);
            double b1, s0, s1, sqrDist;

            if (det >= MathUtil.ZeroTolerance) {
                b1 = -kDiff.Dot(ray.Direction);
                s1 = a01 * b0 - b1;

                if (s1 >= (double)0) {
                    // Two interior points are closest, one on line and one on ray.
                    double invDet = ((double)1) / det;
                    s0 = (a01 * b1 - b0) * invDet;
                    s1 *= invDet;
                    sqrDist = s0 * (s0 + a01 * s1 + ((double)2) * b0) +
                        s1 * (a01 * s0 + s1 + ((double)2) * b1) + c;
                } else {
                    // Origin of ray and interior point of line are closest.
                    s0 = -b0;
                    s1 = (double)0;
                    sqrDist = b0 * s0 + c;
                }
            } else {
                // Lines are parallel, closest pair with one point at ray origin.
                s0 = -b0;
                s1 = (double)0;
                sqrDist = b0 * s0 + c;
            }

            LineClosest = line.Origin + s0 * line.Direction;
            RayClosest = ray.Origin + s1 * ray.Direction;
            LineParameter = s0;
            RayParameter = s1;

            // Account for numerical round-off errors.
            if (sqrDist < (double)0) {
                sqrDist = (double)0;
            }
            DistanceSquared = sqrDist;

            return sqrDist;
        }



    }
}
