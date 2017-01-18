using System;
using System.Collections.Generic;
using System.Text;

namespace g3
{
    public struct Vector3i : IComparable<Vector3i>, IEquatable<Vector3i>
    {
        public int x;
        public int y;
        public int z;

        public Vector3i(int f) { x = y = z = f; }
        public Vector3i(int x, int y, int z) { this.x = x; this.y = y; this.z = z; }
        public Vector3i(int[] v2) { x = v2[0]; y = v2[1]; z = v2[2]; }

        static public readonly Vector3i Zero = new Vector3i(0, 0, 0);
        static public readonly Vector3i AxisX = new Vector3i(1, 0, 0);
        static public readonly Vector3i AxisY = new Vector3i(0, 1, 0);
        static public readonly Vector3i AxisZ = new Vector3i(0, 0, 1);

        public int this[int key]
        {
            get { return (key == 0) ? x : (key == 1) ? y : z; }
            set { if (key == 0) x = value; else if (key == 1) y = value; else z = value; }
        }

        public int[] array {
            get { return new int[] { x, y, z }; }
        }

        public void Add(int s) { x += s;  y += s;  z += s; }



        public static bool operator ==(Vector3i a, Vector3i b)
        {
            return (a.x == b.x && a.y == b.y && a.z == b.z);
        }
        public static bool operator !=(Vector3i a, Vector3i b)
        {
            return (a.x != b.x || a.y != b.y || a.z != b.z);
        }
        public override bool Equals(object obj)
        {
            return this == (Vector3i)obj;
        }
        public override int GetHashCode()
        {
            unchecked // Overflow is fine, just wrap
            {
                int hash = (int) 2166136261;
                // Suitable nullity checks etc, of course :)
                hash = (hash * 16777619) ^ x.GetHashCode();
                hash = (hash * 16777619) ^ y.GetHashCode();
                hash = (hash * 16777619) ^ z.GetHashCode();
                return hash;
            }
        }
        public int CompareTo(Vector3i other)
        {
            if (x != other.x)
                return x < other.x ? -1 : 1;
            else if (y != other.y)
                return y < other.y ? -1 : 1;
            else if (z != other.z)
                return z < other.z ? -1 : 1;
            return 0;
        }
        public bool Equals(Vector3i other)
        {
            return (x == other.x && y == other.y && z == other.z);
        }



        public override string ToString() {
            return string.Format("{0} {1} {2}", x, y, z);
        }

    }
}
