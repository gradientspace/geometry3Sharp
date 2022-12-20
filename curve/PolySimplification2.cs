using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    /// <summary>
    /// 2D Polyline/Polygon simplification.
    /// 
    /// This is a more complex approach than Polygon.Simplify(), which uses sequential vtx clustering
    /// and then runs douglas-peucker algorithm. That method can end up modifying long straight segments,
    /// which is not ideal in many contexts (eg manufacturing).
    /// 
    /// Strategy here is :
    ///   1) find runs of vertices that are very close to straight lines (default 0.01mm deviation tol)
    ///   2) find all straight segments longer than threshold distance (default 2mm)
    ///   3) discard vertices that deviate less than tolerance (default = 0.2mm)
    ///      from sequential-points-segment, unless they are required to preserve
    ///      straight segments
    ///      
    /// [TODO] currently doing greedy search in 1,3. Could do more optimal search.
    /// [TODO] currently measuring deviation of p1...pN-1 from line [p0,pN] for points [p0,p1,...pN].
    ///   could alternately fit best segment to p1...pN (p0 is already fixed).
    /// [TODO] 2d variant of variational shape segmentation?
    /// 
    /// </summary>
    public class PolySimplification2
    {
        List<Vector2d> Vertices;
        bool IsLoop;

        /// <summary>
        /// A series of points that deviates less than this distance from
        /// a line segment are considered 'on' that line
        /// </summary>
        public double StraightLineDeviationThreshold = 0.01;

        /// <summary>
        /// After collapsing straight lines, any segment longer than
        /// this distance is explicitly preserved
        /// </summary>
        public double PreserveStraightSegLen = 2.0f;

        /// <summary>
        /// we skip vertices that deviate less than this distance from
        /// the currently-accumulated line segment
        /// </summary>
        public double SimplifyDeviationThreshold = 0.2;

        public List<Vector2d> Result;


        public PolySimplification2(Polygon2d polygon)
        {
            Vertices = new List<Vector2d>(polygon.Vertices);
            IsLoop = true;
        }

        public PolySimplification2(PolyLine2d polycurve)
        {
            Vertices = new List<Vector2d>(polycurve.Vertices);
            IsLoop = false;
        }



        /// <summary>
        /// simplify outer and holes of a polygon solid with same thresholds
        /// </summary>
        public static void Simplify(GeneralPolygon2d solid, double deviationThresh)
        {
            PolySimplification2 simp = new PolySimplification2(solid.Outer);
            simp.SimplifyDeviationThreshold = deviationThresh;
            simp.Simplify();
            solid.Outer.SetVertices(simp.Result, true);

            foreach (var hole in solid.Holes) {
                PolySimplification2 holesimp = new PolySimplification2(hole);
                holesimp.SimplifyDeviationThreshold = deviationThresh;
                holesimp.Simplify();
                hole.SetVertices(holesimp.Result, true);
            }
        }



        public void Simplify()
        {
            bool[] keep_seg = new bool[Vertices.Count];
            Array.Clear(keep_seg, 0, keep_seg.Length);

            // collapse straight lines
            List<Vector2d> linear = collapse_by_deviation_tol(Vertices, keep_seg, StraightLineDeviationThreshold);

            find_constrained_segments(linear, keep_seg);

            Result = collapse_by_deviation_tol(linear, keep_seg, SimplifyDeviationThreshold);
        }



        void find_constrained_segments(List<Vector2d> vertices, bool[] markers)
        {
            int N = vertices.Count;
            int NStop = (IsLoop) ? vertices.Count : vertices.Count - 1;
            for (int si = 0; si < NStop; si++) {
                int i0 = si, i1 = (si + 1) % N;
                if (vertices[i0].DistanceSquared(vertices[i1]) > PreserveStraightSegLen)
                    markers[i0] = true;
            }

        }



        List<Vector2d> collapse_by_deviation_tol(List<Vector2d> input, bool[] keep_segments, double offset_threshold)
        {
            int N = input.Count;
            int NStop = (IsLoop) ? input.Count : input.Count-1;

            List<Vector2d> result = new List<Vector2d>();
            result.Add(input[0]);

            double thresh_sqr = offset_threshold * offset_threshold;

            int last_i = 0;
            int cur_i = 1;
            int skip_count = 0;

            if (keep_segments[0]) {         // if first segment is constrained
                result.Add(input[1]);
                last_i = 1; cur_i = 2;
            }

            while ( cur_i < NStop) { 
                int i0 = cur_i, i1 = (cur_i + 1) % N;

                if ( keep_segments[i0] ) {
                    if (last_i != i0) {
                        // skip join segment if it is degenerate
                        double join_dist = input[i0].Distance(result[result.Count - 1]);
                        if ( join_dist > MathUtil.Epsilon)
                            result.Add(input[i0]);
                    }
                    result.Add(input[i1]);
                    last_i = i1;
                    skip_count = 0;
                    if (i1 == 0) {
                        cur_i = NStop;
                    } else {
                        cur_i = i1;
                    }
                    continue;
                }

                Vector2d dir = input[i1] - input[last_i];
                Line2d accum_line = new Line2d(input[last_i], dir.Normalized);

                // find deviation of vertices between last_i and next
                double max_dev_sqr = 0;
                for (int k = last_i + 1; k <= cur_i; k++) {
                    double distSqr = accum_line.DistanceSquared(input[k]);
                    if (distSqr > max_dev_sqr)
                        max_dev_sqr = distSqr;
                }

                // if we deviated too much, we keep this first vertex
                if ( max_dev_sqr > thresh_sqr) {
                    result.Add(input[cur_i]);
                    last_i = cur_i;
                    cur_i++;
                    skip_count = 0;
                } else {
                    // skip this vertex
                    cur_i++;
                    skip_count++;
                }
            }

            
            if ( IsLoop ) {
                // if we skipped everything, rest of code doesn't work
                if (result.Count < 3)
                    return handle_tiny_case(result, input, keep_segments, offset_threshold);

                Line2d last_line = Line2d.FromPoints(input[last_i], input[cur_i % N]);
                bool collinear_startv = last_line.DistanceSquared(result[0]) < thresh_sqr;
                bool collinear_starts = last_line.DistanceSquared(result[1]) < thresh_sqr;
                if (collinear_startv && collinear_starts && result.Count > 3) {
                    // last seg is collinear w/ start seg, merge them
                    result[0] = input[last_i];
                    result.RemoveAt(result.Count - 1);

                } else if (collinear_startv) {
                    // skip last vertex

                } else {
                    result.Add(input[input.Count - 1]);
                }

            } else {
                // in polyline we always add last vertex
                result.Add(input[input.Count - 1]);
            }

            return result;
        }



        List<Vector2d> handle_tiny_case(List<Vector2d> result, List<Vector2d> input, bool[] keep_segments, double offset_threshold)
        {
            int N = input.Count;
            if (N == 3)
                return input;       // not much we can really do here...

            result.Clear();
            result.Add(input[0]);
            result.Add(input[N/3]);
            result.Add(input[N-N/3]);
            return result;
        }


    }
}
