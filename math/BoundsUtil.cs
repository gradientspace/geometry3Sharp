using System;
using System.Collections.Generic;

namespace g3
{
    public static class BoundsUtil
    {

        public static AxisAlignedBox3d Bounds(ref Triangle3d tri) {
            return Bounds(ref tri.V0, ref tri.V1, ref tri.V2);
        }

        public static AxisAlignedBox3d Bounds(ref Vector3d v0, ref Vector3d v1, ref Vector3d v2)
        {
            AxisAlignedBox3d box;
            MathUtil.MinMax(v0.x, v1.x, v2.x, out box.Min.x, out box.Max.x);
            MathUtil.MinMax(v0.y, v1.y, v2.y, out box.Min.y, out box.Max.y);
            MathUtil.MinMax(v0.z, v1.z, v2.z, out box.Min.z, out box.Max.z);
            return box;
        }



        // AABB of transformed AABB (corners)
        public static AxisAlignedBox3d Bounds(ref AxisAlignedBox3d boxIn, Func<Vector3d,Vector3d> TransformF)
        {
            if (TransformF == null)
                return boxIn;

            AxisAlignedBox3d box = new AxisAlignedBox3d(TransformF(boxIn.Corner(0)));
            for (int i = 1; i < 8; ++i)
                box.Contain(TransformF(boxIn.Corner(i)));
            return box;
        }

    }
}
