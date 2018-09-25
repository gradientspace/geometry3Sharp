using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
	// adapted from IntrRay3Box3
	public class IntrRay3AxisAlignedBox3
	{
		Ray3d ray;
		public Ray3d Ray
		{
			get { return ray; }
			set { ray = value; Result = IntersectionResult.NotComputed; }
		}

		AxisAlignedBox3d box;
		public AxisAlignedBox3d Box
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

		public double RayParam0, RayParam1;
		public Vector3d Point0 = Vector3d.Zero;
		public Vector3d Point1 = Vector3d.Zero;

		public IntrRay3AxisAlignedBox3(Ray3d r, AxisAlignedBox3d b)
		{
			ray = r; box = b;
		}

		public IntrRay3AxisAlignedBox3 Compute()
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
			if ( ray.Direction.IsNormalized == false )  {
				Type = IntersectionType.Empty;
				Result = IntersectionResult.InvalidQuery;
				return false;
			}

			RayParam0 = 0.0;
			RayParam1 = double.MaxValue;
			IntrLine3AxisAlignedBox3.DoClipping(ref RayParam0, ref RayParam1, ref ray.Origin, ref ray.Direction, ref box,
			          true, ref Quantity, ref Point0, ref Point1, ref Type);

			Result = (Type != IntersectionType.Empty) ?
				IntersectionResult.Intersects : IntersectionResult.NoIntersection;
			return (Result == IntersectionResult.Intersects);
		}



		public bool Test ()
		{
            return Intersects(ref ray, ref box);
        }


        /// <summary>
        /// test if ray intersects box.
        /// expandExtents allows you to scale box for hit-testing purposes. 
        /// </summary>
        public static bool Intersects(ref Ray3d ray, ref AxisAlignedBox3d box, double expandExtents = 0)
        {
            Vector3d WdU = Vector3d.Zero;
            Vector3d AWdU = Vector3d.Zero;
            Vector3d DdU = Vector3d.Zero;
            Vector3d ADdU = Vector3d.Zero;
            double RHS;

            Vector3d diff = ray.Origin - box.Center;
            Vector3d extent = box.Extents + expandExtents;

            WdU.x = ray.Direction.x; // ray.Direction.Dot(Vector3d.AxisX);
            AWdU.x = Math.Abs(WdU.x);
            DdU.x = diff.x; // diff.Dot(Vector3d.AxisX);
            ADdU.x = Math.Abs(DdU.x);
            if (ADdU.x > extent.x && DdU.x * WdU.x >= (double)0) {
                return false;
            }

            WdU.y = ray.Direction.y; // ray.Direction.Dot(Vector3d.AxisY);
            AWdU.y = Math.Abs(WdU.y);
            DdU.y = diff.y; // diff.Dot(Vector3d.AxisY);
            ADdU.y = Math.Abs(DdU.y);
            if (ADdU.y > extent.y && DdU.y * WdU.y >= (double)0) {
                return false;
            }

            WdU.z = ray.Direction.z; // ray.Direction.Dot(Vector3d.AxisZ);
            AWdU.z = Math.Abs(WdU.z);
            DdU.z = diff.z; // diff.Dot(Vector3d.AxisZ);
            ADdU.z = Math.Abs(DdU.z);
            if (ADdU.z > extent.z && DdU.z * WdU.z >= (double)0) {
                return false;
            }

            Vector3d WxD = ray.Direction.Cross(diff);
            Vector3d AWxDdU = Vector3d.Zero;

            AWxDdU.x = Math.Abs(WxD.x); // Math.Abs(WxD.Dot(Vector3d.AxisX));
            RHS = extent.y * AWdU.z + extent.z * AWdU.y;
            if (AWxDdU.x > RHS) {
                return false;
            }

            AWxDdU.y = Math.Abs(WxD.y); // Math.Abs(WxD.Dot(Vector3d.AxisY));
            RHS = extent.x * AWdU.z + extent.z * AWdU.x;
            if (AWxDdU.y > RHS) {
                return false;
            }

            AWxDdU.z = Math.Abs(WxD.z); // Math.Abs(WxD.Dot(Vector3d.AxisZ));
            RHS = extent.x * AWdU.y + extent.y * AWdU.x;
            if (AWxDdU.z > RHS) {
                return false;
            }

            return true;
        }


        /// <summary>
        /// Find intersection of ray with AABB, without having to construct any new classes.
        /// Returns ray T-value of first intersection (or double.MaxVlaue on miss)
        /// </summary>
        public static bool FindRayIntersectT(ref Ray3d ray, ref AxisAlignedBox3d box, out double RayParam)
        {
            double RayParam0 = 0.0;
            double RayParam1 = double.MaxValue;
            int Quantity = 0;
            Vector3d Point0 = Vector3d.Zero;
            Vector3d Point1 = Vector3d.Zero;
            IntersectionType Type = IntersectionType.Empty;
            IntrLine3AxisAlignedBox3.DoClipping(ref RayParam0, ref RayParam1, ref ray.Origin, ref ray.Direction, ref box,
                      true, ref Quantity, ref Point0, ref Point1, ref Type);

            if (Type != IntersectionType.Empty) {
                RayParam = RayParam0;
                return true;
            } else {
                RayParam = double.MaxValue;
                return false;
            }
        }




    }
}
