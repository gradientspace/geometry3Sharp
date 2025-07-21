using System;

// derived from GTengine by David Eberly, Geometric Tools (boost license)
// https://github.com/davideberly/GeometricTools/blob/master/GTE/Mathematics/IntrLine2Circle2.h
// https://github.com/davideberly/GeometricTools/blob/master/GTE/Mathematics/IntrSegment2Circle2.h

namespace g3
{
    public struct IntrSegment2Circle2
    {
		Segment2d segment;
		public Segment2d Segment {
			get { return segment; }
			set { segment = value; Result = IntersectionResult.NotComputed; }
		}

		Circle2d circle;
		public Circle2d Circle {
			get { return circle; }
			set { circle = value; Result = IntersectionResult.NotComputed; }
		}

		public int Quantity = 0;
		public IntersectionResult Result = IntersectionResult.NotComputed;
		public IntersectionType Type = IntersectionType.Empty;

		public double SegmentParam0, SegmentParam1;
		public Vector2d Point0 = Vector2d.Zero;
		public Vector2d Point1 = Vector2d.Zero;

		public IntrSegment2Circle2(Segment2d s, Circle2d b)
		{
			segment = s; circle = b;
		}

		public IntrSegment2Circle2 Compute()
		{
			Find();
			return this;
		}


		public bool Find()
		{
			if (Result != IntersectionResult.NotComputed)
				return (Result == IntersectionResult.Intersects);

			// [RMS] fail query if direction is not a normalized vector
			if (segment.Direction.IsNormalized == false) {
				Type = IntersectionType.Empty;
				Result = IntersectionResult.InvalidQuery;
				return false;
			}

			Vector2d circleCenter = circle.Center;
			double circleRadius = circle.Radius;

			Vector2d segOrigin = segment.Center;
			Vector2d segDirection = segment.Direction;
			double segExtent = segment.Extent;

			Type = IntersectionType.Empty;

			LinearIntersection result;
			SegIntersection_DoQuery(segOrigin, segDirection, segExtent, circleCenter, circleRadius, out result);
			if ( result.numIntersections > 0 ) {
				Point0 = segment.PointAt(result.parameter[0]);
				SegmentParam0 = result.parameter[0];
				Type = IntersectionType.Point;
			}
			if (result.numIntersections > 1 ) {
				Point1 = segment.PointAt(result.parameter[1]);
				SegmentParam1 = result.parameter[1];
				Type = IntersectionType.Segment;
			}

			Quantity = result.numIntersections;
			Result = (Type != IntersectionType.Empty) ?
				IntersectionResult.Intersects : IntersectionResult.NoIntersection;
			return (Result == IntersectionResult.Intersects);
		}




		public static void SegIntersection_DoQuery(Vector2d segOrigin, Vector2d segDirection, double segExtent, Vector2d circleCenter, double circleRadius, out LinearIntersection result)
		{
			result = LinearIntersection.NoIntersection;
			LineIntersection_DoQuery(segOrigin, segDirection, circleCenter, circleRadius, out result);

			if (result.intersects)
			{
				// The line containing the segment intersects the circle; the t-interval
				// is [t0,t1].  The segment intersects the circle as long as [t0,t1]
				// overlaps the segment t-interval [-segExtent,+segExtent].
				Interval1d segInterval = new Interval1d(-segExtent, segExtent);
				Interval1d.FindIntersection(result.parameter, segInterval, out result);
			}
		}



		public static void LineIntersection_DoQuery(Vector2d lineOrigin, Vector2d lineDirection, Vector2d circleCenter, double circleRadius, out LinearIntersection result)
		{
			result = LinearIntersection.NoIntersection;

			// Intersection of a the line P+t*D and the circle |X-C| = R.
			// The line direction is unit length. The t-value is a
			// real-valued root to the quadratic equation
			//   0 = |t*D+P-C|^2 - R^2
			//     = t^2 + 2*Dot(D,P-C)*t + |P-C|^2-R^2
			//     = t^2 + 2*a1*t + a0
			// If there are two distinct roots, the order is t0 < t1.
			Vector2d diff = lineOrigin - circleCenter;
			double a0 = diff.Dot(diff) - circleRadius * circleRadius;
			double a1 = lineDirection.Dot(diff);
			double discr = a1 * a1 - a0;
			if (discr > (double)0)
			{
				double root = Math.Sqrt(discr);
				result.intersects = true;
				result.numIntersections = 2;
				result.parameter = new Interval1d(-a1 - root, -a1 + root);
			} 
			else if (discr < (double)0)
			{
				;       // no intersection
			} 
			else  // discr == 0, line is tangent to the circle. 
			{
				result.intersects = true;
				result.numIntersections = 1;
				result.parameter = new Interval1d(-a1, -a1);
			}
		}

	}
}
