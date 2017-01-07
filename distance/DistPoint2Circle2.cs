using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    // ported from WildMagic 5's  DistPoint3Circle3  (didn't have point2circle2)
    // https://www.geometrictools.com/Downloads/Downloads.html

    public class DistPoint2Circle2
    {
        Vector2d point;
        public Vector2d Point
        {
            get { return point; }
            set { point = value; DistanceSquared = -1.0; }
        }

        Circle2d circle;
        public Circle2d Circle
        {
            get { return circle; }
            set { circle = value; DistanceSquared = -1.0; }
        }

        public double DistanceSquared = -1.0;

        public Vector2d CircleClosest;
        public bool AllCirclePointsEquidistant;


        public DistPoint2Circle2(Vector2d PointIn, Circle2d circleIn )
        {
            point = PointIn; circle = circleIn;
        }

        public DistPoint2Circle2 Compute()
        {
            GetSquared();
            return this;
        }

        public double Get()
        {
            return Math.Sqrt(GetSquared());
        }


        public double GetSquared()
        {
            if (DistanceSquared >= 0)
                return DistanceSquared;

            // Projection of P-C onto plane is Q-C = P-C - Dot(N,P-C)*N.
            Vector2d PmC = point - circle.Center;
            double lengthPmC = PmC.Length;
            if (lengthPmC > MathUtil.Epsilon) {
                CircleClosest = circle.Center + circle.Radius * PmC / lengthPmC;
                AllCirclePointsEquidistant = false;
            } else {
                // All circle points are equidistant from P.  Return one of them.
                CircleClosest = circle.Center + circle.Radius;
                AllCirclePointsEquidistant = true;
            }

            Vector2d diff = point - CircleClosest;
            double sqrDistance = diff.Dot(diff);

            // Account for numerical round-off error.
            if (sqrDistance < 0) {
                sqrDistance = 0;
            }
            DistanceSquared = sqrDistance;
            return sqrDistance;
        }
    }
}
