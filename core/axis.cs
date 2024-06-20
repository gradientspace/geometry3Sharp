using System;

namespace VirgisGeometry
{

    /// <summary>
    /// structures used to record axis order
    /// </summary>
    public enum AxisType
    {
        Other = 0,
        North = 1,
        South = 2,
        East = 3,
        West = 4,
        Up = 5,
        Down = 6
    }

    public struct AxisOrder
    {
        public AxisType Axis1;
        public AxisType Axis2;
        public AxisType Axis3;

        public static readonly AxisOrder ENU = new()
        {
            Axis1 = AxisType.East,
            Axis2 = AxisType.North,
            Axis3 = AxisType.Up
        };

        public static readonly AxisOrder NED = new()
        {
            Axis1 = AxisType.North,
            Axis2 = AxisType.East,
            Axis3 = AxisType.Down
        };

        public static readonly AxisOrder EUN = new()
        {
            Axis1 = AxisType.East,
            Axis2 = AxisType.Up,
            Axis3 = AxisType.North
        };

        /// <summary>
        /// This considers the case where the axis order needs to swap between:
        ///  [e,n,s]u[n,e,s] (which is needed for Unity) and
        ///  [e,n,s][n,e,s]u (which are the majority case in epsg
        ///  
        ///  This is not reprojection - the coordinates MUSTbe in the correct CRS and this 
        ///  just swaps the axes for those specific cases
        ///  
        /// ToDo this is not generic enough
        /// 
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public readonly Matrix4d TransformTo(AxisOrder other)
        {
            if (other == null) throw new Exception("Invalid AxisOrder");
            if (this == other) return Matrix4d.Identity;
            if (Axis1 != other.Axis1) { throw new Exception("The geometries are not in the same CRS"); }
            return new Matrix4d(1, 0, 0, 0,
                                0, 0, 1, 0,
                                0, 1, 0, 0,
                                0, 0, 0, 1);
        }

        public static bool operator ==(AxisOrder lhs, AxisOrder rhs)
        {
            return lhs.Axis1 == rhs.Axis1 && lhs.Axis2 == rhs.Axis2 && lhs.Axis3 == rhs.Axis3;
        }

        public static bool operator !=(AxisOrder lhs, AxisOrder rhs)
        {
            return !(lhs == rhs);
        }

        public static implicit operator AxisOrder(string value)
        {
            switch (value)
            {
                case "ENU":
                    return ENU;
                case "EUN":
                    return EUN;
                case "NED":
                    return NED;
                default:
                    return default;
            }
        }

        public readonly byte[] ToArray()
        {
            return new byte[] { (byte)Axis1, (byte)Axis2, (byte)Axis3 };
        }
    }
}
