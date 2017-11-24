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
    public class DGraph3
	{
		public const int InvalidID = -1;
		public const int DuplicateEdgeID = -1;

		public static readonly Vector3d InvalidVertex = new Vector3d(Double.MaxValue, 0, 0);
		public static readonly Index2i InvalidEdgeV = new Index2i(InvalidID, InvalidID);
        public static readonly Index3i InvalidEdge3 = new Index3i(InvalidID, InvalidID, InvalidID);


        RefCountVector vertices_refcount;
		DVector<double> vertices;
        DVector<float> colors;

        DVector<List<int>> vertex_edges;

		RefCountVector edges_refcount;
		DVector<int> edges;   // each edge is a tuple (v0,v0,group_id)

		int timestamp = 0;
		int shape_timestamp = 0;

		int max_group_id = 0;

		public DGraph3()
		{
			vertices = new DVector<double>();
			vertex_edges = new DVector<List<int>>();
			vertices_refcount = new RefCountVector();

			edges = new DVector<int>();
			edges_refcount = new RefCountVector();
			max_group_id = 0;
		}

        public DGraph3(DGraph3 copy)
        {
            vertices = new DVector<double>();
            vertex_edges = new DVector<List<int>>();
            vertices_refcount = new RefCountVector();

            edges = new DVector<int>();
            edges_refcount = new RefCountVector();
            max_group_id = 0;

            AppendGraph(copy);
        }


		void updateTimeStamp(bool bShapeChange) {
			timestamp++;
			if (bShapeChange)
				shape_timestamp++;
		}
		public int Timestamp {
			get { return timestamp; }
		}
		public int ShapeTimestamp {
			get { return shape_timestamp; }
		}



		public int VertexCount {
			get { return vertices_refcount.count; }
		}
		public int EdgeCount {
			get { return edges_refcount.count; }
		}


		// these values are (max_used+1), ie so an iteration should be < MaxVertexID, not <=
		public int MaxVertexID {
			get { return vertices_refcount.max_index; }
		}
		public int MaxEdgeID {
			get { return edges_refcount.max_index; }
		}
		public int MaxGroupID {
			get { return max_group_id; }
		}


		public bool IsVertex(int vID) {
			return vertices_refcount.isValid(vID);
		}
		public bool IsEdge(int eID) {
			return edges_refcount.isValid(eID);
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


		public ReadOnlyCollection<int> GetVtxEdges(int vID) {
			return vertices_refcount.isValid(vID) ?
				vertex_edges[vID].AsReadOnly() : null;
		}

		public int GetVtxEdgeCount(int vID) {
			return vertices_refcount.isValid(vID) ?
				vertex_edges[vID].Count : -1;
		}


		public int GetMaxVtxEdgeCount() {
			int max = 0;
			foreach (int vid in vertices_refcount)
				max = Math.Max(max, vertex_edges[vid].Count);
			return max;
		}





		public int GetEdgeGroup(int eid)
		{
			return edges_refcount.isValid(eid) ? edges[3 * eid + 2] : -1;
		}

		public void SetEdgeGroup(int eid, int group_id) {
			Debug.Assert(edges_refcount.isValid(eid));
			if (edges_refcount.isValid(eid)) {
				edges[3 * eid + 2] = group_id;
				max_group_id = Math.Max(max_group_id, group_id + 1);
				updateTimeStamp(false);
			}
		}

		public int AllocateEdgeGroup() {
			return max_group_id++;
		}



		public Index2i GetEdgeV(int eID)
		{
			return edges_refcount.isValid(eID) ?
				new Index2i(edges[3 * eID], edges[3 * eID + 1]) : InvalidEdgeV;
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

		public Index3i GetEdge(int eID)
		{
            int j = 3 * eID;
			return edges_refcount.isValid(eID) ?
				new Index3i(edges[j], edges[j + 1], edges[j + 2]) : InvalidEdge3;
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
			int vid = vertices_refcount.allocate();
			int i = 3 * vid;
            vertices.insert(v[2], i + 2);
            vertices.insert(v[1], i + 1);
			vertices.insert(v[0], i);

            if (colors != null) {
                colors.insert(c.z, i + 2);
                colors.insert(c.y, i + 1);
                colors.insert(c.x, i);
            }

            vertex_edges.insert(new List<int>(), vid);
			updateTimeStamp(true);
			return vid;
		}



		public int AppendEdge(int v0, int v1, int gid = -1){
			return AppendEdge(new Index2i(v0, v1), gid);
		}
		public int AppendEdge(Index2i ev, int gid = -1)
		{
			if (IsVertex(ev[0]) == false || IsVertex(ev[1]) == false) {
				Util.gDevAssert(false);
				return InvalidID;
			}
			if (ev[0] == ev[1]) {
				Util.gDevAssert(false);
				return InvalidID;
			}
			int e0 = FindEdge(ev[0], ev[1]);
			if (e0 != InvalidID)
				return DuplicateEdgeID;

			// increment ref counts and update/create edges
			vertices_refcount.increment(ev[0]);
			vertices_refcount.increment(ev[1]);
			max_group_id = Math.Max(max_group_id, gid + 1);

			// now safe to insert edge
			int eid = add_edge(ev[0], ev[1], gid);

			updateTimeStamp(true);
			return eid;
		}

		int add_edge(int a, int b, int gid) {
			if (b < a) {
				int t = b; b = a; a = t;
			}
			int eid = edges_refcount.allocate();
			int i = 3 * eid;
			edges.insert(a, i);
			edges.insert(b, i + 1);
			edges.insert(gid, i + 2);

			vertex_edges[a].Add(eid);
			vertex_edges[b].Add(eid);
			return eid;
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

        public IEnumerable<int> VertexIndices() {
			foreach (int vid in vertices_refcount)
				yield return vid;
		}
		public IEnumerable<int> EdgeIndices() {
			foreach (int eid in edges_refcount)
				yield return eid;
		}




		public IEnumerable<Vector3d> Vertices() {
			foreach (int vid in vertices_refcount) {
				int i = 3 * vid;
				yield return new Vector3d(vertices[i], vertices[i+1], vertices[i+2]);
			}
		}

		// return value is [v0,v1,gid]
		public IEnumerable<Index3i> Edges() {
			foreach (int eid in edges_refcount) {
				int i = 3 * eid;
				yield return new Index3i(edges[i], edges[i+1], edges[i+2]);
			}
		}


		public IEnumerable<int> VtxVerticesItr(int vID) {
			if (vertices_refcount.isValid(vID)) {
				List<int> vedges = vertex_edges[vID];
				int N = vedges.Count;
				for (int i = 0; i < N; ++i)
					yield return edge_other_v(vedges[i], vID);
			}
		}


		public IEnumerable<int> VtxEdgesItr(int vID) {
			if (vertices_refcount.isValid(vID)) {
				List<int> vedges = vertex_edges[vID];
				int N = vedges.Count;
				for (int i = 0; i < N; ++i)
					yield return vedges[i];
			}
		}


		public int FindEdge(int vA, int vB) {
			int vO = Math.Max(vA, vB);
			List<int> e0 = vertex_edges[Math.Min(vA, vB)];
			int N = e0.Count;
			for (int i = 0; i < N; ++i ) {
				if (edge_has_v(e0[i], vO))
					return e0[i];
			}
			return InvalidID;
		}


		public MeshResult RemoveEdge(int eID, bool bRemoveIsolatedVertices)
		{
			if (!edges_refcount.isValid(eID)) {
				Util.gDevAssert(false);
				return MeshResult.Failed_NotAnEdge;
			}

			int i = 3 * eID;
			Index2i ev = new Index2i(edges[i], edges[i + 1]);
			vertex_edges[ev.a].Remove(eID);
			vertex_edges[ev.b].Remove(eID);

			edges_refcount.decrement(eID);

			// Decrement vertex refcounts. If any hit 1 and we got remove-isolated flag,
			// we need to remove that vertex
			for (int j = 0; j < 2; ++j) {
				int vid = ev[j];
				vertices_refcount.decrement(vid);
				if (bRemoveIsolatedVertices && vertices_refcount.refCount(vid) == 1) {
					vertices_refcount.decrement(vid);
					Util.gDevAssert(vertices_refcount.isValid(vid) == false);
					vertex_edges[vid] = null;
				}
			}

			updateTimeStamp(true);
			return MeshResult.Ok;
		}



        public MeshResult RemoveVertex(int vid, bool bRemoveIsolatedVertices)
        {
            List<int> edges = new List<int>(GetVtxEdges(vid));
            foreach (int eid in edges) {
                MeshResult result = RemoveEdge(eid, bRemoveIsolatedVertices);
                if (result != MeshResult.Ok)
                    return result;
            }
            return MeshResult.Ok;
        }




		public struct EdgeSplitInfo
		{
			public int vNew;
			public int eNewBN;      // new edge [vNew,vB] (original was AB)
		}
		public MeshResult SplitEdge(int vA, int vB, out EdgeSplitInfo split)
		{
			int eid = FindEdge(vA, vB);
			if (eid == InvalidID) {
				split = new EdgeSplitInfo();
				return MeshResult.Failed_NotAnEdge;
			}
			return SplitEdge(eid, out split);
		}
		public MeshResult SplitEdge(int eab, out EdgeSplitInfo split)
		{
			split = new EdgeSplitInfo();
			if (!IsEdge(eab))
				return MeshResult.Failed_NotAnEdge;

			// look up primary edge
			int eab_i = 3 * eab;
			int a = edges[eab_i], b = edges[eab_i + 1];
			int gid = edges[eab_i + 2];

			// create new vertex
			Vector3d vNew = 0.5 * (GetVertex(a) + GetVertex(b));
            Vector3f cNew = (HasVertexColors) ? (0.5f * (GetVertexColor(a) + GetVertexColor(b))) : Vector3f.One;
			int f = AppendVertex(vNew, cNew);

			// rewrite edge bc, create edge af
			int eaf = eab;
			replace_edge_vertex(eaf, b, f);
			vertex_edges[b].Remove(eab);
			vertex_edges[f].Add(eaf);

			// create new edge fb
			int efb = add_edge(f, b, gid);

			// update vertex refcounts
			vertices_refcount.increment(f, 2);

			split.vNew = f;
			split.eNewBN = efb;

			updateTimeStamp(true);
			return MeshResult.Ok;
		}


		public struct EdgeCollapseInfo
		{
			public int vKept;
			public int vRemoved;

			public int eCollapsed;              // edge we collapsed
		}
		public MeshResult CollapseEdge(int vKeep, int vRemove, out EdgeCollapseInfo collapse)
		{
			bool DiscardIsolatedVertices = true;

			collapse = new EdgeCollapseInfo();
			if (IsVertex(vKeep) == false || IsVertex(vRemove) == false)
				return MeshResult.Failed_NotAnEdge;

			int b = vKeep;      // renaming for sanity. We remove a and keep b
			int a = vRemove;

			int eab = FindEdge(a, b);
			if (eab == InvalidID)
				return MeshResult.Failed_NotAnEdge;

			List<int> edges_b = vertex_edges[b];
			List<int> edges_a = vertex_edges[a];

            // [TODO] if we are down to a single triangle (a,b,c), then
            //   this will happily discard vtx c and edges ac,bc, leaving us
            //   with a single edge...

			// get rid of any edges that will be duplicates
			bool done = false;
			while (!done) {
				done = true;
				foreach (int eax in edges_a) {
					int o = edge_other_v(eax, a);
					if (o != b && FindEdge(b,o) != InvalidID) {
						RemoveEdge(eax, DiscardIsolatedVertices);
						done = false;
						break;
					}
				}
			}

			edges_b.Remove(eab);
			foreach ( int eax in edges_a ) {
				int o = edge_other_v(eax, a);
				if (o == b)
					continue;       // discard this edge
				replace_edge_vertex(eax, a, b);
				vertices_refcount.decrement(a);
				edges_b.Add(eax);
				vertices_refcount.increment(b);
			}

			edges_refcount.decrement(eab);
			vertices_refcount.decrement(b);
			vertices_refcount.decrement(a);
			if (DiscardIsolatedVertices) {
				vertices_refcount.decrement(a); // second decrement discards isolated vtx
				Util.gDevAssert(!IsVertex(a));
				vertex_edges[a] = null;
			}

			edges_a.Clear();

			collapse.vKept = vKeep;
			collapse.vRemoved = vRemove;
			collapse.eCollapsed = eab;

			updateTimeStamp(true);
			return MeshResult.Ok;
		}



		bool edge_has_v(int eid, int vid) {
			int i = 3 * eid;
			return (edges[i] == vid) || (edges[i + 1] == vid);
		}
		int edge_other_v(int eID, int vID) {
			int i = 3 * eID;
			int ev0 = edges[i], ev1 = edges[i + 1];
			return (ev0 == vID) ? ev1 : ((ev1 == vID) ? ev0 : InvalidID);
		}
		int replace_edge_vertex(int eID, int vOld, int vNew)
		{
			int i = 3 * eID;
			int a = edges[i], b = edges[i + 1];
			if (a == vOld) {
				edges[i] = Math.Min(b, vNew);
				edges[i + 1] = Math.Max(b, vNew);
				return 0;
			} else if (b == vOld) {
				edges[i] = Math.Min(a, vNew);
				edges[i + 1] = Math.Max(a, vNew);
				return 1;
			} else
				return -1;
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




		public bool IsCompact {
			get { return vertices_refcount.is_dense && edges_refcount.is_dense; }
		}
		public bool IsCompactV {
			get { return vertices_refcount.is_dense; }
		}




        public bool IsBoundaryVertex(int vID) {
            return vertices_refcount.isValid(vID) && vertex_edges[vID].Count == 1;
        }

        public bool IsJunctionVertex(int vID) {
            return vertices_refcount.isValid(vID) && vertex_edges[vID].Count > 2;
        }

        public bool IsRegularVertex(int vID) {
            return vertices_refcount.isValid(vID) && vertex_edges[vID].Count == 2;
        }




		public enum FailMode { DebugAssert, gDevAssert, Throw, ReturnOnly }

		/// <summary>
		// This function checks that the graph is well-formed, ie all internal data
		// structures are consistent
		/// </summary>
		public bool CheckValidity(FailMode eFailMode = FailMode.Throw)
		{
			bool is_ok = true;
			Action<bool> CheckOrFailF = (b) => {
				is_ok = is_ok && b;
			};
			if (eFailMode == FailMode.DebugAssert) {
				CheckOrFailF = (b) => {
					Debug.Assert(b);
					is_ok = is_ok && b;
				};
			} else if (eFailMode == FailMode.gDevAssert) {
				CheckOrFailF = (b) => {
					Util.gDevAssert(b);
					is_ok = is_ok && b;
				};
			} else if (eFailMode == FailMode.Throw) {
				CheckOrFailF = (b) => {
					if (b == false)
						throw new Exception("DGraph3.CheckValidity: check failed");
				};
			}

			// edge verts/tris must exist
			foreach (int eID in EdgeIndices()) {
				CheckOrFailF(IsEdge(eID));
				CheckOrFailF(edges_refcount.refCount(eID) == 1);
				Index2i ev = GetEdgeV(eID);
				CheckOrFailF(IsVertex(ev[0]));
				CheckOrFailF(IsVertex(ev[1]));
				CheckOrFailF(ev[0] < ev[1]);
			}

			// verify compact check
			bool is_compact = vertices_refcount.is_dense;
			if (is_compact) {
				for (int vid = 0; vid < vertices.Length / 3; ++vid) {
					CheckOrFailF(vertices_refcount.isValid(vid));
				}
			}

			// vertex edges must exist and reference this vert
			foreach (int vID in VertexIndices()) {
				CheckOrFailF(IsVertex(vID));

				Vector3d v = GetVertex(vID);
				CheckOrFailF(double.IsNaN(v.LengthSquared) == false);
				CheckOrFailF(double.IsInfinity(v.LengthSquared) == false);

				List<int> l = vertex_edges[vID];
				foreach (int edgeid in l) {
					CheckOrFailF(IsEdge(edgeid));
					CheckOrFailF(edge_has_v(edgeid, vID));

					int otherV = edge_other_v(edgeid, vID);
					int e2 = FindEdge(vID, otherV);
					CheckOrFailF(e2 != InvalidID);
					CheckOrFailF(e2 == edgeid);
					e2 = FindEdge(otherV, vID);
					CheckOrFailF(e2 != InvalidID);
					CheckOrFailF(e2 == edgeid);
				}

				CheckOrFailF(vertices_refcount.refCount(vID) == l.Count + 1);

			}

			return is_ok;
		}


        [Conditional("DEBUG")]
        public void debug_check_is_vertex(int v) {
            if (!IsVertex(v))
                throw new Exception("DGraph3.debug_is_vertex - not a vertex!");
        }
        [Conditional("DEBUG")]
        public void debug_check_is_edge(int e) {
            if (!IsEdge(e))
                throw new Exception("DGraph3.debug_is_edge - not an edge!");
        }

	}
}
