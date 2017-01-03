using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    public struct Triangle2d
    {
        public Vector2d V0, V1, V2;

        public Triangle2d(Vector2d v0, Vector2d v1, Vector2d v2)
        {
            V0 = v0; V1 = v1; V2 = v2;
        }

        public Vector2d this[int key]
        {
            get { return (key == 0) ? V0 : (key == 1) ? V1 : V2; }
            set { if (key == 0) V0 = value; else if (key == 1) V1 = value; else V2 = value; }
        }

        public Vector2d PointAt(double bary0, double bary1, double bary2)
        {
            return bary0 * V0 + bary1 * V1 + bary2 * V2;
        }
        public Vector2d PointAt(Vector3d bary)
        {
            return bary.x* V0 + bary.y* V1 + bary.z* V2;
        }

        // conversion operators
        public static implicit operator Triangle2d(Triangle2f v)
        {
            return new Triangle2d(v.V0, v.V1, v.V2);
        }
        public static explicit operator Triangle2f(Triangle2d v)
        {
            return new Triangle2f((Vector2f)v.V0, (Vector2f)v.V1, (Vector2f)v.V2);
        }
    }



    public struct Triangle2f
    {
        public Vector2f V0, V1, V2;

        public Triangle2f(Vector2f v0, Vector2f v1, Vector2f v2)
        {
            V0 = v0; V1 = v1; V2 = v2;
        }

        public Vector2f this[int key]
        {
            get { return (key == 0) ? V0 : (key == 1) ? V1 : V2; }
            set { if (key == 0) V0 = value; else if (key == 1) V1 = value; else V2 = value; }
        }


        public Vector2f PointAt(float bary0, float bary1, float bary2)
        {
            return bary0 * V0 + bary1 * V1 + bary2 * V2;
        }
        public Vector2f PointAt(Vector3f bary)
        {
            return bary.x * V0 + bary.y * V1 + bary.z * V2;
        }
    }

}
