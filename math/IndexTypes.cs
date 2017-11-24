using System;

namespace g3
{

    public struct Index3i : IComparable<Index3i>, IEquatable<Index3i>
    {
        public int a;
        public int b;
        public int c;

        public Index3i(int z) { a = b = c = z; }
        public Index3i(int ii, int jj, int kk) { a = ii; b = jj; c = kk; }
        public Index3i(int[] i2) { a = i2[0]; b = i2[1]; c = i2[2]; }
        public Index3i(Index3i copy) { a = copy.a; b = copy.b; c = copy.b; }

        // reverse last two indices if cycle is true (useful for cw/ccw codes)
        public Index3i(int ii, int jj, int kk, bool cycle) {
            a = ii;
            if (cycle) { b = kk; c = jj; }
                  else { b = jj; c = kk; }
        }

        static public readonly Index3i Zero = new Index3i(0, 0, 0);
        static public readonly Index3i One = new Index3i(1, 1, 1);
        static public readonly Index3i Max = new Index3i(int.MaxValue, int.MaxValue, int.MaxValue);
        static public readonly Index3i Min = new Index3i(int.MinValue, int.MinValue, int.MinValue);


        public int this[int key]
        {
            get { return (key == 0) ? a : (key == 1) ? b : c; }
            set { if (key == 0) a = value; else if (key == 1) b = value; else c = value; }
        }

        public int[] array {
            get { return new int[] { a, b, c }; }
        }


        public int LengthSquared
        {
            get { return a * a + b * b + c * c; }
        }
        public int Length
        {
            get { return (int)Math.Sqrt(LengthSquared); }
        }


        public void Set(Index3i o)
        {
            a = o[0]; b = o[1]; c = o[2];
        }
        public void Set(int ii, int jj, int kk)
        {
            a = ii; b = jj; c = kk;
        }


        public static Index3i operator -(Index3i v)
        {
            return new Index3i(-v.a, -v.b, -v.c);
        }

        public static Index3i operator *(int f, Index3i v)
        {
            return new Index3i(f * v.a, f * v.b, f * v.c);
        }
        public static Index3i operator *(Index3i v, int f)
        {
            return new Index3i(f * v.a, f * v.b, f * v.c);
        }
        public static Index3i operator /(Index3i v, int f)
        {
            return new Index3i(v.a / f, v.b / f, v.c / f);
        }


        public static Index3i operator *(Index3i a, Index3i b)
        {
            return new Index3i(a.a * b.a, a.b * b.b, a.c * b.c);
        }
        public static Index3i operator /(Index3i a, Index3i b)
        {
            return new Index3i(a.a / b.a, a.b / b.b, a.c / b.c);
        }


        public static Index3i operator +(Index3i v0, Index3i v1)
        {
            return new Index3i(v0.a + v1.a, v0.b + v1.b, v0.c + v1.c);
        }
        public static Index3i operator +(Index3i v0, int f)
        {
            return new Index3i(v0.a + f, v0.b + f, v0.c + f);
        }

        public static Index3i operator -(Index3i v0, Index3i v1)
        {
            return new Index3i(v0.a - v1.a, v0.b - v1.b, v0.c - v1.c);
        }
        public static Index3i operator -(Index3i v0, int f)
        {
            return new Index3i(v0.a - f, v0.b - f, v0.c - f);
        }


        public static bool operator ==(Index3i a, Index3i b)
        {
            return (a.a == b.a && a.b == b.b && a.c == b.c);
        }
        public static bool operator !=(Index3i a, Index3i b)
        {
            return (a.a != b.a || a.b != b.b || a.c != b.c);
        }
        public override bool Equals(object obj)
        {
            return this == (Index3i)obj;
        }
        public override int GetHashCode()
        {
            unchecked // Overflow is fine, just wrap
            {
                int hash = (int) 2166136261;
                // Suitable nullity checks etc, of course :)
                hash = (hash * 16777619) ^ a.GetHashCode();
                hash = (hash * 16777619) ^ b.GetHashCode();
                hash = (hash * 16777619) ^ c.GetHashCode();
                return hash;
            }
        }
        public int CompareTo(Index3i other)
        {
            if (a != other.a)
                return a < other.a ? -1 : 1;
            else if (b != other.b)
                return b < other.b ? -1 : 1;
            else if (c != other.c)
                return c < other.c ? -1 : 1;
            return 0;
        }
        public bool Equals(Index3i other)
        {
            return (a == other.a && b == other.b && c == other.c);
        }        


        public override string ToString() {
            return string.Format("[{0},{1},{2}]", a, b, c);
        }

    }












    public struct Index2i : IComparable<Index2i>, IEquatable<Index2i>
    {
        public int a;
        public int b;

        public Index2i(int z) { a = b = z; }
        public Index2i(int ii, int jj) { a = ii; b = jj; }
        public Index2i(int[] i2) { a = i2[0]; b = i2[1]; }
        public Index2i(Index2i copy) { a = copy.a; b = copy.b; }

        static public readonly Index2i Zero = new Index2i(0, 0);
        static public readonly Index2i One = new Index2i(1, 1);
        static public readonly Index2i Max = new Index2i(int.MaxValue, int.MaxValue);
        static public readonly Index2i Min = new Index2i(int.MinValue, int.MinValue);


        public int this[int key]
        {
            get { return (key == 0) ? a : b; }
            set { if (key == 0) a = value; else b = value; }
        }

        public int[] array {
            get { return new int[] { a, b }; }
        }


        public int LengthSquared
        {
            get { return a * a + b * b; }
        }
        public int Length
        {
            get { return (int)Math.Sqrt(LengthSquared); }
        }


        public void Set(Index2i o)
        {
            a = o[0]; b = o[1];
        }
        public void Set(int ii, int jj)
        {
            a = ii; b = jj;
        }


        public static Index2i operator -(Index2i v)
        {
            return new Index2i(-v.a, -v.b);
        }

        public static Index2i operator *(int f, Index2i v)
        {
            return new Index2i(f * v.a, f * v.b);
        }
        public static Index2i operator *(Index2i v, int f)
        {
            return new Index2i(f * v.a, f * v.b);
        }
        public static Index2i operator /(Index2i v, int f)
        {
            return new Index2i(v.a / f, v.b / f);
        }


        public static Index2i operator *(Index2i a, Index2i b)
        {
            return new Index2i(a.a * b.a, a.b * b.b);
        }
        public static Index2i operator /(Index2i a, Index2i b)
        {
            return new Index2i(a.a / b.a, a.b / b.b);
        }


        public static Index2i operator +(Index2i v0, Index2i v1)
        {
            return new Index2i(v0.a + v1.a, v0.b + v1.b);
        }
        public static Index2i operator +(Index2i v0, int f)
        {
            return new Index2i(v0.a + f, v0.b + f);
        }

        public static Index2i operator -(Index2i v0, Index2i v1)
        {
            return new Index2i(v0.a - v1.a, v0.b - v1.b);
        }
        public static Index2i operator -(Index2i v0, int f)
        {
            return new Index2i(v0.a - f, v0.b - f);
        }


        public static bool operator ==(Index2i a, Index2i b)
        {
            return (a.a == b.a && a.b == b.b);
        }
        public static bool operator !=(Index2i a, Index2i b)
        {
            return (a.a != b.a || a.b != b.b);
        }
        public override bool Equals(object obj)
        {
            return this == (Index2i)obj;
        }
        public override int GetHashCode()
        {
            unchecked // Overflow is fine, just wrap
            {
                int hash = (int) 2166136261;
                // Suitable nullity checks etc, of course :)
                hash = (hash * 16777619) ^ a.GetHashCode();
                hash = (hash * 16777619) ^ b.GetHashCode();
                return hash;
            }
        }
        public int CompareTo(Index2i other)
        {
            if (a != other.a)
                return a < other.a ? -1 : 1;
            else if (b != other.b)
                return b < other.b ? -1 : 1;
            return 0;
        }
        public bool Equals(Index2i other)
        {
            return (a == other.a && b == other.b);
        }        


        public override string ToString() {
            return string.Format("[{0},{1}]", a, b);
        }

    }









    public struct Index4i
    {
        public int a;
        public int b;
        public int c;
        public int d;

        public Index4i(int z) { a = b = c = d = z; }
        public Index4i(int aa, int bb, int cc, int dd) { a = aa; b = bb; c = cc; d = dd; }
        public Index4i(int[] i2) { a = i2[0]; b = i2[1]; c = i2[2]; d = i2[3]; }
        public Index4i(Index4i copy) { a = copy.a; b = copy.b; c = copy.b; d = copy.d; }

        static public readonly Index4i Zero = new Index4i(0, 0, 0, 0);
        static public readonly Index4i One = new Index4i(1, 1, 1, 1);
        static public readonly Index4i Max = new Index4i(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue);


        public int this[int key]
        {
            get { return (key == 0) ? a : (key == 1) ? b : (key == 2) ? c : d; }
            set { if (key == 0) a = value; else if (key == 1) b = value; else if (key == 2) c = value; else d = value; }
        }

        public int[] array {
            get { return new int[4] { a, b, c, d }; }
        }


        public int LengthSquared
        {
            get { return a * a + b * b + c * c + d*d; }
        }
        public int Length
        {
            get { return (int)Math.Sqrt(LengthSquared); }
        }


        public void Set(Index4i o)
        {
            a = o[0]; b = o[1]; c = o[2]; d = o[3];
        }
        public void Set(int aa, int bb, int cc, int dd)
        {
            a = aa; b = bb; c = cc; d = dd;
        }


        public bool Contains(int val)
        {
            return a == val || b == val || c == val || d == val;
        }

        public void Sort()
        {
            int tmp;   // if we use 2 temp ints, we can swap in a different order where some test pairs
                       // could be done simultaneously, but no idea if compiler would optimize that anyway...
            if (d < c) { tmp = d; d = c; c = tmp; }
            if (c < b) { tmp = c; c = b; b = tmp; }
            if (b < a) { tmp = b; b = a; a = tmp; }   // now a is smallest value
            if (b > c) { tmp = c; c = b; b = tmp; }
            if (c > d) { tmp = d; d = c; c = tmp; }   // now d is largest value
            if (b > c) { tmp = c; c = b; b = tmp; }   // bow b,c are sorted
        }


        public static Index4i operator -(Index4i v)
        {
            return new Index4i(-v.a, -v.b, -v.c, -v.d);
        }

        public static Index4i operator *(int f, Index4i v)
        {
            return new Index4i(f * v.a, f * v.b, f * v.c, f * v.d);
        }
        public static Index4i operator *(Index4i v, int f)
        {
            return new Index4i(f * v.a, f * v.b, f * v.c, f * v.d);
        }
        public static Index4i operator /(Index4i v, int f)
        {
            return new Index4i(v.a / f, v.b / f, v.c / f, v.d / f);
        }


        public static Index4i operator *(Index4i a, Index4i b)
        {
            return new Index4i(a.a * b.a, a.b * b.b, a.c * b.c, a.d * b.d);
        }
        public static Index4i operator /(Index4i a, Index4i b)
        {
            return new Index4i(a.a / b.a, a.b / b.b, a.c / b.c, a.d / b.d);
        }


        public static Index4i operator +(Index4i v0, Index4i v1)
        {
            return new Index4i(v0.a + v1.a, v0.b + v1.b, v0.c + v1.c, v0.d + v1.d);
        }
        public static Index4i operator +(Index4i v0, int f)
        {
            return new Index4i(v0.a + f, v0.b + f, v0.c + f, v0.d + f);
        }

        public static Index4i operator -(Index4i v0, Index4i v1)
        {
            return new Index4i(v0.a - v1.a, v0.b - v1.b, v0.c - v1.c, v0.d - v1.d);
        }
        public static Index4i operator -(Index4i v0, int f)
        {
            return new Index4i(v0.a - f, v0.b - f, v0.c - f, v0.d - f);
        }


        public static bool operator ==(Index4i a, Index4i b)
        {
            return (a.a == b.a && a.b == b.b && a.c == b.c && a.d == b.d);
        }
        public static bool operator !=(Index4i a, Index4i b)
        {
            return (a.a != b.a || a.b != b.b || a.c != b.c || a.d != b.d);
        }
        public override bool Equals(object obj)
        {
            return this == (Index4i)obj;
        }
        public override int GetHashCode()
        {
            unchecked // Overflow is fine, just wrap
            {
                int hash = (int) 2166136261;
                // Suitable nullity checks etc, of course :)
                hash = (hash * 16777619) ^ a.GetHashCode();
                hash = (hash * 16777619) ^ b.GetHashCode();
                hash = (hash * 16777619) ^ c.GetHashCode();
                hash = (hash * 16777619) ^ d.GetHashCode();
                return hash;
            }
        }
        public int CompareTo(Index4i other)
        {
            if (a != other.a)
                return a < other.a ? -1 : 1;
            else if (b != other.b)
                return b < other.b ? -1 : 1;
            else if (c != other.c)
                return c < other.c ? -1 : 1;
            else if (d != other.d)
                return d < other.d ? -1 : 1;
            return 0;
        }
        public bool Equals(Index4i other)
        {
            return (a == other.a && b == other.b && c == other.c&& d == other.d);
        }  
        


        public override string ToString() {
            return string.Format("[{0},{1},{2},{3}]", a, b, c, d);
        }

    }



}
