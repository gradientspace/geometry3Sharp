using System;

namespace g3 
{
	// ported from WildMagic5 
    // [TODO] Vector2d 6-tuple, to avoid internal arrays
	public class IntrTriangle2Triangle2
	{
		Triangle2d triangle0;
		public Triangle2d Triangle0
		{
			get { return triangle0; }
			set { triangle0 = value; Result = IntersectionResult.NotComputed; }
		}

		Triangle2d triangle1;
		public Triangle2d Triangle1
		{
			get { return triangle1; }
			set { triangle1 = value; Result = IntersectionResult.NotComputed; }
		}


		public int Quantity = 0;
		public IntersectionResult Result = IntersectionResult.NotComputed;
		public IntersectionType Type = IntersectionType.Empty;

		public bool IsSimpleIntersection {
			get { return Result == IntersectionResult.Intersects && Type == IntersectionType.Point; }
		}

		// intersection polygon - this array will always be 6 elements long,
		// however only the first Quantity vertices will be valid
		public Vector2d[] Points;

		public IntrTriangle2Triangle2(Triangle2d t0, Triangle2d t1)
		{
			triangle0 = t0;
			triangle1 = t1;
			Points = null;
		}


		public bool Test ()
		{
			int i0, i1;
			Vector2d dir = Vector2d.Zero;

			// Test edges of triangle0 for separation.
			for (i0 = 0, i1 = 2; i0 < 3; i1 = i0++)
			{
				// Test axis V0[i1] + t*perp(V0[i0]-V0[i1]), perp(x,y) = (y,-x).
				dir.x = triangle0[i0].y - triangle0[i1].y;
				dir.y = triangle0[i1].x - triangle0[i0].x;
				if (WhichSide(triangle1, triangle0[i1], dir) > 0) {
					// Triangle1 is entirely on positive side of triangle0 edge.
					return false;
				}
			}

			// Test edges of triangle1 for separation.
			for (i0 = 0, i1 = 2; i0 < 3; i1 = i0++)
			{
				// Test axis V1[i1] + t*perp(V1[i0]-V1[i1]), perp(x,y) = (y,-x).
				dir.x = triangle1[i0].y - triangle1[i1].y;
				dir.y = triangle1[i1].x - triangle1[i0].x;
				if (WhichSide(triangle0, triangle1[i1], dir) > 0) {
					// Triangle0 is entirely on positive side of triangle1 edge.
					return false;
				}
			}

			return true;
		}

			



		public IntrTriangle2Triangle2 Compute()
		{
			Find();
			return this;
		}


		public bool Find()
		{
			if (Result != IntersectionResult.NotComputed)
				return (Result == IntersectionResult.Intersects);

			// The potential intersection is initialized to triangle1.  The set of
			// vertices is refined based on clipping against each edge of triangle0.
			Quantity = 3;
			Points = new Vector2d[6];
			for (int i = 0; i < 3; ++i) {
				Points[i] = triangle1[i];
			}

			for (int i1 = 2, i0 = 0; i0 < 3; i1 = i0++)
			{
				// Clip against edge <V0[i1],V0[i0]>.
				Vector2d N = new Vector2d(
					triangle0[i1].y - triangle0[i0].y,
					triangle0[i0].x - triangle0[i1].x);
				double c = N.Dot(triangle0[i1]);
				ClipConvexPolygonAgainstLine(N, c, ref Quantity, ref Points);
				if (Quantity == 0) {
					// Triangle completely clipped, no intersection occurs.
					Type = IntersectionType.Empty;
				} else if ( Quantity == 1 ) {
					Type = IntersectionType.Point;
				} else if ( Quantity == 2 ) {
					Type = IntersectionType.Segment;
				} else {
					Type = IntersectionType.Polygon;
				}
			}

			Result = (Type != IntersectionType.Empty) ?
				IntersectionResult.Intersects : IntersectionResult.NoIntersection;
			return (Result == IntersectionResult.Intersects);
		}




		public static int WhichSide (Triangle2d V, Vector2d P, Vector2d D)
		{
			// Vertices are projected to the form P+t*D.  Return value is +1 if all
			// t > 0, -1 if all t < 0, 0 otherwise, in which case the line splits the
			// triangle.

			int positive = 0, negative = 0, zero = 0;
			for (int i = 0; i < 3; ++i)
			{
				double t = D.Dot(V[i] - P);
				if (t > (double)0)
				{
					++positive;
				}
				else if (t < (double)0)
				{
					++negative;
				}
				else
				{
					++zero;
				}

				if (positive > 0 && negative > 0)
				{
					return 0;
				}
			}
			return (zero == 0 ? (positive > 0 ? 1 : -1) : 0);
		}



		// Vin is input polygon vertices, returns clipped polygon, vertex count of
		//   clipped polygon is returned in quantity
		// **NOTE** returned array may have more elements than quantity!!
		public static void ClipConvexPolygonAgainstLine (
			Vector2d N, double  c, ref int quantity, ref Vector2d[] V)
		{
			// The input vertices are assumed to be in counterclockwise order.  The
			// ordering is an invariant of this function.

			// Test on which side of line the vertices are.
			int positive = 0, negative = 0, pIndex = -1;
			double[] test = new double[6];
			int i;
			for (i = 0; i < quantity; ++i)
			{
				test[i] = N.Dot(V[i]) - c;
				if (test[i] > (double)0)
				{
					positive++;
					if (pIndex < 0)
					{
						pIndex = i;
					}
				}
				else if (test[i] < (double)0)
				{
					negative++;
				}
			}

			if (positive > 0)
			{
				if (negative > 0)
				{
					// Line transversely intersects polygon.
					Vector2d[] CV = new Vector2d[6];
					int cQuantity = 0, cur, prv;
					double t;

					if (pIndex > 0) {
						// First clip vertex on line.
						cur = pIndex;
						prv = cur - 1;
						t = test[cur]/(test[cur] - test[prv]);
						CV[cQuantity++] = V[cur] + t*(V[prv] - V[cur]);

						// Vertices on positive side of line.
						while (cur < quantity && test[cur] > (double)0) {
							CV[cQuantity++] = V[cur++];
						}

						// Last clip vertex on line.
						if (cur < quantity) {
							prv = cur - 1;
						} else {
							cur = 0;
							prv = quantity - 1;
						}
						t = test[cur]/(test[cur] - test[prv]);
						CV[cQuantity++] = V[cur] + t*(V[prv]-V[cur]);
					}
					else  // pIndex is 0
					{
						// Vertices on positive side of line.
						cur = 0;
						while (cur < quantity && test[cur] > (double)0)
						{
							CV[cQuantity++] = V[cur++];
						}

						// Last clip vertex on line.
						prv = cur - 1;
						t = test[cur]/(test[cur] - test[prv]);
						CV[cQuantity++] = V[cur] + t*(V[prv] - V[cur]);

						// Skip vertices on negative side.
						while (cur < quantity && test[cur] <= (double)0)
						{
							++cur;
						}

						// First clip vertex on line.
						if (cur < quantity)
						{
							prv = cur - 1;
							t = test[cur]/(test[cur] - test[prv]);
							CV[cQuantity++] = V[cur] + t*(V[prv] - V[cur]);

							// Vertices on positive side of line.
							while (cur < quantity && test[cur] > (double)0)
							{
								CV[cQuantity++] = V[cur++];
							}
						}
						else
						{
							// cur = 0
							prv = quantity - 1;
							t = test[0]/(test[0] - test[prv]);
							CV[cQuantity++] = V[0] + t*(V[prv] - V[0]);
						}
					}

					quantity = cQuantity;
					Array.Copy(CV, V, cQuantity);
				}
				// else polygon fully on positive side of line, nothing to do.
			} else {
				// Polygon does not intersect positive side of line, clip all.
				quantity = 0;
			}
		}


	}
}
