using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    public static class Distance
    {

        public static Vector3f ClosestPointOnLine(Vector3f p0, Vector3f dir, Vector3f pt)
        {
            float t = (pt - p0).Dot(dir);
            return p0 + t * dir;
        }
        public static float ClosestPointOnLineT(Vector3f p0, Vector3f dir, Vector3f pt)
        {
            float t = (pt - p0).Dot(dir);
            return t;
        }
    }
}
