using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    public class Segment3d
    {
        // Center-direction-extent representation.
        public Vector3d Center;
        public Vector3d Direction;
        public double Extent;

        public Segment3d(Vector3d p0, Vector3d p1) {
            update_from_endpoints(p0, p1);
        }
        public Segment3d(Vector3d center, Vector3d direction, double extent) {
            Center = center; Direction = direction; Extent = extent;
        }

        public void SetEndpoints(Vector3d p0, Vector3d p1) {
            update_from_endpoints(p0, p1);
        }


        public Vector3d P0 {
            get { return Center - Extent * Direction; }
            set { update_from_endpoints(value, P1); }
        }
        public Vector3d P1 {
            get { return Center + Extent * Direction; }
            set { update_from_endpoints(P0, value); }
        }

        // parameter is signed distance from center in direction
        public Vector3d PointAt(double d) {
            return Center + d * Direction;
        }

        // t ranges from [0,1] over [P0,P1]
        public Vector3d PointBetween(double t) {
            return Center + (2 * t - 1) * Extent * Direction;
        }


        void update_from_endpoints(Vector3d p0, Vector3d p1) {
            Center = 0.5 * (p0 + p1);
            Direction = p1 - p0;
            Extent = 0.5* Direction.Normalize();
        }
    }



    public class Segment3f
    {
        // Center-direction-extent representation.
        public Vector3f Center;
        public Vector3f Direction;
        public float Extent;

        public Segment3f(Vector3f p0, Vector3f p1)
        {
            update_from_endpoints(p0, p1);
        }
        public Segment3f(Vector3f center, Vector3f direction, float extent)
        {
            Center = center; Direction = direction; Extent = extent;
        }


        public void SetEndpoints(Vector3f p0, Vector3f p1) {
            update_from_endpoints(p0, p1);
        }


        public Vector3f P0
        {
            get { return Center - Extent * Direction; }
            set { update_from_endpoints(value, P1); }
        }
        public Vector3f P1
        {
            get { return Center + Extent * Direction; }
            set { update_from_endpoints(P0, value); }
        }

        // parameter is signed distance from center in direction
        public Vector3f PointAt(float d) {
            return Center + d * Direction;
        }


        // t ranges from [0,1] over [P0,P1]
        public Vector3f PointBetween(float t) {
            return Center + (2 * t - 1) * Extent * Direction;
        }



        void update_from_endpoints(Vector3f p0, Vector3f p1)
        {
            Center = 0.5f * (p0 + p1);
            Direction = p1 - p0;
            Extent = 0.5f * Direction.Normalize();
        }
    }

}
