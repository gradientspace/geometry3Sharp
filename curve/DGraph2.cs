using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Diagnostics;


namespace g3
{
    /// <summary>
    /// Arbitrary-Topology 2D Graph. This is similar to DMesh3 but without faces. 
    /// Each vertex can be connected to an arbitrary number of edges.
    /// Each vertex can have a 3-float color, and edge edge can have an integer GroupID
    /// </summary>
	public class DGraph2 : DGraph
    {
		public static readonly Vector2d InvalidVertex = new Vector2d(Double.MaxValue, 0);

		DVector<double> vertices;
        DVector<float> colors;


		public DGraph2() : base()
		{
			vertices = new DVector<double>();
		}

        public DGraph2(DGraph2 copy) : base()
        {
            vertices = new DVector<double>();
            AppendGraph(copy);
        }


		public Vector2d GetVertex(int vID) {
			return vertices_refcount.isValid(vID) ?
				new Vector2d(vertices[2 * vID], vertices[2 * vID + 1]) : InvalidVertex;
		}

		public void SetVertex(int vID, Vector2d vNewPos) {
			Debug.Assert(vNewPos.IsFinite);     // this will really catch a lot of bugs...
			if (vertices_refcount.isValid(vID)) {
				int i = 2 * vID;
				vertices[i] = vNewPos.x; vertices[i + 1] = vNewPos.y;
				updateTimeStamp(true);
			}
		}


		public Vector3f GetVertexColor(int vID) {
            if (colors == null) {
                return Vector3f.One;
            } else {
                debug_check_is_vertex(vID);
                int i = 3 * vID;
                return new Vector3f(colors[i], colors[i + 1], colors[i + 2]);
            }
		}

		public void SetVertexColor(int vID, Vector3f vNewColor) {
			if ( HasVertexColors ) {
                debug_check_is_vertex(vID);
                int i = 3*vID;
				colors[i] = vNewColor.x; colors[i+1] = vNewColor.y; colors[i+2] = vNewColor.z;
                updateTimeStamp(false);
			}
		}


		public bool GetEdgeV(int eID, ref Vector2d a, ref Vector2d b)
		{
			if (edges_refcount.isValid(eID)) {
				int iv0 = 2 * edges[3 * eID];
				a.x = vertices[iv0]; a.y = vertices[iv0 + 1];
				int iv1 = 2 * edges[3 * eID + 1];
				b.x = vertices[iv1]; b.y = vertices[iv1 + 1];
				return true;
			}
			return false;
		}
        

        public Segment2d GetEdgeSegment(int eID)
        {
            if (edges_refcount.isValid(eID)) {
                int iv0 = 2 * edges[3 * eID];
                int iv1 = 2 * edges[3 * eID + 1];
                return new Segment2d(new Vector2d(vertices[iv0], vertices[iv0 + 1]),
                                     new Vector2d(vertices[iv1], vertices[iv1 + 1]));
            }
            throw new Exception("DGraph2.GetEdgeSegment: invalid segment with id " + eID);
        }

        public Vector2d GetEdgeCenter(int eID)
        {
            if (edges_refcount.isValid(eID)) {
                int iv0 = 2 * edges[3 * eID];
                int iv1 = 2 * edges[3 * eID + 1];
                return new Vector2d((vertices[iv0] + vertices[iv1]) * 0.5,
                                    (vertices[iv0 + 1] + vertices[iv1 + 1]) * 0.5);
            }
            throw new Exception("DGraph2.GetEdgeCenter: invalid segment with id " + eID);
        }

        public int AppendVertex(Vector2d v) {
            return AppendVertex(v, Vector3f.One);
        }
        public int AppendVertex(Vector2d v, Vector3f c)
		{
            int vid = append_vertex_internal();
			int i = 2 * vid;
			vertices.insert(v[1], i + 1);
			vertices.insert(v[0], i);

            if (colors != null) {
                i = 3 * vid;
                colors.insert(c.z, i + 2);
                colors.insert(c.y, i + 1);
                colors.insert(c.x, i);
            }

			return vid;
		}


        


		public void AppendPolygon(Polygon2d poly, int gid = -1) {
			int first = -1;
			int prev = -1;
            int N = poly.VertexCount;
            for ( int i = 0; i < N; ++i ) {
				int cur = AppendVertex(poly[i]);
				if (prev == -1)
					first = cur;
				else
					AppendEdge(prev, cur, gid);
				prev = cur;
			}
			AppendEdge(prev, first, gid);
		}
        public void AppendPolygon(GeneralPolygon2d poly, int gid = -1)
        {
            AppendPolygon(poly.Outer, gid);
            foreach ( var hole in poly.Holes )
                AppendPolygon(hole, gid);
        }


        public void AppendPolyline(PolyLine2d poly, int gid = -1)
        {
            int prev = -1;
            int N = poly.VertexCount;
            for (int i = 0; i < N; ++i) {
                int cur = AppendVertex(poly[i]);
                if ( i > 0 )
                    AppendEdge(prev, cur, gid);
                prev = cur;
            }
        }


        public void AppendGraph(DGraph2 graph, int gid = -1)
        {
            int[] mapV = new int[graph.MaxVertexID];
            foreach ( int vid in graph.VertexIndices()) {
                mapV[vid] = this.AppendVertex(graph.GetVertex(vid));
            }
            foreach ( int eid in graph.EdgeIndices()) {
                Index2i ev = graph.GetEdgeV(eid);
                int use_gid = (gid == -1) ? graph.GetEdgeGroup(eid) : gid;
                this.AppendEdge(mapV[ev.a], mapV[ev.b], use_gid);
            }
        }



        public bool HasVertexColors { get { return colors != null; } }

        public void EnableVertexColors(Vector3f initial_color)
        {
            if (HasVertexColors)
                return;
            colors = new DVector<float>();
            int NV = MaxVertexID;
            colors.resize(3 * NV);
            for (int i = 0; i < NV; ++i) {
                int vi = 3 * i;
                colors[vi] = initial_color.x;
                colors[vi + 1] = initial_color.y;
                colors[vi + 2] = initial_color.z;
            }
        }
        public void DiscardVertexColors()
        {
            colors = null;
        }





        // iterators


		public IEnumerable<Vector2d> Vertices() {
			foreach (int vid in vertices_refcount) {
				int i = 2 * vid;
				yield return new Vector2d(vertices[i], vertices[i + 1]);
			}
		}


        /// <summary>
        /// return edges around vID sorted by angle, in clockwise order
        /// </summary>
        public int[] SortedVtxEdges(int vID)
        {

            if (vertices_refcount.isValid(vID) == false)
                return null;
            List<int> vedges = vertex_edges[vID];
            int N = vedges.Count;
            int[] sorted = new int[N];
            double[] angles = new double[N];
            Vector2d v = new Vector2d(vertices[2*vID], vertices[2 * vID + 1]);
            for (int i = 0; i < N; ++i) {
                int nbr_vid = edge_other_v(vedges[i], vID);
                double dx = vertices[2 * nbr_vid] - v.x;
                double dy = vertices[2 * nbr_vid + 1] - v.y;
                //angles[i] = Math.Atan2(dy, dx) + Math.PI;   // shift to range [0,2pi]
                angles[i] = MathUtil.Atan2Positive(dy, dx);
                sorted[i] = vedges[i];
            }
            Array.Sort(angles, sorted);
            return sorted;
        }









		// compute vertex bounding box
		public AxisAlignedBox2d GetBounds()
		{
			double x = 0, y = 0;
			foreach (int vi in vertices_refcount) {
				x = vertices[2 * vi]; y = vertices[2 * vi + 1];;
				break;
			}
			double minx = x, maxx = x, miny = y, maxy = y;
			foreach (int vi in vertices_refcount) {
				x = vertices[2 * vi]; y = vertices[2 * vi + 1];;
				if (x < minx) minx = x; else if (x > maxx) maxx = x;
				if (y < miny) miny = y; else if (y > maxy) maxy = y;
			}
			return new AxisAlignedBox2d(minx, miny, maxx, maxy);
		}

		AxisAlignedBox2d cached_bounds;
		int cached_bounds_timestamp = -1;

		//! cached bounding box, lazily re-computed on access if mesh has changed
		public AxisAlignedBox2d CachedBounds {
			get {
				if (cached_bounds_timestamp != Timestamp) {
					cached_bounds = GetBounds();
					cached_bounds_timestamp = Timestamp;
				}
				return cached_bounds;
			}
		}





        /// <summary>
        /// Compute opening angle at vertex vID. 
        /// If not a vertex, or valence != 2, returns invalidValue argument.
        /// If either edge is degenerate, returns invalidValue argument.
        /// </summary>
        public double OpeningAngle(int vID, double invalidValue = double.MaxValue)
        {
            if (vertices_refcount.isValid(vID) == false)
                return invalidValue;
            List<int> vedges = vertex_edges[vID];
            if (vedges.Count != 2)
                return invalidValue;

            int nbra = edge_other_v(vedges[0], vID);
            int nbrb = edge_other_v(vedges[1], vID);

            Vector2d v = new Vector2d(vertices[2 * vID], vertices[2 * vID + 1]);
            Vector2d a = new Vector2d(vertices[2 * nbra], vertices[2 * nbra + 1]);
            Vector2d b = new Vector2d(vertices[2 * nbrb], vertices[2 * nbrb + 1]);
            a -= v;
            if (a.Normalize() == 0)
                return invalidValue;
            b -= v;
            if (b.Normalize() == 0)
                return invalidValue;
            return Vector2d.AngleD(a, b);
        }






        // internal used in SplitEdge
        protected override int append_new_split_vertex(int a, int b)
        {
            Vector2d vNew = 0.5 * (GetVertex(a) + GetVertex(b));
            Vector3f cNew = (HasVertexColors) ? (0.5f * (GetVertexColor(a) + GetVertexColor(b))) : Vector3f.One;
            int f = AppendVertex(vNew, cNew);
            return f;
        }


        protected override void subclass_validity_checks(Action<bool> CheckOrFailF)
        {
            foreach (int vID in VertexIndices()) {
                Vector2d v = GetVertex(vID);
                CheckOrFailF(double.IsNaN(v.LengthSquared) == false);
                CheckOrFailF(double.IsInfinity(v.LengthSquared) == false);
            }
        }




	}
}
