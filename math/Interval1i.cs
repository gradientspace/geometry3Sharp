using System;
using System.Collections;
using System.Collections.Generic;

namespace g3
{
	// Interval [a,b] over Integers
    // Note that Interval1i has an enumerator, so you can directly
    //  enumerate over the range of values (inclusive!!)
    //
	//   TODO: should check that a <= b !!
    public struct Interval1i : IEnumerable<int>
    {
		public int a;
		public int b;

        public Interval1i(int f) { a = b = f; }
        public Interval1i(int x, int y) { this.a = x; this.b = y; }
        public Interval1i(int[] v2) { a = v2[0]; b = v2[1]; }
        public Interval1i(Interval1i copy) { a = copy.a; b = copy.b; }


        static public readonly Interval1i Zero = new Interval1i(0, 0);
		static public readonly Interval1i Empty = new Interval1i(int.MaxValue, -int.MaxValue);
		static public readonly Interval1i Infinite = new Interval1i(-int.MaxValue, int.MaxValue);

        /// <summary> construct interval [0, N-1] </summary>
        static public Interval1i Range(int N) { return new Interval1i(0, N - 1); }

        /// <summary> construct interval [0, N-1] </summary>
        static public Interval1i RangeInclusive(int N) { return new Interval1i(0, N); }

        /// <summary> construct interval [start, start+N-1] </summary>
        static public Interval1i Range(int start, int N) { return new Interval1i(start, start+N - 1); }


		/// <summary> construct interval [a, b] </summary>
		static public Interval1i FromToInclusive(int a, int b) { return new Interval1i(a, b); }



        public int this[int key]
        {
            get { return (key == 0) ? a : b; }
            set { if (key == 0) a = value; else b = value; }
        }


        public int LengthSquared
        {
			get { return (a-b)*(a-b); }
        }
        public int Length
        {
            get { return b-a; }
        }

        public int Center {
            get { return (b + a) / 2; }
        }

		public void Contain(int d) {
            if (d < a)
                a = d;
            if (d > b)
                b = d;
		}

		public bool Contains(int d) {
			return d >= a && d <= b;
		}


		public bool Overlaps(Interval1i o) {
			return ! ( o.a > b || o.b < a ); 
		}

        public int SquaredDist(Interval1i o) {
			if ( b < o.a )
				return (o.a - b)*(o.a - b);
			else if ( a > o.b )
				return (a - o.b)*(a - o.b);
			else
				return 0;
        }
        public int Dist(Interval1i o) {
			if ( b < o.a )
				return o.a - b;
			else if ( a > o.b )
				return a - o.b;
			else
				return 0;
        }


        public void Set(Interval1i o) {
            a = o.a; b = o.b;
        }
        public void Set(int fA, int fB) {
            a = fA; b = fB;
        }



		public static Interval1i operator -(Interval1i v) {
			return new Interval1i(-v.a, -v.b);
		}


        public static Interval1i operator +(Interval1i a, int f) {
            return new Interval1i(a.a + f, a.b + f);
        }
        public static Interval1i operator -(Interval1i a, int f) {
            return new Interval1i(a.a - f, a.b - f);
        }

        public static Interval1i operator *(Interval1i a, int f) {
            return new Interval1i(a.a * f, a.b * f);
        }


        public IEnumerator<int> GetEnumerator() {
            for (int i = a; i <= b; ++i)
                yield return i;
        }
        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public override string ToString() {
            return string.Format("[{0},{1}]", a, b);
        }


    }
}
