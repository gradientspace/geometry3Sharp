using System;

namespace g3
{
	public static class IntrAxisBox2AxisBox2
	{
		/**
		 * Compute the time at which box A which moves by TranslationA
		 * intersects with box B which moves by TranslationB. The time
		 * is returned as a unit parameter in range [0,1], ie the collision
		 * occurs at (A + UnitCollisionTime*TranslationA). If no collision
		 * is found this value is returned as double.MaxValue
		 * 
		 * @return true if collision occurs, false if not
		 */
		public static bool IntersectionTime_Translation(
			AxisAlignedBox2d A, Vector2d TranslationA, 
			AxisAlignedBox2d B, Vector2d TranslationB, 
			out double UnitCollisionTime)
		{
			UnitCollisionTime = double.MaxValue;

			// the calculation here is based on minkowski-sum approach

			// transform to equivalent problem where B is stationary and only A moves
			Vector2d CombinedTranslationA = TranslationA - TranslationB;
			// transform to equivalent problem where ExpandedB is the minkowski-sum of B+A,
			// and CenterA is A-A, ie it becomes a point
			Vector2d CenterA = A.Center;
			// minkowski sum of two AABB's just grows by dimensions
			AxisAlignedBox2d ExpandedB = new AxisAlignedBox2d(B.Center, (A.Width+B.Width)/2.0, (A.Height+B.Height)/2.0);

			// now CenterA is moves by CombinedTranslationA, ie it's a line segment and
			// intersection time is where that segment intersects the box
			Segment2d Seg = new Segment2d(CenterA, CenterA+CombinedTranslationA);
			IntrSegment2AxisAlignedBox2 intersection = new IntrSegment2AxisAlignedBox2(
				Seg, ExpandedB, true);
			if (!intersection.Find())
				return false;

			// map distance along line segment to unit parameter
			//UnitCollisionTime = Seg.ProjectToUnitParam(intersection.Point0);
			UnitCollisionTime = Seg.DistanceToUnitParam(intersection.SegmentParam0);
			return true;
		}

	}
}
