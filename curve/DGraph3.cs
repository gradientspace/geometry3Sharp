using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Diagnostics;


namespace g3
{
    /// <summary>
    /// Arbitrary-Topology 3D Graph. This is similar to DMesh3 but without faces. 
    /// Each vertex can be connected to an arbitrary number of edges.
    /// Each vertex can have a 3-float color, and edge edge can have an integer GroupID
    /// </summary>
    public class DGraph3 : DGraph
	{

		public static readonly Vector3d InvalidVertex = new Vector3d(Double.MaxValue, 0, 0);

		DVector<double> vertices;
        DVector<float> colors;

		public DGraph3() : base()
		{
			vertices = new DVector<double>();
		}

        public DGraph3(DGraph3 copy) : base()
        {
            vertices = new DVector<double>();
            AppendGraph(copy);
        }



		public Vector3d GetVertex(int vID) {
            debug_check_is_vertex(vID);
            int i = 3 * vID;
			return new Vector3d(vertices[i], vertices[i+1], vertices[i+2]);
		}

		public void SetVertex(int vID, Vector3d vNewPos) {
			Debug.Assert(vNewPos.IsFinite);     // this will really catch a lot of bugs...
			if (vertices_refcount.isValid(vID)) {
				int i = 3 * vID;
				vertices[i] = vNewPos.x; vertices[i + 1] = vNewPos.y; vertices[i + 2] = vNewPos.z;
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


        public bool GetEdgeV(int eID, ref Vector3d a, ref Vector3d b)
        {
            if (edges_refcount.isValid(eID)) {
                int iv0 = 3 * edges[3 * eID];
                a.x = vertices[iv0]; a.y = vertices[iv0 + 1]; a.z = vertices[iv0 + 2];
                int iv1 = 3 * edges[3 * eID + 1];
                b.x = vertices[iv1]; b.y = vertices[iv1 + 1]; b.z = vertices[iv1 + 2];
                return true;
            }
            return false;
        }

        public Segment3d GetEdgeSegment(int eID)
        {
            if (edges_refcount.isValid(eID)) {
                int iv0 = 3 * edges[3 * eID];
                int iv1 = 3 * edges[3 * eID + 1];
                return new Segment3d(new Vector3d(vertices[iv0], vertices[iv0+1], vertices[iv0+2]),
                                     new Vector3d(vertices[iv1], vertices[iv1+1], vertices[iv1+2]));
            }
            throw new Exception("DGraph3.GetEdgeSegment: invalid segment with id " + eID);
        }

        public Vector3d GetEdgeCenter(int eID)
        {
            if (edges_refcount.isValid(eID)) {
                int iv0 = 3 * edges[3 * eID];
                int iv1 = 3 * edges[3 * eID + 1];
                return new Vector3d((vertices[iv0] + vertices[iv1]) * 0.5,
                                    (vertices[iv0+1] + vertices[iv1+1]) * 0.5,
                                    (vertices[iv0+2] + vertices[iv1+2]) * 0.5);
            }
            throw new Exception("DGraph3.GetEdgeCenter: invalid segment with id " + eID);
        }


        public IEnumerable<Segment3d> Segments()
        {
            foreach (int eid in edges_refcount) {
                yield return GetEdgeSegment(eid);
            }
        }




        public int AppendVertex(Vector3d v) {
            return AppendVertex(v, Vector3f.One);
        }
        public int AppendVertex(Vector3d v, Vector3f c)
		{
            int vid = append_vertex_internal();
            int i = 3 * vid;
            vertices.insert(v[2], i + 2);
            vertices.insert(v[1], i + 1);
			vertices.insert(v[0], i);

            if (colors != null) {
                colors.insert(c.z, i + 2);
                colors.insert(c.y, i + 1);
                colors.insert(c.x, i);
            }
			return vid;
		}




        public void AppendGraph(DGraph3 graph, int gid = -1)
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


		public IEnumerable<Vector3d> Vertices() {
			foreach (int vid in vertices_refcount) {
				int i = 3 * vid;
				yield return new Vector3d(vertices[i], vertices[i+1], vertices[i+2]);
			}
		}


     



		// compute vertex bounding box
		public AxisAlignedBox3d GetBounds()
		{
			double x = 0, y = 0, z = 0;
			foreach (int vi in vertices_refcount) {
				x = vertices[3*vi]; y = vertices[3*vi +1]; z = vertices[3*vi +2];
				break;
			}
            double minx = x, maxx = x, miny = y, maxy = y, minz = z, maxz = z;
			foreach (int vi in vertices_refcount) {
                int i = 3 * vi;
                x = vertices[i]; y = vertices[i + 1]; z = vertices[i + 2];
                if (x < minx) minx = x; else if (x > maxx) maxx = x;
				if (y < miny) miny = y; else if (y > maxy) maxy = y;
                if (z < minz) minz = z; else if (z > maxz) maxz = z;
            }
            return new AxisAlignedBox3d(minx, miny, minz, maxx, maxy, maxz);
		}

		AxisAlignedBox3d cached_bounds;
		int cached_bounds_timestamp = -1;

		//! cached bounding box, lazily re-computed on access if mesh has changed
		public AxisAlignedBox3d CachedBounds {
			get {
				if (cached_bounds_timestamp != Timestamp) {
					cached_bounds = GetBounds();
					cached_bounds_timestamp = Timestamp;
				}
				return cached_bounds;
			}
		}







        // internal used in SplitEdge
        protected override int append_new_split_vertex(int a, int b)
        {
            Vector3d vNew = 0.5 * (GetVertex(a) + GetVertex(b));
            Vector3f cNew = (HasVertexColors) ? (0.5f * (GetVertexColor(a) + GetVertexColor(b))) : Vector3f.One;
            int f = AppendVertex(vNew, cNew);
            return f;
        }



        protected override void subclass_validity_checks(Action<bool> CheckOrFailF)
        {
            foreach (int vID in VertexIndices()) {
                Vector3d v = GetVertex(vID);
                CheckOrFailF(double.IsNaN(v.LengthSquared) == false);
                CheckOrFailF(double.IsInfinity(v.LengthSquared) == false);
            }
        }



    }
}
