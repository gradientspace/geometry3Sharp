using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    public class RayIntersection
    {
        private RayIntersection()
        {

        }

        // basic ray-sphere intersection
        public static bool Sphere(Vector3f vOrigin, Vector3f vDirection, Vector3f vCenter, float fRadius, out float fRayT)
        {
            bool bHit = SphereSigned(vOrigin, vDirection, vCenter, fRadius, out fRayT);
            fRayT = Math.Abs(fRayT);
            return bHit;
        }

        public static bool SphereSigned(Vector3f vOrigin, Vector3f vDirection, Vector3f vCenter, float fRadius, out float fRayT)
        {
            fRayT = 0.0f;
            Vector3f m = vOrigin - vCenter;
            float b = m.Dot(vDirection);
            float c = m.Dot(m) - fRadius * fRadius;

            // Exit if r’s origin outside s (c > 0) and r pointing away from s (b > 0) 
            if (c > 0.0f && b > 0.0f)
                return false;
            float discr = b * b - c;

            // A negative discriminant corresponds to ray missing sphere 
            if (discr < 0.0f)
                return false;

            // Ray now found to intersect sphere, compute smallest t value of intersection
            fRayT = -b - (float)Math.Sqrt(discr);

            return true;
        }
    }
}
