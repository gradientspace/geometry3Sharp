using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace g3
{
    public class Polygon2d
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





		public bool IsClockwise() {
			return SignedArea < 0;
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
				return fArea;	
			}
		}


		public bool IsInside( Vector2d vTest )
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




        static public Polygon2d MakeCircle(float fRadius, int nSteps)
        {
            VectorArray2d vertices = new VectorArray2d(nSteps);

            for ( int i = 0; i < nSteps; ++i ) {
                double t = (double)i / (double)nSteps;
                double a = MathUtil.TwoPI * t;
                vertices.Set(i, fRadius * Math.Cos(a), fRadius * Math.Sin(a));
            }

            return new Polygon2d(vertices);
        }


    }
}
