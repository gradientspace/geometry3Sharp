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
    /// Work in progress. Idea is that this class will analyze the hole and choose correct filling
    /// strategy. Mainly just calling other fillers. 
    /// 
    /// Also contains prototype of filler that decomposes hole into spans based on normals and
    /// then uses PlanarSpansFiller. See comments, is not really functional.
    /// 
    /// </summary>
    public class AutoHoleFill
    {
        public DMesh3 Mesh;

        public double TargetEdgeLength = 2.5;

        public EdgeLoop FillLoop;

        /*
         *  Outputs
         */

        /// <summary> Final fill triangles. May include triangles outside initial fill loop, if ConstrainToHoleInterior=false </summary>
        public int[] FillTriangles;


        public AutoHoleFill(DMesh3 mesh, EdgeLoop fillLoop)
        {
            this.Mesh = mesh;
            this.FillLoop = fillLoop;
        }


        enum UseFillType
        {
            PlanarFill,
            MinimalFill,
            PlanarSpansFill,
            SmoothFill
        }



        public bool Apply()
        {
            UseFillType type = classify_hole();

            bool bResult = false;

            bool DISABLE_PLANAR_FILL = false;

            if (type == UseFillType.PlanarFill && DISABLE_PLANAR_FILL == false)
                bResult = fill_planar();
            else if (type == UseFillType.MinimalFill)
                bResult = fill_minimal();
            else if (type == UseFillType.PlanarSpansFill)
                bResult = fill_planar_spans();
            else
                bResult = fill_smooth();

            if (bResult == false && type != UseFillType.SmoothFill)
                bResult = fill_smooth();

            return bResult;
        }




        UseFillType classify_hole()
        {
            return UseFillType.MinimalFill;
#if false

            int NV = FillLoop.VertexCount;
            int NE = FillLoop.EdgeCount;

            Vector3d size = FillLoop.ToCurve().GetBoundingBox().Diagonal;

            NormalHistogram hist = new NormalHistogram(4096, true);

            for (int k = 0; k < NE; ++k) {
                int eid = FillLoop.Edges[k];
                Index2i et = Mesh.GetEdgeT(eid);
                Vector3d n = Mesh.GetTriNormal(et.a);
                hist.Count(n, 1.0, true);
            }

            if (hist.UsedBins.Count == 1)
                return UseFillType.PlanarFill;

            //int nontrivial_bins = 0;
            //foreach ( int bin in hist.UsedBins ) {
            //    if (hist.Counts[bin] > 8)
            //        nontrivial_bins++;
            //}
            //if (nontrivial_bins > 0)
            //    return UseFillType.PlanarSpansFill;

            return UseFillType.SmoothFill;
#endif
        }




        bool fill_smooth()
        {
            SmoothedHoleFill fill = new SmoothedHoleFill(Mesh, FillLoop);
            fill.TargetEdgeLength = TargetEdgeLength;
            fill.SmoothAlpha = 1.0f;
            fill.ConstrainToHoleInterior = true;
            //fill.SmoothSolveIterations = 3;   // do this if we have a complicated hole - should be able to tell by normal histogram...
            return fill.Apply();
        }



        bool fill_planar()
        {
            Vector3d n = Vector3d.Zero, c = Vector3d.Zero;
            int NE = FillLoop.EdgeCount;
            for (int k = 0; k < NE; ++k) {
                int eid = FillLoop.Edges[k];
                n += Mesh.GetTriNormal(Mesh.GetEdgeT(eid).a);
                c += Mesh.GetEdgePoint(eid, 0.5);
            }
            n.Normalize(); c /= (double)NE;

            PlanarHoleFiller filler = new PlanarHoleFiller(Mesh);
            filler.FillTargetEdgeLen = TargetEdgeLength;
            filler.AddFillLoop(FillLoop);
            filler.SetPlane(c, n);

            bool bOK = filler.Fill();
            return bOK;
        }



        bool fill_minimal()
        {
            MinimalHoleFill minfill = new MinimalHoleFill(Mesh, FillLoop);
            bool bOK = minfill.Apply();
            return bOK;
        }





        /// <summary>
        /// Here are reasons this isn't working:
        ///    1) find_coplanar_span_sets does not actually limit to coplanar (see comments)
        ///    2) 
        /// 
        /// </summary>
        bool fill_planar_spans()
        {
            Dictionary<Vector3d, List<EdgeSpan>> span_sets = find_coplanar_span_sets(Mesh, FillLoop);

            foreach ( var set in span_sets ) {
                Vector3d normal = set.Key;
                List<EdgeSpan> spans = set.Value;
                Vector3d pos = spans[0].GetVertex(0);

                if (spans.Count > 1) {
                    List<List<EdgeSpan>> subset_set = sort_planar_spans(spans, normal);
                    foreach ( var subset in subset_set) {
                        if (subset.Count == 1 ) {
                            PlanarSpansFiller filler = new PlanarSpansFiller(Mesh, subset);
                            filler.FillTargetEdgeLen = TargetEdgeLength;
                            filler.SetPlane(pos, normal);
                            filler.Fill();
                        }
                    }

                } else {
                    PlanarSpansFiller filler = new PlanarSpansFiller(Mesh, spans);
                    filler.FillTargetEdgeLen = TargetEdgeLength;
                    filler.SetPlane(pos, normal);
                    filler.Fill();
                }
            }

            return true;
        }


        /// <summary>
        /// This function is supposed to take a set of spans in a plane and sort them
        /// into regions that can be filled with a polygon. Currently kind of clusters
        /// based on intersecting bboxes. Does not work.
        /// 
        /// I think fundamentally it needs to look back at the input mesh, to see what 
        /// is connected/not-connected. Or possibly use polygon winding number? Need
        /// to somehow define what the holes are...
        /// </summary>
        List<List<EdgeSpan>> sort_planar_spans(List<EdgeSpan> allspans, Vector3d normal)
        {
            List<List<EdgeSpan>> result = new List<List<EdgeSpan>>();
            Frame3f polyFrame = new Frame3f(Vector3d.Zero, normal);

            int N = allspans.Count;

            List<PolyLine2d> plines = new List<PolyLine2d>();
            foreach (EdgeSpan span in allspans) {
                plines.Add(to_polyline(span, polyFrame));
            }

            bool[] bad_poly = new bool[N];
            for (int k = 0; k < N; ++k)
                bad_poly[k] = false; // self_intersects(plines[k]);

            bool[] used = new bool[N];
            for (int k = 0; k < N; ++k) {
                if (used[k])
                    continue;
                bool is_bad = bad_poly[k];
                AxisAlignedBox2d bounds = plines[k].Bounds;
                used[k] = true;

                List<int> set = new List<int>() { k };

                for ( int j = k+1; j < N; ++j ) {
                    if (used[j])
                        continue;
                    AxisAlignedBox2d boundsj = plines[j].Bounds;
                    if ( bounds.Intersects(boundsj) ) {
                        used[j] = true;
                        is_bad = is_bad || bad_poly[j];
                        bounds.Contain(boundsj);
                        set.Add(j);
                    }
                }

                if ( is_bad == false ) {
                    List<EdgeSpan> span_set = new List<EdgeSpan>();
                    foreach (int idx in set)
                        span_set.Add(allspans[idx]);
                    result.Add(span_set);
                }

            }

            return result;
        }
        PolyLine2d to_polyline(EdgeSpan span, Frame3f polyFrame)
        {
            int NV = span.VertexCount;
            PolyLine2d poly = new PolyLine2d();
            for (int k = 0; k < NV; ++k)
                poly.AppendVertex(polyFrame.ToPlaneUV((Vector3f)span.GetVertex(k), 2));
            return poly;
        }
        Polygon2d to_polygon(EdgeSpan span, Frame3f polyFrame)
        {
            int NV = span.VertexCount;
            Polygon2d poly = new Polygon2d();
            for (int k = 0; k < NV; ++k)
                poly.AppendVertex(polyFrame.ToPlaneUV((Vector3f)span.GetVertex(k), 2));
            return poly;
        }
        bool self_intersects(PolyLine2d poly)
        {
            Segment2d seg = new Segment2d(poly.Start, poly.End);
            int NS = poly.VertexCount - 2;
            for ( int i = 1; i < NS; ++i ) { 
                if (poly.Segment(i).Intersects(ref seg))
                    return true;
            }
            return false;
        }




        // NO DOES NOT WORK. DOES NOT FIND EDGE SPANS THAT ARE IN PLANE BUT HAVE DIFFERENT NORMAL!
        // NEED TO COLLECT UP SPANS USING NORMAL HISTOGRAM NORMALS!
        // ALSO NEED TO ACTUALLY CHECK FOR COPLANARITY, NOT JUST SAME NORMAL!!

        Dictionary<Vector3d,List<EdgeSpan>> find_coplanar_span_sets(DMesh3 mesh, EdgeLoop loop)
        {
            double dot_thresh = 0.999;

            Dictionary<Vector3d, List<EdgeSpan>> span_sets = new Dictionary<Vector3d, List<EdgeSpan>>();

            int NV = loop.Vertices.Length;
            int NE = loop.Edges.Length;

            Vector3d[] edge_normals = new Vector3d[NE];
            for (int k = 0; k < NE; ++k)
                edge_normals[k] = mesh.GetTriNormal(mesh.GetEdgeT(loop.Edges[k]).a);
            
            // find coplanar verts
            // [RMS] this is wrong, if normals vary smoothly enough we will mark non-coplanar spans as coplanar
            bool[] vert_coplanar = new bool[NV];
            int nc = 0;
            for ( int k = 0; k < NV; ++k ) {
                int prev = (k==0) ? NV-1 : k-1;
                if (edge_normals[k].Dot(ref edge_normals[prev]) > dot_thresh) {
                    vert_coplanar[k] = true;
                    nc++;
                }
            }
            if (nc < 2)
                return null;

            int iStart = 0;
            while (vert_coplanar[iStart])
                iStart++;

            int iPrev = iStart;
            int iCur = iStart+1;
            while (iCur != iStart) {
                if (vert_coplanar[iCur] == false) {
                    iPrev = iCur;
                    iCur = (iCur + 1) % NV;
                    continue;
                }

                List<int> edges = new List<int>() { loop.Edges[iPrev] };
                int span_start_idx = iCur;
                while (vert_coplanar[iCur]) {
                    edges.Add(loop.Edges[iCur]);
                    iCur = (iCur + 1) % NV;
                }

                if ( edges.Count > 1 ) {
                    Vector3d span_n = edge_normals[span_start_idx];
                    EdgeSpan span = EdgeSpan.FromEdges(mesh, edges);
                    span.CheckValidity();
                    foreach ( var pair in span_sets ) {
                        if ( pair.Key.Dot(ref span_n) > dot_thresh ) {
                            span_n = pair.Key;
                            break;
                        }
                    }
                    List<EdgeSpan> found;
                    if (span_sets.TryGetValue(span_n, out found) == false)
                        span_sets[span_n] = new List<EdgeSpan>() { span };
                    else
                        found.Add(span);
                }

            }



            return span_sets;

        }


    }
}
