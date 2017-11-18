using System;
using System.Collections.Generic;
using System.Linq;

namespace g3
{
    /// <summary>
    /// Arrangement2d constructs a planar arrangement of a set of 2D line segments.
    /// When a segment is inserted, existing edges are split, and the inserted
    /// segment becomes multiple graph edges. So, the resulting DGraph2 should
    /// not have any edges that intersect.
    /// 
    /// Calculations are performed in double-precision, so there is no guarantee
    /// of correctness. 
    /// 
    /// 
    /// [TODO] multi-level segment has to accelerate find_intersecting_edges()
    /// [TODO] maybe smarter handling
    /// 
    /// </summary>
    public class Arrangement2d
    {
        // graph of arrangement
        public DGraph2 Graph;

        // pointhash for vertices of graph
        public PointHashGrid2d<int> PointHash;

        // points within this tolerance are merged
        public double VertexSnapTol = 0.00001;


        public Arrangement2d(AxisAlignedBox2d boundsHint)
        {
            Graph = new DGraph2();

            double cellSize = boundsHint.MaxDim / 64;
            PointHash = new PointHashGrid2d<int>(cellSize, -1);
        }



        /// <summary>
        /// insert segment [a,b] into the arrangement
        /// </summary>
        public void Insert(Vector2d a, Vector2d b, int gid = -1)
        {
            insert_segment(a, b, gid);
        }

        /// <summary>
        /// insert segment into the arrangement
        /// </summary>
        public void Insert(Segment2d segment, int gid = -1)
        {
            insert_segment(segment.P0, segment.P1, gid);
        }

        /// <summary>
        /// sequentially insert segments of polyline
        /// </summary>
        public void Insert(PolyLine2d pline, int gid = -1)
        {
            int N = pline.VertexCount - 1;
            for (int i = 0; i < N; ++i) {
                Vector2d a = pline[i];
                Vector2d b = pline[i + 1];
                insert_segment(a, b, gid);
            }
        }

        /// <summary>
        /// sequentially insert segments of polygon
        /// </summary>
        public void Insert(Polygon2d poly, int gid = -1)
        {
            int N = poly.VertexCount;
            for (int i = 0; i < N; ++i) {
                Vector2d a = poly[i];
                Vector2d b = poly[(i + 1) % N];
                insert_segment(a, b, gid);
            }
        }



        /*
         *  Graph improvement
         */



        /// <summary>
        /// connect open boundary vertices within distThresh, by inserting new segments
        /// </summary>
        public void ConnectOpenBoundaries(double distThresh)
        {
            int max_vid = Graph.MaxVertexID;
            for (int vid = 0; vid < max_vid; ++vid) {
                if (Graph.IsBoundaryVertex(vid) == false)
                    continue;

                Vector2d v = Graph.GetVertex(vid);
                int snap_with = find_nearest_boundary_vertex(v, distThresh, vid);
                if (snap_with != -1) {
                    Vector2d v2 = Graph.GetVertex(snap_with);
                    Insert(v, v2);
                }
            }
        }







        protected struct SegmentPoint
        {
            public double t;
            public int vid;
        }

        /// <summary>
        /// insert edge [a,b] into the arrangement, splitting existing edges as necessary
        /// </summary>
        protected bool insert_segment(ref Vector2d a, ref Vector2d b, int gid = -1, double tol = 0)
        {
            // handle degenerate edges
            int a_idx = find_existing_vertex(a);
            int b_idx = find_existing_vertex(b);
            if (a_idx == b_idx && a_idx >= 0)
                return false;

            // ok find all intersections
            List<Intersection> hits = new List<Intersection>();
            find_intersecting_edges(ref a, ref b, hits, tol);
            int N = hits.Count;

            // we are going to construct a list of <t,vertex_id> values along segment AB
            List<SegmentPoint> points = new List<SegmentPoint>();
            Segment2d segAB = new Segment2d(a, b);

            // insert intersections into existing segments
            for ( int i = 0; i < N; ++i ) {
                Intersection intr = hits[i];
                int eid = intr.eid;
                double t0 = intr.intr.Parameter0, t1 = intr.intr.Parameter1;

                // insert first point at t0
                int new_eid = -1;
                if ( intr.intr.Type == IntersectionType.Point  || intr.intr.Type == IntersectionType.Segment) {
                    Index2i new_info = split_segment_at_t(eid, t0, VertexSnapTol);
                    new_eid = new_info.b;
                    Vector2d v = Graph.GetVertex(new_info.a);
                    points.Add(new SegmentPoint() { t = segAB.Project(v), vid = new_info.a });
                }

                // if intersection was on-segment, then we have a second point at t1
                if ( intr.intr.Type == IntersectionType.Segment ) {
                    if (new_eid == -1) {
                        // did not actually split edge for t0, so we can still use eid
                        Index2i new_info = split_segment_at_t(eid, t1, VertexSnapTol);
                        Vector2d v = Graph.GetVertex(new_info.a);
                        points.Add(new SegmentPoint() { t = segAB.Project(v), vid = new_info.a });

                    } else {
                        // find t1 was in eid, rebuild in new_eid
                        Segment2d new_seg = Graph.GetEdgeSegment(new_eid);
                        Vector2d p1 = intr.intr.Segment1.PointAt(t1);
                        double new_t1 = new_seg.Project(p1);
                        Util.gDevAssert(new_t1 <= Math.Abs(new_seg.Extent));

                        Index2i new_info = split_segment_at_t(new_eid, new_t1, VertexSnapTol);
                        Vector2d v = Graph.GetVertex(new_info.a);
                        points.Add(new SegmentPoint() { t = segAB.Project(v), vid = new_info.a });
                    }

                }

            }

            // find or create start and end points
            if (a_idx == -1)
                a_idx = find_existing_vertex(a);
            if (a_idx == -1) {
                a_idx = Graph.AppendVertex(a);
                PointHash.InsertPointUnsafe(a_idx, a);
            }
            if (b_idx == -1)
                b_idx = find_existing_vertex(b);
            if (b_idx == -1) {
                b_idx = Graph.AppendVertex(b);
                PointHash.InsertPointUnsafe(b_idx, b);
            }

            // add start/end to points list. These may be duplicates but we will sort that out after
            points.Add(new SegmentPoint() { t = segAB.Project(a), vid = a_idx });
            points.Add(new SegmentPoint() { t = segAB.Project(b), vid = b_idx });
            // sort by t
            points.Sort((pa, pb) => { return (pa.t < pb.t) ? -1 : ((pa.t > pb.t) ? 1 : 0); });

            // connect sequential points, as long as they aren't the same point,
            // and the segment doesn't already exist
            for (int k = 0; k < points.Count-1; ++k ) {
                int v0 = points[k].vid;
                int v1 = points[k + 1].vid;
                if (v0 == v1)
                    continue;
                if (Math.Abs(points[k].t - points[k + 1].t) < MathUtil.Epsilonf)
                    System.Console.WriteLine("insert_segment: different points with same t??");

                if (Graph.FindEdge(v0, v1) == DGraph2.InvalidID)
                    Graph.AppendEdge(v0, v1, gid);
            }

            return true;
        }
        protected bool insert_segment(Vector2d a, Vector2d b, int gid = -1, double tol = 0)
        {
            return insert_segment(ref a, ref b, gid, tol);
        }



        /// <summary>
        /// insert new point into segment eid at parameter value t
        /// If t is within tol of endpoint of segment, we use that instead.
        /// </summary>
        protected Index2i split_segment_at_t(int eid, double t, double tol)
        {
            Index2i ev = Graph.GetEdgeV(eid);
            Segment2d seg = new Segment2d(Graph.GetVertex(ev.a), Graph.GetVertex(ev.b));

            int use_vid = -1;
            int new_eid = -1;
            if (t < -(seg.Extent - tol)) {
                use_vid = ev.a;
            } else if (t > (seg.Extent - tol)) {
                use_vid = ev.b;
            } else {
                DGraph2.EdgeSplitInfo splitInfo;
                MeshResult result = Graph.SplitEdge(eid, out splitInfo);
                if (result != MeshResult.Ok)
                    throw new Exception("insert_into_segment: edge split failed?");
                use_vid = splitInfo.vNew;
                new_eid = splitInfo.eNewBN;
                Vector2d pt = seg.PointAt(t);
                Graph.SetVertex(use_vid, pt);
                PointHash.InsertPointUnsafe(splitInfo.vNew, pt);
            }
            return new Index2i(use_vid, new_eid);
        }


        /// <summary>
        /// find existing vertex at point, if it exists
        /// </summary>
        protected int find_existing_vertex(Vector2d pt)
        {
            return find_nearest_vertex(pt, VertexSnapTol);
        }
        /// <summary>
        /// find closest vertex, within searchRadius
        /// </summary>
        protected int find_nearest_vertex(Vector2d pt, double searchRadius, int ignore_vid = -1)
        {
            KeyValuePair<int, double> found = (ignore_vid == -1) ?
                PointHash.FindNearestInRadius(pt, searchRadius,
                            (b) => { return pt.DistanceSquared(Graph.GetVertex(b)); })
                            :
                PointHash.FindNearestInRadius(pt, searchRadius,
                            (b) => { return pt.DistanceSquared(Graph.GetVertex(b)); },
                            (vid) => { return vid == ignore_vid; });
            if (found.Key == PointHash.InvalidValue)
                return -1;
            return found.Key;
        }


        /// <summary>
        /// find nearest boundary vertex, within searchRadius
        /// </summary>
        protected int find_nearest_boundary_vertex(Vector2d pt, double searchRadius, int ignore_vid = -1)
        {
            KeyValuePair<int, double> found = 
                PointHash.FindNearestInRadius(pt, searchRadius,
                            (b) => { return pt.Distance(Graph.GetVertex(b)); },
                            (vid) => { return Graph.IsBoundaryVertex(vid) == false || vid == ignore_vid; });
            if (found.Key == PointHash.InvalidValue)
                return -1;
            return found.Key;
        }



        protected struct Intersection
        {
            public int eid;
            public int sidex;
            public int sidey;
            public IntrSegment2Segment2 intr;
        }

        /// <summary>
        /// find set of edges in graph that intersect with edge [a,b]
        /// </summary>
        protected bool find_intersecting_edges(ref Vector2d a, ref Vector2d b, List<Intersection> hits, double tol = 0)
        {
            int num_hits = 0;
            Vector2d x = Vector2d.Zero, y = Vector2d.Zero;
            foreach ( int eid in Graph.EdgeIndices() ) {
                Graph.GetEdgeV(eid, ref x, ref y);
                int sidex = Segment2d.WhichSide(ref a, ref b, ref x, tol);
                int sidey = Segment2d.WhichSide(ref a, ref b, ref y, tol);
                if (sidex == sidey && sidex != 0)
                    continue; // both pts on same side

                IntrSegment2Segment2 intr = new IntrSegment2Segment2(new Segment2d(x, y), new Segment2d(a, b));
                if ( intr.Find() ) {
                    hits.Add(new Intersection() {
                        eid = eid, sidex = sidex, sidey = sidey, intr = intr
                    });
                    num_hits++;
                }
            }
            return (num_hits > 0);
        }




    }
}
