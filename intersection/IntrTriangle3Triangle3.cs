using System;
using System.Diagnostics;

namespace g3
{
    // ported from WildMagic5 IntrTriangle3Triangle3
    // use Test() for fast boolean query, does not compute intersection info
    // use Find() to compute full information
    // By default fully-contained co-planar triangles are not reported as intersecting.
    // set ReportCoplanarIntersection=true to handle this case (more expensive)
    public class IntrTriangle3Triangle3
    {
        Triangle3d triangle0;
        public Triangle3d Triangle0
        {
            get { return triangle0; }
            set { triangle0 = value; Result = IntersectionResult.NotComputed; }
        }

        Triangle3d triangle1;
        public Triangle3d Triangle1
        {
            get { return triangle1; }
            set { triangle1 = value; Result = IntersectionResult.NotComputed; }
        }

        // If true, will return intersection polygons for co-planar triangles.
        // This is somewhat expensive, default is false.
        // Note that when false, co-planar intersections will **NOT** be reported as intersections
        public bool ReportCoplanarIntersection = false;

        // result flags
		public int Quantity = 0;
		public IntersectionResult Result = IntersectionResult.NotComputed;
		public IntersectionType Type = IntersectionType.Empty;

        // intersection points (for point, line)
        // only first Quantity elements are relevant
        public Vector3dTuple3 Points;

        // only valid if intersection type is Polygon
        public Vector3d[] PolygonPoints;


		public IntrTriangle3Triangle3(Triangle3d t0, Triangle3d t1)
		{
			triangle0 = t0;
            triangle1 = t1;
		}


        public IntrTriangle3Triangle3 Compute()
        {
            Find();
            return this;
        }


        public bool Find()
        {
            if (Result != IntersectionResult.NotComputed)
                return (Result != g3.IntersectionResult.NoIntersection);


            // in this code the results get initialized in subroutines, so we
            // set the defautl value here...
            Result = IntersectionResult.NoIntersection;


            int i, iM, iP;

            // Get the plane of triangle0.
            Plane3d plane0 = new Plane3d(triangle0.V0, triangle0.V1, triangle0.V2);

            // Compute the signed distances of triangle1 vertices to plane0.  Use
            // an epsilon-thick plane test.
            int pos1, neg1, zero1;
            Index3i sign1;
            Vector3d dist1;
            TrianglePlaneRelations(ref triangle1, ref plane0, out dist1, out sign1, out pos1, out neg1, out zero1);

            if (pos1 == 3 || neg1 == 3) {
                // Triangle1 is fully on one side of plane0.
                return false;
            }

            if (zero1 == 3) {
                // Triangle1 is contained by plane0.
                if (ReportCoplanarIntersection) {
                    return GetCoplanarIntersection(ref plane0, ref triangle0, ref triangle1);
                }
                return false;
            }

            // Check for grazing contact between triangle1 and plane0.
            if (pos1 == 0 || neg1 == 0) {
                if (zero1 == 2) {
                    // An edge of triangle1 is in plane0.
                    for (i = 0; i < 3; ++i) {
                        if (sign1[i] != 0) {
                            iM = (i + 2) % 3;
                            iP = (i + 1) % 3;
                            return IntersectsSegment(ref plane0, ref triangle0, triangle1[iM], triangle1[iP]);
                        }
                    }
                } else {// zero1 == 1
                    // A vertex of triangle1 is in plane0.
                    for (i = 0; i < 3; ++i) {
                        if (sign1[i] == 0) {
                            return ContainsPoint(ref triangle0, ref plane0, triangle1[i] );
                        }
                    }
                }
            }

            // At this point, triangle1 tranversely intersects plane 0.  Compute the
            // line segment of intersection.  Then test for intersection between this
            // segment and triangle 0.
            double t;
            Vector3d intr0, intr1;
            if (zero1 == 0) {
                int iSign = (pos1 == 1 ? +1 : -1);
                for (i = 0; i < 3; ++i) {
                    if (sign1[i] == iSign) {
                        iM = (i + 2) % 3;
                        iP = (i + 1) % 3;
                        t = dist1[i] / (dist1[i] - dist1[iM]);
                        intr0 = triangle1[i] + t * (triangle1[iM] - triangle1[i]);
                        t = dist1[i] / (dist1[i] - dist1[iP]);
                        intr1 = triangle1[i] + t * (triangle1[iP] - triangle1[i]);
                        return IntersectsSegment(ref plane0, ref triangle0, intr0, intr1);
                    }
                }
            }

            // zero1 == 1
            for (i = 0; i < 3; ++i) {
                if (sign1[i] == 0) {
                    iM = (i + 2) % 3;
                    iP = (i + 1) % 3;
                    t = dist1[iM] / (dist1[iM] - dist1[iP]);
                    intr0 = triangle1[iM] + t * (triangle1[iP] - triangle1[iM]);
                    return IntersectsSegment(ref plane0, ref triangle0, triangle1[i], intr0);
                }
            }

            // should never get here...
            Debug.Assert(false);
            return false;
        }


        public bool Test()
        {
            // Get edge vectors for triangle0.
            Vector3dTuple3 E0;
            E0.V0 = triangle0.V1 - triangle0.V0;
            E0.V1 = triangle0.V2 - triangle0.V1;
            E0.V2 = triangle0.V0 - triangle0.V2;

            // Get normal vector of triangle0.
            Vector3d N0 = E0[0].UnitCross(E0[1]);

            // Project triangle1 onto normal line of triangle0, test for separation.
            double N0dT0V0 = N0.Dot(triangle0.V0);
            double min1, max1;
            ProjectOntoAxis(ref triangle1, ref N0, out min1, out max1);
            if (N0dT0V0 < min1 || N0dT0V0 > max1) {
                return false;
            }

            // Get edge vectors for triangle1.
            Vector3dTuple3 E1;
            E1.V0 = triangle1.V1 - triangle1.V0;
            E1.V1 = triangle1.V2 - triangle1.V1;
            E1.V2 = triangle1.V0 - triangle1.V2;

            // Get normal vector of triangle1.
            Vector3d N1 = E1[0].UnitCross(E1[1]);

            Vector3d dir;
            double min0, max0;
            int i0, i1;

            Vector3d N0xN1 = N0.UnitCross(N1);
            if (N0xN1.Dot(N0xN1) >= MathUtil.ZeroTolerance) {
                // Triangles are not parallel.

                // Project triangle0 onto normal line of triangle1, test for
                // separation.
                double N1dT1V0 = N1.Dot(triangle1.V0);
                ProjectOntoAxis(ref triangle0, ref N1, out min0, out max0);
                if (N1dT1V0 < min0 || N1dT1V0 > max0) {
                    return false;
                }

                // Directions E0[i0]xE1[i1].
                for (i1 = 0; i1 < 3; ++i1) {
                    for (i0 = 0; i0 < 3; ++i0) {
                        dir = E0[i0].UnitCross(E1[i1]);
                        ProjectOntoAxis(ref triangle0, ref dir, out min0, out max0);
                        ProjectOntoAxis(ref triangle1, ref dir, out min1, out max1);
                        if (max0 < min1 || max1 < min0) {
                            return false;
                        }
                    }
                }

                // The test query does not know the intersection set.
                Type = IntersectionType.Unknown;
            } else { // Triangles are parallel (and, in fact, coplanar).
                // Directions N0xE0[i0].
                for (i0 = 0; i0 < 3; ++i0) {
                    dir = N0.UnitCross(E0[i0]);
                    ProjectOntoAxis(ref triangle0, ref dir, out min0, out max0);
                    ProjectOntoAxis(ref triangle1, ref dir, out min1, out max1);
                    if (max0 < min1 || max1 < min0) {
                        return false;
                    }
                }

                // Directions N1xE1[i1].
                for (i1 = 0; i1 < 3; ++i1) {
                    dir = N1.UnitCross(E1[i1]);
                    ProjectOntoAxis(ref triangle0, ref dir, out min0, out max0);
                    ProjectOntoAxis(ref triangle1, ref dir, out min1, out max1);
                    if (max0 < min1 || max1 < min0) {
                        return false;
                    }
                }

                // The test query does not know the intersection set.
                Type = IntersectionType.Plane;
            }

            return true;
        }







        public static bool Intersects(ref Triangle3d triangle0, ref Triangle3d triangle1)
        {
            // Get edge vectors for triangle0.
            Vector3dTuple3 E0;
            E0.V0 = triangle0.V1 - triangle0.V0;
            E0.V1 = triangle0.V2 - triangle0.V1;
            E0.V2 = triangle0.V0 - triangle0.V2;

            // Get normal vector of triangle0.
            Vector3d N0 = E0.V0.UnitCross(ref E0.V1);

            // Project triangle1 onto normal line of triangle0, test for separation.
            double N0dT0V0 = N0.Dot(ref triangle0.V0);
            double min1, max1;
            ProjectOntoAxis(ref triangle1, ref N0, out min1, out max1);
            if (N0dT0V0 < min1 || N0dT0V0 > max1) {
                return false;
            }

            // Get edge vectors for triangle1.
            Vector3dTuple3 E1;
            E1.V0 = triangle1.V1 - triangle1.V0;
            E1.V1 = triangle1.V2 - triangle1.V1;
            E1.V2 = triangle1.V0 - triangle1.V2;

            // Get normal vector of triangle1.
            Vector3d N1 = E1.V0.UnitCross(ref E1.V1);

            Vector3d dir;
            double min0, max0;
            int i0, i1;

            Vector3d N0xN1 = N0.UnitCross(ref N1);
            if (N0xN1.Dot(ref N0xN1) >= MathUtil.ZeroTolerance) {
                // Triangles are not parallel.

                // Project triangle0 onto normal line of triangle1, test for
                // separation.
                double N1dT1V0 = N1.Dot(ref triangle1.V0);
                ProjectOntoAxis(ref triangle0, ref N1, out min0, out max0);
                if (N1dT1V0 < min0 || N1dT1V0 > max0) {
                    return false;
                }

                // Directions E0[i0]xE1[i1].
                for (i1 = 0; i1 < 3; ++i1) {
                    for (i0 = 0; i0 < 3; ++i0) {
                        dir = E0[i0].UnitCross(E1[i1]);  // could pass ref if we reversed these...need to negate?
                        ProjectOntoAxis(ref triangle0, ref dir, out min0, out max0);
                        ProjectOntoAxis(ref triangle1, ref dir, out min1, out max1);
                        if (max0 < min1 || max1 < min0) {
                            return false;
                        }
                    }
                }

            } else { // Triangles are parallel (and, in fact, coplanar).
                // Directions N0xE0[i0].
                for (i0 = 0; i0 < 3; ++i0) {
                    dir = N0.UnitCross(E0[i0]);
                    ProjectOntoAxis(ref triangle0, ref dir, out min0, out max0);
                    ProjectOntoAxis(ref triangle1, ref dir, out min1, out max1);
                    if (max0 < min1 || max1 < min0) {
                        return false;
                    }
                }

                // Directions N1xE1[i1].
                for (i1 = 0; i1 < 3; ++i1) {
                    dir = N1.UnitCross(E1[i1]);
                    ProjectOntoAxis(ref triangle0, ref dir, out min0, out max0);
                    ProjectOntoAxis(ref triangle1, ref dir, out min1, out max1);
                    if (max0 < min1 || max1 < min0) {
                        return false;
                    }
                }
            }

            return true;
        }













        static public void ProjectOntoAxis ( ref Triangle3d triangle, ref Vector3d axis, out double fmin, out double fmax)
        {
            double dot0 = axis.Dot(triangle.V0);
            double dot1 = axis.Dot(triangle.V1);
            double dot2 = axis.Dot(triangle.V2);

            fmin = dot0;
            fmax = fmin;

            if (dot1 < fmin)
            {
                fmin = dot1;
            }
            else if (dot1 > fmax)
            {
                fmax = dot1;
            }

            if (dot2 < fmin)
            {
                fmin = dot2;
            }
            else if (dot2 > fmax)
            {
                fmax = dot2;
            }
        }



        static public void TrianglePlaneRelations ( ref Triangle3d triangle, ref Plane3d plane,
            out Vector3d distance, out Index3i sign, out int positive, out int negative, out int zero)
        {
            // Compute the signed distances of triangle vertices to the plane.  Use
            // an epsilon-thick plane test.
            positive = 0;
            negative = 0;
            zero = 0;
            distance = Vector3d.Zero;
            sign = Index3i.Zero;
            for (int i = 0; i < 3; ++i) {
                distance[i] = plane.DistanceTo(triangle[i]);
                if (distance[i] > MathUtil.ZeroTolerance) {
                    sign[i] = 1;
                    positive++;
                } else if (distance[i] < -MathUtil.ZeroTolerance) {
                    sign[i] = -1;
                    negative++;
                } else {
                    distance[i] = (double)0;
                    sign[i] = 0;
                    zero++;
                }
            }
        }




        bool ContainsPoint ( ref Triangle3d triangle, ref Plane3d plane, Vector3d point)
        {
            // Generate a coordinate system for the plane.  The incoming triangle has
            // vertices <V0,V1,V2>.  The incoming plane has unit-length normal N.
            // The incoming point is P.  V0 is chosen as the origin for the plane. The
            // coordinate axis directions are two unit-length vectors, U0 and U1,
            // constructed so that {U0,U1,N} is an orthonormal set.  Any point Q
            // in the plane may be written as Q = V0 + x0*U0 + x1*U1.  The coordinates
            // are computed as x0 = Dot(U0,Q-V0) and x1 = Dot(U1,Q-V0).
            Vector3d U0 = Vector3d.Zero, U1 = Vector3d.Zero;
            Vector3d.GenerateComplementBasis(ref U0, ref U1, plane.Normal);

            // Compute the planar coordinates for the points P, V1, and V2.  To
            // simplify matters, the origin is subtracted from the points, in which
            // case the planar coordinates are for P-V0, V1-V0, and V2-V0.
            Vector3d PmV0 = point - triangle[0];
            Vector3d V1mV0 = triangle[1] - triangle[0];
            Vector3d V2mV0 = triangle[2] - triangle[0];

            // The planar representation of P-V0.
            Vector2d ProjP = new Vector2d(U0.Dot(PmV0), U1.Dot(PmV0));

            // The planar representation of the triangle <V0-V0,V1-V0,V2-V0>.
            Vector2dTuple3 ProjV = new Vector2dTuple3(
                Vector2d.Zero,
                new Vector2d(U0.Dot(V1mV0), U1.Dot(V1mV0)),
                new Vector2d(U0.Dot(V2mV0), U1.Dot(V2mV0)));

            // Test whether P-V0 is in the triangle <0,V1-V0,V2-V0>.
            QueryTuple2d query = new QueryTuple2d(ProjV);
            if ( query.ToTriangle(ProjP,0,1,2) <= 0 ) {
                Result = IntersectionResult.Intersects;
                Type = IntersectionType.Point;
                Quantity = 1;
                Points[0] = point;
                return true;
            }

            return false;
        }





        bool IntersectsSegment ( ref Plane3d plane, ref Triangle3d triangle, Vector3d end0, Vector3d end1)
        {
            // Compute the 2D representations of the triangle vertices and the
            // segment endpoints relative to the plane of the triangle.  Then
            // compute the intersection in the 2D space.

            // Project the triangle and segment onto the coordinate plane most
            // aligned with the plane normal.
            int maxNormal = 0;
            double fmax = Math.Abs(plane.Normal.x);
            double absMax = Math.Abs(plane.Normal.y);
            if (absMax > fmax) {
                maxNormal = 1;
                fmax = absMax;
            }
            absMax = Math.Abs(plane.Normal.z);
            if (absMax > fmax) {
                maxNormal = 2;
            }

            Triangle2d projTri = new Triangle2d();
            Vector2d projEnd0 = Vector2d.Zero, projEnd1 = Vector2d.Zero;
            int i;

            if (maxNormal == 0) {
                // Project onto yz-plane.
                for (i = 0; i < 3; ++i) {
                    projTri[i] = triangle[i].yz;
                    projEnd0.x = end0.y;
                    projEnd0.y = end0.z;
                    projEnd1.x = end1.y;
                    projEnd1.y = end1.z;
                }
            } else if (maxNormal == 1) {
                // Project onto xz-plane.
                for (i = 0; i < 3; ++i) {
                    projTri[i] = triangle[i].xz;
                    projEnd0.x = end0.x;
                    projEnd0.y = end0.z;
                    projEnd1.x = end1.x;
                    projEnd1.y = end1.z;
                }
            } else {
                // Project onto xy-plane.
                for (i = 0; i < 3; ++i) {
                    projTri[i] = triangle[i].xy;
                    projEnd0.x = end0.x;
                    projEnd0.y = end0.y;
                    projEnd1.x = end1.x;
                    projEnd1.y = end1.y;
                }
            }

            Segment2d projSeg = new Segment2d(projEnd0, projEnd1);
            IntrSegment2Triangle2 calc = new IntrSegment2Triangle2(projSeg, projTri);
            if ( ! calc.Find() ) {
                return false;
            }

            Vector2dTuple2 intr = new Vector2dTuple2();
            if (calc.Type == IntersectionType.Segment) {
                Result = IntersectionResult.Intersects;
                Type = IntersectionType.Segment;
                Quantity = 2;
                intr.V0 = calc.Point0;
                intr.V1 = calc.Point1;
            } else {
                Debug.Assert(calc.Type == IntersectionType.Point);
                //"Intersection must be a point\n";
                Result = IntersectionResult.Intersects;
                Type = IntersectionType.Point;
                Quantity = 1;
                intr.V0 = calc.Point0;
            }

            // Unproject the segment of intersection.
            if (maxNormal == 0) {
                double invNX = ((double)1)/plane.Normal.x;
                for (i = 0; i < Quantity; ++i) {
                    double y = intr[i].x;
                    double z = intr[i].y;
                    double x = invNX*(plane.Constant - plane.Normal.y*y - plane.Normal.z*z);
                    Points[i] = new Vector3d(x, y, z);
                }
            } else if (maxNormal == 1) {
                double invNY = ((double)1)/plane.Normal.y;
                for (i = 0; i < Quantity; ++i) {
                    double x = intr[i].x;
                    double z = intr[i].y;
                    double y = invNY*(plane.Constant - plane.Normal.x*x - plane.Normal.z*z);
                    Points[i] = new Vector3d(x, y, z);
                }
            } else {
                double invNZ = ((double)1)/plane.Normal.z;
                for (i = 0; i < Quantity; ++i) {
                    double x = intr[i].x;
                    double y = intr[i].y;
                    double z = invNZ*(plane.Constant - plane.Normal.x*x - plane.Normal.y*y);
                    Points[i] = new Vector3d(x, y, z);
                }
            }

            return true;
        }




        bool GetCoplanarIntersection ( ref Plane3d plane, ref Triangle3d tri0, ref Triangle3d tri1)
        {
            // Project triangles onto coordinate plane most aligned with plane
            // normal.
            int maxNormal = 0;
            double fmax = Math.Abs(plane.Normal.x);
            double absMax = Math.Abs(plane.Normal.y);
            if (absMax > fmax) {
                maxNormal = 1;
                fmax = absMax;
            }
            absMax = Math.Abs(plane.Normal.z);
            if (absMax > fmax) {
                maxNormal = 2;
            }

            Triangle2d projTri0 = new Triangle2d(), projTri1 = new Triangle2d();
            int i;

            if (maxNormal == 0) {
                // Project onto yz-plane.
                for (i = 0; i < 3; ++i) {
                    projTri0[i] = tri0[i].yz;
                    projTri1[i] = tri1[i].yz;
                }
            } else if (maxNormal == 1) {
                // Project onto xz-plane.
                for (i = 0; i < 3; ++i) {
                    projTri0[i] = tri0[i].xz;
                    projTri1[i] = tri1[i].xz;
                }
            } else {
                // Project onto xy-plane.
                for (i = 0; i < 3; ++i) {
                    projTri0[i] = tri0[i].xy;
                    projTri1[i] = tri1[i].xy;
                }
            }

            // 2D triangle intersection routines require counterclockwise ordering.
            Vector2d save;
            Vector2d edge0 = projTri0[1] - projTri0[0];
            Vector2d edge1 = projTri0[2] - projTri0[0];
            if (edge0.DotPerp(edge1) < (double)0) {
                // Triangle is clockwise, reorder it.
                save = projTri0[1];
                projTri0[1] = projTri0[2];
                projTri0[2] = save;
            }

            edge0 = projTri1[1] - projTri1[0];
            edge1 = projTri1[2] - projTri1[0];
            if (edge0.DotPerp(edge1) < (double)0) {
                // Triangle is clockwise, reorder it.
                save = projTri1[1];
                projTri1[1] = projTri1[2];
                projTri1[2] = save;
            }

            IntrTriangle2Triangle2 intr = new IntrTriangle2Triangle2(projTri0,projTri1);
            if (!intr.Find()) {
                return false;
            }

            PolygonPoints = new Vector3d[intr.Quantity];

            // Map 2D intersections back to the 3D triangle space.
            Quantity = intr.Quantity;
            if (maxNormal == 0) {
                double invNX = ((double)1)/plane.Normal.x;
                for (i = 0; i < Quantity; i++) {
                    double y = intr.Points[i].x;
                    double z = intr.Points[i].y;
                    double x = invNX*(plane.Constant - plane.Normal.y*y - plane.Normal.z*z);
                    PolygonPoints[i] = new Vector3d(x, y, z);
                }
            } else if (maxNormal == 1) {
                double invNY = ((double)1)/plane.Normal.y;
                for (i = 0; i < Quantity; i++) {
                    double x = intr.Points[i].x;
                    double z = intr.Points[i].y;
                    double y = invNY*(plane.Constant - plane.Normal.x*x - plane.Normal.z*z);
                    PolygonPoints[i] = new Vector3d(x, y, z);
                }
            } else{
                double invNZ = ((double)1)/plane.Normal.z;
                for (i = 0; i < Quantity; i++) {
                    double x = intr.Points[i].x;
                    double y = intr.Points[i].y;
                    double z = invNZ*(plane.Constant - plane.Normal.x*x - plane.Normal.y*y);
                    PolygonPoints[i] = new Vector3d(x, y, z);
                }
            }

            Result = IntersectionResult.Intersects;
            Type = IntersectionType.Polygon;
            return true;
        }





    }
}
