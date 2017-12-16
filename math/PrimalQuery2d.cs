using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{

    // Queries about the relation of a point to various geometric objects.  
    // Ported from https://www.geometrictools.com/GTEngine/Include/Mathematics/GtePrimalQuery2.h
    class PrimalQuery2d
    {
        Func<int, Vector2d> PointF;


        public PrimalQuery2d(Func<int, Vector2d> PositionFunc)
        {
            PointF = PositionFunc;
        }


        // In the following, point P refers to vertices[i] or 'test' and Vi refers
        // to vertices[vi].

        // For a line with origin V0 and direction <V0,V1>, ToLine returns
        //   +1, P on right of line
        //   -1, P on left of line
        //    0, P on the line
        public int ToLine(int i, int v0, int v1)
        {
            return ToLine(PointF(i), v0, v1);
        }
        public int ToLine(Vector2d test, int v0, int v1)
        {
            Vector2d vec0 = PointF(v0);
            Vector2d vec1 = PointF(v1);

            double x0 = test[0] - vec0[0];
            double y0 = test[1] - vec0[1];
            double x1 = vec1[0] - vec0[0];
            double y1 = vec1[1] - vec0[1];
            double x0y1 = x0 * y1;
            double x1y0 = x1 * y0;
            double det = x0y1 - x1y0;
            const double zero = 0.0;

            return (det > zero ? +1 : (det < zero ? -1 : 0));
        }

        // For a line with origin V0 and direction <V0,V1>, ToLine returns
        //   +1, P on right of line
        //   -1, P on left of line
        //    0, P on the line
        // The 'order' parameter is
        //   -3, points not collinear, P on left of line
        //   -2, P strictly left of V0 on the line
        //   -1, P = V0
        //    0, P interior to line segment [V0,V1]
        //   +1, P = V1
        //   +2, P strictly right of V0 on the line
        // This is the same as the first-listed ToLine calls because the worst-case
        // path has the same computational complexity.
        public int ToLine(int i, int v0, int v1, out int order)
        {
            return ToLine(PointF(i), v0, v1, out order);

        }
        public int ToLine(Vector2d test, int v0, int v1, out int order)
        {
            Vector2d vec0 = PointF(v0);
            Vector2d vec1 = PointF(v1);

            double x0 = test[0] - vec0[0];
            double y0 = test[1] - vec0[1];
            double x1 = vec1[0] - vec0[0];
            double y1 = vec1[1] - vec0[1];
            double x0y1 = x0 * y1;
            double x1y0 = x1 * y0;
            double det = x0y1 - x1y0;
            const double zero = 0.0;

            if (det > zero) {
                order = +3;
                return +1;
            }

            if (det < zero) {
                order = -3;
                return -1;
            }

            double x0x1 = x0 * x1;
            double y0y1 = y0 * y1;
            double dot = x0x1 + y0y1;
            if (dot == zero) {
                order = -1;
            } else if (dot < zero) {
                order = -2;
            } else {
                double x0x0 = x0 * x0;
                double y0y0 = y0 * y0;
                double sqrLength = x0x0 + y0y0;
                if (dot == sqrLength) {
                    order = +1;
                } else if (dot > sqrLength) {
                    order = +2;
                } else {
                    order = 0;
                }
            }

            return 0;
        }

        // For a triangle with counterclockwise vertices V0, V1, and V2,
        // ToTriangle returns
        //   +1, P outside triangle
        //   -1, P inside triangle
        //    0, P on triangle
        // The query involves three calls to ToLine, so the numbers match those
        // of ToLine.
        public int ToTriangle(int i, int v0, int v1, int v2)
        {
            return ToTriangle(PointF(i), v0, v1, v2);

        }
        public int ToTriangle(Vector2d test, int v0, int v1, int v2)
        {
            int sign0 = ToLine(test, v1, v2);
            if (sign0 > 0) {
                return +1;
            }

            int sign1 = ToLine(test, v0, v2);
            if (sign1 < 0) {
                return +1;
            }

            int sign2 = ToLine(test, v0, v1);
            if (sign2 > 0) {
                return +1;
            }

            return ((sign0 != 0 && sign1 != 0 && sign2 != 0) ? -1 : 0);
        }



        // [RMS] added to handle queries where mesh is not consistently oriented
        // For a triangle with vertices V0, V1, and V2, oriented cw or ccw,
        // ToTriangleUnsigned returns
        //   +1, P outside triangle
        //   -1, P inside triangle
        //    0, P on triangle
        // The query involves three calls to ToLine, so the numbers match those
        // of ToLine.
        public int ToTriangleUnsigned(int i, int v0, int v1, int v2)
        {
            return ToTriangleUnsigned(PointF(i), v0, v1, v2);

        }
        public int ToTriangleUnsigned(Vector2d test, int v0, int v1, int v2)
        {
            int sign0 = ToLine(test, v1, v2);
            int sign1 = ToLine(test, v0, v2);
            int sign2 = ToLine(test, v0, v1);

            // valid sign patterns are -+- and +-+, but also we might
            // have zeros...can't figure out a more clever test right now
            if ( (sign0 <= 0 && sign1 >= 0 && sign2 <= 0) ||
                 (sign0 >= 0 && sign1 <= 0 && sign2 >= 0 )) 
            {
                return ((sign0 != 0 && sign1 != 0 && sign2 != 0) ? -1 : 0);
            }
            return +1;
        }



        // For a triangle with counterclockwise vertices V0, V1, and V2,
        // ToCircumcircle returns
        //   +1, P outside circumcircle of triangle
        //   -1, P inside circumcircle of triangle
        //    0, P on circumcircle of triangle
        // The query involves three calls of ToLine, so the numbers match those
        // of ToLine.
        public int ToCircumcircle(int i, int v0, int v1, int v2)
        {
            return ToCircumcircle(PointF(i), v0, v1, v2);
        }
        public int ToCircumcircle(Vector2d test, int v0, int v1, int v2)
        {
            Vector2d vec0 = PointF(v0);
            Vector2d vec1 = PointF(v1);
            Vector2d vec2 = PointF(v2);

            double x0 = vec0[0] - test[0];
            double y0 = vec0[1] - test[1];
            double s00 = vec0[0] + test[0];
            double s01 = vec0[1] + test[1];
            double t00 = s00 * x0;
            double t01 = s01 * y0;
            double z0 = t00 + t01;

            double x1 = vec1[0] - test[0];
            double y1 = vec1[1] - test[1];
            double s10 = vec1[0] + test[0];
            double s11 = vec1[1] + test[1];
            double t10 = s10 * x1;
            double t11 = s11 * y1;
            double z1 = t10 + t11;

            double x2 = vec2[0] - test[0];
            double y2 = vec2[1] - test[1];
            double s20 = vec2[0] + test[0];
            double s21 = vec2[1] + test[1];
            double t20 = s20 * x2;
            double t21 = s21 * y2;
            double z2 = t20 + t21;

            double y0z1 = y0 * z1;
            double y0z2 = y0 * z2;
            double y1z0 = y1 * z0;
            double y1z2 = y1 * z2;
            double y2z0 = y2 * z0;
            double y2z1 = y2 * z1;
            double c0 = y1z2 - y2z1;
            double c1 = y2z0 - y0z2;
            double c2 = y0z1 - y1z0;
            double x0c0 = x0 * c0;
            double x1c1 = x1 * c1;
            double x2c2 = x2 * c2;
            double term = x0c0 + x1c1;
            double det = term + x2c2;
            const double zero = 0.0;

            return (det < zero ? 1 : (det > zero ? -1 : 0));
        }

        // An extended classification of the relationship of a point to a line
        // segment.  For noncollinear points, the return value is
        //   ORDER_POSITIVE when <P,Q0,Q1> is a counterclockwise triangle
        //   ORDER_NEGATIVE when <P,Q0,Q1> is a clockwise triangle
        // For collinear points, the line direction is Q1-Q0.  The return value is
        //   ORDER_COLLINEAR_LEFT when the line ordering is <P,Q0,Q1>
        //   ORDER_COLLINEAR_RIGHT when the line ordering is <Q0,Q1,P>
        //   ORDER_COLLINEAR_CONTAIN when the line ordering is <Q0,P,Q1>
        public enum OrderType
        {
            ORDER_Q0_EQUALS_Q1,
            ORDER_P_EQUALS_Q0,
            ORDER_P_EQUALS_Q1,
            ORDER_POSITIVE,
            ORDER_NEGATIVE,
            ORDER_COLLINEAR_LEFT,
            ORDER_COLLINEAR_RIGHT,
            ORDER_COLLINEAR_CONTAIN
        };

        // Choice of N for UIntegerFP32<N>.
        //    input type | compute type | N
        //    -----------+--------------+-----
        //    float      | BSNumber     |   18
        //    double     | BSNumber     |  132
        //    float      | BSRational   |  214
        //    double     | BSRational   | 1587
        // This is the same as the first-listed ToLine calls because the worst-case
        // path has the same computational complexity.
        public OrderType ToLineExtended(Vector2d P, Vector2d Q0, Vector2d Q1)
        {
            const double zero = 0.0;

            double x0 = Q1[0] - Q0[0];
            double y0 = Q1[1] - Q0[1];
            if (x0 == zero && y0 == zero) {
                return OrderType.ORDER_Q0_EQUALS_Q1;
            }

            double x1 = P[0] - Q0[0];
            double y1 = P[1] - Q0[1];
            if (x1 == zero && y1 == zero) {
                return OrderType.ORDER_P_EQUALS_Q0;
            }

            double x2 = P[0] - Q1[0];
            double y2 = P[1] - Q1[1];
            if (x2 == zero && y2 == zero) {
                return OrderType.ORDER_P_EQUALS_Q1;
            }

            // The theoretical classification relies on computing exactly the sign of
            // the determinant.  Numerical roundoff errors can cause misclassification.
            double x0y1 = x0 * y1;
            double x1y0 = x1 * y0;
            double det = x0y1 - x1y0;

            if (det != zero) {
                if (det > zero) {
                    // The points form a counterclockwise triangle <P,Q0,Q1>.
                    return OrderType.ORDER_POSITIVE;
                } else {
                    // The points form a clockwise triangle <P,Q1,Q0>.
                    return OrderType.ORDER_NEGATIVE;
                }
            } else {
                // The points are collinear; P is on the line through Q0 and Q1.
                double x0x1 = x0 * x1;
                double y0y1 = y0 * y1;
                double dot = x0x1 + y0y1;
                if (dot < zero) {
                    // The line ordering is <P,Q0,Q1>.
                    return OrderType.ORDER_COLLINEAR_LEFT;
                }

                double x0x0 = x0 * x0;
                double y0y0 = y0 * y0;
                double sqrLength = x0x0 + y0y0;
                if (dot > sqrLength) {
                    // The line ordering is <Q0,Q1,P>.
                    return OrderType.ORDER_COLLINEAR_RIGHT;
                }

                // The line ordering is <Q0,P,Q1> with P strictly between Q0 and Q1.
                return OrderType.ORDER_COLLINEAR_CONTAIN;
            }
        }
    }

}
