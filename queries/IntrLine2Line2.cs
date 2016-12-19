using System;

namespace g3
{
	// ported from WildMagic5 
	public class IntrLine2Line2
    {
        Line2d line1;
        public Line2d Line1
        {
			get { return line1; }
			set { line1 = value; Result = IntersectionResult.NotComputed; }
        }

        Line2d line2;
        public Line2d Line2
        {
			get { return line2; }
			set { line2 = value; Result = IntersectionResult.NotComputed; }
        }

		double dotThresh = MathUtil.ZeroTolerance;
		public double DotThreshold
		{
			get { return dotThresh; }
			set { dotThresh = Math.Max(value, 0); Result = IntersectionResult.NotComputed; }
		}

		public int Quantity = 0;
		public IntersectionResult Result = IntersectionResult.NotComputed;
		public IntersectionType Type = IntersectionType.Empty;

        public bool IsSimpleIntersection {
            get { return Result == IntersectionResult.Intersects && Type == IntersectionType.Point; }
        }


		public Vector2d Point;
        public double Segment1Parameter;
		public double Segment2Parameter;


		public IntrLine2Line2(Line2d l1, Line2d l2)
		{
			line1 = l1; line2 = l2;
		}


		public IntrLine2Line2 Compute()
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
			if ( line1.Direction.IsNormalized == false || line2.Direction.IsNormalized == false )  {
				Type = IntersectionType.Empty;
				Result = IntersectionResult.InvalidQuery;
				return false;
			}

			Vector2d s = Vector2d.Zero;
			Type = Classify(line1.Origin, line1.Direction,
			                line2.Origin, line2.Direction, dotThresh, ref s);

			if (Type == IntersectionType.Point) {
				Quantity = 1;
				Point = line1.Origin + s.x*line1.Direction;
				Segment1Parameter = s.x;
				Segment2Parameter = s.y;
			} else if (Type == IntersectionType.Line) {
				Quantity = int.MaxValue;
			} else {
				Quantity = 0;
			}

			Result = (Type != IntersectionType.Empty) ?
				IntersectionResult.Intersects : IntersectionResult.NoIntersection;
			return (Result == IntersectionResult.Intersects);
        }



		public static IntersectionType Classify( Vector2d P0, Vector2d D0,  Vector2d P1,  Vector2d D1,
		             double dotThreshold, ref Vector2d s)
		{
			// Ensure dotThreshold is nonnegative.
			dotThreshold = Math.Max(dotThreshold, (double)0);

			// The intersection of two lines is a solution to P0+s0*D0 = P1+s1*D1.
			// Rewrite this as s0*D0 - s1*D1 = P1 - P0 = Q.  If D0.Dot(Perp(D1)) = 0,
			// the lines are parallel.  Additionally, if Q.Dot(Perp(D1)) = 0, the
			// lines are the same.  If D0.Dot(Perp(D1)) is not zero, then
			//   s0 = Q.Dot(Perp(D1))/D0.Dot(Perp(D1))
			// produces the point of intersection.  Also,
			//   s1 = Q.Dot(Perp(D0))/D0.Dot(Perp(D1))

			Vector2d diff = P1 - P0;
			double D0DotPerpD1 = D0.DotPerp(D1);
			if ( Math.Abs(D0DotPerpD1) > dotThreshold) {
			// Lines intersect in a single point.
				double invD0DotPerpD1 = ((double)1)/D0DotPerpD1;
				double diffDotPerpD0 = diff.DotPerp(D0);
				double diffDotPerpD1 = diff.DotPerp(D1);
				s[0] = diffDotPerpD1*invD0DotPerpD1;
				s[1] = diffDotPerpD0*invD0DotPerpD1;
				return IntersectionType.Point;
			}

			// Lines are parallel.
			diff.Normalize();
			double diffNDotPerpD1 = diff.DotPerp(D1);
			if (Math.Abs(diffNDotPerpD1) <= dotThreshold) {
				// Lines are colinear.
				return IntersectionType.Line;
			}

			// Lines are parallel, but distinct.
			return IntersectionType.Empty;
		}

    }
}
