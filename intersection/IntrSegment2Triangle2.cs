using System;

namespace g3 
{
	// ported from WildMagic5 
	public class IntrSegment2Triangle2
	{
		Segment2d segment;
		public Segment2d Segment
		{
			get { return segment; }
			set { segment = value; Result = IntersectionResult.NotComputed; }
		}

		Triangle2d triangle;
		public Triangle2d Triangle
		{
			get { return triangle; }
			set { triangle = value; Result = IntersectionResult.NotComputed; }
		}

		public int Quantity = 0;
		public IntersectionResult Result = IntersectionResult.NotComputed;
		public IntersectionType Type = IntersectionType.Empty;

		public bool IsSimpleIntersection {
			get { return Result == IntersectionResult.Intersects && Type == IntersectionType.Point; }
		}


		public Vector2d Point0;
		public Vector2d Point1;
		public double Param0;
		public double Param1;


		public IntrSegment2Triangle2(Segment2d s, Triangle2d t)
		{
			segment = s; triangle = t;
		}


		public IntrSegment2Triangle2 Compute()
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
			if ( segment.Direction.IsNormalized == false )  {
				Type = IntersectionType.Empty;
				Result = IntersectionResult.InvalidQuery;
				return false;
			}

			Vector3d dist = Vector3d.Zero;
			Vector3i sign = Vector3i.Zero;
			int positive = 0, negative = 0, zero = 0;
			IntrLine2Triangle2.TriangleLineRelations(segment.Center, segment.Direction, triangle,
			                      ref dist, ref sign, ref positive, ref negative, ref zero);

			if (positive == 3 || negative == 3)
			{
				// No intersections.
				Quantity = 0;
				Type = IntersectionType.Empty;
			} else {
				Vector2d param = Vector2d.Zero;
				IntrLine2Triangle2.GetInterval(segment.Center, segment.Direction, triangle, dist, sign, ref param);

				Intersector1 intr = new Intersector1(param[0], param[1], -segment.Extent, +segment.Extent);
				intr.Find();

				Quantity = intr.NumIntersections;
				if (Quantity == 2) {
					// Segment intersection.
					Type = IntersectionType.Segment;
					Param0 = intr.GetIntersection(0);
					Point0 = segment.Center + Param0*segment.Direction;
					Param1 = intr.GetIntersection(1);
					Point1 = segment.Center + Param1*segment.Direction;
				} else if (Quantity == 1) {
					// Point intersection.
					Type = IntersectionType.Point;
					Param0 = intr.GetIntersection(0);
					Point0 = segment.Center + Param0*segment.Direction;
				} else {
					// No intersections.
					Type = IntersectionType.Empty;
				}
			}

			Result = (Type != IntersectionType.Empty) ?
				IntersectionResult.Intersects : IntersectionResult.NoIntersection;
			return (Result == IntersectionResult.Intersects);
		}



	}
}
