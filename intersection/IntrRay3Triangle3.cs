using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    public class IntrRay3Triangle3
    {
        Ray3d ray;
        public Ray3d Ray
        {
            get { return ray; }
            set { ray = value; Result = IntersectionResult.NotComputed; }
        }

        Triangle3d triangle;
        public Triangle3d Triangle
        {
            get { return triangle; }
            set { triangle = value; Result = IntersectionResult.NotComputed; }
        }

		public int Quantity = 0;
		public IntersectionResult Result = IntersectionResult.NotComputed;
		public IntersectionType Type = IntersectionType.Empty;

        public bool IsSimpleIntersection {
            get { return Result == IntersectionResult.Intersects && Type == IntersectionType.Point; }
        }


        public double RayParameter;
        public Vector3d TriangleBaryCoords;


		public IntrRay3Triangle3(Ray3d r, Triangle3d t)
		{
			ray = r; triangle = t;
		}


        public IntrRay3Triangle3 Compute()
        {
            Find();
            return this;
        }


        public bool Find()
        {
            if (Result != IntersectionResult.NotComputed)
                return (Result != g3.IntersectionResult.NoIntersection);

            // Compute the offset origin, edges, and normal.
            Vector3d diff = ray.Origin - triangle.V0;
            Vector3d edge1 = triangle.V1 - triangle.V0;
            Vector3d edge2 = triangle.V2 - triangle.V0;
            Vector3d normal = edge1.Cross(edge2);

            // Solve Q + t*D = b1*E1 + b2*E2 (Q = kDiff, D = ray direction,
            // E1 = kEdge1, E2 = kEdge2, N = Cross(E1,E2)) by
            //   |Dot(D,N)|*b1 = sign(Dot(D,N))*Dot(D,Cross(Q,E2))
            //   |Dot(D,N)|*b2 = sign(Dot(D,N))*Dot(D,Cross(E1,Q))
            //   |Dot(D,N)|*t = -sign(Dot(D,N))*Dot(Q,N)
            double DdN = ray.Direction.Dot(normal);
            double sign;
            if (DdN > MathUtil.ZeroTolerance) {
                sign = 1;
            } else if (DdN < -MathUtil.ZeroTolerance) {
                sign = -1;
                DdN = -DdN;
            } else {
                // Ray and triangle are parallel, call it a "no intersection"
                // even if the ray does intersect.
                Result = IntersectionResult.NoIntersection;
                return false;
            }

            double DdQxE2 = sign * ray.Direction.Dot(diff.Cross(edge2));
            if (DdQxE2 >= 0) {
                double DdE1xQ = sign * ray.Direction.Dot(edge1.Cross(diff));
                if (DdE1xQ >= 0) {
                    if (DdQxE2 + DdE1xQ <= DdN) {
                        // Line intersects triangle, check if ray does.
                        double QdN = -sign * diff.Dot(normal);
                        if (QdN >= 0) {
                            // Ray intersects triangle.
                            double inv = (1) / DdN;
                            RayParameter = QdN * inv;
                            double mTriBary1 = DdQxE2 * inv;
                            double mTriBary2 = DdE1xQ * inv;
                            TriangleBaryCoords = new Vector3d(1 - mTriBary1 - mTriBary2, mTriBary1, mTriBary2);
                            Type = IntersectionType.Point;
							Quantity = 1;
                            Result = IntersectionResult.Intersects;
                            return true;
                        }
                        // else: t < 0, no intersection
                    }
                    // else: b1+b2 > 1, no intersection
                }
                // else: b2 < 0, no intersection
            }
            // else: b1 < 0, no intersection

            Result = IntersectionResult.NoIntersection;
            return false;
        }



        /// <summary>
        /// minimal intersection test, computes ray-t
        /// </summary>
        public static bool Intersects(ref Ray3d ray, ref Vector3d V0, ref Vector3d V1, ref Vector3d V2, out double rayT)
        {
            // Compute the offset origin, edges, and normal.
            Vector3d diff = ray.Origin - V0;
            Vector3d edge1 = V1 - V0;
            Vector3d edge2 = V2 - V0;
            Vector3d normal = edge1.Cross(ref edge2);

            rayT = double.MaxValue;

            // Solve Q + t*D = b1*E1 + b2*E2 (Q = kDiff, D = ray direction,
            // E1 = kEdge1, E2 = kEdge2, N = Cross(E1,E2)) by
            //   |Dot(D,N)|*b1 = sign(Dot(D,N))*Dot(D,Cross(Q,E2))
            //   |Dot(D,N)|*b2 = sign(Dot(D,N))*Dot(D,Cross(E1,Q))
            //   |Dot(D,N)|*t = -sign(Dot(D,N))*Dot(Q,N)
            double DdN = ray.Direction.Dot(ref normal);
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

            Vector3d cross = diff.Cross(ref edge2);
            double DdQxE2 = sign * ray.Direction.Dot(ref cross);
            if (DdQxE2 >= 0) {
                cross = edge1.Cross(ref diff);
                double DdE1xQ = sign * ray.Direction.Dot(ref cross);
                if (DdE1xQ >= 0) {
                    if (DdQxE2 + DdE1xQ <= DdN) {
                        // Line intersects triangle, check if ray does.
                        double QdN = -sign * diff.Dot(ref normal);
                        if (QdN >= 0) {
                            // Ray intersects triangle.
                            double inv = (1) / DdN;
                            rayT = QdN * inv;
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
