using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

#if G3_USING_UNITY
using UnityEngine;
#endif

namespace g3
{
    public struct Ray3d
    {
        public Vector3d Origin = Vector3d.Zero;
        public Vector3d Direction = Vector3d.AxisX;

        public Ray3d()
        {
            Origin = Vector3d.Zero;
            Direction = Vector3d.AxisX;
        }

        public Ray3d(Vector3d origin, Vector3d direction, bool bIsNormalized = false)
        {
            this.Origin = origin;
            this.Direction = direction;
            if (bIsNormalized == false && Direction.IsNormalized == false)
                Direction.Normalize();
        }

        public Ray3d(Vector3f origin, Vector3f direction)
        {
            this.Origin = origin;
            this.Direction = direction;
            this.Direction.Normalize();     // float cast may not be normalized in double, is trouble in algorithms!
        }

        // parameter is distance along ray
        public readonly Vector3d PointAt(double d) {
            return Origin + d * Direction;
        }


        public readonly double Project(Vector3d p)
        {
            return (p - Origin).Dot(Direction);
        }

        public readonly double DistanceSquared(Vector3d p)
        {
            double t = (p - Origin).Dot(Direction);
            if (t < 0) {
                return Origin.DistanceSquared(p);
            } else {
                Vector3d proj = Origin + t * Direction;
                return (proj - p).LengthSquared;
            }
        }
        public readonly double Distance(Vector3d p)
        {
            return Math.Sqrt(DistanceSquared(p));
        }

        public readonly Vector3d ClosestPoint(Vector3d p)
        {
            double t = (p - Origin).Dot(Direction);
            if (t < 0) {
                return Origin;
            } else {
                return Origin + t * Direction;
            }
        }


#if G3_USING_UNITY
        public static implicit operator Ray3d(UnityEngine.Ray r)
        {
            return new Ray3d(r.origin, ((Vector3d)r.direction).Normalized);
        }
        public static explicit operator Ray(Ray3d r)
        {
            return new Ray((Vector3)r.Origin, ((Vector3)r.Direction).normalized);
        }
#endif

    }

}
