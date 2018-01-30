using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    public struct Line2d
    {
        public Vector2d Origin;
        public Vector2d Direction;

        public Line2d(Vector2d origin, Vector2d direction) {
            this.Origin = origin;
            this.Direction = direction;
        }

        public Line2d(ref Vector2d origin, ref Vector2d direction) {
            this.Origin = origin;
            this.Direction = direction;
        }

        public static Line2d FromPoints(Vector2d p0, Vector2d p1) {
            return new Line2d(p0, (p1 - p0).Normalized);
        }
        public static Line2d FromPoints(ref Vector2d p0, ref Vector2d p1) {
            return new Line2d(p0, (p1 - p0).Normalized);
        }

        // parameter is distance along Line
        public Vector2d PointAt(double d) {
            return Origin + d * Direction;
        }

        public double Project(Vector2d p)
        {
            return (p - Origin).Dot(Direction);
        }

        public double DistanceSquared(Vector2d p)
        {
            double t = (p - Origin).Dot(Direction);
            Vector2d proj = Origin + t * Direction;
            return (proj - p).LengthSquared;
        }



        /// <summary>
        /// Returns:
        ///   +1, on right of line
        ///   -1, on left of line
        ///    0, on the line
        /// </summary>
        public int WhichSide(Vector2d test, double tol = 0)
        {
            double x0 = test.x - Origin.x;
            double y0 = test.y - Origin.y;
            double x1 = Direction.x;
            double y1 = Direction.y;
            double det = x0 * y1 - x1 * y0;
            return (det > tol ? +1 : (det < -tol ? -1 : 0));
        }
        public int WhichSide(ref Vector2d test, double tol = 0)
        {
            double x0 = test.x - Origin.x;
            double y0 = test.y - Origin.y;
            double x1 = Direction.x;
            double y1 = Direction.y;
            double det = x0 * y1 - x1 * y0;
            return (det > tol ? +1 : (det < -tol ? -1 : 0));
        }



        /// <summary>
        /// Calculate intersection point between this line and another one.
        /// Returns Vector2d.MaxValue if lines are parallel.
        /// </summary>
        /// <returns></returns>
        public Vector2d IntersectionPoint(ref Line2d other, double dotThresh = MathUtil.ZeroTolerance)
        {
            // see IntrLine2Line2 for explanation of algorithm
            Vector2d diff = other.Origin - Origin;
            double D0DotPerpD1 = Direction.DotPerp(other.Direction);
            if (Math.Abs(D0DotPerpD1) > dotThresh) {                    // Lines intersect in a single point.
                double invD0DotPerpD1 = ((double)1) / D0DotPerpD1;
                double diffDotPerpD1 = diff.DotPerp(other.Direction);
                double s = diffDotPerpD1 * invD0DotPerpD1;
                return Origin + s * Direction;
            }
            // Lines are parallel.
            return Vector2d.MaxValue;
        }





        // conversion operators
        public static implicit operator Line2d(Line2f v)
        {
            return new Line2d(v.Origin, v.Direction);
        }
        public static explicit operator Line2f(Line2d v)
        {
            return new Line2f((Vector2f)v.Origin, (Vector2f)v.Direction);
        }


    }


    public struct Line2f
    {
        public Vector2f Origin;
        public Vector2f Direction;

        public Line2f(Vector2f origin, Vector2f direction)
        {
            this.Origin = origin;
            this.Direction = direction;
        }

        // parameter is distance along Line
        public Vector2f PointAt(float d)
        {
            return Origin + d * Direction;
        }

        public float Project(Vector2f p)
        {
            return (p - Origin).Dot(Direction);
        }

        public float DistanceSquared(Vector2f p)
        {
            float t = (p - Origin).Dot(Direction);
            Vector2f proj = Origin + t * Direction;
            return (proj - p).LengthSquared;
        }
    }
}
