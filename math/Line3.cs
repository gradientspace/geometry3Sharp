using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    public class Line3d
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


        // conversion operators
        public static implicit operator Line3d(Line3f v)
        {
            return new Line3d(v.Origin, v.Direction);
        }
        public static explicit operator Line3f(Line3d v)
        {
            return new Line3f((Vector3f)v.Origin, (Vector3f)v.Direction);
        }


    }


    public class Line3f
    {
        public Vector3f Origin;
        public Vector3f Direction;

        public Line3f(Vector3f origin, Vector3f direction)
        {
            this.Origin = origin;
            this.Direction = direction;
        }

        // parameter is distance along Line
        public Vector3f PointAt(float d)
        {
            return Origin + d * Direction;
        }
    }
}
