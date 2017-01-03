using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
	// ported from WildMagic5 
	public class IntrSegment3Box3
	{
		Segment3d segment;
		public Segment3d Segment
		{
			get { return segment; }
			set { segment = value; Result = IntersectionResult.NotComputed; }
		}

		Box3d box;
		public Box3d Box
		{
			get { return box; }
			set { box = value; Result = IntersectionResult.NotComputed; }
		}

		bool solid = false;
		public bool Solid {
			get { return solid; }
			set { solid = value; Result = IntersectionResult.NotComputed; }
		}

		public int Quantity = 0;
		public IntersectionResult Result = IntersectionResult.NotComputed;
		public IntersectionType Type = IntersectionType.Empty;

		public bool IsSimpleIntersection {
			get { return Result == IntersectionResult.Intersects && Type == IntersectionType.Point; }
		}

		public double SegmentParam0, SegmentParam1;
		public Vector3d Point0 = Vector3d.Zero;
		public Vector3d Point1 = Vector3d.Zero;

		// solidBox == false means fully contained segment does not intersect
		public IntrSegment3Box3(Segment3d s, Box3d b, bool solidBox)
		{
			segment = s; box = b; this.solid = solidBox;
		}

		public IntrSegment3Box3 Compute()
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

			SegmentParam0 = -segment.Extent;
			SegmentParam1 = segment.Extent;
			DoClipping(ref SegmentParam0, ref SegmentParam1, segment.Center, segment.Direction, box,
			          solid, ref Quantity, ref Point0, ref Point1, ref Type);

			Result = (Type != IntersectionType.Empty) ?
				IntersectionResult.Intersects : IntersectionResult.NoIntersection;
			return (Result == IntersectionResult.Intersects);
		}




		public bool Test ()
		{
			Vector3d AWdU = Vector3d.Zero;
			Vector3d ADdU = Vector3d.Zero;
			Vector3d AWxDdU = Vector3d.Zero;
			double RHS;

			Vector3d diff = segment.Center - box.Center;

			AWdU[0] = Math.Abs(segment.Direction.Dot(box.AxisX));
			ADdU[0] = Math.Abs(diff.Dot(box.AxisX));
			RHS = box.Extent.x + segment.Extent*AWdU[0];
			if (ADdU[0] > RHS)
			{
				return false;
			}

			AWdU[1] = Math.Abs(segment.Direction.Dot(box.AxisY));
			ADdU[1] = Math.Abs(diff.Dot(box.AxisY));
			RHS = box.Extent.y + segment.Extent*AWdU[1];
			if (ADdU[1] > RHS)
			{
				return false;
			}

			AWdU[2] = Math.Abs(segment.Direction.Dot(box.AxisZ));
			ADdU[2] = Math.Abs(diff.Dot(box.AxisZ));
			RHS = box.Extent.z + segment.Extent*AWdU[2];
			if (ADdU[2] > RHS)
			{
				return false;
			}

			Vector3d WxD = segment.Direction.Cross(diff);

			AWxDdU[0] = Math.Abs(WxD.Dot(box.AxisX));
			RHS = box.Extent.y*AWdU[2] + box.Extent.z*AWdU[1];
			if (AWxDdU[0] > RHS)
			{
				return false;
			}

			AWxDdU[1] = Math.Abs(WxD.Dot(box.AxisY));
			RHS = box.Extent.x*AWdU[2] + box.Extent.z*AWdU[0];
			if (AWxDdU[1] > RHS)
			{
				return false;
			}

			AWxDdU[2] = Math.Abs(WxD.Dot(box.AxisZ));
			RHS = box.Extent.x*AWdU[1] + box.Extent.y*AWdU[0];
			if (AWxDdU[2] > RHS)
			{
				return false;
			}

			return true;
		}




		static public bool DoClipping (ref double t0, ref double t1,
		                 Vector3d origin, Vector3d direction,
		                 Box3d box, bool solid, ref int quantity, 
                         ref Vector3d point0, ref Vector3d point1,
		                 ref IntersectionType  intrType)
		{
			// Convert linear component to box coordinates.
			Vector3d diff = origin - box.Center;
			Vector3d BOrigin = new Vector3d(
				diff.Dot(box.AxisX),
				diff.Dot(box.AxisY),
				diff.Dot(box.AxisZ)
			);
			Vector3d BDirection = new Vector3d(
				direction.Dot(box.AxisX),
				direction.Dot(box.AxisY),
				direction.Dot(box.AxisZ)
			);

			double saveT0 = t0, saveT1 = t1;
			bool notAllClipped =
				Clip(+BDirection.x, -BOrigin.x-box.Extent.x, ref t0, ref t1) &&
				Clip(-BDirection.x, +BOrigin.x-box.Extent.x, ref t0, ref t1) &&
				Clip(+BDirection.y, -BOrigin.y-box.Extent.y, ref t0, ref t1) &&
				Clip(-BDirection.y, +BOrigin.y-box.Extent.y, ref t0, ref t1) &&
				Clip(+BDirection.z, -BOrigin.z-box.Extent.z, ref t0, ref t1) &&
				Clip(-BDirection.z, +BOrigin.z-box.Extent.z, ref t0, ref t1);

			if (notAllClipped && (solid || t0 != saveT0 || t1 != saveT1)) {
				if (t1 > t0) {
					intrType = IntersectionType.Segment;
					quantity = 2;
					point0 = origin + t0*direction;
					point1 = origin + t1*direction;
				} else {
					intrType = IntersectionType.Point;
					quantity = 1;
					point0 = origin + t0*direction;
				}
			} else {
				quantity = 0;
				intrType = IntersectionType.Empty;
			}

			return intrType != IntersectionType.Empty;
		}




		static public bool Clip (double denom, double numer, ref double t0, ref double t1)
		{
			// Return value is 'true' if line segment intersects the current test
			// plane.  Otherwise 'false' is returned in which case the line segment
			// is entirely clipped.

			if (denom > (double)0)
			{
				if (numer > denom*t1)
				{
					return false;
				}
				if (numer > denom*t0)
				{
					t0 = numer/denom;
				}
				return true;
			}
			else if (denom < (double)0)
			{
				if (numer > denom*t0)
				{
					return false;
				}
				if (numer > denom*t1)
				{
					t1 = numer/denom;
				}
				return true;
			}
			else
			{
				return numer <= (double)0;
			}
		}


	}
}
