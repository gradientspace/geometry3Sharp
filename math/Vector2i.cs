using System;

namespace g3
{
    public struct Vector2i : IComparable<Vector2i>, IEquatable<Vector2i>
    {
        public int x;
        public int y;

        public Vector2i(int f) { x = y = f; }
        public Vector2i(int x, int y) { this.x = x; this.y = y; }
        public Vector2i(int[] v2) { x = v2[0]; y = v2[1]; }

        static public readonly Vector3i Zero = new Vector3i(0, 0, 0);
        static public readonly Vector3i AxisX = new Vector3i(1, 0, 0);
        static public readonly Vector3i AxisY = new Vector3i(0, 1, 0);
        static public readonly Vector3i AxisZ = new Vector3i(0, 0, 1);

        public int this[int key]
        {
            get { return (key == 0) ? x : y; }
            set { if (key == 0) x = value; else y = value; }
        }

        public int[] array
        {
            get { return new int[] { x, y }; }
        }

        public void Add(int s) { x += s; y += s; }


        public static bool operator ==(Vector2i a, Vector2i b)
        {
            return (a.x == b.x && a.y == b.y);
        }
        public static bool operator !=(Vector2i a, Vector2i b)
        {
            return (a.x != b.x || a.y != b.y);
        }
        public override bool Equals(object obj)
        {
            return this == (Vector2i)obj;
        }
        public override int GetHashCode()
        {
            unchecked { 
                int hash = (int) 2166136261;
                // Suitable nullity checks etc, of course :)
                hash = (hash * 16777619) ^ x.GetHashCode();
                hash = (hash * 16777619) ^ y.GetHashCode();
                return hash;
            }

        }
        public int CompareTo(Vector2i other)
        {
            if (x != other.x)
                return x < other.x ? -1 : 1;
            else if (y != other.y)
                return y < other.y ? -1 : 1;
            return 0;
        }
        public bool Equals(Vector2i other)
        {
            return (x == other.x && y == other.y);
        }



        public override string ToString()
        {
            return string.Format("{0} {1}", x, y);
        }

    }
}
