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

        public Ray3d(Vector3d origin, Vector3d direction)
        {
            this.Origin = origin;
            this.Direction = direction;
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
            Vector3d proj = Origin + t * Direction;
            return (proj - p).LengthSquared;
        }


        // conversion operators
        public static implicit operator Ray3d(Ray3f v)
        {
            return new Ray3d(v.Origin, v.Direction);
        }
        public static explicit operator Ray3f(Ray3d v)
        {
            return new Ray3f((Vector3f)v.Origin, (Vector3f)v.Direction);
        }


#if G3_USING_UNITY
        public static implicit operator Ray3d(UnityEngine.Ray r)
        {
            return new Ray3d(r.origin, r.direction);
        }
        public static explicit operator Ray(Ray3d r)
        {
            return new Ray((Vector3)r.Origin, (Vector3)r.Direction);
        }
#endif

    }



    public struct Ray3f
    {
        public Vector3f Origin;
        public Vector3f Direction;

        public Ray3f(Vector3f origin, Vector3f direction)
        {
            this.Origin = origin;
            this.Direction = direction;
        }

        // parameter is distance along ray
        public Vector3f PointAt(float d)
        {
            return Origin + d * Direction;
        }

        public float Project(Vector3f p)
        {
            return (p - Origin).Dot(Direction);
        }

        public float DistanceSquared(Vector3f p)
        {
            float t = (p - Origin).Dot(Direction);
            Vector3f proj = Origin + t * Direction;
            return (proj - p).LengthSquared;
        }


#if G3_USING_UNITY
        public static implicit operator Ray3f(UnityEngine.Ray r)
        {
            return new Ray3f(r.origin, r.direction);
        }
        public static implicit operator Ray(Ray3f r)
        {
            return new Ray(r.Origin, r.Direction);
        }
#endif
    }
}
