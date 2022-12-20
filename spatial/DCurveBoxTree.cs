using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{

    /// <summary>
    /// tree of Oriented Boxes (OBB) for a DCurve3. 
    /// Construction is sequential, ie pairs of segments are merged into boxes, then pairs of boxes, and so on
    /// 
    /// [TODO] is this the best strategy? is there maybe some kind of sorting/sweepline algo?
    /// [TODO] would it make more sense to have more than just 2 segments at lowest level?
    /// 
    /// </summary>
    public class DCurve3BoxTree
    {
        public DCurve3 Curve;

        Box3d[] boxes;
        int layers;
        List<int> layer_counts;

        public DCurve3BoxTree(DCurve3 curve)
        {
            Curve = curve;
            build_sequential(curve);
        }


        public double DistanceSquared(Vector3d pt) {
            int iSeg; double segT;
            double distSqr = SquaredDistance(pt, out iSeg, out segT);
            return distSqr;
        }
        public double Distance(Vector3d pt)
        {
            int iSeg; double segT;
            double distSqr = SquaredDistance(pt, out iSeg, out segT);
            return Math.Sqrt(distSqr);
        }
        public Vector3d NearestPoint(Vector3d pt)
        {
            int iSeg; double segT;
            SquaredDistance(pt, out iSeg, out segT);
            return Curve.PointAt(iSeg, segT);
        }



        public double SquaredDistance(Vector3d pt, out int iNearSeg, out double fNearSegT, double max_dist = double.MaxValue)
        {
            int iRoot = boxes.Length - 1;
            int iLayer = layers - 1;

            double min_dist = max_dist;
            iNearSeg = -1;
            fNearSegT = 0;

            find_min_distance(ref pt, ref min_dist, ref iNearSeg, ref fNearSegT, 0, iRoot, iLayer);
            if (iNearSeg == -1)
                return double.MaxValue;
            return min_dist;
        }



        void find_min_distance(ref Vector3d pt, ref double min_dist, ref int min_dist_seg, ref double min_dist_segt, int bi, int iLayerStart, int iLayer)
        {
            // hit polygon layer, check segments
            if (iLayer == 0) {
                int seg_i = 2 * bi;
                Segment3d seg_a = Curve.GetSegment(seg_i);
                double segt;
                double segdist = seg_a.DistanceSquared(pt, out segt);
                if (segdist <= min_dist) {
                    min_dist = segdist;
                    min_dist_seg = seg_i;
                    min_dist_segt = segt;
                }
                if ( (seg_i+1) < Curve.SegmentCount ) {
                    Segment3d seg_b = Curve.GetSegment(seg_i + 1);
                    segdist = seg_b.DistanceSquared(pt, out segt);
                    if (segdist <= min_dist) {
                        min_dist = segdist;
                        min_dist_seg = seg_i + 1;
                        min_dist_segt = segt;
                    }
                }

                return;
            }

            // test both boxes and recurse
            int prev_layer = iLayer - 1;
            int prev_count = layer_counts[prev_layer];
            int prev_start = iLayerStart - prev_count;
            int prev_a = prev_start + 2 * bi;
            double dist = boxes[prev_a].DistanceSquared(pt);
            if (dist <= min_dist) {
                find_min_distance(ref pt, ref min_dist, ref min_dist_seg, ref min_dist_segt, 2 * bi, prev_start, prev_layer);
            }
            if ((2 * bi + 1) >= prev_count)
                return;
            int prev_b = prev_a + 1;
            double dist2 = boxes[prev_b].DistanceSquared(pt);
            if (dist2 <= min_dist) {
                find_min_distance(ref pt, ref min_dist, ref min_dist_seg, ref min_dist_segt, 2 * bi + 1, prev_start, prev_layer);
            }
        }



        /// <summary>
        /// Find min-distance between ray and curve. Pass max_dist if you only care about a certain distance
        /// TODO: not 100% sure this is working properly... ?
        /// </summary>
        public double SquaredDistance(Ray3d ray, out int iNearSeg, out double fNearSegT, out double fRayT, double max_dist = double.MaxValue)
        {
            int iRoot = boxes.Length - 1;
            int iLayer = layers - 1;

            double min_dist = max_dist;
            iNearSeg = -1;
            fNearSegT = 0;
            fRayT = double.MaxValue;

            find_min_distance(ref ray, ref min_dist, ref iNearSeg, ref fNearSegT, ref fRayT, 0, iRoot, iLayer);
            if (iNearSeg == -1)
                return double.MaxValue;
            return min_dist;
        }

        void find_min_distance(ref Ray3d ray, ref double min_dist, ref int min_dist_seg, ref double min_dist_segt, ref double min_dist_rayt, int bi, int iLayerStart, int iLayer)
        {
            // hit polygon layer, check segments
            if (iLayer == 0) {
                int seg_i = 2 * bi;
                Segment3d seg_a = Curve.GetSegment(seg_i);
                double segt, rayt;
                double segdist_sqr = DistRay3Segment3.SquaredDistance(ref ray, ref seg_a, out rayt, out segt);
                double segdist = Math.Sqrt(segdist_sqr);
                if (segdist <= min_dist) {
                    min_dist = segdist;
                    min_dist_seg = seg_i;
                    min_dist_segt = segt;
                    min_dist_rayt = rayt;
                }
                if ((seg_i + 1) < Curve.SegmentCount) {
                    Segment3d seg_b = Curve.GetSegment(seg_i + 1);
                    segdist_sqr = DistRay3Segment3.SquaredDistance(ref ray, ref seg_b, out rayt, out segt);
                    segdist = Math.Sqrt(segdist_sqr);
                    if (segdist <= min_dist) {
                        min_dist = segdist;
                        min_dist_seg = seg_i + 1;
                        min_dist_segt = segt;
                        min_dist_rayt = rayt;
                    }
                }

                return;
            }

            // test both boxes and recurse
            // TODO: verify that this intersection strategy makes sense?
            int prev_layer = iLayer - 1;
            int prev_count = layer_counts[prev_layer];
            int prev_start = iLayerStart - prev_count;
            int prev_a = prev_start + 2 * bi;
            bool intersects = IntrRay3Box3.Intersects(ref ray, ref boxes[prev_a], min_dist);
            if (intersects) {
                find_min_distance(ref ray, ref min_dist, ref min_dist_seg, ref min_dist_segt, ref min_dist_rayt, 2 * bi, prev_start, prev_layer);
            }
            if ((2 * bi + 1) >= prev_count)
                return;
            int prev_b = prev_a + 1;
            bool intersects2 = IntrRay3Box3.Intersects(ref ray, ref boxes[prev_b], min_dist);
            if (intersects2) {
                find_min_distance(ref ray, ref min_dist, ref min_dist_seg, ref min_dist_segt, ref min_dist_rayt, 2 * bi + 1, prev_start, prev_layer);
            }
        }







        /// <summary>
        /// Find min-distance between ray and curve. Pass max_dist if you only care about a certain distance
        /// TODO: not 100% sure this is working properly... ?
        /// </summary>
        public bool FindClosestRayIntersction(Ray3d ray, double radius, out int hitSegment, out double fRayT)
        {
            int iRoot = boxes.Length - 1;
            int iLayer = layers - 1;

            hitSegment = -1;
            fRayT = double.MaxValue;

            find_closest_ray_intersction(ref ray, radius, ref hitSegment, ref fRayT, 0, iRoot, iLayer);
            return (hitSegment != -1);
        }

        void find_closest_ray_intersction(ref Ray3d ray, double radius, ref int nearestSegment, ref double nearest_ray_t, int bi, int iLayerStart, int iLayer)
        {
            // hit polygon layer, check segments
            if (iLayer == 0) {
                int seg_i = 2 * bi;
                Segment3d seg_a = Curve.GetSegment(seg_i);
                double segt, rayt;
                double segdist_sqr = DistRay3Segment3.SquaredDistance(ref ray, ref seg_a, out rayt, out segt);
                if (segdist_sqr <= radius*radius && rayt < nearest_ray_t) {
                    nearestSegment = seg_i;
                    nearest_ray_t = rayt;
                }
                if ((seg_i + 1) < Curve.SegmentCount) {
                    Segment3d seg_b = Curve.GetSegment(seg_i + 1);
                    segdist_sqr = DistRay3Segment3.SquaredDistance(ref ray, ref seg_b, out rayt, out segt);
                    if (segdist_sqr <= radius * radius && rayt < nearest_ray_t) {
                        nearestSegment = seg_i+1;
                        nearest_ray_t = rayt;
                    }
                }

                return;
            }

            // test both boxes and recurse
            // TODO: verify that this intersection strategy makes sense?
            int prev_layer = iLayer - 1;
            int prev_count = layer_counts[prev_layer];
            int prev_start = iLayerStart - prev_count;
            int prev_a = prev_start + 2 * bi;
            bool intersects = IntrRay3Box3.Intersects(ref ray, ref boxes[prev_a], radius);
            if (intersects) {
                find_closest_ray_intersction(ref ray, radius, ref nearestSegment, ref nearest_ray_t, 2 * bi, prev_start, prev_layer);
            }
            if ((2 * bi + 1) >= prev_count)
                return;
            int prev_b = prev_a + 1;
            bool intersects2 = IntrRay3Box3.Intersects(ref ray, ref boxes[prev_b], radius);
            if (intersects2) {
                find_closest_ray_intersction(ref ray, radius, ref nearestSegment, ref nearest_ray_t, 2 * bi + 1, prev_start, prev_layer);
            }
        }






        // build tree of boxes as sequential array
        void build_sequential(DCurve3 curve)
        {
            int NV = curve.VertexCount;
            int N = (curve.Closed) ? NV : NV - 1;
            int boxCount = 0;
            layers = 0;
            layer_counts = new List<int>();

            // count how many boxes in each layer, building up from initial segments
            int bi = 0;
            while (N > 1) {
                int layer_boxes = (N / 2) + (N % 2 == 0 ? 0 : 1);
                boxCount += layer_boxes;
                N = layer_boxes;

                layer_counts.Add(layer_boxes);
                bi += layer_boxes;
                layers++;
            }
            // [RMS] this case happens if N = 1, previous loop is skipped and we have to 
            // hardcode initialization to this redundant box
            if ( layers == 0 ) {
                layers = 1;
                boxCount = 1;
                layer_counts = new List<int>() { 1 };
            }

            boxes = new Box3d[boxCount];
            bi = 0;

            // make first layer
            int NStop = (curve.Closed) ? NV : NV - 1;
            for (int si = 0; si < NStop; si += 2) {
                Vector3d v1 = curve[(si + 1) % NV];
                Segment3d seg1 = new Segment3d(curve[si], v1);
                Box3d box = new Box3d(seg1);
                if (si < NV - 1) {
                    Segment3d seg2 = new Segment3d(v1, curve[(si + 2) % NV]);
                    Box3d box2 = new Box3d(seg2);
                    box = Box3d.Merge(ref box, ref box2);
                }
                boxes[bi++] = box;
            }

            // repeatedly build layers until we hit a single box
            N = bi;
            if (N == 1)
                return;
            int prev_layer_start = 0;
            bool done = false;
            while (done == false) {
                int layer_start = bi;

                for (int k = 0; k < N; k += 2) {
                    Box3d mbox = Box3d.Merge(ref boxes[prev_layer_start + k], ref boxes[prev_layer_start + k + 1]);
                    boxes[bi++] = mbox;
                }

                N = (N / 2) + (N % 2 == 0 ? 0 : 1);
                prev_layer_start = layer_start;
                if (N == 1)
                    done = true;
            }
        }


    }
}
