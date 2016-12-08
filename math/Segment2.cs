using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    public struct Segment2d
    {
        // Center-direction-extent representation.
        public Vector2d Center;
        public Vector2d Direction;
        public double Extent;

        public Segment2d(Vector2d p0, Vector2d p1)
        {
            //update_from_endpoints(p0, p1);
            Center = 0.5 * (p0 + p1);
            Direction = p1 - p0;
            Extent = 0.5 * Direction.Normalize();
        }
        public Segment2d(Vector2d center, Vector2d direction, double extent)
        {
            Center = center; Direction = direction; Extent = extent;
        }

        public Vector2d P0
        {
            get { return Center - Extent * Direction; }
            set { update_from_endpoints(value, P1); }
        }
        public Vector2d P1
        {
            get { return Center + Extent * Direction; }
            set { update_from_endpoints(P0, value); }
        }


        void update_from_endpoints(Vector2d p0, Vector2d p1)
        {
            Center = 0.5 * (p0 + p1);
            Direction = p1 - p0;
            Extent = 0.5 * Direction.Normalize();
        }
    }



    public struct Segment2f
    {
        // Center-direction-extent representation.
        public Vector2f Center;
        public Vector2f Direction;
        public float Extent;

        public Segment2f(Vector2f p0, Vector2f p1)
        {
            //update_from_endpoints(p0, p1);
            Center = 0.5f * (p0 + p1);
            Direction = p1 - p0;
            Extent = 0.5f * Direction.Normalize();
        }
        public Segment2f(Vector2f center, Vector2f direction, float extent)
        {
            Center = center; Direction = direction; Extent = extent;
        }

        public Vector2f P0
        {
            get { return Center - Extent * Direction; }
            set { update_from_endpoints(value, P1); }
        }
        public Vector2f P1
        {
            get { return Center + Extent * Direction; }
            set { update_from_endpoints(P0, value); }
        }


        void update_from_endpoints(Vector2f p0, Vector2f p1)
        {
            Center = 0.5f * (p0 + p1);
            Direction = p1 - p0;
            Extent = 0.5f * Direction.Normalize();
        }
    }

}
