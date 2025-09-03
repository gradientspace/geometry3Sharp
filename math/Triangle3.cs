using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    public struct Triangle3d
    {
        public Vector3d V0, V1, V2;

        public Triangle3d(Vector3d v0, Vector3d v1, Vector3d v2)
        {
            V0 = v0; V1 = v1; V2 = v2;
        }

        public Vector3d this[int key]
        {
            get { return (key == 0) ? V0 : (key == 1) ? V1 : V2; }
            set { if (key == 0) V0 = value; else if (key == 1) V1 = value; else V2 = value; }
        }

        public Vector3d Normal {
            get { return MathUtil.Normal(ref V0, ref V1, ref V2); }
        }
        public double Area {
            get { return MathUtil.Area(ref V0, ref V1, ref V2); }
        }
        public double AspectRatio {
            get { return MathUtil.AspectRatio(ref V0, ref V1, ref V2); }
        }

        public Vector3d PointAt(double bary0, double bary1, double bary2)
        {
            return bary0 * V0 + bary1 * V1 + bary2 * V2;
        }
        public Vector3d PointAt(Vector3d bary)
        {
            return bary.x* V0 + bary.y* V1 + bary.z* V2;
        }

        public Vector3d BarycentricCoords(Vector3d point)
        {
            return MathUtil.BarycentricCoords(point, V0, V1, V2);
        }

    }


}
