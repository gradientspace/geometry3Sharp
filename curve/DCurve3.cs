using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    public class DCurve3 : ISampledCurve3d
    {
        // [TODO] use dvector? or double-indirection indexing?
        //   question is how to insert efficiently...
        protected List<Vector3d> vertices;
        public bool Closed { get; set; }
        public int Timestamp;

        public DCurve3()
        {
            vertices = new List<Vector3d>();
            Closed = false;
            Timestamp = 1;
        }

        public DCurve3(List<Vector3d> verticesIn, bool bClosed, bool bTakeOwnership = false)
        {
            if (bTakeOwnership)
                this.vertices = verticesIn;
            else
                this.vertices = new List<Vector3d>(verticesIn);
            Closed = bClosed;
            Timestamp = 1;
        }
        public DCurve3(IEnumerable<Vector3d> verticesIn, bool bClosed)
        {
            this.vertices = new List<Vector3d>(verticesIn);
            Closed = bClosed;
            Timestamp = 1;
        }

        public DCurve3(DCurve3 copy)
        {
            vertices = new List<Vector3d>(copy.vertices);
            Closed = copy.Closed;
            Timestamp = 1;
        }

        public DCurve3(ISampledCurve3d icurve)
        {
            this.vertices = new List<Vector3d>(icurve.Vertices);
            Closed = icurve.Closed;
            Timestamp = 1;
        }

        public void AppendVertex(Vector3d v) {
            vertices.Add(v);
            Timestamp++;
        }

        public int VertexCount {
            get { return vertices.Count; }
        }

        public Vector3d GetVertex(int i) {
            return vertices[i];
        }
        public void SetVertex(int i, Vector3d v) {
            vertices[i] = v;
            Timestamp++;
        }

        public void SetVertices(VectorArray3d v)
        {
            vertices = new List<Vector3d>();
            for (int i = 0; i < v.Count; ++i)
                vertices.Add(v[i]);
            Timestamp++;
        }

        public void SetVertices(IEnumerable<Vector3d> v)
        {
            vertices = new List<Vector3d>(v);
            Timestamp++;
        }

        public void SetVertices(List<Vector3d> vertices, bool bTakeOwnership)
        {
            if (bTakeOwnership)
                this.vertices = vertices;
            else
                this.vertices = new List<Vector3d>(vertices);
            Timestamp++;
        }

        public void ClearVertices()
        {
            vertices = new List<Vector3d>();
            Closed = false;
            Timestamp++;
        }

        public void RemoveVertex(int idx)
        {
            vertices.RemoveAt(idx);
            Timestamp++;
        }

        public void Reverse() {
			vertices.Reverse();
			Timestamp++;
		}


        public Vector3d this[int key]
        {
            get { return vertices[key]; }
            set { vertices[key] = value; Timestamp++; }
        }

        public Vector3d Start {
            get { return vertices[0]; }
        }
        public Vector3d End {
            get { return vertices.Last(); }
        }

        public IEnumerable<Vector3d> Vertices {
            get { return vertices; }
        }


        public Segment3d Segment(int iSegment)
        {
            return new Segment3d(vertices[iSegment], vertices[iSegment + 1]);
        }

        public IEnumerable<Segment3d> SegmentItr()
        {
            for (int i = 0; i < vertices.Count - 1; ++i)
                yield return new Segment3d(vertices[i], vertices[i + 1]);
        }


        public AxisAlignedBox3d GetBoundingBox()
        {
            // [RMS] problem w/ readonly because vector is a class...
            //AxisAlignedBox3d box = AxisAlignedBox3d.Empty;
            AxisAlignedBox3d box = new AxisAlignedBox3d(false);
            foreach (Vector3d v in vertices)
                box.Contain(v);
            return box;
        }

        public double ArcLength {
            get {
                double dLen = 0;
                for (int i = 1; i < vertices.Count; ++i)
                    dLen += (vertices[i] - vertices[i - 1]).Length;
                return dLen;
            }
        }

        public Vector3d Tangent(int i)
        {
            if (i == 0)
                return (vertices[1] - vertices[0]).Normalized;
            else if (i == vertices.Count - 1)
                return (vertices.Last() - vertices[vertices.Count - 2]).Normalized;
            else
                return (vertices[i + 1] - vertices[i - 1]).Normalized;
        }

        public Vector3d Centroid(int i)
        {
            if (i == 0 || i == vertices.Count - 1)
                return vertices[i];
            else
                return 0.5 * (vertices[i + 1] + vertices[i - 1]);
        }



        public int NearestVertex(Vector3d p)
        {
            double nearSqr = double.MaxValue;
            int i = -1;
            int N = vertices.Count;
            for ( int vi = 0; vi < N; ++vi ) {
                double distSqr = vertices[vi].DistanceSquared(ref p);
                if ( distSqr < nearSqr ) {
                    nearSqr = distSqr;
                    i = vi;
                }
            }
            return i;
        }


        public double DistanceSquared(Vector3d p, out int iNearSeg, out double fNearSegT)
        {
            iNearSeg = -1;
            fNearSegT = double.MaxValue;
            double dist = double.MaxValue;
            int N = (Closed) ? vertices.Count : vertices.Count - 1;
            for (int vi = 0; vi < N; ++vi) {
                int a = vi;
                int b = (vi + 1) % vertices.Count;
                Segment3d seg = new Segment3d(vertices[a], vertices[b]);
                double t = (p - seg.Center).Dot(seg.Direction);
                double d = double.MaxValue;
                if (t >= seg.Extent)
                    d = seg.P1.DistanceSquared(p);
                else if (t <= -seg.Extent)
                    d = seg.P0.DistanceSquared(p);
                else
                    d = (seg.PointAt(t) - p).LengthSquared;
                if (d < dist) {
                    dist = d;
                    iNearSeg = vi;
                    fNearSegT = t;
                }
            }
            return dist;
        }
        public double DistanceSquared(Vector3d p) {
            int iseg; double segt;
            return DistanceSquared(p, out iseg, out segt);
        }

    }
}
