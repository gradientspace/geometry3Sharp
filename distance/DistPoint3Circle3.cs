using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    // ported from WildMagic 5
    // https://www.geometrictools.com/Downloads/Downloads.html

    public class DistPoint3Circle3
    {
        Vector3d point;
        public Vector3d Point
        {
            get { return point; }
            set { point = value; DistanceSquared = -1.0; }
        }

        Circle3d circle;
        public Circle3d Circle
        {
            get { return circle; }
            set { circle = value; DistanceSquared = -1.0; }
        }

        public double DistanceSquared = -1.0;

        public Vector3d CircleClosest;
        public bool AllCirclePointsEquidistant;


        public DistPoint3Circle3(Vector3d PointIn, Circle3d circleIn )
        {
            point = PointIn; circle = circleIn;
        }

        public DistPoint3Circle3 Compute()
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
            Vector3d PmC = point - circle.Center;
            Vector3d QmC = PmC - circle.Normal.Dot(PmC) * circle.Normal;
            double lengthQmC = QmC.Length;
            if (lengthQmC > MathUtil.Epsilon) {
                CircleClosest = circle.Center + circle.Radius * QmC / lengthQmC;
                AllCirclePointsEquidistant = false;
            } else {
                // All circle points are equidistant from P.  Return one of them.
                CircleClosest = circle.Center + circle.Radius * circle.PlaneX;
                AllCirclePointsEquidistant = true;
            }

            Vector3d diff = point - CircleClosest;
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
