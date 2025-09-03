using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    public struct Line3d
    {
        public Vector3d Origin;
        public Vector3d Direction;

        public Line3d(Vector3d origin, Vector3d direction)
        {
            this.Origin = origin;
            this.Direction = direction;
        }

        // parameter is distance along Line
        public Vector3d PointAt(double d) {
            return Origin + d * Direction;
        }

        public double Project(Vector3d p)
        {
            return (p - Origin).Dot(Direction);
        }

        public double DistanceSquared(Vector3d p)
        {
            double t = (p - Origin).Dot(Direction);
            Vector3d proj = Origin + t * Direction;
            return (proj - p).LengthSquared;
        }

        public Vector3d ClosestPoint(Vector3d p)
        {
            double t = (p - Origin).Dot(Direction);
            return Origin + t * Direction;
        }
    }

}
