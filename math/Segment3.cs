using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
	public struct Segment3d : IParametricCurve3d
    {
        // Center-direction-extent representation.
        // Extent is half length of segment
        public Vector3d Center;
        public Vector3d Direction;
        public double Extent;

        public Segment3d(Vector3d p0, Vector3d p1) {
            //update_from_endpoints(p0, p1);
            Center = 0.5 * (p0 + p1);
            Direction = p1 - p0;
            Extent = 0.5 * Direction.Normalize();
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
        public double Length {
            get { return 2 * Extent; }
        }

        // parameter is signed distance from center in direction
        public Vector3d PointAt(double d) {
            return Center + d * Direction;
        }

        // t ranges from [0,1] over [P0,P1]
        public Vector3d PointBetween(double t) {
            return Center + (2 * t - 1) * Extent * Direction;
        }


		public double DistanceSquared(Vector3d p)
		{
			double t = (p - Center).Dot(Direction);
			if ( t >= Extent )
				return P1.DistanceSquared(p);
			else if ( t <= -Extent )
				return P0.DistanceSquared(p);
			Vector3d proj = Center + t * Direction;
			return (proj - p).LengthSquared;
		}
        public double DistanceSquared(Vector3d p, out double t)
        {
            t = (p - Center).Dot(Direction);
            if (t >= Extent) {
                t = Extent;
                return P1.DistanceSquared(p);
            } else if (t <= -Extent) {
                t = -Extent;
                return P0.DistanceSquared(p);
            }
            Vector3d proj = Center + t * Direction;
            return (proj - p).LengthSquared;
        }


        public Vector3d NearestPoint(Vector3d p)
        {
			double t = (p - Center).Dot(Direction);
            if (t >= Extent)
                return P1;
            if (t <= -Extent)
                return P0;
			return Center + t * Direction;
        }

        public double Project(Vector3d p)
        {
            return (p - Center).Dot(Direction);
        }


        void update_from_endpoints(Vector3d p0, Vector3d p1) {
            Center = 0.5 * (p0 + p1);
            Direction = p1 - p0;
            Extent = 0.5* Direction.Normalize();
        }


		// IParametricCurve3d interface

		public bool IsClosed { get { return false; } }

		public double ParamLength { get { return 1.0f; } }

		// t in range[0,1] spans arc
		public Vector3d SampleT(double t) {
			return Center + (2 * t - 1) * Extent * Direction;
		}

		public Vector3d TangentT(double t) {
			return Direction;
		}

		public bool HasArcLength { get { return true; } }
		public double ArcLength { get { return 2*Extent; } }

		public Vector3d SampleArcLength(double a) {
			return P0 + a * Direction;
		}

		public void Reverse() {
			update_from_endpoints(P1,P0);
		}

		public IParametricCurve3d Clone() {
			return new Segment3d(this.Center, this.Direction, this.Extent);
		}


    }


}
