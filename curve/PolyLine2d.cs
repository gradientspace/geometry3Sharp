using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace g3
{
	public class PolyLine2d
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

		public void AppendVertex(Vector2d v)
		{
			vertices.Add(v);
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


		public IEnumerable<Segment2d> SegmentItr() {
			for ( int i = 0; i < vertices.Count-1; ++i )
				yield return new Segment2d( vertices[i], vertices[i+1] );
		}

	}
}
