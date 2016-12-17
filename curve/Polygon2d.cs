using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace g3
{
    public class Polygon2d
    {
        List<Vector2d> vertices;

        public Polygon2d() {
            vertices = new List<Vector2d>();
        }

        public Polygon2d(Polygon2d copy)
        {
            vertices = new List<Vector2d>(copy.vertices);
        }

        public Polygon2d(Vector2d[] v)
        {
            vertices = new List<Vector2d>(v);
        }
        public Polygon2d(VectorArray2d v)
        {
            vertices = new List<Vector2d>(v.AsVector2d());
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
