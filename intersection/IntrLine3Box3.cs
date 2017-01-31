using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
	// ported from WildMagic5 
	public class IntrLine3Box3
	{
		Line3d line;
		public Line3d Line
		{
			get { return line; }
			set { line = value; Result = IntersectionResult.NotComputed; }
		}

		Box3d box;
		public Box3d Box
		{
			get { return box; }
			set { box = value; Result = IntersectionResult.NotComputed; }
		}

		public int Quantity = 0;
		public IntersectionResult Result = IntersectionResult.NotComputed;
		public IntersectionType Type = IntersectionType.Empty;

		public bool IsSimpleIntersection {
			get { return Result == IntersectionResult.Intersects && Type == IntersectionType.Point; }
		}

		public double LineParam0, LineParam1;
		public Vector3d Point0 = Vector3d.Zero;
		public Vector3d Point1 = Vector3d.Zero;

		public IntrLine3Box3(Line3d l, Box3d b)
		{
			line = l; box = b;
		}

		public IntrLine3Box3 Compute()
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

			LineParam0 = -double.MaxValue;
			LineParam1 = double.MaxValue;
			DoClipping(ref LineParam0, ref LineParam1, line.Origin, line.Direction, box,
			          true, ref Quantity, ref Point0, ref Point1, ref Type);

			Result = (Type != IntersectionType.Empty) ?
				IntersectionResult.Intersects : IntersectionResult.NoIntersection;
			return (Result == IntersectionResult.Intersects);
		}




		public bool Test ()
		{
			Vector3d AWdU = Vector3d.Zero;
			Vector3d AWxDdU = Vector3d.Zero;
			double RHS;

			Vector3d diff = line.Origin - box.Center;
			Vector3d WxD = line.Direction.Cross(diff);

			AWdU[1] = Math.Abs(line.Direction.Dot(box.AxisY));
			AWdU[2] = Math.Abs(line.Direction.Dot(box.AxisZ));
			AWxDdU[0] = Math.Abs(WxD.Dot(box.AxisX));
			RHS = box.Extent.y*AWdU[2] + box.Extent.z*AWdU[1];
			if (AWxDdU[0] > RHS) {
				return false;
			}

			AWdU[0] = Math.Abs(line.Direction.Dot(box.AxisX));
			AWxDdU[1] = Math.Abs(WxD.Dot(box.AxisY));
			RHS = box.Extent.x*AWdU[2] + box.Extent.z*AWdU[0];
			if (AWxDdU[1] > RHS) {
				return false;
			}

			AWxDdU[2] = Math.Abs(WxD.Dot(box.AxisZ));
			RHS = box.Extent.x*AWdU[1] + box.Extent.y*AWdU[0];
			if (AWxDdU[2] > RHS) {
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
                if (numer - denom*t1 > MathUtil.ZeroTolerance)
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
                if (numer - denom*t0 > MathUtil.ZeroTolerance)
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
