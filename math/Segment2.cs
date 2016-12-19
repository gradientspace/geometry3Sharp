using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
	public struct Segment2d : IParametricCurve2d
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

		// parameter is signed distance from center in direction
		public Vector2d PointAt(double d) {
			return Center + d * Direction;
		}

		// t ranges from [0,1] over [P0,P1]
		public Vector2d PointBetween(double t) {
			return Center + (2 * t - 1) * Extent * Direction;
		}

        void update_from_endpoints(Vector2d p0, Vector2d p1)
        {
            Center = 0.5 * (p0 + p1);
            Direction = p1 - p0;
            Extent = 0.5 * Direction.Normalize();
        }

		public double DistanceSquared(Vector2d p)
		{
			double t = (p - Center).Dot(Direction);
			if ( t >= Extent )
				return P1.SquaredDist(p);
			else if ( t <= Extent )
				return P0.SquaredDist(p);
			Vector2d proj = Center + t * Direction;
			return (proj - p).LengthSquared;
		}


		// IParametricCurve2d interface

		public bool IsClosed { get { return false; } }

		public double ParamLength { get { return 1.0f; } }

		// t in range[0,1] spans arc
		public Vector2d SampleT(double t) {
			return Center + (2 * t - 1) * Extent * Direction;
		}

		public bool HasArcLength { get { return true; } }
		public double ArcLength { get { return 2*Extent; } }

		public Vector2d SampleArcLength(double a) {
			return P0 + a * Direction;
		}

		public void Reverse() {
			update_from_endpoints(P1,P0);
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
