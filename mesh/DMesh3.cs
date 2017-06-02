using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Diagnostics;

namespace g3
{
    public enum MeshResult
    {
        Ok = 0,
        Failed_NotAVertex = 1,
        Failed_NotATriangle = 2,
        Failed_NotAnEdge = 3,

        Failed_BrokenTopology = 10,

        Failed_IsBoundaryEdge = 20,
        Failed_FlippedEdgeExists = 21,
        Failed_IsBowtieVertex = 22,
        Failed_InvalidNeighbourhood = 23,       // these are all failures for CollapseEdge
        Failed_FoundDuplicateTriangle = 24,
        Failed_CollapseTetrahedron = 25,
        Failed_CollapseTriangle = 26,

        Failed_WouldCreateBowtie = 30
    };


    [Flags]
    public enum MeshComponents
    {
        None = 0,
        VertexNormals = 1,
        VertexColors = 2,
        VertexUVs = 4,
        FaceGroups = 8,
        All = VertexNormals | VertexColors | VertexUVs | FaceGroups
    }


	//
	// DMesh3 is a dynamic triangle mesh class. The mesh has has connectivity, 
	//  is an indexed mesh, and allows for gaps in the index space.
	//
	// internally, all data is stored in POD-type buffers, except for the vertex->edge
	// links, which are stored as List<int>'s. The arrays of POD data are stored in
	// DVector's, so they grow in chunks, which is relatively efficient. The actual
	// blocks are arrays, so they can be efficiently mem-copied into larger buffers
	// if necessary.
	//
	// Reference counts for verts/tris/edges are stored as separate RefCountVector
	// instances. 
	//
	// Vertices are stored as doubles, although this should be easily changed
	// if necessary, as the internal data structure is not exposed
	//
	// Per-vertex Vertex Normals, Colors, and UVs are optional and stored as floats.
	//
	// For each vertex, vertex_edges[i] is the unordered list of connected edges. The
	// elements of the list are indices into the edges list.
	// This list is unsorted but can be traversed in-order (ie cw/ccw) at some additional cost. 
	//
	// Triangles are stored as 3 ints, with optionally a per-triangle integer group id.
	//
	// The edges of a triangle are similarly stored as 3 ints, in triangle_edes. If the 
	// triangle is [v1,v2,v3], then the triangle edges [e1,e2,e3] are 
	// e1=edge(v1,v2), e2=edge(v2,v3), e3=edge(v3,v1), where the e# are indexes into edges.
	//
	// Edges are stored as tuples of 4 ints. If the edge is between v1 and v2, with neighbour
	// tris t1 and t2, then the edge is [min(v1,v2), max(v1,v2), t1, t2]. For a boundary
	// edge, t2 is InvalidID. t1 is never InvalidID.
	//
	// Most of the class assumes that the mesh is manifold. Many functions will
	// work if the topology is non-manifold, but behavior of operators like Split/Flip/Collapse
	// edge is untested. 
	//
	// The function CheckValidity() does extensive sanity checking on the mesh data structure.
	// Use this to test your code, both for mesh construction and editing!!
	// 
    //
    // TODO:
    //  - DVector w/ 'stride' option, so that we can guarantee that tuples are in single block.
    //    The can have custom accessor that looks up entire tuple
    public partial class DMesh3 : IDeformableMesh
    {
        public const int InvalidID = -1;
        public const int NonManifoldID = -2;


        public static readonly Vector3d InvalidVertex = new Vector3d(Double.MaxValue, 0, 0);
        public static readonly Index3i InvalidTriangle = new Index3i(InvalidID, InvalidID, InvalidID);
        public static readonly Index2i InvalidEdge = new Index2i(InvalidID, InvalidID);


        RefCountVector vertices_refcount;
        DVector<double> vertices;
		DVector<float> normals;
		DVector<float> colors;
		DVector<float> uv;

        // [TODO] this seems like it will not be efficient! 
        //   do our own short_list backed my a memory pool?
        // [TODO] this is optional if we only want to use this class as an iterable mesh-with-nbrs
        //   make it optional with a flag? (however find_edge depends on it...)
        DVector<List<int>> vertex_edges;

        RefCountVector triangles_refcount;
        DVector<int> triangles;
        DVector<int> triangle_edges;
		DVector<int> triangle_groups;

        RefCountVector edges_refcount;
        DVector<int> edges;

        int timestamp = 0;
        int shape_timestamp = 0;

        int max_group_id = 0;


        /// <summary>
        /// Support attaching arbitrary data to mesh. 
        /// Note that metadata is currently **NOT** copied when copying a mesh.
        /// </summary>
        Dictionary<string, object> Metadata = null;


        public DMesh3(bool bWantNormals = true, bool bWantColors = false, bool bWantUVs = false, bool bWantTriGroups = false)
        {
            vertices = new DVector<double>();
			if ( bWantNormals)
				normals = new DVector<float>();
			if ( bWantColors )
				colors = new DVector<float>();
			if ( bWantUVs )
				uv = new DVector<float>();
            vertex_edges = new DVector<List<int>>();
            vertices_refcount = new RefCountVector();

            triangles = new DVector<int>();
            triangle_edges = new DVector<int>();
            triangles_refcount = new RefCountVector();
			if ( bWantTriGroups )
				triangle_groups = new DVector<int>();
            max_group_id = 0;

            edges = new DVector<int>();
            edges_refcount = new RefCountVector();
        }
        public DMesh3(MeshComponents flags) : 
            this( (flags & MeshComponents.VertexNormals) != 0,  (flags & MeshComponents.VertexColors) != 0,
                  (flags & MeshComponents.VertexUVs) != 0,      (flags & MeshComponents.FaceGroups) != 0 )
        {
        }

        // normals/colors/uvs will only be copied if they exist
        public DMesh3(DMesh3 copy, bool bCompact = false, bool bWantNormals = true, bool bWantColors = true, bool bWantUVs = true)
        {
            if (bCompact)
                CompactCopy(copy, bWantNormals, bWantColors, bWantUVs);
            else
                Copy(copy, bWantNormals, bWantColors, bWantUVs);
        }
        public DMesh3(DMesh3 copy, bool bCompact, MeshComponents flags) : 
            this(copy, bCompact, (flags & MeshComponents.VertexNormals) != 0,  (flags & MeshComponents.VertexColors) != 0,
                  (flags & MeshComponents.VertexUVs) != 0 )
        {
        }


        public DMesh3(IMesh copy, bool bIsCompactHint, bool bWantNormals = true, bool bWantColors = true, bool bWantUVs = true)
        {
            Copy(copy, bIsCompactHint, bWantNormals, bWantColors, bWantUVs);
        }
        public DMesh3(IMesh copy, bool bIsCompactHint, MeshComponents flags) : 
            this(copy, bIsCompactHint, (flags & MeshComponents.VertexNormals) != 0,  (flags & MeshComponents.VertexColors) != 0,
                  (flags & MeshComponents.VertexUVs) != 0 )
        {
        }


        public void CompactCopy(DMesh3 copy, bool bNormals = true, bool bColors = true, bool bUVs = true)
        {
            if ( copy.IsCompact ) {
                Copy(copy, bNormals, bColors, bUVs);
                return;
            }

            vertices = new DVector<double>();
            vertex_edges = new DVector<List<int>>();
            vertices_refcount = new RefCountVector();
            triangles = new DVector<int>();
            triangle_edges = new DVector<int>();
            triangles_refcount = new RefCountVector();
            edges = new DVector<int>();
            edges_refcount = new RefCountVector();
            max_group_id = 0;

            normals = (bNormals && copy.normals != null) ? new DVector<float>() : null;
            colors = (bColors && copy.colors != null) ? new DVector<float>() : null;
            uv = (bUVs && copy.uv != null) ? new DVector<float>() : null;
            triangle_groups = (copy.triangle_groups != null) ? new DVector<int>() : null;

            // [TODO] if we knew some of these were dense we could copy directly...

            NewVertexInfo vinfo = new NewVertexInfo();
            int[] mapV = new int[copy.MaxVertexID];
            foreach ( int vid in copy.vertices_refcount ) {
                copy.GetVertex(vid, ref vinfo, bNormals, bColors, bUVs);
                mapV[vid] = AppendVertex(vinfo);
            }

            // [TODO] would be much faster to explicitly copy triangle & edge data structures!!

            foreach ( int tid in copy.triangles_refcount ) {
                Index3i t = copy.GetTriangle(tid);
                t.a = mapV[t.a]; t.b = mapV[t.b]; t.c = mapV[t.c];
                int g = (copy.HasTriangleGroups) ? copy.GetTriangleGroup(tid) : InvalidID;
                AppendTriangle(t, g);
                max_group_id = Math.Max(max_group_id, g);
            }
        }


        public void Copy(DMesh3 copy, bool bNormals = true, bool bColors = true, bool bUVs = true)
        {
            vertices = new DVector<double>(copy.vertices);

            normals = (bNormals && copy.normals != null) ? new DVector<float>(copy.normals) : null;
            colors = (bColors && copy.colors != null) ? new DVector<float>(copy.colors) : null;
            uv = (bUVs && copy.uv != null) ? new DVector<float>(copy.uv) : null;

            vertices_refcount = new RefCountVector(copy.vertices_refcount);
            vertex_edges = new DVector<List<int>>(copy.vertex_edges);
            int N = vertex_edges.Length;
            for ( int i = 0; i < N; ++i ) {
                if (vertices_refcount.isValid(i))
                    vertex_edges[i] = new List<int>(copy.vertex_edges[i]);
                else
                    vertex_edges[i] = null;
            }

            triangles = new DVector<int>(copy.triangles);
            triangle_edges = new DVector<int>(copy.triangle_edges);
            triangles_refcount = new RefCountVector(copy.triangles_refcount);
            if (copy.triangle_groups != null)
                triangle_groups = new DVector<int>(copy.triangle_groups);
            max_group_id = copy.max_group_id;

            edges = new DVector<int>(copy.edges);
            edges_refcount = new RefCountVector(copy.edges_refcount);
        }



        public void Copy(IMesh copy, bool bIsDense = false, bool bNormals = true, bool bColors = true, bool bUVs = true)
        {
            if (bIsDense == false)
                throw new Exception("DMesh3.Copy: current requires that IMesh be dense/compact");

            vertices = new DVector<double>();
            vertex_edges = new DVector<List<int>>();
            vertices_refcount = new RefCountVector();
            triangles = new DVector<int>();
            triangle_edges = new DVector<int>();
            triangles_refcount = new RefCountVector();
            edges = new DVector<int>();
            edges_refcount = new RefCountVector();
            max_group_id = 0;

            normals = (bNormals && copy.HasVertexNormals) ? new DVector<float>() : null;
            colors = (bColors && copy.HasVertexColors) ? new DVector<float>() : null;
            uv = (bUVs && copy.HasVertexUVs) ? new DVector<float>() : null;
            triangle_groups = (copy.HasTriangleGroups) ? new DVector<int>() : null;

            int NV = copy.MaxVertexID;
            //int[] mapV = new int[NV];
            for ( int vid = 0; vid < NV; ++vid ) {
                NewVertexInfo vinfo = copy.GetVertexAll(vid);
                int new_vid = AppendVertex(vinfo);
                Debug.Assert(new_vid == vid);
            }

            int NT = copy.MaxTriangleID;
            for ( int tid = 0; tid < NT; tid++ ) { 
                Index3i t = copy.GetTriangle(tid);
                //t.a = mapV[t.a]; t.b = mapV[t.b]; t.c = mapV[t.c];
                int g = (copy.HasTriangleGroups) ? copy.GetTriangleGroup(tid) : InvalidID;
                AppendTriangle(t, g);
                max_group_id = Math.Max(max_group_id, g);
            }
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


        // IMesh impl

        public int VertexCount {
            get { return vertices_refcount.count; }
        }
        public int TriangleCount {
            get { return triangles_refcount.count; }
        }
		public int EdgeCount {
			get { return edges_refcount.count; }
		}
		public int MaxVertexID {
			get { return vertices_refcount.max_index; }
		}
		public int MaxTriangleID {
			get { return triangles_refcount.max_index; }
		}
		public int MaxEdgeID {
			get { return edges_refcount.max_index; }
		}
        public int MaxGroupID {
            get { return max_group_id; }
        }

        public bool HasVertexColors { get { return colors != null; } }
        public bool HasVertexNormals { get { return normals != null; } }
        public bool HasVertexUVs { get { return uv != null; } }
		public bool HasTriangleGroups { get { return triangle_groups != null; } }

        public MeshComponents Components {
            get {
                MeshComponents c = 0;
                if (normals != null) c |= MeshComponents.VertexNormals;
                if (colors != null) c |= MeshComponents.VertexColors;
                if (uv != null) c |= MeshComponents.VertexUVs;
                if (triangle_groups != null) c |= MeshComponents.FaceGroups;
                return c;
            }
        }

        // info

        public bool IsVertex(int vID) {
            return vertices_refcount.isValid(vID);
        }
        public bool IsTriangle(int tID) {
            return triangles_refcount.isValid(tID);
        }
        public bool IsEdge(int eID) {
            return edges_refcount.isValid(eID);
        }


        // getters


        public Vector3d GetVertex(int vID) {
            return vertices_refcount.isValid(vID) ?
                new Vector3d(vertices[3 * vID], vertices[3 * vID + 1], vertices[3 * vID + 2]) : InvalidVertex;
        }

        public void SetVertex(int vID, Vector3d vNewPos) {
            Debug.Assert(vNewPos.IsFinite);     // this will really catch a lot of bugs...

			if ( vertices_refcount.isValid(vID) ) {
				int i = 3*vID;
				vertices[i] = vNewPos.x; vertices[i+1] = vNewPos.y; vertices[i+2] = vNewPos.z;
                updateTimeStamp(true);
			}
		}

		public Vector3f GetVertexNormal(int vID) {
            if (normals == null)
                return Vector3f.AxisY;
            else
                return vertices_refcount.isValid(vID) ?
                    new Vector3f(normals[3 * vID], normals[3 * vID + 1], normals[3 * vID + 2]) : Vector3f.AxisY;
		}

		public void SetVertexNormal(int vID, Vector3f vNewNormal) {
			if ( HasVertexNormals && vertices_refcount.isValid(vID) ) {
				int i = 3*vID;
				normals[i] = vNewNormal.x; normals[i+1] = vNewNormal.y; normals[i+2] = vNewNormal.z;
                updateTimeStamp(false);
			}
		}

		public Vector3f GetVertexColor(int vID) { 
			return vertices_refcount.isValid(vID) ?
                new Vector3f(colors[3 * vID], colors[3 * vID + 1], colors[3 * vID + 2]) : Vector3f.One;
		}

		public void SetVertexColor(int vID, Vector3f vNewColor) {
			if ( HasVertexColors && vertices_refcount.isValid(vID) ) {
				int i = 3*vID;
				colors[i] = vNewColor.x; colors[i+1] = vNewColor.y; colors[i+2] = vNewColor.z;
                updateTimeStamp(false);
			}
		}

		public Vector2f GetVertexUV(int vID) { 
			return vertices_refcount.isValid(vID) ?
                new Vector2f(uv[2 * vID], uv[2 * vID + 1]) : Vector2f.Zero;
		}

		public void SetVertexUV(int vID, Vector2f vNewUV) {
			if ( HasVertexUVs && vertices_refcount.isValid(vID) ) {
				int i = 2*vID;
				uv[i] = vNewUV.x; uv[i+1] = vNewUV.y;
                updateTimeStamp(false);
			}
		}

        public bool GetVertex(int vID, ref NewVertexInfo vinfo, bool bWantNormals, bool bWantColors, bool bWantUVs)
        {
            if (vertices_refcount.isValid(vID) == false)
                return false;
            vinfo.v.Set(vertices[3 * vID], vertices[3 * vID + 1], vertices[3 * vID + 2]);
            vinfo.bHaveN = vinfo.bHaveUV = vinfo.bHaveC = false;
            if (HasVertexColors && bWantNormals) {
                vinfo.bHaveN = true;
                vinfo.n.Set(normals[3 * vID], normals[3 * vID + 1], normals[3 * vID + 2]);
            }
            if (HasVertexColors && bWantColors) {
                vinfo.bHaveC = true;
                vinfo.c.Set(colors[3 * vID], colors[3 * vID + 1], colors[3 * vID + 2]);
            }
            if (HasVertexUVs && bWantUVs) {
                vinfo.bHaveUV = true;
                vinfo.uv.Set(uv[2 * vID], uv[2 * vID + 1]);
            }
            return true;
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

		public NewVertexInfo GetVertexAll(int i) {
			NewVertexInfo vi = new NewVertexInfo();
			vi.v = GetVertex(i);
			if ( HasVertexNormals ) {
				vi.bHaveN = true;
				vi.n = GetVertexNormal(i);
			} else
				vi.bHaveN = false;
			if ( HasVertexColors ) {
				vi.bHaveC = true;
				vi.c = GetVertexColor(i);
			} else
				vi.bHaveC = false;
			if ( HasVertexUVs ) {
				vi.bHaveUV = true;
				vi.uv = GetVertexUV(i);
			} else
				vi.bHaveUV = false;
			return vi;
		}


        /// <summary>
        /// Compute a normal/tangent frame at vertex that is "stable" as long as
        /// the mesh topology doesn't change, meaning that one axis of the frame
        /// will be computed from projection of outgoing edge.
        /// Requires that vertex normals are available.
        /// </summary>
        public Frame3f GetVertexFrame(int vID, int nFrameNormal = 2)
        {
            Debug.Assert(HasVertexNormals);

            int vi = 3 * vID;
            Vector3d v = new Vector3d(vertices[vi], vertices[vi + 1], vertices[vi + 2]);
            Vector3d normal = new Vector3d(normals[vi], normals[vi + 1], normals[vi + 2]);
            int eid = vertex_edges[vID][0];
            int ovi = 3 * edge_other_v(eid, vID);
            Vector3d ov = new Vector3d(vertices[ovi], vertices[ovi + 1], vertices[ovi + 2]);
            Vector3d edge = (ov - v);
            edge.Normalize();

            Vector3d t2 = edge.Cross(normal);
            t2.Normalize();
            Vector3d t1 = normal.Cross(t2);
            t1.Normalize();

            return new Frame3f((Vector3f)v, (Vector3f)t1, (Vector3f)t2, (Vector3f)normal);
        }




        public Index3i GetTriangle(int tID) {
            return triangles_refcount.isValid(tID) ?
                new Index3i(triangles[3 * tID], triangles[3 * tID + 1], triangles[3 * tID + 2]) : InvalidTriangle;
        }

        public Index3i GetTriEdges(int tID) {
            return triangles_refcount.isValid(tID) ?
                new Index3i(triangle_edges[3 * tID], triangle_edges[3 * tID + 1], triangle_edges[3 * tID + 2]) : InvalidTriangle;
        }

        public Index3i GetTriNeighbourTris(int tID) {
            if (triangles_refcount.isValid(tID)) {
                int tei = 3 * tID;
                Index3i nbr_t = Index3i.Zero;
                for (int j = 0; j < 3; ++j) {
                    int ei = 4 * triangle_edges[tei + j];
                    nbr_t[j] = (edges[ei + 2] == tID) ? edges[ei + 3] : edges[ei + 2];
                }
                return nbr_t;
            } else
                return InvalidTriangle;
        }

		public int GetTriangleGroup(int tID) { 
			return (triangle_groups == null) ? -1 
                : ( triangles_refcount.isValid(tID) ? triangle_groups[tID] : 0 );
		}

		public void SetTriangleGroup(int tid, int group_id) {
			if ( triangle_groups != null && triangles_refcount.isValid(tid) ) {
                triangle_groups[tid] = group_id;
                max_group_id = Math.Max(max_group_id, group_id);
                updateTimeStamp(false);
			}
		}

        public int AllocateTriangleGroup() {
            return max_group_id++;
        }


        public void GetTriVertices(int tID, ref Vector3d v0, ref Vector3d v1, ref Vector3d v2) {
            int ai = 3 * triangles[3 * tID];
            v0.x = vertices[ai]; v0.y = vertices[ai + 1]; v0.z = vertices[ai + 2];
            int bi = 3 * triangles[3 * tID + 1];
            v1.x = vertices[bi]; v1.y = vertices[bi + 1]; v1.z = vertices[bi + 2];
            int ci = 3 * triangles[3 * tID + 2];
            v2.x = vertices[ci]; v2.y = vertices[ci + 1]; v2.z = vertices[ci + 2];
        }

        public Vector3d GetTriVertex(int tid, int j) {
            int a = triangles[3 * tid + j];
            return new Vector3d(vertices[3 * a], vertices[3 * a + 1], vertices[3 * a + 2]);
        }

        public Vector3d GetTriBaryPoint(int tID, double bary0, double bary1, double bary2) { 
            int ai = 3 * triangles[3 * tID], 
                bi = 3 * triangles[3 * tID + 1], 
                ci = 3 * triangles[3 * tID + 2];
            return new Vector3d(
                (bary0*vertices[ai] + bary1*vertices[bi] + bary2*vertices[ci]),
                (bary0*vertices[ai + 1] + bary1*vertices[bi + 1] + bary2*vertices[ci + 1]),
                (bary0*vertices[ai + 2] + bary1*vertices[bi + 2] + bary2*vertices[ci + 2]));
        }

        public Vector3d GetTriNormal(int tID)
        {
            Vector3d v0 = Vector3d.Zero, v1 = Vector3d.Zero, v2 = Vector3d.Zero;
            GetTriVertices(tID, ref v0, ref v1, ref v2);
            return MathUtil.Normal(v0, v1, v2);
        }

        public double GetTriArea(int tID)
        {
            Vector3d v0 = Vector3d.Zero, v1 = Vector3d.Zero, v2 = Vector3d.Zero;
            GetTriVertices(tID, ref v0, ref v1, ref v2);
            return MathUtil.Area(v0, v1, v2);
        }

        public Vector3d GetTriBaryNormal(int tID, double bary0, double bary1, double bary2) { 
            int ai = 3 * triangles[3 * tID], 
                bi = 3 * triangles[3 * tID + 1], 
                ci = 3 * triangles[3 * tID + 2];
            Vector3d n = new Vector3d(
                (bary0*normals[ai] + bary1*normals[bi] + bary2*normals[ci]),
                (bary0*normals[ai + 1] + bary1*normals[bi + 1] + bary2*normals[ci + 1]),
                (bary0*normals[ai + 2] + bary1*normals[bi + 2] + bary2*normals[ci + 2]));
            n.Normalize();
            return n;
        }

        public Vector3d GetTriCentroid(int tID)
        {
            int ai = 3 * triangles[3 * tID], 
                bi = 3 * triangles[3 * tID + 1], 
                ci = 3 * triangles[3 * tID + 2];
            double f = (1.0 / 3.0);
            return new Vector3d(
                (vertices[ai] + vertices[bi] + vertices[ci]) * f,
                (vertices[ai + 1] + vertices[bi + 1] + vertices[ci + 1]) * f,
                (vertices[ai + 2] + vertices[bi + 2] + vertices[ci + 2]) * f );
        }



        public AxisAlignedBox3d GetTriBounds(int tID)
        {
            int vi = 3 * triangles[3 * tID];
            double x = vertices[vi], y = vertices[vi + 1], z = vertices[vi + 2];
            double minx = x, maxx = x, miny = y, maxy = y, minz = z, maxz = z;
            for (int i = 1; i < 3; ++i) {
                vi = 3 * triangles[3 * tID + i];
                x = vertices[vi]; y = vertices[vi + 1]; z = vertices[vi + 2];
                if (x < minx) minx = x; else if (x > maxx) maxx = x;
                if (y < miny) miny = y; else if (y > maxy) maxy = y;
                if (z < minz) minz = z; else if (z > maxz) maxz = z;
            }
            return new AxisAlignedBox3d(minx, miny, minz, maxx, maxy, maxz);
        }


        public Frame3f GetTriFrame(int tID, int nEdge = 0)
        {
            int ti = 3 * tID;
            int a = triangles[ti + (nEdge % 3)];
            int b = triangles[ti + ((nEdge+1) % 3)];
            int c = triangles[ti + ((nEdge+2) % 3)];
            Vector3d v0 = new Vector3d(vertices[3 * a], vertices[3 * a + 1], vertices[3 * a + 2]);
            Vector3d v1 = new Vector3d(vertices[3 * b], vertices[3 * b + 1], vertices[3 * b + 2]);
            Vector3d v2 = new Vector3d(vertices[3 * c], vertices[3 * c + 1], vertices[3 * c + 2]);
            Vector3f edge = (Vector3f)(v1 - v0).Normalized;
            Vector3f normal = (Vector3f)MathUtil.Normal(v0, v1, v2);
            Vector3f other = edge.Cross(normal);
            Vector3f center = (Vector3f)(v0 + v1 + v2) / 3;
            return new Frame3f(center, edge, other, normal);
        }



        public Index2i GetEdgeV(int eID) {
            return edges_refcount.isValid(eID) ?
                new Index2i(edges[4 * eID], edges[4 * eID + 1]) : InvalidEdge;
        }
        public bool GetEdgeV(int eID, ref Vector3d a, ref Vector3d b) {
            if ( edges_refcount.isValid(eID) ) {
                int iv0 = 3 * edges[4 * eID];
                a.x = vertices[iv0]; a.y = vertices[iv0 + 1]; a.z = vertices[iv0 + 2];
                int iv1 = 3 * edges[4 * eID + 1];
                b.x = vertices[iv1]; b.y = vertices[iv1 + 1]; b.z = vertices[iv1 + 2];
                return true;
            }
            return false;
        }

        public Index2i GetEdgeT(int eID) {
            return edges_refcount.isValid(eID) ?
                new Index2i(edges[4 * eID + 2], edges[4 * eID + 3]) : InvalidEdge;
        }

		public bool GetEdge(int eID, ref int a, ref int b, ref int t0, ref int t1) {
			if ( edges_refcount.isValid(eID) == false )
				return false;
			int i = eID*4;
			a = edges[i]; b = edges[i+1]; t0 = edges[i+2]; t1 = edges[i+3];
			return true;
		}

        // return same indices as GetEdgeV, but oriented based on attached triangle
        public Index2i GetOrientedBoundaryEdgeV(int eID)
        {
            if ( edges_refcount.isValid(eID) ) {
                int ei = 4 * eID;
                if ( edges[ei+3] == InvalidID) {
                    int a = edges[ei], b = edges[ei + 1];
                    int ti = 3 * edges[ei + 2];
                    Index3i tri = new Index3i(triangles[ti], triangles[ti + 1], triangles[ti + 2]);
                    int ai = IndexUtil.find_edge_index_in_tri(a, b, ref tri);
                    return new Index2i(tri[ai], tri[(ai + 1) % 3]);
                }
            }
            Debug.Assert(false);        // should not happen!
            return InvalidEdge;
        }
			
        // average of 1 or 2 face normals
        public Vector3d GetEdgeNormal(int eID)
        {
            if (edges_refcount.isValid(eID)) {
                int ei = 4 * eID;
                Vector3d n = GetTriNormal(edges[ei + 2]);
                if (edges[ei + 3] != InvalidID) {
                    n += GetTriNormal(edges[ei + 3]);
                    n.Normalize();
                }
                return n;
            }
            Debug.Assert(false);
            return Vector3d.Zero;
        }


        // mesh-building


        public int AppendVertex(Vector3d v) {
            return AppendVertex(new NewVertexInfo() {
                v = v, bHaveC = false, bHaveUV = false, bHaveN = false
            });
        }
        public int AppendVertex(NewVertexInfo info)
        {
            int vid = vertices_refcount.allocate();
			int i = 3*vid;
            vertices.insert(info.v[2], i + 2);
            vertices.insert(info.v[1], i + 1);
            vertices.insert(info.v[0], i);

			if ( normals != null ) {
				Vector3f n = (info.bHaveN) ? info.n : Vector3f.AxisY;
				normals.insert(n[2], i + 2);
				normals.insert(n[1], i + 1);
				normals.insert(n[0], i);
			}

			if ( colors != null ) {
				Vector3f c = (info.bHaveC) ? info.c : Vector3f.One;
				colors.insert(c[2], i + 2);
				colors.insert(c[1], i + 1);
				colors.insert(c[0], i);
			}

			if ( uv != null ) {
				Vector2f u = (info.bHaveUV) ? info.uv : Vector2f.Zero;
				int j = 2*vid;
				uv.insert(u[1], j + 1);
				uv.insert(u[0], j);
			}

            vertex_edges.insert(new List<int>(), vid);

            updateTimeStamp(true);
            return vid;
        }

        // direct copy from source mesh
        public int AppendVertex(DMesh3 from, int fromVID)
        {
            int bi = 3 * fromVID;

            int vid = vertices_refcount.allocate();
			int i = 3*vid;
            vertices.insert(from.vertices[bi+2], i + 2);
            vertices.insert(from.vertices[bi+1], i + 1);
            vertices.insert(from.vertices[bi], i);
			if ( normals != null ) {
                if (from.normals != null) {
                    normals.insert(from.normals[bi + 2], i + 2);
                    normals.insert(from.normals[bi + 1], i + 1);
                    normals.insert(from.normals[bi], i);
                } else {
                    normals.insert(0, i + 2);
                    normals.insert(1, i + 1);       // y-up
                    normals.insert(0, i);
                }
			}

			if ( colors != null ) {
                if (from.normals != null) {
                    colors.insert(from.colors[bi + 2], i + 2);
                    colors.insert(from.colors[bi + 1], i + 1);
                    colors.insert(from.colors[bi], i);
                } else {
                    colors.insert(1, i + 2);
                    colors.insert(1, i + 1);       // white
                    colors.insert(1, i);
                }
			}

			if ( uv != null ) {
				int j = 2*vid;
                if (from.normals != null) {
                    int bj = 2 * fromVID;
                    colors.insert(from.uv[bj + 1], j + 1);
                    colors.insert(from.uv[bj], j);
                } else {
                    uv.insert(0, j + 1);
                    uv.insert(0, j);
                }
			}

            vertex_edges.insert(new List<int>(), vid);

            updateTimeStamp(true);
            return vid;
        }


        public int AppendTriangle(int v0, int v1, int v2, int gid = -1) {
            return AppendTriangle(new Index3i(v0, v1, v2), gid);
        }
        public int AppendTriangle(Index3i tv, int gid = -1) {
            if (IsVertex(tv[0]) == false || IsVertex(tv[1]) == false || IsVertex(tv[2]) == false) {
                Util.gDevAssert(false);
                return InvalidID;
            }
            if (tv[0] == tv[1] || tv[0] == tv[2] || tv[1] == tv[2]) {
                Util.gDevAssert(false);
                return InvalidID;
            }

            // look up edges. if any already have two triangles, this would 
            // create non-manifold geometry and so we do not allow it
            int e0 = find_edge(tv[0], tv[1]);
            int e1 = find_edge(tv[1], tv[2]);
            int e2 = find_edge(tv[2], tv[0]);
            if ((e0 != InvalidID && edge_is_boundary(e0) == false)
                 || (e1 != InvalidID && edge_is_boundary(e1) == false)
                 || (e2 != InvalidID && edge_is_boundary(e2) == false)) {
                return NonManifoldID;
            }

            // now safe to insert triangle
            int tid = triangles_refcount.allocate();
			int i = 3*tid;
            triangles.insert(tv[2], i + 2);
            triangles.insert(tv[1], i + 1);
            triangles.insert(tv[0], i);
            if (triangle_groups != null) {
                triangle_groups.insert(gid, tid);
                max_group_id = Math.Max(max_group_id, gid);
            }

            // increment ref counts and update/create edges
            vertices_refcount.increment(tv[0]);
            vertices_refcount.increment(tv[1]);
            vertices_refcount.increment(tv[2]);

            add_tri_edge(tid, tv[0], tv[1], 0, e0);
            add_tri_edge(tid, tv[1], tv[2], 1, e1);
            add_tri_edge(tid, tv[2], tv[0], 2, e2);

            updateTimeStamp(true);
            return tid;
        }
        // helper fn for above, just makes code cleaner
        void add_tri_edge(int tid, int v0, int v1, int j, int eid)
        {
            if (eid != InvalidID) {
                edges[4 * eid + 3] = tid;
                triangle_edges.insert(eid, 3 * tid + j);
            } else
                triangle_edges.insert(add_edge(v0, v1, tid), 3 * tid + j);
        }





        public void EnableVertexNormals(Vector3f initial_normal)
        {
            if (HasVertexNormals)
                return;
            normals = new DVector<float>();
            int NV = MaxVertexID;
            normals.resize(3*NV);
            for (int i = 0; i < NV; ++i) {
                int vi = 3 * i;
                normals[vi] = initial_normal.x;
                normals[vi + 1] = initial_normal.y;
                normals[vi + 2] = initial_normal.z;
            }
        }
        public void DiscardVertexNormals() {
            normals = null;
        }

        public void EnableVertexColors(Vector3f initial_color)
        {
            if (HasVertexColors)
                return;
            colors = new DVector<float>();
            int NV = MaxVertexID;
            colors.resize(3*NV);
            for (int i = 0; i < NV; ++i) {
                int vi = 3 * i;
                colors[vi] = initial_color.x;
                colors[vi + 1] = initial_color.y;
                colors[vi + 2] = initial_color.z;
            }
        }
        public void DiscardVertexColors() {
            colors= null;
        }

        public void EnableVertexUVs(Vector2f initial_uv)
        {
            if (HasVertexUVs)
                return;
            uv = new DVector<float>();
            int NV = MaxVertexID;
            uv.resize(2*NV);
            for (int i = 0; i < NV; ++i) {
                int vi = 2 * i;
                uv[vi] = initial_uv.x;
                uv[vi + 1] = initial_uv.y;
            }
        }
        public void DiscardVertexUVs() {
            uv = null;
        }

        public void EnableTriangleGroups(int initial_group = 0)
        {
            if (HasTriangleGroups)
                return;
            triangle_groups = new DVector<int>();
            int NT = MaxTriangleID;
            triangle_groups.resize(NT);
            for (int i = 0; i < NT; ++i)
                triangle_groups[i] = initial_group;
            max_group_id = 0;
        }
        public void DiscardTriangleGroups() {
            triangle_groups = null;
            max_group_id = 0;
        }









        // iterators

        public IEnumerable<int> VertexIndices() {
            foreach (int vid in vertices_refcount)
                yield return vid;
        }
        public IEnumerable<int> TriangleIndices() {
            foreach (int tid in triangles_refcount)
                yield return tid;
        }
        public IEnumerable<int> EdgeIndices() {
            foreach (int eid in edges_refcount)
                yield return eid;
        }


        public IEnumerable<int> BoundaryEdgeIndices() {
            foreach ( int eid in edges_refcount ) {
                if (edges[4 * eid + 3] == InvalidID)
                    yield return eid;
            }
        }


        public IEnumerable<Vector3d> Vertices() {
            foreach (int vid in vertices_refcount) {
                int i = 3 * vid;
                yield return new Vector3d(vertices[i], vertices[i + 1], vertices[i + 2]);
            }
        }
        public IEnumerable<Index3i> Triangles() {
            foreach (int tid in triangles_refcount) {
                int i = 3 * tid;
                yield return new Index3i(triangles[i], triangles[i + 1], triangles[i + 2]);
            }
        }

        // return value is [v0,v1,t0,t1], where t1 will be InvalidID if this is a boundary edge
        public IEnumerable<Index4i> Edges() {
            foreach (int eid in edges_refcount) {
                int i = 4 * eid;
                yield return new Index4i(edges[i], edges[i + 1], edges[i + 2], edges[i + 3]);
            }
        }


        // queries

        // linear search through edges of vA
        public int FindEdge(int vA, int vB) {
            debug_check_is_vertex(vA);
            debug_check_is_vertex(vB);
            return find_edge(vA, vB);
        }

        // faster than FindEdge
        public int FindEdgeFromTri(int vA, int vB, int t) {
            return find_edge_from_tri(vA, vB, t);
        }

		// [RMS] this does more work than necessary, see (??? comment never finished...)
        public Index2i GetEdgeOpposingV(int eID)
        {
			// ** it is important that verts returned maintain [c,d] order!!
			int i = 4*eID;
            int a = edges[i], b = edges[i + 1];
            int t0 = edges[i + 2], t1 = edges[i + 3];
			int c = IndexUtil.find_tri_other_vtx(a, b, triangles, t0);
            if (t1 != InvalidID) {
				int d = IndexUtil.find_tri_other_vtx(a, b, triangles, t1);
                return new Index2i(c, d);
            } else
                return new Index2i(c, InvalidID);
        }


        public int FindTriangle(int a, int b, int c)
        {
            int eid = find_edge(a, b);
            if (eid == InvalidID)
                return InvalidID;
            int ei = 4 * eid;
            int ti = 3*edges[ei + 2];
            Index3i tri = new Index3i(triangles[ti], triangles[ti + 1], triangles[ti + 2]);
            if (IndexUtil.is_same_triangle(a, b, c, ref tri))
                return edges[ei + 2];
            if ( edges[ei + 3] != InvalidID ) {
                ti = 3 * edges[ei + 3];
                tri.a = triangles[ti]; tri.b = triangles[ti + 1]; tri.c = triangles[ti + 2];
                if (IndexUtil.is_same_triangle(a, b, c, ref tri))
                    return edges[ei + 3];
            }
            return InvalidID;
        }



		public IEnumerable<int> VtxVerticesItr(int vID) {
			if ( vertices_refcount.isValid(vID) ) {
				List<int> edges = vertex_edges[vID];
				int N = edges.Count;
				for ( int i = 0; i < N; ++i )
					yield return edge_other_v(edges[i], vID);
			}
		}


		public IEnumerable<int> VtxEdgesItr(int vID) {
			if ( vertices_refcount.isValid(vID) ) {
				List<int> edges = vertex_edges[vID];
				int N = edges.Count;
                for (int i = 0; i < N; ++i)
                    yield return edges[i];
			}
		}


        // Returns count of boundary edges at vertex, and 
        // the first two boundary edges if found. 
        // If return is > 2, call VtxAllBoundaryEdges
        public int VtxBoundaryEdges(int vID, ref int e0, ref int e1)
        {
            if ( vertices_refcount.isValid(vID) ) {
                int count = 0;
				List<int> vtx_edges = vertex_edges[vID];
				int N = vtx_edges.Count;
                for (int i = 0; i < N; ++i) {
                    int eid = vtx_edges[i];
                    int ei = 4 * eid;
                    if ( edges[ei+3] == InvalidID ) {
                        if (count == 0)
                            e0 = eid;
                        else if (count == 1)
                            e1 = eid;
                        count++;
                    }
                }
                return count;
            }
            Debug.Assert(false);
            return -1;
        }


        // e needs to be large enough (ie call VtxBoundaryEdges, or as large as max one-ring)
        // returns count, ie number of elements of e that were filled
        public int VtxAllBoundaryEdges(int vID, int[] e)
        {
            if (vertices_refcount.isValid(vID)) {
                int count = 0;
				List<int> vtx_edges = vertex_edges[vID];
				int N = vtx_edges.Count;
                for (int i = 0; i < N; ++i) {
                    int eid = vtx_edges[i];
                    int ei = 4 * eid;
                    if ( edges[ei+3] == InvalidID ) 
                        e[count++] = eid;
                }
                return count;
            }
            Debug.Assert(false);
            return -1;
        }



        // ack...duplicate of GetTriNbrTris, but unused??
   //     public Index3i GetTriTriangles(int tID) {
   //         if (!IsTriangle(tID))
   //             return InvalidTriangle;
			//int i = 3*tID;
   //         return new Index3i(
   //             edge_other_t(triangle_edges[i], tID),
   //             edge_other_t(triangle_edges[i + 1], tID),
   //             edge_other_t(triangle_edges[i + 2], tID));
   //     }

        public MeshResult GetVtxTriangles(int vID, List<int> vTriangles, bool bUseOrientation)
        {
            if (!IsVertex(vID))
                return MeshResult.Failed_NotAVertex;
            List<int> vedges = vertex_edges[vID];

            if (bUseOrientation) {
                foreach (int eid in vedges) {
                    int vOther = edge_other_v(eid, vID);
					int i = 4*eid;
                    int et0 = edges[i + 2];
                    if (tri_has_sequential_v(et0, vID, vOther))
                        vTriangles.Add(et0);
                    int et1 = edges[i + 3];
                    if (et1 != InvalidID && tri_has_sequential_v(et1, vID, vOther))
                        vTriangles.Add(et1);
                }
            } else {
                // brute-force method
                foreach (int eid in vedges) {
					int i = 4*eid;					
                    int t0 = edges[i + 2];
                    if (vTriangles.Contains(t0) == false)
                        vTriangles.Add(t0);
                    int t1 = edges[i + 3];
                    if (t1 != InvalidID && vTriangles.Contains(t1) == false)
                        vTriangles.Add(t1);
                }
            }
            return MeshResult.Ok;
        }


        /// <summary>
        /// return # of triangles attached to vID, or -1 if invalid vertex
        /// if bBruteForce = true, explicitly checks, which creates a list and is expensive
        /// default is false, uses orientation, no memory allocation
        /// </summary>
        public int GetVtxTriangleCount(int vID, bool bBruteForce = false)
        {
            if ( bBruteForce ) {
                List<int> vTriangles = new List<int>();
                if (GetVtxTriangles(vID, vTriangles, false) != MeshResult.Ok)
                    return -1;
                return vTriangles.Count;
            }

            if (!IsVertex(vID))
                return -1;
            List<int> vedges = vertex_edges[vID];
            int N = 0;
            foreach (int eid in vedges) {
                int vOther = edge_other_v(eid, vID);
				int i = 4*eid;
                int et0 = edges[i + 2];
                if (tri_has_sequential_v(et0, vID, vOther))
                    N++;
                int et1 = edges[i + 3];
                if (et1 != InvalidID && tri_has_sequential_v(et1, vID, vOther))
                    N++;
            }
            return N;
        }


		public IEnumerable<int> VtxTrianglesItr(int vID) {
			if ( IsVertex(vID) ) {
				List<int> vedges = vertex_edges[vID];
				foreach (int eid in vedges) {
					int vOther = edge_other_v(eid, vID);
					int i = 4*eid;
					int et0 = edges[i + 2];
					if (tri_has_sequential_v(et0, vID, vOther))
						yield return et0;
					int et1 = edges[i + 3];
					if (et1 != InvalidID && tri_has_sequential_v(et1, vID, vOther))
						yield return et1;
				}
			}
		}


		public int GetVtxEdgeValence(int vID) {
			return vertex_edges[vID].Count;
		}

		// from edge and vert, returns other vert, two opposing verts, and two triangles
		public void GetVtxNbrhood(int eID, int vID, ref int vOther, ref int oppV1, ref int oppV2, ref int t1, ref int t2)
		{
			int i = 4*eID;
			vOther = (edges[i] == vID) ? edges[i+1] : edges[i];
			t1 = edges[i + 2];
			oppV1 = IndexUtil.find_tri_other_vtx(vID, vOther, triangles, t1);
			t2 = edges[i + 3];
			if ( t2 != InvalidID )
				oppV2 = IndexUtil.find_tri_other_vtx(vID, vOther, triangles, t2);
			else
				t2 = InvalidID;
		}


        public bool tri_has_v(int tID, int vID) {
			int i = 3*tID;
            return triangles[i] == vID 
                || triangles[i + 1] == vID
                || triangles[i + 2] == vID;
        }

        public bool tri_is_boundary(int tID) {
			int i = 3*tID;
            return edge_is_boundary(triangle_edges[i])
                || edge_is_boundary(triangle_edges[i + 1])
                || edge_is_boundary(triangle_edges[i + 2]);
        }

        public bool tri_has_neighbour_t(int tCheck, int tNbr) {
			int i = 3*tCheck;
            return edge_has_t(triangle_edges[i], tNbr)
                || edge_has_t(triangle_edges[i + 1], tNbr)
                || edge_has_t(triangle_edges[i + 2], tNbr);
        }

        public bool tri_has_sequential_v(int tID, int vA, int vB)
        {
			int i = 3*tID;
            int v0 = triangles[i], v1 = triangles[i + 1], v2 = triangles[i + 2];
            if (v0 == vA && v1 == vB) return true;
            if (v1 == vA && v2 == vB) return true;
            if (v2 == vA && v0 == vB) return true;
            return false;
        }

		//! returns edge ID
		public int find_tri_neighbour_edge(int tID, int vA, int vB)
		{
			int i = 3*tID;
			int tv0 = triangles[i], tv1 = triangles[i+1];
			if ( IndexUtil.same_pair_unordered(tv0, tv1, vA, vB) ) return triangle_edges[3*tID];
			int tv2 = triangles[i+2];
			if ( IndexUtil.same_pair_unordered(tv1, tv2, vA, vB) ) return triangle_edges[3*tID+1];
			if ( IndexUtil.same_pair_unordered(tv2, tv0, vA, vB) ) return triangle_edges[3*tID+2];
			return InvalidID;	
		}

		// returns 0/1/2
		public int find_tri_neighbour_index(int tID, int vA, int vB)
		{
			int i = 3*tID;
			int tv0 = triangles[i], tv1 = triangles[i+1];
			if ( IndexUtil.same_pair_unordered(tv0, tv1, vA, vB) ) return 0;
			int tv2 = triangles[i+2];
			if ( IndexUtil.same_pair_unordered(tv1, tv2, vA, vB) ) return 1;
			if ( IndexUtil.same_pair_unordered(tv2, tv0, vA, vB) ) return 2;
			return InvalidID;	
		}


        public bool IsBoundaryEdge(int eid) {
            return edges[4 * eid + 3] == InvalidID;
        }
        public bool edge_is_boundary(int eid) {
            return edges[4 * eid + 3] == InvalidID;
        }
        public bool edge_has_v(int eid, int vid) {
			int i = 4*eid;
            return (edges[i] == vid) || (edges[i + 1] == vid);
        }
        public bool edge_has_t(int eid, int tid) {
			int i = 4*eid;
            return (edges[i + 2] == tid) || (edges[i + 3] == tid);
        }
        public int edge_other_v(int eID, int vID)
        {
			int i = 4*eID;
            int ev0 = edges[i], ev1 = edges[i + 1];
            return (ev0 == vID) ? ev1 : ((ev1 == vID) ? ev0 : InvalidID);
        }
        public int edge_other_t(int eID, int tid) {
			int i = 4*eID;
            int et0 = edges[i + 2], et1 = edges[i + 3];
            return (et0 == tid) ? et1 : ((et1 == tid) ? et0 : InvalidID);
        }


        // ugh need to deprecate this...weird API!
		public bool vertex_is_boundary(int vid) {
			foreach ( int e in vertex_edges[vid] )
				if ( edge_is_boundary(e) )
					return true;
			return false;
		}
        public bool IsBoundaryVertex(int vid) {
            foreach (int e in vertex_edges[vid]) {
                if (edges[4 * e + 3] == InvalidID)
                    return true;
            }
            return false;
        }



        int find_edge(int vA, int vB)
        {
            int vO = Math.Max(vA, vB);
            List<int> e0 = vertex_edges[Math.Min(vA, vB)];
            int idx = e0.FindIndex((x) => edge_has_v(x, vO));
            return (idx == -1) ? InvalidID : e0[idx];
        }

        int find_edge_from_tri(int vA, int vB, int tID)
        {
            int i = 3 * tID;
            if (IndexUtil.same_pair_unordered(vA, vB, triangles[i], triangles[i + 1]))
                return triangle_edges[i];
            if (IndexUtil.same_pair_unordered(vA, vB, triangles[i+1], triangles[i + 2]))
                return triangle_edges[i+1];
            if (IndexUtil.same_pair_unordered(vA, vB, triangles[i+2], triangles[i]))
                return triangle_edges[i+2];
            return InvalidID;
        }





        // queries


        public bool IsGroupBoundaryEdge(int eID)
        {
            if ( IsEdge(eID) == false )
                throw new Exception("DMesh3.IsGroupBoundaryEdge: " + eID + " is not a valid edge");
            int et1 = edges[4 * eID + 3];
            if (et1 == InvalidID)
                return false;
            int g1 = triangle_groups[et1];
            int et0 = edges[4 * eID + 2];
            int g0 = triangle_groups[et0];
            return g1 != g0;
        }


        // returns true if vertex has more than one tri groups in its tri nbrhood
        public bool IsGroupBoundaryVertex(int vID)
        {
            if (IsVertex(vID) == false)
                throw new Exception("DMesh3.IsGroupBoundaryVertex: " + vID + " is not a valid vertex");
            if (triangle_groups == null)
                return false;
			List<int> vedges = vertex_edges[vID];
            int group_id = int.MinValue;
            foreach (int eID in vedges) {
                int et0 = edges[4 * eID + 2];
                int g0 = triangle_groups[et0];
                if (group_id != g0) {
                    if (group_id == int.MinValue)
                        group_id = g0;
                    else
                        return true;        // saw multiple group IDs
                }
                int et1 = edges[4 * eID + 3];
                if (et1 != InvalidID) {
                    int g1 = triangle_groups[et1];
                    if (group_id != g1)
                        return true;        // saw multiple group IDs
                }
            }
            return false;
        }



        // returns true if more than two group border edges meet at vertex
        public bool IsGroupJunctionVertex(int vID)
        {
            if (IsVertex(vID) == false)
                throw new Exception("DMesh3.IsGroupBoundaryVertex: " + vID + " is not a valid vertex");
            if (triangle_groups == null)
                return false;
			List<int> vedges = vertex_edges[vID];
            Index2i groups = Index2i.Max;
            foreach (int eID in vedges) {
                Index2i et = new Index2i(edges[4 * eID + 2], edges[4 * eID + 3]);
                for (int k = 0; k < 2; ++k) {
                    if (et[k] == InvalidID)
                        continue;
                    int g0 = triangle_groups[et[k]];
                    if (g0 != groups.a && g0 != groups.b) {
                        if (groups.a != Index2i.Max.a && groups.b != Index2i.Max.b)
                            return true;
                        if (groups.a == Index2i.Max.a)
                            groups.a = g0;
                        else
                            groups.b = g0;
                    }
                }
            }
            return false;
        }




        /// <summary>
        /// returns true if vID is a "bowtie" vertex, ie multiple disjoint triangle sets in one-ring
        /// </summary>
        public bool IsBowtieVertex(int vID)
        {
            if (vertices_refcount.isValid(vID)) {
                int nTris = GetVtxTriangleCount(vID);
                List<int> edges = vertex_edges[vID];
                if (!(nTris == GetVtxEdges(vID).Count || nTris == GetVtxEdges(vID).Count - 1))
                    return true;
            }
            return false;
        }


        // compute vertex bounding box
        public AxisAlignedBox3d GetBounds()
        {
            double x = 0, y = 0, z = 0;
            foreach ( int vi in vertices_refcount ) {
                x = vertices[3*vi]; y = vertices[3*vi + 1]; z = vertices[3*vi + 2];
                break;
            }
            double minx = x, maxx = x, miny = y, maxy = y, minz = z, maxz = z;
            foreach ( int vi in vertices_refcount ) {
                x = vertices[3*vi]; y = vertices[3*vi + 1]; z = vertices[3*vi + 2];
                if (x < minx) minx = x; else if (x > maxx) maxx = x;
                if (y < miny) miny = y; else if (y > maxy) maxy = y;
                if (z < minz) minz = z; else if (z > maxz) maxz = z;
            }
            return new AxisAlignedBox3d(minx, miny, minz, maxx, maxy, maxz);
        }

        AxisAlignedBox3d cached_bounds;
        int cached_bounds_timestamp = -1;

        //! cached bounding box, lazily re-computed on access if mesh has changed
        public AxisAlignedBox3d CachedBounds
        {
            get {
                if (cached_bounds_timestamp != Timestamp) {
                    cached_bounds = GetBounds();
                    cached_bounds_timestamp = Timestamp;
                }
                return cached_bounds;
            }
        }




        bool cached_is_closed = false;
        int cached_is_closed_timstamp = -1;

        public bool IsClosed() {
            if (TriangleCount == 0)
                return false;
            // [RMS] under possibly-mistaken belief that foreach() has some overhead...
            if (MaxEdgeID / EdgeCount > 5) {
                foreach (int eid in edges_refcount)
                    if (edge_is_boundary(eid))
                        return false;
            } else {
                int N = MaxEdgeID;
                for (int i = 0; i < N; ++i)
                    if (edges_refcount.isValid(i) && edge_is_boundary(i))
                        return false;
            }
            return true;            
        }

        public bool CachedIsClosed {
            get {
                if (cached_is_closed_timstamp != Timestamp) {
                    cached_is_closed = IsClosed();
                    cached_is_closed_timstamp = Timestamp;
                }
                return cached_is_closed;
            }
        }




        public bool IsCompact {
            get { return vertices_refcount.is_dense && edges_refcount.is_dense && triangles_refcount.is_dense; }
        }
        public bool IsCompactV {
            get { return vertices_refcount.is_dense; }
        }





        // Metadata support

        public bool HasMetadata {
            get { return Metadata != null && Metadata.Keys.Count > 0; }
        }
        public void AttachMetadata(string key, object o)
        {
            if (Metadata == null)
                Metadata = new Dictionary<string, object>();
            Metadata.Add(key, o);
        }
        public object FindMetadata(string key)
        {
            if (Metadata == null)
                return null;
            object o = null;
            bool bFound = Metadata.TryGetValue(key, out o);
            return (bFound) ? o : null;
        }
        public bool RemoveMetadata(string key)
        {
            if (Metadata == null)
                return false;
            return Metadata.Remove(key);
        }
        public void ClearMetadata()
        {
            if (Metadata != null) {
                Metadata.Clear();
                Metadata = null;
            }
        }







        // direct access to internal dvectors - dangerous!!

        public DVector<double> VerticesBuffer {
            get { return vertices; }
        }
        public RefCountVector VerticesRefCounts {
            get { return vertices_refcount; }
        }
        public DVector<float> NormalsBuffer {
            get { return normals; }
        }
        public DVector<float> ColorsBuffer {
            get { return colors; }
        }
        public DVector<float> UVBuffer {
            get { return uv; }
        }

        public DVector<int> TrianglesBuffer {
            get { return triangles; }
        }
        public RefCountVector TrianglesRefCounts {
            get { return triangles_refcount; }
        }
        public DVector<int> GroupsBuffer {
            get { return triangle_groups; }
        }

        public DVector<int> EdgesBuffer{
            get { return edges; }
        }
        public RefCountVector EdgesRefCounts {
            get { return edges_refcount; }
        }



        // assumes that we have initialized vertices, triangles, and edges buffers,
        // and edges refcounts. Rebuilds vertex and tri refcounts, triangle edges,
        // vertex edges
        public void RebuildFromEdgeRefcounts()
        {
            int MaxVID = vertices.Length / 3;
            int MaxTID = triangles.Length / 3;

            triangle_edges.resize(triangles.Length);
            triangles_refcount.RawRefCounts.resize(MaxTID);

            vertex_edges.resize(MaxVID);
            vertices_refcount.RawRefCounts.resize(MaxVID);

            int MaxEID = edges.Length / 4;
            for ( int eid = 0; eid < MaxEID; ++eid ) {
                if (edges_refcount.isValid(eid) == false)
                    continue;
                int va = edges[4 * eid];
                int vb = edges[4 * eid + 1];
                int t0 = edges[4 * eid + 2];
                int t1 = edges[4 * eid + 3];

                // set vertex and tri refcounts to 1
                // find edges [a,b] in each triangle and set its tri-edge to this edge

                vertices_refcount.set_Unsafe(va, 1);
                vertices_refcount.set_Unsafe(vb, 1);
                triangles_refcount.set_Unsafe(t0, 1);
                Index3i tri0 = GetTriangle(t0);
                int idx0 = IndexUtil.find_edge_index_in_tri(va, vb, ref tri0);
                triangle_edges[3 * t0 + idx0] = eid;

                if (t1 != InvalidID) {
                    triangles_refcount.set_Unsafe(t1, 1);
                    Index3i tri1 = GetTriangle(t1);
                    int idx1 = IndexUtil.find_edge_index_in_tri(va, vb, ref tri1);
                    triangle_edges[3 * t1 + idx1] = eid;
                }

                // add this edge to both vertices

                if ( vertex_edges[va] == null ) 
                    vertex_edges[va] = new List<int>();
                vertex_edges[va].Add(eid);

                if (vertex_edges[vb] == null)
                    vertex_edges[vb] = new List<int>();
                vertex_edges[vb].Add(eid);
            }

            // iterate over triangles and increment vtx refcount for each tri
            for ( int tid = 0; tid < MaxTID; ++tid ) {
                if (triangles_refcount.isValid(tid) == false)
                    continue;
                int a = triangles[3 * tid], b = triangles[3 * tid + 1], c = triangles[3 * tid + 2];
                vertices_refcount.increment(a);
                vertices_refcount.increment(b);
                vertices_refcount.increment(c);
            }

            vertices_refcount.rebuild_free_list();
            triangles_refcount.rebuild_free_list();
            edges_refcount.rebuild_free_list();

            updateTimeStamp(true);
        }

	}
}

