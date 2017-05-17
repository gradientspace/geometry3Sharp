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
        public double Length {
            get { return 2 * Extent; }
        }

		public Vector2d Endpoint(int i) {
			return (i == 0) ? (Center - Extent * Direction) : (Center + Extent * Direction);
		}

		// parameter is signed distance from center in direction
		public Vector2d PointAt(double d) {
			return Center + d * Direction;
		}

		// t ranges from [0,1] over [P0,P1]
		public Vector2d PointBetween(double t) {
			return Center + (2 * t - 1) * Extent * Direction;
		}

		public double DistanceSquared(Vector2d p)
		{
			double t = (p - Center).Dot(Direction);
			if ( t >= Extent )
				return P1.DistanceSquared(p);
			else if ( t <= -Extent )
				return P0.DistanceSquared(p);
			Vector2d proj = Center + t * Direction;
			return (proj - p).LengthSquared;
		}

        public Vector2d NearestPoint(Vector2d p)
        {
			double t = (p - Center).Dot(Direction);
            if (t >= Extent)
                return P1;
            if (t <= -Extent)
                return P0;
			return Center + t * Direction;
        }

        public double Project(Vector2d p)
        {
            return (p - Center).Dot(Direction);
        }

        void update_from_endpoints(Vector2d p0, Vector2d p1)
        {
            Center = 0.5 * (p0 + p1);
            Direction = p1 - p0;
            Extent = 0.5 * Direction.Normalize();
        }


		// IParametricCurve2d interface

		public bool IsClosed { get { return false; } }

		public double ParamLength { get { return 1.0f; } }

		// t in range[0,1] spans arc
		public Vector2d SampleT(double t) {
			return Center + (2 * t - 1) * Extent * Direction;
		}

		public Vector2d TangentT(double t) {
            return Direction;
		}

		public bool HasArcLength { get { return true; } }
		public double ArcLength { get { return 2*Extent; } }

		public Vector2d SampleArcLength(double a) {
			return P0 + a * Direction;
		}

		public void Reverse() {
			update_from_endpoints(P1,P0);
		}

        public IParametricCurve2d Clone() {
            return new Segment2d(this.Center, this.Direction, this.Extent);
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
        public float Length {
            get { return 2 * Extent; }
        }


		// parameter is signed distance from center in direction
		public Vector2f PointAt(float d) {
			return Center + d * Direction;
		}

		// t ranges from [0,1] over [P0,P1]
		public Vector2f PointBetween(float t) {
			return Center + (2.0f * t - 1.0f) * Extent * Direction;
		}

		public float DistanceSquared(Vector2f p)
		{
			float t = (p - Center).Dot(Direction);
			if ( t >= Extent )
				return P1.DistanceSquared(p);
			else if ( t <= -Extent )
				return P0.DistanceSquared(p);
			Vector2f proj = Center + t * Direction;
			return (proj - p).LengthSquared;
		}

        public Vector2f NearestPoint(Vector2f p)
        {
			float t = (p - Center).Dot(Direction);
            if (t >= Extent)
                return P1;
            if (t <= -Extent)
                return P0;
			return Center + t * Direction;
        }

        public float Project(Vector2f p)
        {
            return (p - Center).Dot(Direction);
        }



        void update_from_endpoints(Vector2f p0, Vector2f p1)
        {
            Center = 0.5f * (p0 + p1);
            Direction = p1 - p0;
            Extent = 0.5f * Direction.Normalize();
        }
    }

}
