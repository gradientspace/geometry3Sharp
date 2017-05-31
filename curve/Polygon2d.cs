using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace g3
{
	public class Polygon2d : IEnumerable<Vector2d>, IDuplicatable<Polygon2d>
    {
        protected List<Vector2d> vertices;
		public int Timestamp;

        public Polygon2d() {
            vertices = new List<Vector2d>();
			Timestamp = 0;
        }

        public Polygon2d(Polygon2d copy)
        {
            vertices = new List<Vector2d>(copy.vertices);
			Timestamp = 0;
        }

        public Polygon2d(Vector2d[] v)
        {
            vertices = new List<Vector2d>(v);
			Timestamp = 0;
        }
        public Polygon2d(VectorArray2d v)
        {
            vertices = new List<Vector2d>(v.AsVector2d());
			Timestamp = 0;
        }

		public virtual Polygon2d Duplicate() {
			Polygon2d p = new Polygon2d(this);
			p.Timestamp = this.Timestamp;
			return p;
		}


		public Vector2d this[int key]
		{
			get { return vertices[key]; }
			set { vertices[key] = value; Timestamp++; }
		}

		public Vector2d Start {
			get { return vertices[0]; }
		}
		public Vector2d End {
			get { return vertices.Last(); }
		}


        public ReadOnlyCollection<Vector2d> Vertices {
            get { return vertices.AsReadOnly(); }
        }

        public int VertexCount
        {
            get { return vertices.Count; }
        }

        public void AppendVertex(Vector2d v)
        {
            vertices.Add(v);
			Timestamp++; 
        }

		public void Reverse()
		{
			vertices.Reverse();
			Timestamp++;
		}


        public Vector2d GetTangent(int i)
        {
			Vector2d next = vertices[(i+1)%vertices.Count];
			Vector2d prev = vertices[i==0 ? vertices.Count-1 : i-1];
			return (next-prev).Normalized;
        }


		public AxisAlignedBox2d GetBounds() {
			if ( vertices.Count == 0 )
				return AxisAlignedBox2d.Empty;
			AxisAlignedBox2d box = new AxisAlignedBox2d(vertices[0]);
			for ( int i = 1; i < vertices.Count; ++i )
				box.Contain(vertices[i]);
			return box;
		}


		public IEnumerable<Segment2d> SegmentItr() {
			for ( int i = 0; i < vertices.Count; ++i )
				yield return new Segment2d( vertices[i], vertices[ (i+1) % vertices.Count ] );
		}

		public IEnumerator<Vector2d> GetEnumerator() {
			for ( int i = 0; i < vertices.Count; ++i )
				yield return vertices[i];
			yield return vertices[0];
		}
		IEnumerator IEnumerable.GetEnumerator() {
			for ( int i = 0; i < vertices.Count; ++i )
				yield return vertices[i];
			yield return vertices[0];
		}



		public bool IsClockwise {
			get { return SignedArea < 0; }
		}
		public double SignedArea {
			get {
				double fArea = 0;
				int N = vertices.Count;
				for (int i = 0; i < N; ++i) {
					Vector2d v1 = vertices[i];
					Vector2d v2 = vertices[(i+1) % N];
					fArea += v1.x * v2.y - v1.y * v2.x;
				}
				return fArea * 0.5;	
			}
		}


        public double Perimeter
        {
            get {
                double fPerim = 0;
				int N = vertices.Count;
				for (int i = 0; i < N; ++i) 
                    fPerim += vertices[i].Distance( vertices[(i+1) % N] );
                return fPerim;
            }
        }



        public void NeighboursP(int iVertex, ref Vector2d p0, ref Vector2d p1)
        {
            int N = vertices.Count;
            p0 = vertices[(iVertex == 0) ? N - 1 : iVertex - 1];
            p1 = vertices[(iVertex + 1) % N];
        }
        public void NeighboursV(int iVertex, ref Vector2d v0, ref Vector2d v1, bool bNormalize = false)
        {
            int N = vertices.Count;
            v0 = vertices[(iVertex == 0) ? N - 1 : iVertex - 1] - vertices[iVertex];
            v1 = vertices[(iVertex + 1) % N] - vertices[iVertex];
            if ( bNormalize ) {
                v0.Normalize();
                v1.Normalize();
            }
        }

        public double OpeningAngleDeg(int iVertex)
        {
            Vector2d e0 = Vector2d.Zero, e1 = Vector2d.Zero;
            NeighboursV(iVertex, ref e0, ref e1, true);
            return Vector2d.AngleD(e0, e1);
        }



		public bool Contains( Vector2d vTest )
		{
			int nWindingNumber = 0;   // winding number counter

			int N = vertices.Count;
			for (int i = 0; i < N; ++i) {

				int iNext = (i+1) % N;

				if (vertices[i].y <= vTest.y) {         
					// start y <= P.y
					if (vertices[iNext].y > vTest.y) {                         // an upward crossing
						if (MathUtil.IsLeft( vertices[i], vertices[iNext], vTest) > 0)  // P left of edge
							++nWindingNumber;                                      // have a valid up intersect
					}
				} else {                       
					// start y > P.y (no test needed)
					if (vertices[iNext].y <= vTest.y) {                        // a downward crossing
						if (MathUtil.IsLeft( vertices[i], vertices[iNext], vTest) < 0)  // P right of edge
							--nWindingNumber;                                      // have a valid down intersect
					}
				}
			}

			return nWindingNumber != 0;
		}



		public bool Contains(Polygon2d o) {

			// [TODO] fast bbox check?

			int N = o.VertexCount;
			for ( int i = 0; i < N; ++i ) {
				if ( Contains(o[i]) == false )
					return false;
			}

			if ( Intersects(o) )
				return false;

			return true;
		}


		public bool Intersects(Polygon2d o) {
			if ( ! this.GetBounds().Intersects( o.GetBounds() ) )
				return false;

			foreach ( Segment2d seg in SegmentItr() ) {
				foreach ( Segment2d oseg in o.SegmentItr() ) {
					IntrSegment2Segment2 intr = new IntrSegment2Segment2(seg, oseg);
					if ( intr.Find() )
						return true;
				}
			}
			return false;
		}


		public List<Vector2d> FindIntersections(Polygon2d o) {
			List<Vector2d> v = new List<Vector2d>();
			if ( ! this.GetBounds().Intersects( o.GetBounds() ) )
				return v;

			foreach ( Segment2d seg in SegmentItr() ) {
				foreach ( Segment2d oseg in o.SegmentItr() ) {
					IntrSegment2Segment2 intr = new IntrSegment2Segment2(seg, oseg);
					if ( intr.Find() ) {
						v.Add( intr.Point0 );
						if ( intr.Quantity == 2 )
							v.Add( intr.Point1 );
						break;
					}
				}
			}
			return v;
		}



		public void Translate(Vector2d translate) {
			int N = vertices.Count;
			for (int i = 0; i < N; ++i)
				vertices[i] += translate;
		}

		public void Scale(Vector2d scale, Vector2d origin) {
			int N = vertices.Count;
			for (int i = 0; i < N; ++i)
				vertices[i] = scale * (vertices[i] - origin) + origin;
		}



		// Polygon simplification
		// code adapted from: http://softsurfer.com/Archive/algorithm_0205/algorithm_0205.htm
		// simplifyDP():
		//  This is the Douglas-Peucker recursive simplification routine
		//  It just marks vertices that are part of the simplified polyline
		//  for approximating the polyline subchain v[j] to v[k].
		//    Input:  tol = approximation tolerance
		//            v[] = polyline array of vertex points
		//            j,k = indices for the subchain v[j] to v[k]
		//    Output: mk[] = array of markers matching vertex array v[]
		static void simplifyDP( double tol, Vector2d[] v, int j, int k, bool[] mk )
		{
			if (k <= j+1) // there is nothing to simplify
				return;

			// check for adequate approximation by segment S from v[j] to v[k]
			int maxi = j;          // index of vertex farthest from S
			double maxd2 = 0;         // distance squared of farthest vertex
			double tol2 = tol * tol;  // tolerance squared
			Segment2d S = new Segment2d(v[j], v[k]);    // segment from v[j] to v[k]

			// test each vertex v[i] for max distance from S
			// Note: this works in any dimension (2D, 3D, ...)
			for (int i = j+1; i < k; i++) {
				double dv2 = S.DistanceSquared(v[i]);
				if (dv2 <= maxd2)
					continue;
				// v[i] is a new max vertex
				maxi = i;
				maxd2 = dv2;
			}
			if (maxd2 > tol2) {       // error is worse than the tolerance
				// split the polyline at the farthest vertex from S
				mk[maxi] = true;      // mark v[maxi] for the simplified polyline
				// recursively simplify the two subpolylines at v[maxi]
				simplifyDP( tol, v, j, maxi, mk );  // polyline v[j] to v[maxi]
				simplifyDP( tol, v, maxi, k, mk );  // polyline v[maxi] to v[k]
			}
			// else the approximation is OK, so ignore intermediate vertices
			return;
		}



		public void Simplify( double clusterTol = 0.0001,
		                      double lineDeviationTol = 0.01,
							  bool bSimplifyStraightLines = true )
		{
			int n = vertices.Count;

			int i, k, pv;            // misc counters
			Vector2d[] vt = new Vector2d[n];  // vertex buffer
			bool[] mk = new bool[n];
			for ( i = 0; i < n; ++i )		// marker buffer
				mk[i] = false;		 

			// STAGE 1.  Vertex Reduction within tolerance of prior vertex cluster
			double clusterTol2 = clusterTol*clusterTol;
			vt[0] = vertices[0];              // start at the beginning
			for (i = k = 1, pv = 0; i < n; i++) {
				if ( (vertices[i] - vertices[pv]).LengthSquared < clusterTol2 )
					continue;
				vt[k++] = vertices[i];
				pv = i;
			}
			if (pv < n-1)
				vt[k++] = vertices[n-1];      // finish at the end

			// STAGE 2.  Douglas-Peucker polyline simplification
			if (lineDeviationTol > 0) {
				mk[0] = mk[k-1] = true;       // mark the first and last vertices
				simplifyDP( lineDeviationTol, vt, 0, k-1, mk );
			} else {
				for (i = 0; i < k; ++i )
					mk[i] = true;
			}

			// copy marked vertices back to this polygon
			vertices = new List<Vector2d>();
			for (i = 0; i < k; ++i) {
				if (mk[i])
					vertices.Add( vt[i] );
			}
			Timestamp++;

			return;
		}








        static public Polygon2d MakeCircle(double fRadius, int nSteps, double angleShiftRad = 0)
        {
            VectorArray2d vertices = new VectorArray2d(nSteps);

            for ( int i = 0; i < nSteps; ++i ) {
                double t = (double)i / (double)nSteps;
                double a = MathUtil.TwoPI * t + angleShiftRad;
                vertices.Set(i, fRadius * Math.Cos(a), fRadius * Math.Sin(a));
            }

            return new Polygon2d(vertices);
        }


    }
}
