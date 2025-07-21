using System;

// derived from GTengine by David Eberly, Geometric Tools (boost license)
// https://github.com/davideberly/GeometricTools/blob/master/GTE/Mathematics/IntrLine2AlignedBox2.h
// https://github.com/davideberly/GeometricTools/blob/master/GTE/Mathematics/IntrSegment2AlignedBox2.h

namespace g3
{
    public struct IntrSegment2AxisAlignedBox2
    {
		Segment2d segment;
		public Segment2d Segment {
			get { return segment; }
			set { segment = value; Result = IntersectionResult.NotComputed; }
		}

		AxisAlignedBox2d box;
		public AxisAlignedBox2d Box {
			get { return box; }
			set { box = value; Result = IntersectionResult.NotComputed; }
		}

		public int Quantity = 0;
		public IntersectionResult Result = IntersectionResult.NotComputed;
		public IntersectionType Type = IntersectionType.Empty;

		public double SegmentParam0, SegmentParam1;
		public Vector2d Point0 = Vector2d.Zero;
		public Vector2d Point1 = Vector2d.Zero;

		public IntrSegment2AxisAlignedBox2(Segment2d s, AxisAlignedBox2d b)
		{
			segment = s; box = b;
		}

		public IntrSegment2AxisAlignedBox2 Compute()
		{
			Find();
			return this;
		}


		public bool Find()
		{
			if (Result != IntersectionResult.NotComputed)
				return (Result == IntersectionResult.Intersects);

			// [RMS] if either line direction is not a normalized vector, 
			//   results are garbage, so fail query
			if (segment.Direction.IsNormalized == false) {
				Type = IntersectionType.Empty;
				Result = IntersectionResult.InvalidQuery;
				return false;
			}

			Vector2d boxCenter = box.Center;
			Vector2d boxExtent = box.Extents;

			Vector2d segOrigin = segment.Center - boxCenter;		// transform segment into box coordinate system
			Vector2d segDirection = segment.Direction;
			double segExtent = segment.Extent;

			Type = IntersectionType.Empty;

			LinearIntersection result;
			SegIntersection_DoQuery(segOrigin, segDirection, segExtent, boxExtent, out result);
			if ( result.numIntersections > 0 ) {
				Point0 = boxCenter + (segOrigin + result.parameter[0] * segDirection);
				SegmentParam0 = result.parameter[0];
				Type = IntersectionType.Point;
			}
			if (result.numIntersections > 1 ) {
				Point1 = boxCenter + (segOrigin + result.parameter[1] * segDirection);
				SegmentParam1 = result.parameter[1];
				Type = IntersectionType.Segment;
			}

			Quantity = result.numIntersections;
			Result = (Type != IntersectionType.Empty) ?
				IntersectionResult.Intersects : IntersectionResult.NoIntersection;
			return (Result == IntersectionResult.Intersects);
		}




		public static void SegIntersection_DoQuery(Vector2d segOrigin, Vector2d segDirection, double segExtent, Vector2d boxExtent, out LinearIntersection result)
		{
			result = LinearIntersection.NoIntersection;
			LineIntersection_DoQuery(segOrigin, segDirection, boxExtent, out result);

			if (result.intersects) {
				// The line containing the segment intersects the box; the t-interval
				// is [t0,t1].  The segment intersects the box as long as [t0,t1]
				// overlaps the segment t-interval [-segExtent,+segExtent].
				Interval1d segInterval = new Interval1d(-segExtent, segExtent);
				Interval1d.FindIntersection(result.parameter, segInterval, out result);
			}
		}



		public static void LineIntersection_DoQuery(Vector2d lineOrigin, Vector2d lineDirection, Vector2d boxExtent, out LinearIntersection result)
		{
			result = LinearIntersection.NoIntersection;

			// The line t-values are in the interval (-infinity,+infinity).  Clip the
			// line against all four planes of an aligned box in centered form.  The
			// result.numPoints is
			//   0, no intersection
			//   1, intersect in a single point (t0 is line parameter of point)
			//   2, intersect in a segment (line parameter interval is [t0,t1])
			double t0 = -double.MaxValue;
			double t1 = double.MaxValue;
			if (IntersectionLowLevel.Clip(+lineDirection[0], -lineOrigin[0] - boxExtent[0], ref t0, ref t1) &&
				IntersectionLowLevel.Clip(-lineDirection[0], +lineOrigin[0] - boxExtent[0], ref t0, ref t1) &&
				IntersectionLowLevel.Clip(+lineDirection[1], -lineOrigin[1] - boxExtent[1], ref t0, ref t1) &&
				IntersectionLowLevel.Clip(-lineDirection[1], +lineOrigin[1] - boxExtent[1], ref t0, ref t1) ) 
			{
				result.intersects = true;
				if (t1 > t0) {
					result.numIntersections = 2;
					result.parameter = new Interval1d(t0, t1);
				} else {
					result.numIntersections = 1;
					result.parameter = new Interval1d(t0, t0);
				}
			}
		}

	}
}
