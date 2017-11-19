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
            return proj.DistanceSquared(p);
		}
        public double DistanceSquared(Vector2d p, out double t)
        {
            t = (p - Center).Dot(Direction);
            if (t >= Extent) {
                t = Extent;
                return P1.DistanceSquared(p);
            } else if (t <= -Extent) {
                t = -Extent;
                return P0.DistanceSquared(p);
            }
            Vector2d proj = Center + t * Direction;
            return proj.DistanceSquared(p);
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




        /// <summary>
        /// Returns:
        ///   +1, on right of line
        ///   -1, on left of line
        ///    0, on the line
        /// </summary>
        public int WhichSide(Vector2d test, double tol = 0)
        {
            // [TODO] subtract Center from test?
            Vector2d vec0 = Center + Extent * Direction;
            Vector2d vec1 = Center - Extent * Direction;
            double x0 = test.x - vec0.x;
            double y0 = test.y - vec0.y;
            double x1 = vec1.x - vec0.x;
            double y1 = vec1.y - vec0.y;
            double det = x0 * y1 - x1 * y0;
            return (det > tol ? +1 : (det < -tol ? -1 : 0));
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

        public bool IsTransformable { get { return true; } }
        public void Transform(ITransform2 xform)
        {
            Center = xform.TransformP(Center);
            Direction = xform.TransformN(Direction);
            Extent = xform.TransformScalar(Extent);
        }



        /// <summary>
        /// distance from pt to segment (a,b), with no square roots
        /// </summary>
        public static double FastDistanceSquared(ref Vector2d a, ref Vector2d b, ref Vector2d pt)
        {
            double vx = b.x - a.x, vy = b.y - a.y;
            double len2 = vx*vx + vy*vy;
            double dx = pt.x - a.x, dy = pt.y - a.y;
            if (len2 < 1e-13) {
                return dx * dx + dy * dy;
            }
            double t = (dx*vx + dy*vy);
            if (t <= 0) {
                return dx * dx + dy * dy;
            } else if (t >= len2) {
                dx = pt.x - b.x; dy = pt.y - b.y;
                return dx * dx + dy * dy;
            }

            dx = pt.x - (a.x + ((t * vx)/len2));
            dy = pt.y - (a.y + ((t * vy)/len2));
            return dx * dx + dy * dy;
        }


        /// <summary>
        /// Returns:
        ///   +1, on right of line
        ///   -1, on left of line
        ///    0, on the line
        /// </summary>
        public static int WhichSide(ref Vector2d a, ref Vector2d b, ref Vector2d test, double tol = 0)
        {
            double x0 = test.x - a.x;
            double y0 = test.y - a.y;
            double x1 = b.x - a.x;
            double y1 = b.y - a.y;
            double det = x0 * y1 - x1 * y0;
            return (det > tol ? +1 : (det < -tol ? -1 : 0));
        }




        /// <summary>
        /// Test if segments intersect. Returns true for parallel-line overlaps.
        /// Returns same result as IntrSegment2Segment2.
        /// </summary>
        public bool Intersects(ref Segment2d seg2, double dotThresh = double.Epsilon, double intervalThresh = 0)
        {
            // see IntrLine2Line2 and IntrSegment2Segment2 for details on this code

            Vector2d diff = seg2.Center - Center;
            double D0DotPerpD1 = Direction.DotPerp(seg2.Direction);
            if (Math.Abs(D0DotPerpD1) > dotThresh) {   // Lines intersect in a single point.
                double invD0DotPerpD1 = ((double)1) / D0DotPerpD1;
                double diffDotPerpD0 = diff.DotPerp(Direction);
                double diffDotPerpD1 = diff.DotPerp(seg2.Direction);
                double s = diffDotPerpD1 * invD0DotPerpD1;
                double s2 = diffDotPerpD0 * invD0DotPerpD1;
                return Math.Abs(s) <= (Extent + intervalThresh) 
                        && Math.Abs(s2) <= (seg2.Extent + intervalThresh);
            }

            // Lines are parallel.
            diff.Normalize();
            double diffNDotPerpD1 = diff.DotPerp(seg2.Direction);
            if (Math.Abs(diffNDotPerpD1) <= dotThresh) {
                // Compute the location of segment1 endpoints relative to segment0.
                diff = seg2.Center - Center;
                double t1 = Direction.Dot(diff);
                double tmin = t1 - seg2.Extent;
                double tmax = t1 + seg2.Extent;
                Interval1d extents = new Interval1d(-Extent, Extent);
                if (extents.Overlaps(new Interval1d(tmin, tmax)))
                    return true;
                return false;
            }

            // lines are parallel but not collinear
            return false;
        }
        public bool Intersects(Segment2d seg2, double dotThresh = double.Epsilon, double intervalThresh = 0) {
            return Intersects(ref seg2, dotThresh, intervalThresh);
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




        /// <summary>
        /// distance from pt to segment (a,b), with no square roots
        /// </summary>
        public static float FastDistanceSquared(ref Vector2f a, ref Vector2f b, ref Vector2f pt)
        {
            float vx = b.x - a.x, vy = b.y - a.y;
            float len2 = vx * vx + vy * vy;
            float dx = pt.x - a.x, dy = pt.y - a.y;
            if (len2 < 1e-7) {
                return dx * dx + dy * dy;
            }
            float t = (dx * vx + dy * vy);
            if (t <= 0) {
                return dx * dx + dy * dy;
            } else if (t >= len2) {
                dx = pt.x - b.x; dy = pt.y - b.y;
                return dx * dx + dy * dy;
            }

            dx = pt.x - (a.x + ((t * vx) / len2));
            dy = pt.y - (a.y + ((t * vy) / len2));
            return dx * dx + dy * dy;
        }

    }





	public class Segment2dBox 
	{
		public Segment2d Segment;

		public Segment2dBox() { }
		public Segment2dBox(Segment2d seg) {
			Segment = seg;
		}

		public static implicit operator Segment2d(Segment2dBox box)
		{
			return box.Segment;
		}
	}




}
