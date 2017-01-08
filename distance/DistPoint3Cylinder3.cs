using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    // ported from GTEngine
    // https://www.geometrictools.com/Downloads/Downloads.html
    // However, code is modified to compute signed distance, instead of distance
    // to cylinder solid (which is 0 inside cylinder). If you want solid distance,
    // check IsInside, and if true, distance is 0 and point is input point.
    // SolidDistance will return this distance for you, but you have to do
    // the Point classification yourself.
    //
    // DistanceSquared is always positive!!
    //
    public class DistPoint3Cylinder3
    {
        Vector3d point;
        public Vector3d Point
        {
            get { return point; }
            set { point = value; DistanceSquared = -1.0; }
        }

        Cylinder3d cylinder;
        public Cylinder3d Cylinder
        {
            get { return cylinder; }
            set { cylinder = value; DistanceSquared = -1.0; }
        }

        public double DistanceSquared = -1.0;

        // negative on inside
        public double SignedDistance = 0.0f;

        public bool IsInside { get { return SignedDistance < 0; } }
        public double SolidDistance { get { return (SignedDistance < 0) ? 0 : SignedDistance; } }

        public Vector3d CylinderClosest;

        public DistPoint3Cylinder3(Vector3d PointIn, Cylinder3d CylinderIn )
        {
            point = PointIn; cylinder = CylinderIn;
        }

        public DistPoint3Cylinder3 Compute()
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


            if (cylinder.Height >= double.MaxValue)
                return get_squared_infinite();


            // Convert the point to the cylinder coordinate system.  In this system,
            // the point believes (0,0,0) is the cylinder axis origin and (0,0,1) is
            // the cylinder axis direction.
            Vector3d basis0 = cylinder.Axis.Direction;
            Vector3d basis1 = Vector3d.Zero, basis2 = Vector3d.Zero;
            Vector3d.ComputeOrthogonalComplement(1, basis0, ref basis1, ref basis2);
            double height = Cylinder.Height / 2.0;

            Vector3d delta = point - cylinder.Axis.Origin;
            Vector3d P = new Vector3d(basis1.Dot(delta), basis2.Dot(delta), basis0.Dot(delta));

            double result_distance = 0;     // signed!
            Vector3d result_closest = Vector3d.Zero;

            double sqrRadius = cylinder.Radius * cylinder.Radius;
            double sqrDistance = P[0] * P[0] + P[1] * P[1];

            // The point is outside the infinite cylinder, or on the cylinder wall.
            double distance = Math.Sqrt(sqrDistance);
            double inf_distance = distance - Cylinder.Radius;
            double temp = Cylinder.Radius / distance;
            Vector3d inf_closest = new Vector3d(temp * P.x, temp * P.y, P.z);
            bool bOutside = (sqrDistance >= sqrRadius);

            result_closest = inf_closest;
            result_distance = inf_distance;

            if ( inf_closest.z >= height ) {
                result_closest = (bOutside) ? inf_closest : P;
                result_closest.z = height;
                result_distance = result_closest.Distance(P);       // TODO: only compute sqr here
                bOutside = true;
            } else if ( inf_closest.z <= -height ) {
                result_closest = (bOutside) ? inf_closest : P;
                result_closest.z = -height;
                result_distance = result_closest.Distance(P);       // TODO: only compute sqr here
                bOutside = true;
            } else if ( bOutside == false ) {
                if (inf_closest.z > 0 && Math.Abs(inf_closest.z - height) < Math.Abs(inf_distance)) {
                    result_closest = P;
                    result_closest.z = height;
                    result_distance = result_closest.Distance(P);       // TODO: only compute sqr here
                } else if ( inf_closest.z < 0 && Math.Abs(inf_closest.z - -height) < Math.Abs(inf_distance) ) {
                    result_closest = P;
                    result_closest.z = -height;
                    result_distance = result_closest.Distance(P);       // TODO: only compute sqr here
                }
            } 
            SignedDistance = (bOutside) ? Math.Abs(result_distance) : -Math.Abs(result_distance);

            // Convert the closest point from the cylinder coordinate system to the
            // original coordinate system.
            CylinderClosest = cylinder.Axis.Origin +
                result_closest.x * basis1 +
                result_closest.y * basis2 +
                result_closest.z * basis0;

            DistanceSquared = result_distance * result_distance;

            return DistanceSquared;
        }



        public double get_squared_infinite()
        {
            // Convert the point to the cylinder coordinate system.  In this system,
            // the point believes (0,0,0) is the cylinder axis origin and (0,0,1) is
            // the cylinder axis direction.
            Vector3d basis0 = cylinder.Axis.Direction;
            Vector3d basis1 = Vector3d.Zero, basis2 = Vector3d.Zero;
            Vector3d.ComputeOrthogonalComplement(1, basis0, ref basis1, ref basis2);

            Vector3d delta = point - cylinder.Axis.Origin;
            Vector3d P = new Vector3d(basis1.Dot(delta), basis2.Dot(delta), basis0.Dot(delta));

            double result_distance = 0;     // signed!
            Vector3d result_closest = Vector3d.Zero;

            double sqrDistance = P[0] * P[0] + P[1] * P[1];

            // The point is outside the cylinder or on the cylinder wall.
            double distance = Math.Sqrt(sqrDistance);
            result_distance = distance - Cylinder.Radius;
            double temp = Cylinder.Radius / distance;
            result_closest = new Vector3d(temp * P.x, temp * P.y, P.z);


            // Convert the closest point from the cylinder coordinate system to the
            // original coordinate system.
            CylinderClosest = cylinder.Axis.Origin +
                result_closest.x * basis1 +
                result_closest.y * basis2 +
                result_closest.z * basis0;
            SignedDistance = result_distance;
            DistanceSquared = result_distance * result_distance;
            return DistanceSquared;
        }

    }
}
