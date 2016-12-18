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

        public Line2d(Vector2d origin, Vector2d direction)
        {
            this.Origin = origin;
            this.Direction = direction;
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
