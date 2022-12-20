// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Distributed under the Boost Software License, Version 1.0. http://www.boost.org/LICENSE_1_0.txt
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace g3
{
    /// <summary>
    /// 
    /// 
    /// TODO:
    ///    - track descendant triangles of each input face
    ///    - for missing segments, can resolve in 2D in plane of face
    /// 
    /// 
    /// </summary>
    public class MeshMeshCut
    {
        public DMesh3 Target;
        public DMesh3 CutMesh;

        PointHashGrid3d<int> PointHash;

        // points within this tolerance are merged
        public double VertexSnapTol = 0.00001;

        // List of vertices in output Target that are on the
        // cut path, after calling RemoveContained. 
        // TODO: still missing some vertices??
        public List<int> CutVertices;


        public void Compute()
        {
            double cellSize = Target.CachedBounds.MaxDim / 64;
            PointHash = new PointHashGrid3d<int>(cellSize, -1);

            // insert target vertices into hash
            foreach ( int vid in Target.VertexIndices()) {
                Vector3d v = Target.GetVertex(vid);
                int existing = find_existing_vertex(v);
                if (existing != -1)
                    System.Console.WriteLine("VERTEX {0} IS DUPLICATE OF {1}!", vid, existing);
                PointHash.InsertPointUnsafe(vid, v);
            }

            initialize();
            find_segments();
            insert_face_vertices();
            insert_edge_vertices();
            connect_edges();

            // SegmentInsertVertices was constructed by planar polygon
            // insertions in MeshInsertUVPolyCurve calls, but we also
            // need to the segment vertices
            foreach (SegmentVtx sv in SegVertices)
                SegmentInsertVertices.Add(sv.vtx_id);
        }


        public void RemoveContained()
        {
            DMeshAABBTree3 spatial = new DMeshAABBTree3(CutMesh, true);
            spatial.WindingNumber(Vector3d.Zero);
            SafeListBuilder<int> removeT = new SafeListBuilder<int>();
            gParallel.ForEach(Target.TriangleIndices(), (tid) => {
                Vector3d v = Target.GetTriCentroid(tid);
                if (spatial.WindingNumber(v) > 0.9)
                    removeT.SafeAdd(tid);
            });
            MeshEditor.RemoveTriangles(Target, removeT.Result);

            // [RMS] construct set of on-cut vertices? This is not
            // necessarily all boundary vertices...
            CutVertices = new List<int>();
            foreach (int vid in SegmentInsertVertices) {
                if (Target.IsVertex(vid))
                    CutVertices.Add(vid);
            }
        }

        public void AppendSegments(double r)
        {
            foreach ( var seg in Segments ) {
                Segment3d s = new Segment3d(seg.v0.v, seg.v1.v);
                if ( Target.FindEdge(seg.v0.vtx_id, seg.v1.vtx_id) == DMesh3.InvalidID )
                    MeshEditor.AppendLine(Target, s, (float)r);
            }
        }

        public void ColorFaces()
        {
            int counter = 1;
            Dictionary<int, int> gidmap = new Dictionary<int, int>();
            foreach (var key in SubFaces.Keys)
                gidmap[key] = counter++;
            Target.EnableTriangleGroups(0);
            foreach ( int tid in Target.TriangleIndices() ) {
                if (ParentFaces.ContainsKey(tid))
                    Target.SetTriangleGroup(tid, gidmap[ParentFaces[tid]]);
                else if (SubFaces.ContainsKey(tid))
                    Target.SetTriangleGroup(tid, gidmap[tid]);
            }
        }


        class SegmentVtx
        {
            public Vector3d v;
            public int type = -1;
            public int initial_type = -1;
            public int vtx_id = DMesh3.InvalidID;
            public int elem_id = DMesh3.InvalidID;
        }
        List<SegmentVtx> SegVertices;
        Dictionary<int, SegmentVtx> VIDToSegVtxMap;


        // segment vertices in each triangle that we still have to insert
        Dictionary<int, List<SegmentVtx>> FaceVertices;

        // segment vertices in each edge that we still have to insert
        Dictionary<int, List<SegmentVtx>> EdgeVertices;


        class IntersectSegment
        {
            public int base_tid;
            public SegmentVtx v0;
            public SegmentVtx v1;
            public SegmentVtx this[int key] {
                get { return (key == 0) ? v0 : v1; }
                set { if (key == 0) v0 = value; else v1 = value; }
            }
        }
        IntersectSegment[] Segments;

        Vector3d[] BaseFaceCentroids;
        Vector3d[] BaseFaceNormals;
        Dictionary<int, HashSet<int>> SubFaces;
        Dictionary<int, int> ParentFaces;

        HashSet<int> SegmentInsertVertices;

        void initialize()
        {
            BaseFaceCentroids = new Vector3d[Target.MaxTriangleID];
            BaseFaceNormals = new Vector3d[Target.MaxTriangleID];
            double area = 0;
            foreach (int tid in Target.TriangleIndices())
                Target.GetTriInfo(tid, out BaseFaceNormals[tid], out area, out BaseFaceCentroids[tid]);

            // allocate internals
            SegVertices = new List<SegmentVtx>();
            EdgeVertices = new Dictionary<int, List<SegmentVtx>>();
            FaceVertices = new Dictionary<int, List<SegmentVtx>>();
            SubFaces = new Dictionary<int, HashSet<int>>();
            ParentFaces = new Dictionary<int, int>();
            SegmentInsertVertices = new HashSet<int>();
            VIDToSegVtxMap = new Dictionary<int, SegmentVtx>();
        }



        /// <summary>
        /// 1) Find intersection segments
        /// 2) sort onto existing input mesh vtx/edge/face
        /// </summary>
        void find_segments()
        {
            Dictionary<Vector3d, SegmentVtx> SegVtxMap = new Dictionary<Vector3d, SegmentVtx>();

            // find intersection segments
            // TODO: intersection polygons
            // TODO: do we need to care about intersection vertices?
            DMeshAABBTree3 targetSpatial = new DMeshAABBTree3(Target, true);
            DMeshAABBTree3 cutSpatial = new DMeshAABBTree3(CutMesh, true);
            var intersections = targetSpatial.FindAllIntersections(cutSpatial);

            // for each segment, for each vtx, determine if it is 
            // at an existing vertex, on-edge, or in-face
            Segments = new IntersectSegment[intersections.Segments.Count];
            for ( int i = 0; i < Segments.Length; ++i ) {
                var isect = intersections.Segments[i];
                Vector3dTuple2 points = new Vector3dTuple2(isect.point0, isect.point1);
                IntersectSegment iseg = new IntersectSegment() {
                    base_tid = isect.t0
                };
                Segments[i] = iseg;
                for (int j = 0; j < 2; ++j) {
                    Vector3d v = points[j];

                    // if this exact vtx coord has been seen, use same vtx
                    SegmentVtx sv;
                    if (SegVtxMap.TryGetValue(v, out sv)) {
                        iseg[j] = sv;
                        continue;
                    }
                    sv = new SegmentVtx() { v = v };
                    SegVertices.Add(sv);
                    SegVtxMap[v] = sv;
                    iseg[j] = sv;

                    // this vtx is tol-equal to input mesh vtx
                    int existing_v = find_existing_vertex(isect.point0);
                    if (existing_v >= 0) {
                        sv.initial_type = sv.type = 0;
                        sv.elem_id = existing_v;
                        sv.vtx_id = existing_v;
                        VIDToSegVtxMap[sv.vtx_id] = sv;
                        continue;
                    }

                    Triangle3d tri = new Triangle3d();
                    Target.GetTriVertices(isect.t0, ref tri.V0, ref tri.V1, ref tri.V2);
                    Index3i tv = Target.GetTriangle(isect.t0);

                    // this vtx is tol-on input mesh edge
                    int on_edge_i = on_edge(ref tri, ref v);
                    if ( on_edge_i >= 0 ) {
                        sv.initial_type = sv.type = 1;
                        sv.elem_id = Target.FindEdge(tv[on_edge_i], tv[(on_edge_i+1)%3]);
                        Util.gDevAssert(sv.elem_id != DMesh3.InvalidID);
                        add_edge_vtx(sv.elem_id, sv);
                        continue;
                    }

                    // otherwise contained in input mesh face
                    sv.initial_type = sv.type = 2;
                    sv.elem_id = isect.t0;
                    add_face_vtx(sv.elem_id, sv);
                }

            }

        }




        /// <summary>
        /// For each on-face vtx, we poke the face, and re-sort 
        /// the remaining vertices on that face onto new faces/edges
        /// </summary>
        void insert_face_vertices()
        {
            while ( FaceVertices.Count > 0 ) {
                var pair = FaceVertices.First();
                int tid = pair.Key;
                List<SegmentVtx> triVerts = pair.Value;
                SegmentVtx v = triVerts[triVerts.Count-1];
                triVerts.RemoveAt(triVerts.Count-1);

                DMesh3.PokeTriangleInfo pokeInfo;
                MeshResult result = Target.PokeTriangle(tid, out pokeInfo);
                if (result != MeshResult.Ok)
                    throw new Exception("shit");
                int new_v = pokeInfo.new_vid;

                Target.SetVertex(new_v, v.v);
                v.vtx_id = new_v;
                VIDToSegVtxMap[v.vtx_id] = v;
                PointHash.InsertPoint(v.vtx_id, v.v);

                // remove this triangles vtx list because it is no longer valid
                FaceVertices.Remove(tid);

                // update remaining verts
                Index3i pokeEdges = pokeInfo.new_edges;
                Index3i pokeTris = new Index3i(tid, pokeInfo.new_t1, pokeInfo.new_t2);
                foreach ( SegmentVtx sv in triVerts ) {
                    update_from_poke(sv, pokeEdges, pokeTris);
                    if (sv.type == 1)
                        add_edge_vtx(sv.elem_id, sv);
                    else if (sv.type == 2)
                        add_face_vtx(sv.elem_id, sv);
                }

                // track poke subfaces
                add_poke_subfaces(tid, ref pokeInfo);
            }
        }



        /// <summary>
        /// figure out which vtx/edge/face the input vtx is on
        /// </summary>
        void update_from_poke(SegmentVtx sv, Index3i pokeEdges, Index3i pokeTris)
        {
            // check if within tolerance of existing vtx, because we did not 
            // sort that out before...
            int existing_v = find_existing_vertex(sv.v);
            if (existing_v >= 0) {
                sv.type = 0;
                sv.elem_id = existing_v;
                sv.vtx_id = existing_v;
                VIDToSegVtxMap[sv.vtx_id] = sv;
                return;
            }

            for ( int j = 0; j < 3; ++j ) {
                if ( is_on_edge(pokeEdges[j], sv.v) ) {
                    sv.type = 1;
                    sv.elem_id = pokeEdges[j];
                    return;
                }
            }

            // [TODO] should use PrimalQuery2d for this!
            for ( int j = 0; j < 3; ++j ) {
                if ( is_in_triangle(pokeTris[j], sv.v) ) {
                    sv.type = 2;
                    sv.elem_id = pokeTris[j];
                    return;
                }
            }

            System.Console.WriteLine("unsorted vertex!");
            sv.elem_id = pokeTris.a;
        }




        /// <summary>
        /// for each on-edge vtx, we split the edge and then
        /// re-sort any of the vertices on that edge onto new edges
        /// </summary>
        void insert_edge_vertices()
        {
            while (EdgeVertices.Count > 0) {
                var pair = EdgeVertices.First();
                int eid = pair.Key;
                List<SegmentVtx> edgeVerts = pair.Value;
                SegmentVtx v = edgeVerts[edgeVerts.Count - 1];
                edgeVerts.RemoveAt(edgeVerts.Count - 1);

                Index2i splitTris = Target.GetEdgeT(eid);

                DMesh3.EdgeSplitInfo splitInfo;
                MeshResult result = Target.SplitEdge(eid, out splitInfo);
                if (result != MeshResult.Ok)
                    throw new Exception("insert_edge_vertices: split failed!");
                int new_v = splitInfo.vNew;
                Index2i splitEdges = new Index2i(eid, splitInfo.eNewBN);

                Target.SetVertex(new_v, v.v);
                v.vtx_id = new_v;
                VIDToSegVtxMap[v.vtx_id] = v;
                PointHash.InsertPoint(v.vtx_id, v.v);

                // remove this triangles vtx list because it is no longer valid
                EdgeVertices.Remove(eid);

                // update remaining verts
                foreach (SegmentVtx sv in edgeVerts) {
                    update_from_split(sv, splitEdges);
                    if (sv.type == 1)
                        add_edge_vtx(sv.elem_id, sv);
                }

                // track subfaces
                add_split_subfaces(splitTris, ref splitInfo);

            }
        }



        /// <summary>
        /// figure out which vtx/edge the input vtx is on
        /// </summary>
        void update_from_split(SegmentVtx sv, Index2i splitEdges)
        {
            // check if within tolerance of existing vtx, because we did not 
            // sort that out before...
            int existing_v = find_existing_vertex(sv.v);
            if (existing_v >= 0) {
                sv.type = 0;
                sv.elem_id = existing_v;
                sv.vtx_id = existing_v;
                VIDToSegVtxMap[sv.vtx_id] = sv;
                return;
            }

            for (int j = 0; j < 2; ++j) {
                if (is_on_edge(splitEdges[j], sv.v)) {
                    sv.type = 1;
                    sv.elem_id = splitEdges[j];
                    return;
                }
            }

            throw new Exception("update_from_split: unsortable vertex?");
        }







        /// <summary>
        /// Make sure that all intersection segments are represented by
        /// a connected chain of edges.
        /// </summary>
        void connect_edges()
        {
            int NS = Segments.Length;
            for ( int si = 0; si < NS; ++si ) {
                IntersectSegment seg = Segments[si];
                if (seg.v0 == seg.v1)
                    continue;       // degenerate!
                if (seg.v0.vtx_id == seg.v1.vtx_id)
                    continue;       // also degenerate and how does this happen?

                int a = seg.v0.vtx_id, b = seg.v1.vtx_id;

                if (a == DMesh3.InvalidID || b == DMesh3.InvalidID)
                    throw new Exception("segment vertex is not defined?");
                int eid = Target.FindEdge(a, b);
                if (eid != DMesh3.InvalidID)
                    continue;       // already connected

                // TODO: in many cases there is an edge we added during a
                // poke or split that we could flip to get edge AB. 
                // this is much faster and we should do it where possible!
                // HOWEVER we need to know which edges we can and cannot flip
                // is_inserted_free_edge() should do this but not implemented yet
                // possibly also requires that we do all these flips before any
                // calls to insert_segment() !

                try {
                    insert_segment(seg);
                } catch (Exception) {
                    // ignore?
                }
            }
        }


        void insert_segment(IntersectSegment seg)
        {
            List<int> subfaces = get_all_baseface_tris(seg.base_tid);

            RegionOperator op = new RegionOperator(Target, subfaces);

            Vector3d n = BaseFaceNormals[seg.base_tid];
            Vector3d c = BaseFaceCentroids[seg.base_tid];
            Vector3d e0, e1;
            Vector3d.MakePerpVectors(ref n, out e0, out e1);

            DMesh3 mesh = op.Region.SubMesh;
            MeshTransforms.PerVertexTransform(mesh, (v) => {
                v -= c;
                return new Vector3d(v.Dot(e0), v.Dot(e1), 0);
            });

            Vector3d end0 = seg.v0.v, end1 = seg.v1.v;
            end0 -= c; end1 -= c;
            Vector2d p0 = new Vector2d(end0.Dot(e0), end0.Dot(e1));
            Vector2d p1 = new Vector2d(end1.Dot(e0), end1.Dot(e1));
            PolyLine2d path = new PolyLine2d();
            path.AppendVertex(p0); path.AppendVertex(p1);

            MeshInsertUVPolyCurve insert = new MeshInsertUVPolyCurve(mesh, path);
            insert.Apply();

            MeshVertexSelection cutVerts = new MeshVertexSelection(mesh);
            cutVerts.SelectEdgeVertices(insert.OnCutEdges);

            MeshTransforms.PerVertexTransform(mesh, (v) => {
                return c + v.x * e0 + v.y * e1;
            });

            op.BackPropropagate();

            // add new cut vertices to cut list
            foreach (int vid in cutVerts)
                SegmentInsertVertices.Add(op.ReinsertSubToBaseMapV[vid]);

            add_regionop_subfaces(seg.base_tid, op);
        }





        void add_edge_vtx(int eid, SegmentVtx vtx)
        {
            List<SegmentVtx> l;
            if (EdgeVertices.TryGetValue(eid, out l)) {
                l.Add(vtx);
            } else {
                l = new List<SegmentVtx>() { vtx };
                EdgeVertices[eid] = l;
            }
        }

        void add_face_vtx(int tid, SegmentVtx vtx)
        {
            List<SegmentVtx> l;
            if (FaceVertices.TryGetValue(tid, out l)) {
                l.Add(vtx);
            } else {
                l = new List<SegmentVtx>() { vtx };
                FaceVertices[tid] = l;
            }
        }



        void add_poke_subfaces(int tid, ref DMesh3.PokeTriangleInfo pokeInfo)
        {
            int parent = get_parent(tid);
            HashSet<int> subfaces = get_subfaces(parent);
            if (tid != parent)
                add_subface(subfaces, parent, tid);
            add_subface(subfaces, parent, pokeInfo.new_t1);
            add_subface(subfaces, parent, pokeInfo.new_t2);
        }
        void add_split_subfaces(Index2i origTris, ref DMesh3.EdgeSplitInfo splitInfo)
        {
            int parent_1 = get_parent(origTris.a);
            HashSet<int> subfaces_1 = get_subfaces(parent_1);
            if (origTris.a != parent_1)
                add_subface(subfaces_1, parent_1, origTris.a);
            add_subface(subfaces_1, parent_1, splitInfo.eNewT2);

            if ( origTris.b != DMesh3.InvalidID ) {
                int parent_2 = get_parent(origTris.b);
                HashSet<int> subfaces_2 = get_subfaces(parent_2);
                if (origTris.b != parent_2)
                    add_subface(subfaces_2, parent_2, origTris.b);
                add_subface(subfaces_2, parent_2, splitInfo.eNewT3);
            }
        }
        void add_regionop_subfaces(int parent, RegionOperator op)
        {
            HashSet<int> subfaces = get_subfaces(parent);
            foreach (int tid in op.CurrentBaseTriangles) {
                if (tid != parent)
                    add_subface(subfaces, parent, tid);
            }
        }


        int get_parent(int tid)
        {
            int parent;
            if (ParentFaces.TryGetValue(tid, out parent) == false)
                parent = tid;
            return parent;
        }
        HashSet<int> get_subfaces(int parent)
        {
            HashSet<int> subfaces;
            if (SubFaces.TryGetValue(parent, out subfaces) == false) {
                subfaces = new HashSet<int>();
                SubFaces[parent] = subfaces;
            }
            return subfaces;
        }
        void add_subface(HashSet<int> subfaces, int parent, int tid)
        {
            subfaces.Add(tid);
            ParentFaces[tid] = parent;
        }
        List<int> get_all_baseface_tris(int base_tid)
        {
            List<int> faces = new List<int>(get_subfaces(base_tid));
            faces.Add(base_tid);
            return faces;
        }

        bool is_inserted_free_edge(int eid)
        {
            Index2i et = Target.GetEdgeT(eid);
            if (get_parent(et.a) != get_parent(et.b))
                return false;
            // TODO need to check if we need to save edge AB to connect vertices!
            throw new Exception("not done yet!");
            return true;
        }




        protected int on_edge(ref Triangle3d tri, ref Vector3d v)
        {
            Segment3d s01 = new Segment3d(tri.V0, tri.V1);
            if (s01.DistanceSquared(v) < VertexSnapTol * VertexSnapTol)
                return 0;
            Segment3d s12 = new Segment3d(tri.V1, tri.V2);
            if (s12.DistanceSquared(v) < VertexSnapTol * VertexSnapTol)
                return 1;
            Segment3d s20 = new Segment3d(tri.V2, tri.V0);
            if (s20.DistanceSquared(v) < VertexSnapTol * VertexSnapTol)
                return 2;
            return -1;
        }
        protected int on_edge_eid(int tid, Vector3d v)
        {
            Index3i tv = Target.GetTriangle(tid);
            Triangle3d tri = new Triangle3d();
            Target.GetTriVertices(tid, ref tri.V0, ref tri.V1, ref tri.V2);
            int eidx = on_edge(ref tri, ref v);
            if (eidx < 0)
                return DMesh3.InvalidID;
            int eid = Target.FindEdge(tv[eidx], tv[(eidx+1)%3]);
            Util.gDevAssert(eid != DMesh3.InvalidID);
            return eid;            
        }
        protected bool is_on_edge(int eid, Vector3d v)
        {
            Index2i ev = Target.GetEdgeV(eid);
            Segment3d seg = new Segment3d(Target.GetVertex(ev.a), Target.GetVertex(ev.b));
            return seg.DistanceSquared(v) < VertexSnapTol * VertexSnapTol;
        }

        protected bool is_in_triangle(int tid, Vector3d v)
        {
            Triangle3d tri = new Triangle3d();
            Target.GetTriVertices(tid, ref tri.V0, ref tri.V1, ref tri.V2);
            Vector3d bary = tri.BarycentricCoords(v);
            return (bary.x >= 0 && bary.y >= 0 && bary.z >= 0
                  && bary.x < 1 && bary.y <= 1 && bary.z <= 1);
                
        }



        /// <summary>
        /// find existing vertex at point, if it exists
        /// </summary>
        protected int find_existing_vertex(Vector3d pt)
        {
            return find_nearest_vertex(pt, VertexSnapTol);
        }
        /// <summary>
        /// find closest vertex, within searchRadius
        /// </summary>
        protected int find_nearest_vertex(Vector3d pt, double searchRadius, int ignore_vid = -1)
        {
            KeyValuePair<int, double> found = (ignore_vid == -1) ?
                PointHash.FindNearestInRadius(pt, searchRadius,
                            (b) => { return pt.DistanceSquared(Target.GetVertex(b)); })
                            :
                PointHash.FindNearestInRadius(pt, searchRadius,
                            (b) => { return pt.DistanceSquared(Target.GetVertex(b)); },
                            (vid) => { return vid == ignore_vid; });
            if (found.Key == PointHash.InvalidValue)
                return -1;
            return found.Key;
        }



    }
}
