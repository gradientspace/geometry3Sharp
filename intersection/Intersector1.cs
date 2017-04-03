using System;

namespace g3 
{
	// ported from WildMagic5
	//
	// A class for intersection of intervals [u0,u1] and [v0,v1].  The end
	// points must be ordered:  u0 <= u1 and v0 <= v1.  Values of MAX_REAL
	// and -MAX_REAL are allowed, and degenerate intervals are allowed:
	// u0 = u1 or v0 = v1.
	//
    // [TODO] could this be struct? is not used in contexts where we necessarily need a new object...
    //
	public class Intersector1 
	{
		// intervals to intersect
		public Interval1d U;
		public Interval1d V;


		// Information about the intersection set.  The number of intersections
		// is 0 (intervals do not overlap), 1 (intervals are just touching), or
		// 2 (intervals intersect in an inteval).
		public int NumIntersections = 0;

		// intersection point/interval, access via GetIntersection
		private Interval1d Intersections = Interval1d.Zero;

		public Intersector1(double u0, double u1, double v0, double v1) {
			// [TODO] validate 0 < 1
			U = new Interval1d(u0,u1);
			V = new Interval1d(v0,v1);
		}
		public Intersector1(Interval1d u, Interval1d v) {
			U = u;
			V = v;
		}

		public bool Test {
			get { return U.a <= V.b && U.b >= V.a; }
		}
			

		public double GetIntersection(int i) {
			return Intersections[i];
		}

		public bool Find() 
		{
			if (U.b < V.a || U.a > V.b) {
				NumIntersections = 0;
			} else if (U.b > V.a) {
				if (U.a < V.b) {
					NumIntersections = 2;
					Intersections.a = (U.a < V.a ? V.a : U.a);
					Intersections.b = (U.b > V.b ? V.b : U.b);
					if (Intersections.a == Intersections.b) {
						NumIntersections = 1;
					}
				} else {  
					// U.a == V.b
					NumIntersections = 1;
					Intersections.a = U.a;
				}
			} else {
				// U.b == V.a
				NumIntersections = 1;
				Intersections.a = U.b;
			}

			return NumIntersections > 0;
		}
	}
}
