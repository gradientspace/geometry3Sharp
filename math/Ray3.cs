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
        public Vector3d Origin;
        public Vector3d Direction;

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
            if (t < 0) {
                return Origin.DistanceSquared(p);
            } else {
                Vector3d proj = Origin + t * Direction;
                return (proj - p).LengthSquared;
            }
        }

        public Vector3d ClosestPoint(Vector3d p)
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
