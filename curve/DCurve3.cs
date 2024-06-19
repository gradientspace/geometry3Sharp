using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VirgisGeometry
{
    /// <summary>
    /// DCurve3 is a 3D polyline, either open or closed (via .Closed)
    /// Despite the D prefix, it is *not* dynamic
    /// </summary>
    public class DCurve3 : ISampledCurve3d
    {
        // [TODO] use dvector? or double-indirection indexing?
        //   question is how to insert efficiently...
        protected List<Vector3d> vertices;
        public bool Closed { get; set; }
        public int Timestamp;
        protected List<object> data = new();

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

        /// <summary>
        /// Assumes SFA standards - LineString is closed if the first and last vertices are the same
        /// </summary>
        /// <param name="verticesIn"></param>
        public DCurve3(IEnumerable<Vector3d> verticesInSFA)
        {
            this.vertices = new List<Vector3d>(verticesInSFA);
            Closed = false;
            if (this.vertices.Last() == this.vertices.First())
            {
                this.vertices.RemoveAt(this.vertices.Count - 1);
                Closed = true;
            }
            Timestamp = 1;
        }

        public DCurve3(DCurve3 copy)
        {
            vertices = new List<Vector3d>(copy.vertices);
            data = new (copy.data);
            Closed = copy.Closed;
            Timestamp = 1;
        }

        public DCurve3(ISampledCurve3d icurve)
        {
            this.vertices = new List<Vector3d>(icurve.Vertices);
            Closed = icurve.Closed;
            Timestamp = 1;
        }

        public DCurve3(Polygon2d poly, int ix = 0, int iy = 1)
        {
            int NV = poly.VertexCount;
            this.vertices = new List<Vector3d>(NV);
            for (int k = 0; k < NV; ++k) {
                Vector3d v = Vector3d.Zero;
                v[ix] = poly[k].x; v[iy] = poly[k].y;
                this.vertices.Add(v);
            }
            Closed = true;
            Timestamp = 1;
        }

        /// <summary>
        /// Creates g3.DCurve from Vector3[]
        /// </summary>
        /// <param name="v_in">Vextor3[]</param>
        /// <param name="bClosed">whether the line is closed</param>
        public DCurve3(Vector3[] v_in, bool bClosed)
        {
            Closed = bClosed;
            vertices = v_in.ToList<Vector3>().Select(vertex => (Vector3d)vertex).ToList<Vector3d>();
            Timestamp = 1;
        }

        public void AppendVertex(Vector3d v) {
            vertices.Add(v);
            Timestamp++;
        }

        /// <summary>
        /// Insert a new vertex as vertex i
        /// </summary>
        /// <param name="v"></param>
        /// <param name="i"></param>
        public void InsertVertex(Vector3d v, int i)
        {
            vertices.Insert(i, v);
        }

        public int VertexCount {
            get { return vertices.Count; }
        }
        public int SegmentCount {
            get { return Closed ? vertices.Count : vertices.Count - 1; }
        }

        public Vector3d GetVertex(int i) {
            return vertices[i];
        }

        /// <summary>
        /// Get a Unity Vertex
        /// 
        /// The vertex is a Vector3 corrected for y-up and has the
        /// Unity Transformation Matrix applied
        /// </summary>
        /// <param name="i"> Vertex Index</param>
        /// <param name="transform"> Matrix4x4 Transformation Matrix</param>
        /// <returns>Vector3</returns>
        public Vector3 GetVertex(int i, Matrix4x4 transform)
        {
            return transform.MultiplyVector((Vector3)vertices[i]);
        }


        /// <summary>
        /// Get a Unity Vertex Iterator
        /// 
        /// The verteces are Vector3's corrected for y-up and have the
        /// Unity Transformation Matrix applied
        /// </summary>
        /// <param name="i"> Vertex Index</param>
        /// <param name="transform"> Matrix4x4 Transformation Matrix</param>
        /// <returns>IEnumerable<Vector3></returns>
        public IEnumerable<Vector3> VertexItr(Matrix4x4 transform)
        {
            foreach (Vector3d v in vertices)
            {
                yield return transform.MultiplyVector((Vector3)v);
            }
        }

        public IEnumerable<Vector3d> VertexItr()
        {
            return vertices;
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
            get { return (Closed) ? vertices[0] : vertices.Last(); }
        }

        public IEnumerable<Vector3d> Vertices {
            get { return vertices; }
        }

        public Segment3d GetSegment(int iSegment)
        {
            return (Closed) ? new Segment3d(vertices[iSegment], vertices[(iSegment+1)%vertices.Count])
                : new Segment3d(vertices[iSegment], vertices[iSegment + 1]);
        }

        public IEnumerable<Segment3d> SegmentItr()
        {
            if (Closed) {
                int NV = vertices.Count;
                for (int i = 0; i < NV; ++i)
                    yield return new Segment3d(vertices[i], vertices[(i + 1)%NV]);
            } else {
                int NV = vertices.Count - 1;
                for (int i = 0; i < NV; ++i)
                    yield return new Segment3d(vertices[i], vertices[i + 1]);
            }
        }

        public Vector3d PointAt(int iSegment, double fSegT)
        {
            Segment3d seg = new Segment3d(vertices[iSegment], vertices[(iSegment + 1) % vertices.Count]);
            return seg.PointAt(fSegT);
        }


        public AxisAlignedBox3d GetBoundingBox() {
            AxisAlignedBox3d box = AxisAlignedBox3d.Empty;
            foreach (Vector3d v in vertices)
                box.Contain(v);
            return box;
        }

        public double ArcLength {
            get { return CurveUtils.ArcLength(vertices, Closed); }
        }

        public Vector3d Tangent(int i) {
            return CurveUtils.GetTangent(vertices, i, Closed);
        }

        public Vector3d Centroid(int i)
        {
            if (Closed) {
                int NV = vertices.Count;
                if (i == 0)
                    return 0.5 * (vertices[1] + vertices[NV - 1]);
                else
                    return 0.5 * (vertices[(i+1)%NV] + vertices[i-1]);
            } else {
                if (i == 0 || i == vertices.Count - 1)
                    return vertices[i];
                else
                    return 0.5 * (vertices[i + 1] + vertices[i - 1]);
            }
        }


        public Index2i Neighbours(int i)
        {
            int NV = vertices.Count;
            if (Closed) {
                if (i == 0)
                    return new Index2i(NV-1, 1);
                else
                    return new Index2i(i-1, (i+1) % NV);
            } else {
                if (i == 0)
                    return new Index2i(-1, 1);
                else if (i == NV-1)
                    return new Index2i(NV-2, -1);
                else
                    return new Index2i(i-1, i+1);
            }
        } 


        /// <summary>
        /// Compute opening angle at vertex i in degrees
        /// </summary>
        public double OpeningAngleDeg(int i)
        {
            int prev = i - 1, next = i + 1;
            if ( Closed ) {
                int NV = vertices.Count;
                prev = (i == 0) ? NV - 1 : prev;
                next = next % NV;
            } else {
                if (i == 0 || i == vertices.Count - 1)
                    return 180;
            }
            Vector3d e1 = (vertices[prev] - vertices[i]);
            Vector3d e2 = (vertices[next] - vertices[i]);
            e1.Normalize(); e2.Normalize();
            return Vector3d.AngleD(e1, e2);
        }


        /// <summary>
        /// Find nearest vertex to point p
        /// </summary>
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


        /// <summary>
        /// find squared distance from p to nearest segment on polyline
        /// </summary>
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



        /// <summary>
        /// Resample curve so that:
        ///   - if opening angle at vertex is > sharp_thresh, we emit two more vertices at +/- corner_t, where the t is used in prev/next lerps
        ///   - if opening angle is > flat_thresh, we skip the vertex entirely (simplification)
        /// This is mainly useful to get nicer polylines to use as the basis for (eg) creating 3D tubes, rendering, etc
        /// 
        /// [TODO] skip tiny segments?
        /// </summary>
        public DCurve3 ResampleSharpTurns(double sharp_thresh = 90, double flat_thresh = 189, double corner_t = 0.01)
        {
            int NV = vertices.Count;
            DCurve3 resampled = new DCurve3() { Closed = this.Closed };
            double prev_t = 1.0 - corner_t;
            for (int k = 0; k < NV; ++k) {
                double open_angle = Math.Abs(OpeningAngleDeg(k));
                if (open_angle > flat_thresh && k > 0) {
                    // ignore skip this vertex
                } else if (open_angle > sharp_thresh) {
                    resampled.AppendVertex(vertices[k]);
                } else {
                    Vector3d n = vertices[(k + 1) % NV];
                    Vector3d p = vertices[k == 0 ? NV - 1 : k - 1];
                    resampled.AppendVertex(Vector3d.Lerp(p, vertices[k], prev_t));
                    resampled.AppendVertex(vertices[k]);
                    resampled.AppendVertex(Vector3d.Lerp(vertices[k], n, corner_t));
                }
            }
            return resampled;
        }

        /// <summary>
        /// Estimates the 3D centroid of a DCurve 
        /// </summary>
        /// <returns>Vector3[]</returns>
        public Vector3d Center()
        {
            Vector3d center = Vector3d.Zero;
            int len = SegmentCount;
            if (!Closed) len++;
            foreach( Vector3d v in Vertices )
            {
                center += v;
            }
            center /= len;
            return center;
        }

        /// <summary>
        /// Estimates the nearest point on a DCurve to the centroid of that DCurve
        /// </summary>
        /// <returns>g3.Vector3d Centroid</returns>
        public Vector3d CenterMark()
        {
            Vector3d center = Center();
            return GetSegment(NearestSegment(center)).NearestPoint(center);
        }

        /// <summary>
        /// Finds the Segment from the DCurve3 closes to the position
        /// </summary>
        /// <param name="position">Vector3d</param>
        /// <returns>Integer Sgement index</returns>
        public int NearestSegment( Vector3d position)
        {
            _ = DistanceSquared(position, out int iSeg, out double tangent);
            return iSeg;
        }

        /// <summary>
        /// Add an item to the data array
        /// </summary>
        /// <param name="item"></param>
        public void SetData(object item, int idx = -1)
        {
            if (idx == -1)
            {
                data.Add(item);
            } else
            {
                data[idx] = item;
            }
        }

        /// <summary>
        /// Insert an item to the data array as vertex idx
        /// </summary>
        /// <param name="item"></param>
        /// <param name="idx"></param>
        public void InsertData(object item, int idx)
        {
            data.Insert(idx, item);
        }

        /// <summary>
        /// Get the idx'th item of data as type T
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="idx"></param>
        /// <returns></returns>
        public T GetData<T>(int idx)
        {
            return (T)data[idx];
        }

        public IEnumerable<T> GetDataItr<T>()
        {
            for(int i =0; i<data.Count; i++)
            {
                yield return GetData<T>(i);
            }
        }
    }
}
