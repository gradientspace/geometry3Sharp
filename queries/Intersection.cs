using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    public enum IntersectionResult
    {
        NotComputed,
        Intersects,
        NoIntersection,
		InvalidQuery
    }

    public enum IntersectionType
    {
        Empty, Point, Segment, Line, Polygon, Plane, Unknown
    }



    public static class Intersection
    {



        // same code as IntrRay3Triangle3, but can be called w/o constructing additional data structures
        public static bool Intersects(Vector3d rayOrigin, Vector3d rayDirection, Vector3d V0, Vector3d V1, Vector3d V2)
        {
            // Compute the offset origin, edges, and normal.
            Vector3d diff = rayOrigin - V0;
            Vector3d edge1 = V1 - V0;
            Vector3d edge2 = V2 - V0;
            Vector3d normal = edge1.Cross(edge2);

            // Solve Q + t*D = b1*E1 + b2*E2 (Q = kDiff, D = ray direction,
            // E1 = kEdge1, E2 = kEdge2, N = Cross(E1,E2)) by
            //   |Dot(D,N)|*b1 = sign(Dot(D,N))*Dot(D,Cross(Q,E2))
            //   |Dot(D,N)|*b2 = sign(Dot(D,N))*Dot(D,Cross(E1,Q))
            //   |Dot(D,N)|*t = -sign(Dot(D,N))*Dot(Q,N)
            double DdN = rayDirection.Dot(normal);
            double sign;
            if (DdN > MathUtil.ZeroTolerance) {
                sign = 1;
            } else if (DdN < -MathUtil.ZeroTolerance) {
                sign = -1;
                DdN = -DdN;
            } else {
                // Ray and triangle are parallel, call it a "no intersection"
                // even if the ray does intersect.
                return false;
            }

            double DdQxE2 = sign * rayDirection.Dot(diff.Cross(edge2));
            if (DdQxE2 >= 0) {
                double DdE1xQ = sign * rayDirection.Dot(edge1.Cross(diff));
                if (DdE1xQ >= 0) {
                    if (DdQxE2 + DdE1xQ <= DdN) {
                        // Line intersects triangle, check if ray does.
                        double QdN = -sign * diff.Dot(normal);
                        if (QdN >= 0) {
                            // Ray intersects triangle.
                            return true;
                        }
                        // else: t < 0, no intersection
                    }
                    // else: b1+b2 > 1, no intersection
                }
                // else: b2 < 0, no intersection
            }
            // else: b1 < 0, no intersection
            return false;

        }


    }



}
