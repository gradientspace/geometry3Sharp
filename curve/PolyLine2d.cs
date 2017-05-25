using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace g3
{
	public class PolyLine2d : IEnumerable<Vector2d>
	{
		protected List<Vector2d> vertices;
		public int Timestamp;

		public PolyLine2d() {
			vertices = new List<Vector2d>();
			Timestamp = 0;
		}

		public PolyLine2d(PolyLine2d copy)
		{
			vertices = new List<Vector2d>(copy.vertices);
			Timestamp = 0;
		}

		public PolyLine2d(Polygon2d copy)
		{
			vertices = new List<Vector2d>(copy);
			vertices.Add(copy.Start);  // duplicate start vert
			Timestamp = 0;
		}

		public PolyLine2d(Vector2d[] v)
		{
			vertices = new List<Vector2d>(v);
			Timestamp = 0;
		}
		public PolyLine2d(VectorArray2d v)
		{
			vertices = new List<Vector2d>(v.AsVector2d());
			Timestamp = 0;
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
			get { return vertices[vertices.Count-1]; }
		}


		public ReadOnlyCollection<Vector2d> Vertices {
			get { return vertices.AsReadOnly(); }
		}

		public int VertexCount
		{
			get { return vertices.Count; }
		}

		public virtual void AppendVertex(Vector2d v)
		{
			vertices.Add(v);
			Timestamp++; 
		}

		public virtual void AppendVertices(IEnumerable<Vector2d> v) 
		{
			vertices.AddRange(v);
			Timestamp++;
		}


		public virtual void Reverse()
		{
			vertices.Reverse();
			Timestamp++;
		}


		public Vector2d GetTangent(int i)
		{
			if (i == 0)
				return (vertices[1] - vertices[0]).Normalized;
			else if (i == vertices.Count - 1)
				return (vertices[vertices.Count - 1] - vertices[vertices.Count - 2]).Normalized;
			else
				return (vertices[i + 1] - vertices[i - 1]).Normalized;
		}


		public AxisAlignedBox2d GetBounds() {
			if ( vertices.Count == 0 )
				return AxisAlignedBox2d.Empty;
			AxisAlignedBox2d box = new AxisAlignedBox2d(vertices[0]);
			for ( int i = 1; i < vertices.Count; ++i )
				box.Contain(vertices[i]);
			return box;
		}



        public double DistanceSquared(Vector2d point)
        {
            double fNearestSqr = Double.MaxValue;
            for ( int i = 0; i < vertices.Count-1; ++i ) {
                Segment2d seg = new Segment2d(vertices[i], vertices[i + 1]);
                double d = seg.DistanceSquared(point);
                if (d < fNearestSqr)
                    fNearestSqr = d;
            }
            return fNearestSqr;
        }


		public IEnumerable<Segment2d> SegmentItr() {
			for ( int i = 0; i < vertices.Count-1; ++i )
				yield return new Segment2d( vertices[i], vertices[i+1] );
		}

		public IEnumerator<Vector2d> GetEnumerator() {
			return vertices.GetEnumerator();
		}
		IEnumerator IEnumerable.GetEnumerator() {
			return vertices.GetEnumerator();
		}


		public double Length {
			get {
				double fLength = 0;
				int N = vertices.Count;
				for (int i = 0; i < N-1; ++i)
					fLength += vertices[i].Distance(vertices[i + 1]);
				return fLength;
			}
		}




		// Polyline simplification (converted from Polyogon2d.simplifyDP)
		// code adapted from: http://softsurfer.com/Archive/algorithm_0205/algorithm_0205.htm
		// simplifyDP():
		//  This is the Douglas-Peucker recursive simplification routine
		//  It just marks vertices that are part of the simplified polyline
		//  for approximating the polyline subchain v[j] to v[k].
		//    Input:  tol = approximation tolerance
		//            v[] = polyline array of vertex points
		//            j,k = indices for the subchain v[j] to v[k]
		//    Output: mk[] = array of markers matching vertex array v[]
		static void simplifyDP(double tol, Vector2d[] v, int j, int k, bool[] mk)
		{
			if (k <= j + 1) // there is nothing to simplify
				return;

			// check for adequate approximation by segment S from v[j] to v[k]
			int maxi = j;          // index of vertex farthest from S
			double maxd2 = 0;         // distance squared of farthest vertex
			double tol2 = tol * tol;  // tolerance squared
			Segment2d S = new Segment2d(v[j], v[k]);    // segment from v[j] to v[k]

			// test each vertex v[i] for max distance from S
			// Note: this works in any dimension (2D, 3D, ...)
			for (int i = j + 1; i < k; i++) {
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
				simplifyDP(tol, v, j, maxi, mk);  // polyline v[j] to v[maxi]
				simplifyDP(tol, v, maxi, k, mk);  // polyline v[maxi] to v[k]
			}
			// else the approximation is OK, so ignore intermediate vertices
			return;
		}



		public virtual void Simplify(double clusterTol = 0.0001,
							  double lineDeviationTol = 0.01,
							  bool bSimplifyStraightLines = true)
		{
			int n = vertices.Count;

			int i, k, pv;            // misc counters
			Vector2d[] vt = new Vector2d[n];  // vertex buffer
			bool[] mk = new bool[n];
			for (i = 0; i < n; ++i)     // marker buffer
				mk[i] = false;

			// STAGE 1.  Vertex Reduction within tolerance of prior vertex cluster
			double clusterTol2 = clusterTol * clusterTol;
			vt[0] = vertices[0];              // start at the beginning
			for (i = k = 1, pv = 0; i < n; i++) {
				if ((vertices[i] - vertices[pv]).LengthSquared < clusterTol2)
					continue;
				vt[k++] = vertices[i];
				pv = i;
			}
			if (pv < n - 1)
				vt[k++] = vertices[n - 1];      // finish at the end

			// STAGE 2.  Douglas-Peucker polyline simplification
			if (lineDeviationTol > 0) {
				mk[0] = mk[k - 1] = true;       // mark the first and last vertices
				simplifyDP(lineDeviationTol, vt, 0, k - 1, mk);
			} else {
				for (i = 0; i < k; ++i)
					mk[i] = true;
			}

			// copy marked vertices back to this polygon
			vertices = new List<Vector2d>();
			for (i = 0; i < k; ++i) {
				if (mk[i])
					vertices.Add(vt[i]);
			}
			Timestamp++;

			return;
		}

	}
}
