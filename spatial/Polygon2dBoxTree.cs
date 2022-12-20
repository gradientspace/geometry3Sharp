using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{


    public class GeneralPolygon2dBoxTree
    {
        public GeneralPolygon2d Polygon;

        Polygon2dBoxTree OuterTree;
        Polygon2dBoxTree[] HoleTrees;


        public GeneralPolygon2dBoxTree(GeneralPolygon2d poly)
        {
            Polygon = poly;
            OuterTree = new Polygon2dBoxTree(poly.Outer);
            int NH = poly.Holes.Count;
            if (NH > 0) {
                HoleTrees = new Polygon2dBoxTree[NH];
                for (int k = 0; k < NH; ++k)
                    HoleTrees[k] = new Polygon2dBoxTree(poly.Holes[k]);
            }
        }


        public double DistanceSquared(Vector2d pt, out int iHoleIndex, out int iNearSeg, out double fNearSegT)
        {
            iHoleIndex = -1;
            double min_dist = OuterTree.SquaredDistance(pt, out iNearSeg, out fNearSegT);
            int NH = (HoleTrees == null) ? 0 : HoleTrees.Length;
            for (int k = 0; k < NH; ++k) {
                int hole_near_seg; double hole_seg_t;
                double hole_dist = HoleTrees[k].SquaredDistance(pt, out hole_near_seg, out hole_seg_t, min_dist);
                if (hole_dist < min_dist) {
                    min_dist = hole_dist;
                    iHoleIndex = k;
                    iNearSeg = hole_near_seg;
                    fNearSegT = hole_seg_t;
                }
            }
            return min_dist;
        }


        public double DistanceSquared(Vector2d pt)
        {
            int iHole, iSeg; double segT;
            double distSqr = DistanceSquared(pt, out iHole, out iSeg, out segT);
            return distSqr;
        }
        public double Distance(Vector2d pt)
        {
            int iHole, iSeg; double segT;
            double distSqr = DistanceSquared(pt, out iHole, out iSeg, out segT);
            return Math.Sqrt(distSqr);
        }
        public Vector2d NearestPoint(Vector2d pt)
        {
            int iHole, iSeg; double segT;
            DistanceSquared(pt, out iHole, out iSeg, out segT);
            return Polygon.PointAt(iSeg, segT, iHole);
        }

    }






    /// <summary>
    /// tree of Oriented Boxes (OBB) for a Polygon2d. 
    /// Construction is sequential, ie pairs of segments are merged into boxes, then pairs of boxes, and so on
    /// 
    /// [TODO] is this the best strategy? is there maybe some kind of sorting/sweepline algo?
    /// [TODO] would it make more sense to have more than just 2 segments at lowest level?
    /// 
    /// </summary>
    public class Polygon2dBoxTree
    {
        public Polygon2d Polygon;

        Box2d[] boxes;
        int layers;
        List<int> layer_counts;

        public Polygon2dBoxTree(Polygon2d poly)
        {
            Polygon = poly;
            build_sequential(poly);
        }


        public double DistanceSquared(Vector2d pt) {
            int iSeg; double segT;
            double distSqr = SquaredDistance(pt, out iSeg, out segT);
            return distSqr;
        }
        public double Distance(Vector2d pt)
        {
            int iSeg; double segT;
            double distSqr = SquaredDistance(pt, out iSeg, out segT);
            return Math.Sqrt(distSqr);
        }
        public Vector2d NearestPoint(Vector2d pt)
        {
            int iSeg; double segT;
            SquaredDistance(pt, out iSeg, out segT);
            return Polygon.PointAt(iSeg, segT);
        }



        public double SquaredDistance(Vector2d pt, out int iNearSeg, out double fNearSegT, double max_dist = double.MaxValue)
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



        void find_min_distance(ref Vector2d pt, ref double min_dist, ref int min_dist_seg, ref double min_dist_segt, int bi, int iLayerStart, int iLayer)
        {
            // hit polygon layer, check segments
            if (iLayer == 0) {
                int seg_i = 2 * bi;
                Segment2d seg_a = Polygon.Segment(seg_i);
                double segt;
                double segdist = seg_a.DistanceSquared(pt, out segt);
                if (segdist <= min_dist) {
                    min_dist = segdist;
                    min_dist_seg = seg_i;
                    min_dist_segt = segt;
                }
                if ((seg_i + 1) < Polygon.VertexCount) {
                    Segment2d seg_b = Polygon.Segment(seg_i + 1);
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


        // build tree of boxes as sequential array
        void build_sequential(Polygon2d poly)
        {
            int NV = poly.VertexCount;
            int N = NV;
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


            boxes = new Box2d[boxCount];
            bi = 0;

            // make first layer
            for (int si = 0; si < NV; si += 2) {
                Vector2d v1 = poly[(si + 1) % NV];
                Segment2d seg1 = new Segment2d(poly[si], v1);
                Box2d box = new Box2d(seg1);
                if (si < NV - 1) {
                    Segment2d seg2 = new Segment2d(v1, poly[(si + 2) % NV]);
                    Box2d box2 = new Box2d(seg2);
                    box = Box2d.Merge(ref box, ref box2);
                }
                boxes[bi++] = box;
            }

            // repeatedly build layers until we hit a single box
            N = bi;
            int prev_layer_start = 0;
            bool done = false;
            while (done == false) {
                int layer_start = bi;

                for (int k = 0; k < N; k += 2) {
                    Box2d mbox = Box2d.Merge(ref boxes[prev_layer_start + k], ref boxes[prev_layer_start + k + 1]);
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
