// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Distributed under the Boost Software License, Version 1.0. http://www.boost.org/LICENSE_1_0.txt
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using g3;

namespace gs
{
    /// <summary>
    /// Construct a "minimal" fill surface for the hole. This surface
    /// is often quasi-developable, reconstructs sharp edges, etc. 
    /// There are various options.
    /// </summary>
    public class MinimalHoleFill
    {
        public DMesh3 Mesh;
        public EdgeLoop FillLoop;

        public bool IgnoreBoundaryTriangles = false;
        public bool OptimizeDevelopability = true;
        public bool OptimizeTriangles = true;
        public double DevelopabilityTolerance = 0.0001;

        /*
         *  Outputs
         */

        /// <summary> Final fill vertices (should be empty?)</summary>
        public int[] FillVertices;

        /// <summary> Final fill triangles </summary>
        public int[] FillTriangles;


        public MinimalHoleFill(DMesh3 mesh, EdgeLoop fillLoop)
        {
            this.Mesh = mesh;
            this.FillLoop = fillLoop;
        }


        RegionOperator regionop;
        DMesh3 fillmesh;
        HashSet<int> boundaryv;
        Dictionary<int, double> exterior_angle_sums;

        double[] curvatures;

        public bool Apply()
        {
            // do a simple fill
            SimpleHoleFiller simplefill = new SimpleHoleFiller(Mesh, FillLoop);
            int fill_gid = Mesh.AllocateTriangleGroup();
            bool bOK = simplefill.Fill(fill_gid);
			if (bOK == false)
				return false;

            if (FillLoop.Vertices.Length <= 3) {
                FillTriangles = simplefill.NewTriangles;
                FillVertices = new int[0];
                return true;
            }

            // extract the simple fill mesh as a submesh, via RegionOperator, so we can backsub later
            HashSet<int> intial_fill_tris = new HashSet<int>(simplefill.NewTriangles);
            regionop = new RegionOperator(Mesh, simplefill.NewTriangles,
                (submesh) => { submesh.ComputeTriMaps = true; });
            fillmesh = regionop.Region.SubMesh;

            // for each boundary vertex, compute the exterior angle sum
            // we will use this to compute gaussian curvature later
            boundaryv = new HashSet<int>(MeshIterators.BoundaryEdgeVertices(fillmesh));
            exterior_angle_sums = new Dictionary<int, double>();
            if (IgnoreBoundaryTriangles == false) {
                foreach (int sub_vid in boundaryv) {
                    double angle_sum = 0;
                    int base_vid = regionop.Region.MapVertexToBaseMesh(sub_vid);
                    foreach (int tid in regionop.BaseMesh.VtxTrianglesItr(base_vid)) {
                        if (intial_fill_tris.Contains(tid) == false) {
                            Index3i et = regionop.BaseMesh.GetTriangle(tid);
                            int idx = IndexUtil.find_tri_index(base_vid, ref et);
                            angle_sum += regionop.BaseMesh.GetTriInternalAngleR(tid, idx);
                        }
                    }
                    exterior_angle_sums[sub_vid] = angle_sum;
                }
            }


            // try to guess a reasonable edge length that will give us enough geometry to work with in simplify pass
            double loop_mine, loop_maxe, loop_avge, fill_mine, fill_maxe, fill_avge;
            MeshQueries.EdgeLengthStatsFromEdges(Mesh, FillLoop.Edges, out loop_mine, out loop_maxe, out loop_avge);
            MeshQueries.EdgeLengthStats(fillmesh, out fill_mine, out fill_maxe, out fill_avge);
            double remesh_target_len = loop_avge;
            if (fill_maxe / remesh_target_len > 10)
                remesh_target_len = fill_maxe / 10;
            //double remesh_target_len = Math.Min(loop_avge, fill_avge / 4);

            // remesh up to target edge length, ideally gives us some triangles to work with
            RemesherPro remesh1 = new RemesherPro(fillmesh);
            remesh1.SmoothSpeedT = 1.0;
            MeshConstraintUtil.FixAllBoundaryEdges(remesh1);
            //remesh1.SetTargetEdgeLength(remesh_target_len / 2);       // would this speed things up? on large regions?
            //remesh1.FastestRemesh();
            remesh1.SetTargetEdgeLength(remesh_target_len);
            remesh1.FastestRemesh();

            /*
             * first round: collapse to minimal mesh, while flipping to try to 
             * get to ballpark minimal mesh. We stop these passes as soon as
             * we have done two rounds where we couldn't do another collapse
             * 
             * This is the most unstable part of the algorithm because there
             * are strong ordering effects. maybe we could sort the edges somehow??
             */

            int zero_collapse_passes = 0;
            int collapse_passes = 0;
            while (collapse_passes++ < 20 && zero_collapse_passes < 2) {

                // collapse pass
                int NE = fillmesh.MaxEdgeID;
                int collapses = 0;
                for (int ei = 0; ei < NE; ++ei) {
                    if (fillmesh.IsEdge(ei) == false || fillmesh.IsBoundaryEdge(ei))
                        continue;
                    Index2i ev = fillmesh.GetEdgeV(ei);
                    bool a_bdry = boundaryv.Contains(ev.a), b_bdry = boundaryv.Contains(ev.b);
                    if (a_bdry && b_bdry)
                        continue;
                    int keepv = (a_bdry) ? ev.a : ev.b;
                    int otherv = (keepv == ev.a) ? ev.b : ev.a;
                    Vector3d newv = fillmesh.GetVertex(keepv);
                    if (MeshUtil.CheckIfCollapseCreatesFlip(fillmesh, ei, newv))
                        continue;
                    DMesh3.EdgeCollapseInfo info;
                    MeshResult result = fillmesh.CollapseEdge(keepv, otherv, out info);
                    if (result == MeshResult.Ok)
                        collapses++;
                }
                if (collapses == 0) zero_collapse_passes++; else zero_collapse_passes = 0;

                // flip pass. we flip in these cases:
                //  1) if angle between current triangles is too small (slightly more than 90 degrees, currently)
                //  2) if angle between flipped triangles is smaller than between current triangles
                //  3) if flipped edge length is shorter *and* such a flip won't flip the normal
                NE = fillmesh.MaxEdgeID;
                Vector3d n1, n2, on1, on2;
                for (int ei = 0; ei < NE; ++ei) {
                    if (fillmesh.IsEdge(ei) == false || fillmesh.IsBoundaryEdge(ei))
                        continue;
                    bool do_flip = false;

                    Index2i ev = fillmesh.GetEdgeV(ei);
                    MeshUtil.GetEdgeFlipNormals(fillmesh, ei, out n1, out n2, out on1, out on2);
                    double dot_cur = n1.Dot(n2);
                    double dot_flip = on1.Dot(on2);
                    if (n1.Dot(n2) < 0.1 || dot_flip > dot_cur+MathUtil.Epsilonf)
                        do_flip = true;

                    if (do_flip == false) {
                        Index2i otherv = fillmesh.GetEdgeOpposingV(ei);
                        double len_e = fillmesh.GetVertex(ev.a).Distance(fillmesh.GetVertex(ev.b));
                        double len_flip = fillmesh.GetVertex(otherv.a).Distance(fillmesh.GetVertex(otherv.b));
                        if (len_flip < len_e) {
                            if (MeshUtil.CheckIfEdgeFlipCreatesFlip(fillmesh, ei) == false)
                                do_flip = true;
                        }
                    }

                    if (do_flip) {
                        DMesh3.EdgeFlipInfo info;
                        MeshResult result = fillmesh.FlipEdge(ei, out info);
                    }
                }
            }

            // Sometimes, for some reason, we have a remaining interior vertex (have only ever seen one?) 
            // Try to force removal of such vertices, even if it makes ugly mesh
            remove_remaining_interior_verts();


            // enable/disable passes. 
            bool DO_FLATTER_PASS = true;
            bool DO_CURVATURE_PASS = OptimizeDevelopability && true;
            bool DO_AREA_PASS = OptimizeDevelopability && OptimizeTriangles && true;


            /*
             * In this pass we repeat the flipping iterations from the previous pass.
             * 
             * Note that because of the always-flip-if-dot-is-small case (commented),
             * this pass will frequently not converge, as some number of edges will
             * be able to flip back and forth (because neither has large enough dot).
             * This is not ideal, but also, if we remove this behavior, then we
             * generally get worse fills. This case basically introduces a sort of 
             * randomization factor that lets us escape local minima...
             * 
             */

            HashSet<int> remaining_edges = new HashSet<int>(fillmesh.EdgeIndices());
            HashSet<int> updated_edges = new HashSet<int>();

            int flatter_passes = 0;
            int zero_flips_passes = 0;
            while ( flatter_passes++ < 40 && zero_flips_passes < 2 && remaining_edges.Count() > 0 && DO_FLATTER_PASS) {
                zero_flips_passes++;
                foreach (int ei in remaining_edges) {
                    if (fillmesh.IsBoundaryEdge(ei))
                        continue;

                    bool do_flip = false;

                    Index2i ev = fillmesh.GetEdgeV(ei);
                    Vector3d n1, n2, on1, on2;
                    MeshUtil.GetEdgeFlipNormals(fillmesh, ei, out n1, out n2, out on1, out on2);
                    double dot_cur = n1.Dot(n2);
                    double dot_flip = on1.Dot(on2);
                    if (flatter_passes < 20 && dot_cur < 0.1)   // this check causes oscillatory behavior
                        do_flip = true;
                    if (dot_flip > dot_cur + MathUtil.Epsilonf)
                        do_flip = true;

                    if (do_flip) {
                        DMesh3.EdgeFlipInfo info;
                        MeshResult result = fillmesh.FlipEdge(ei, out info);
                        if (result == MeshResult.Ok) {
                            zero_flips_passes = 0;
                            add_all_edges(ei, updated_edges);
                        }
                    }
                }

                var tmp = remaining_edges;
                remaining_edges = updated_edges;
                updated_edges = tmp; updated_edges.Clear();
            }


            int curvature_passes = 0;
            if (DO_CURVATURE_PASS) {

                curvatures = new double[fillmesh.MaxVertexID];
                foreach (int vid in fillmesh.VertexIndices())
                    update_curvature(vid);

                remaining_edges = new HashSet<int>(fillmesh.EdgeIndices());
                updated_edges = new HashSet<int>();

                /*
                 *  In this pass we try to minimize gaussian curvature at all the vertices.
                 *  This will recover sharp edges, etc, and do lots of good stuff.
                 *  However, this pass will not make much progress if we are not already
                 *  relatively close to a minimal mesh, so it really relies on the previous
                 *  passes getting us in the ballpark.
                 */
                while (curvature_passes++ < 40 && remaining_edges.Count() > 0 && DO_CURVATURE_PASS) {
                    foreach (int ei in remaining_edges) {
                        if (fillmesh.IsBoundaryEdge(ei))
                            continue;

                        Index2i ev = fillmesh.GetEdgeV(ei);
                        Index2i ov = fillmesh.GetEdgeOpposingV(ei);

                        int find_other = fillmesh.FindEdge(ov.a, ov.b);
                        if (find_other != DMesh3.InvalidID)
                            continue;

                        double total_curv_cur = curvature_metric_cached(ev.a, ev.b, ov.a, ov.b);
                        if (total_curv_cur < MathUtil.ZeroTolerancef)
                            continue;

                        DMesh3.EdgeFlipInfo info;
                        MeshResult result = fillmesh.FlipEdge(ei, out info);
                        if (result != MeshResult.Ok)
                            continue;

                        double total_curv_flip = curvature_metric_eval(ev.a, ev.b, ov.a, ov.b);

                        bool keep_flip = total_curv_flip < total_curv_cur - MathUtil.ZeroTolerancef;
                        if (keep_flip == false) {
                            result = fillmesh.FlipEdge(ei, out info);
                        } else {
                            update_curvature(ev.a); update_curvature(ev.b);
                            update_curvature(ov.a); update_curvature(ov.b);
                            add_all_edges(ei, updated_edges);
                        }
                    }
                    var tmp = remaining_edges;
                    remaining_edges = updated_edges;
                    updated_edges = tmp; updated_edges.Clear();
                }
            }
            //System.Console.WriteLine("collapse {0}   flatter {1}   curvature {2}", collapse_passes, flatter_passes, curvature_passes);

            /*
             * In this final pass, we try to improve triangle quality. We flip if
             * the flipped triangles have better total aspect ratio, and the 
             * curvature doesn't change **too** much. The .DevelopabilityTolerance
             * parameter determines what is "too much" curvature change.
             */
            if (DO_AREA_PASS) {
                remaining_edges = new HashSet<int>(fillmesh.EdgeIndices());
                updated_edges = new HashSet<int>();
                int area_passes = 0;
                while (remaining_edges.Count() > 0 && area_passes < 20) {
                    area_passes++;
                    foreach (int ei in remaining_edges) {
                        if (fillmesh.IsBoundaryEdge(ei))
                            continue;

                        Index2i ev = fillmesh.GetEdgeV(ei);
                        Index2i ov = fillmesh.GetEdgeOpposingV(ei);

                        int find_other = fillmesh.FindEdge(ov.a, ov.b);
                        if (find_other != DMesh3.InvalidID)
                            continue;

                        double total_curv_cur = curvature_metric_cached(ev.a, ev.b, ov.a, ov.b);

                        double a = aspect_metric(ei);
                        if (a > 1)
                            continue;

                        DMesh3.EdgeFlipInfo info;
                        MeshResult result = fillmesh.FlipEdge(ei, out info);
                        if (result != MeshResult.Ok)
                            continue;

                        double total_curv_flip = curvature_metric_eval(ev.a, ev.b, ov.a, ov.b);

                        bool keep_flip = Math.Abs(total_curv_cur - total_curv_flip) < DevelopabilityTolerance;
                        if (keep_flip == false) {
                            result = fillmesh.FlipEdge(ei, out info);
                        } else {
                            update_curvature(ev.a); update_curvature(ev.b);
                            update_curvature(ov.a); update_curvature(ov.b);
                            add_all_edges(ei, updated_edges);
                        }
                    }
                    var tmp = remaining_edges;
                    remaining_edges = updated_edges;
                    updated_edges = tmp; updated_edges.Clear();
                }
            }


            regionop.BackPropropagate();
            FillTriangles = regionop.CurrentBaseTriangles;
            FillVertices = regionop.CurrentBaseInteriorVertices().ToArray();

            return true;

        }





        void remove_remaining_interior_verts()
        {
            HashSet<int> interiorv = new HashSet<int>(MeshIterators.InteriorVertices(fillmesh));
            int prev_count = 0;
            while (interiorv.Count > 0 && interiorv.Count != prev_count) {
                prev_count = interiorv.Count;
                int[] curv = interiorv.ToArray();
                foreach (int vid in curv) {
                    foreach (int e in fillmesh.VtxEdgesItr(vid)) {
                        Index2i ev = fillmesh.GetEdgeV(e);
                        int otherv = (ev.a == vid) ? ev.b : ev.a;
                        DMesh3.EdgeCollapseInfo info;
                        MeshResult result = fillmesh.CollapseEdge(otherv, vid, out info);
                        if (result == MeshResult.Ok)
                            break;
                    }
                    if (fillmesh.IsVertex(vid) == false)
                        interiorv.Remove(vid);
                }
            }
            if (interiorv.Count > 0)
                Util.gBreakToDebugger();
        }





        void add_all_edges(int ei, HashSet<int> edge_set)
        {
            Index2i et = fillmesh.GetEdgeT(ei);
            Index3i te = fillmesh.GetTriEdges(et.a);
            edge_set.Add(te.a); edge_set.Add(te.b); edge_set.Add(te.c);
            te = fillmesh.GetTriEdges(et.b);
            edge_set.Add(te.a); edge_set.Add(te.b); edge_set.Add(te.c);
        }



        double area_metric(int eid)
        {
            Index3i ta, tb, ota, otb;
            MeshUtil.GetEdgeFlipTris(fillmesh, eid, out ta, out tb, out ota, out otb);
            double area_a = get_tri_area(fillmesh, ref ta);
            double area_b = get_tri_area(fillmesh, ref tb);
            double area_c = get_tri_area(fillmesh, ref ota);
            double area_d = get_tri_area(fillmesh, ref otb);
            double avg_ab = (area_a + area_b) * 0.5;
            double avg_cd = (area_c + area_d) * 0.5;
            double metric_ab = Math.Abs(area_a - avg_ab) + Math.Abs(area_b - avg_ab);
            double metric_cd = Math.Abs(area_c - avg_cd) + Math.Abs(area_d - avg_cd);
            return metric_cd / metric_ab;
        }


        double aspect_metric(int eid)
        {
            Index3i ta, tb, ota, otb;
            MeshUtil.GetEdgeFlipTris(fillmesh, eid, out ta, out tb, out ota, out otb);
            double aspect_a = get_tri_aspect(fillmesh, ref ta);
            double aspect_b = get_tri_aspect(fillmesh, ref tb);
            double aspect_c = get_tri_aspect(fillmesh, ref ota);
            double aspect_d = get_tri_aspect(fillmesh, ref otb);
            double metric_ab = Math.Abs(aspect_a - 1.0) + Math.Abs(aspect_b - 1.0);
            double metric_cd = Math.Abs(aspect_c - 1.0) + Math.Abs(aspect_d - 1.0);
            return metric_cd / metric_ab;
        }


        void update_curvature(int vid)
        {
            double angle_sum = 0;
            exterior_angle_sums.TryGetValue(vid, out angle_sum);
            foreach (int tid in fillmesh.VtxTrianglesItr(vid)) {
                Index3i et = fillmesh.GetTriangle(tid);
                int idx = IndexUtil.find_tri_index(vid, ref et);
                angle_sum += fillmesh.GetTriInternalAngleR(tid, idx);
            }
            curvatures[vid] = angle_sum - MathUtil.TwoPI;
        }
        double curvature_metric_cached(int a, int b, int c, int d)
        {
            double defect_a = curvatures[a];
            double defect_b = curvatures[b];
            double defect_c = curvatures[c];
            double defect_d = curvatures[d];
            return Math.Abs(defect_a) + Math.Abs(defect_b) + Math.Abs(defect_c) + Math.Abs(defect_d);
        }


        double curvature_metric_eval(int a, int b, int c, int d)
        {
            double defect_a = compute_gauss_curvature(a);
            double defect_b = compute_gauss_curvature(b);
            double defect_c = compute_gauss_curvature(c);
            double defect_d = compute_gauss_curvature(d);
            return Math.Abs(defect_a) + Math.Abs(defect_b) + Math.Abs(defect_c) + Math.Abs(defect_d);
        }

        double compute_gauss_curvature(int vid)
        {
            double angle_sum = 0;
            exterior_angle_sums.TryGetValue(vid, out angle_sum);
            foreach (int tid in fillmesh.VtxTrianglesItr(vid)) {
                Index3i et = fillmesh.GetTriangle(tid);
                int idx = IndexUtil.find_tri_index(vid, ref et);
                angle_sum += fillmesh.GetTriInternalAngleR(tid, idx);
            }
            return angle_sum - MathUtil.TwoPI;
        }



        Vector3d get_tri_normal(DMesh3 mesh, Index3i tri)
        {
            return MathUtil.Normal(mesh.GetVertex(tri.a), mesh.GetVertex(tri.b), mesh.GetVertex(tri.c));
        }
        double get_tri_area(DMesh3 mesh, ref Index3i tri)
        {
            return MathUtil.Area(mesh.GetVertex(tri.a), mesh.GetVertex(tri.b), mesh.GetVertex(tri.c));
        }
        double get_tri_aspect(DMesh3 mesh, ref Index3i tri)
        {
            return MathUtil.AspectRatio(mesh.GetVertex(tri.a), mesh.GetVertex(tri.b), mesh.GetVertex(tri.c));
        }

    }
}
