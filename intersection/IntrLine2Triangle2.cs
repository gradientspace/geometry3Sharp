using System;

namespace g3 
{
	// ported from WildMagic5 
	public class IntrLine2Triangle2
	{
		Line2d line;
		public Line2d Line
		{
			get { return line; }
			set { line = value; Result = IntersectionResult.NotComputed; }
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


		public IntrLine2Triangle2(Line2d l, Triangle2d t)
		{
			line = l; triangle = t;
		}


		public IntrLine2Triangle2 Compute()
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
			if ( line.Direction.IsNormalized == false )  {
				Type = IntersectionType.Empty;
				Result = IntersectionResult.InvalidQuery;
				return false;
			}

			Vector3d dist = Vector3d.Zero;
			Vector3i sign = Vector3i.Zero;
			int positive = 0, negative = 0, zero = 0;
			TriangleLineRelations(line.Origin, line.Direction, triangle,
			              ref dist, ref sign, ref positive, ref negative, ref zero);

			if (positive == 3 || negative == 3) {
				// No intersections.
				Quantity = 0;
				Type = IntersectionType.Empty;
			} else {
				Vector2d param = Vector2d.Zero;
				GetInterval(line.Origin, line.Direction, triangle, dist, sign, ref param);

				Intersector1 intr = new Intersector1(param[0], param[1], -double.MaxValue, +double.MaxValue);
				intr.Find();

				Quantity = intr.NumIntersections;
				if (Quantity == 2) {
					// Segment intersection.
					Type = IntersectionType.Segment;
					Param0 = intr.GetIntersection(0);
					Point0 = line.Origin + Param0*line.Direction;
					Param1 = intr.GetIntersection(1);
					Point1 = line.Origin + Param1*line.Direction;
				} else if (Quantity == 1) {
					// Point intersection.
					Type = IntersectionType.Point;
					Param0 = intr.GetIntersection(0);
					Point0 = line.Origin + Param0*line.Direction;
				} else {
					// No intersections.
					Type = IntersectionType.Empty;
				}
			}

			Result = (Type != IntersectionType.Empty) ?
				IntersectionResult.Intersects : IntersectionResult.NoIntersection;
			return (Result == IntersectionResult.Intersects);
		}



		public static void TriangleLineRelations (
			Vector2d origin, Vector2d direction,
			Triangle2d tri, ref Vector3d dist, ref Vector3i sign,
			ref int positive, ref int negative, ref int zero)
		{
			positive = 0;
			negative = 0;
			zero = 0;
			for (int i = 0; i < 3; ++i) {
				Vector2d diff = tri[i] - origin;
				dist[i] = diff.DotPerp(direction);
				if (dist[i] > MathUtil.ZeroTolerance)
				{
					sign[i] = 1;
					++positive;
				}
				else if (dist[i] < -MathUtil.ZeroTolerance)
				{
					sign[i] = -1;
					++negative;
				}
				else
				{
					dist[i] = 0.0;
					sign[i] = 0;
					++zero;
				}
			}
		}



		public static void GetInterval (Vector2d origin, Vector2d direction, Triangle2d tri,
		                  Vector3d dist, Vector3i sign, ref Vector2d param)
		{
			// Project triangle onto line.
			Vector3d proj = Vector3d.Zero;
			int i;
			for (i = 0; i < 3; ++i) {
				Vector2d diff = tri[i] - origin;
				proj[i] = direction.Dot(diff);
			}

			// Compute transverse intersections of triangle edges with line.
			double numer, denom;
			int i0, i1, i2;
			int quantity = 0;
			for (i0 = 2, i1 = 0; i1 < 3; i0 = i1++) {
				if (sign[i0]*sign[i1] < 0) {
					if ( quantity >= 2 )
						throw new Exception("IntrLine2Triangle2.GetInterval: too many intersections!");
					numer = dist[i0]*proj[i1] - dist[i1]*proj[i0];
					denom = dist[i0] - dist[i1];
					param[quantity++] = numer/denom;
				}
			}

			// Check for grazing contact.
			if (quantity < 2) {
				for (i0 = 1, i1 = 2, i2 = 0; i2 < 3; i0 = i1, i1 = i2++) {
					if (sign[i2] == 0) {
						if ( quantity >= 2 )
							throw new Exception("IntrLine2Triangle2.GetInterval: too many intersections!");	
						param[quantity++] = proj[i2];
					}
				}
			}

			// Sort.
			if ( quantity < 1 )
				throw new Exception("IntrLine2Triangle2.GetInterval: need at least one intersection");
			if (quantity == 2)
			{
				if (param[0] > param[1])
				{
					double save = param[0];
					param[0] = param[1];
					param[1] = save;
				}
			}
			else
			{
				param[1] = param[0];
			}
		}


	}
}
