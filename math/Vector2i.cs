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

        static public readonly Vector2i Zero = new Vector2i(0, 0);
        static public readonly Vector2i One = new Vector2i(1, 1);
        static public readonly Vector2i AxisX = new Vector2i(1, 0);
        static public readonly Vector2i AxisY = new Vector2i(0, 1);

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


        public int LengthSquared { get { return x * x + y * y; } }


        public static Vector2i operator -(Vector2i v)
        {
            return new Vector2i(-v.x, -v.y);
        }

        public static Vector2i operator *(int f, Vector2i v)
        {
            return new Vector2i(f * v.x, f * v.y);
        }
        public static Vector2i operator *(Vector2i v, int f)
        {
            return new Vector2i(f * v.x, f * v.y);
        }
        public static Vector2i operator /(Vector2i v, int f)
        {
            return new Vector2i(v.x / f, v.y / f);
        }
        public static Vector2i operator /(int f, Vector2i v)
        {
            return new Vector2i(f / v.x, f / v.y);
        }

        public static Vector2i operator *(Vector2i a, Vector2i b)
        {
            return new Vector2i(a.x * b.x, a.y * b.y);
        }
        public static Vector2i operator /(Vector2i a, Vector2i b)
        {
            return new Vector2i(a.x / b.x, a.y / b.y);
        }


        public static Vector2i operator +(Vector2i v0, Vector2i v1)
        {
            return new Vector2i(v0.x + v1.x, v0.y + v1.y);
        }
        public static Vector2i operator +(Vector2i v0, int f)
        {
            return new Vector2i(v0.x + f, v0.y + f);
        }

        public static Vector2i operator -(Vector2i v0, Vector2i v1)
        {
            return new Vector2i(v0.x - v1.x, v0.y - v1.y);
        }
        public static Vector2i operator -(Vector2i v0, int f)
        {
            return new Vector2i(v0.x - f, v0.y - f);
        }



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












    public struct Vector2l : IComparable<Vector2l>, IEquatable<Vector2l>
    {
        public long x;
        public long y;

        public Vector2l(long f) { x = y = f; }
        public Vector2l(long x, long y) { this.x = x; this.y = y; }
        public Vector2l(long[] v2) { x = v2[0]; y = v2[1]; }

        static public readonly Vector2l Zero = new Vector2l(0, 0);
        static public readonly Vector2l One = new Vector2l(1, 1);
        static public readonly Vector2l AxisX = new Vector2l(1, 0);
        static public readonly Vector2l AxisY = new Vector2l(0, 1);

        public long this[long key] {
            get { return (key == 0) ? x : y; }
            set { if (key == 0) x = value; else y = value; }
        }

        public long[] array {
            get { return new long[] { x, y }; }
        }

        public void Add(long s) { x += s; y += s; }




        public static Vector2l operator -(Vector2l v)
        {
            return new Vector2l(-v.x, -v.y);
        }

        public static Vector2l operator *(long f, Vector2l v)
        {
            return new Vector2l(f * v.x, f * v.y);
        }
        public static Vector2l operator *(Vector2l v, long f)
        {
            return new Vector2l(f * v.x, f * v.y);
        }
        public static Vector2l operator /(Vector2l v, long f)
        {
            return new Vector2l(v.x / f, v.y / f);
        }
        public static Vector2l operator /(long f, Vector2l v)
        {
            return new Vector2l(f / v.x, f / v.y);
        }

        public static Vector2l operator *(Vector2l a, Vector2l b)
        {
            return new Vector2l(a.x * b.x, a.y * b.y);
        }
        public static Vector2l operator /(Vector2l a, Vector2l b)
        {
            return new Vector2l(a.x / b.x, a.y / b.y);
        }


        public static Vector2l operator +(Vector2l v0, Vector2l v1)
        {
            return new Vector2l(v0.x + v1.x, v0.y + v1.y);
        }
        public static Vector2l operator +(Vector2l v0, long f)
        {
            return new Vector2l(v0.x + f, v0.y + f);
        }

        public static Vector2l operator -(Vector2l v0, Vector2l v1)
        {
            return new Vector2l(v0.x - v1.x, v0.y - v1.y);
        }
        public static Vector2l operator -(Vector2l v0, long f)
        {
            return new Vector2l(v0.x - f, v0.y - f);
        }



        public static bool operator ==(Vector2l a, Vector2l b)
        {
            return (a.x == b.x && a.y == b.y);
        }
        public static bool operator !=(Vector2l a, Vector2l b)
        {
            return (a.x != b.x || a.y != b.y);
        }
        public override bool Equals(object obj)
        {
            return this == (Vector2l)obj;
        }
        public override int GetHashCode()
        {
            unchecked {
                int hash = (int)2166136261;
                // Suitable nullity checks etc, of course :)
                hash = (hash * 16777619) ^ x.GetHashCode();
                hash = (hash * 16777619) ^ y.GetHashCode();
                return hash;
            }

        }
        public int CompareTo(Vector2l other)
        {
            if (x != other.x)
                return x < other.x ? -1 : 1;
            else if (y != other.y)
                return y < other.y ? -1 : 1;
            return 0;
        }
        public bool Equals(Vector2l other)
        {
            return (x == other.x && y == other.y);
        }



        public override string ToString()
        {
            return string.Format("{0} {1}", x, y);
        }
    }



}
