using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

namespace g3
{
    /// <summary>
    /// Hierarchical Axis-Aligned-Bounding-Box tree for an IPointSet
    /// 
    /// 
    /// TODO: no timestamp support right now...
    /// 
    /// </summary>
    public class PointAABBTree3
    {
        IPointSet points;
        int points_timestamp;

        public PointAABBTree3(IPointSet pointsIn, bool autoBuild = true)
        {
            points = pointsIn;
            if (autoBuild)
                Build();
        }


        public IPointSet Points { get { return points; } }


        // if non-null, return false to ignore certain points
        public Func<int, bool> PointFilterF = null;


        // Top-down build strategies will put at most this many points into a box.
        // Larger value == shallower trees, but leaves cost more to test
        public int LeafMaxPointCount = 32;

        // how should we build the tree?
        public enum BuildStrategy
        {
            Default,                // currently TopDownMidpoint

            TopDownMidpoint,        // Recursively split point set by midpoint of axis interval.
                                    //   This is the fastest and usually produces lower total-volume trees than bottom-up.
                                    //   Resulting trees are unbalanced, though.
            TopDownMedian           // Like TopDownMidpoint except we sort the point lists at each step and split on the median.
                                    //   2-4x slower than TopDownMidpoint, but trees are generally more efficient and balanced.
        }

        // Build the tree. Policy only matters for bottom-up strategies
        public void Build(BuildStrategy eStrategy = BuildStrategy.TopDownMidpoint)
        {
            if (eStrategy == BuildStrategy.TopDownMedian)
                build_top_down(true);
            else if (eStrategy == BuildStrategy.TopDownMidpoint)
                build_top_down(false);
            else if (eStrategy == BuildStrategy.Default)
                build_top_down(false);

            points_timestamp = points.Timestamp;
        }



        /// <summary>
        /// Find the point closest to p, within distance fMaxDist, or return InvalidID
        /// </summary>
        public virtual int FindNearestPoint(Vector3d p, double fMaxDist = double.MaxValue)
        {
            if (points_timestamp != points.Timestamp)
                throw new Exception("PointAABBTree3.FindNearestPoint: mesh has been modified since tree construction");

            double fNearestSqr = (fMaxDist < double.MaxValue) ? fMaxDist * fMaxDist : double.MaxValue;
            int tNearID = DMesh3.InvalidID;
            find_nearest_point(root_index, p, ref fNearestSqr, ref tNearID);
            return tNearID;
        }
        protected void find_nearest_point(int iBox, Vector3d p, ref double fNearestSqr, ref int tID)
        {
            int idx = box_to_index[iBox];
            if ( idx < points_end ) {            // point-list case, array is [N t1 t2 ... tN]
                int num_points = index_list[idx];
                for (int i = 1; i <= num_points; ++i) {
                    int ti = index_list[idx + i];
                    if (PointFilterF != null && PointFilterF(ti) == false)
                        continue;
                    Vector3d pt = points.GetVertex(ti);
                    double fDistSqr = pt.DistanceSquared(p);
                    if (fDistSqr < fNearestSqr ) {
                        fNearestSqr = fDistSqr;
                        tID = ti;
                    }
                }

            } else {                                // internal node, either 1 or 2 child boxes
                int iChild1 = index_list[idx];
                if ( iChild1 < 0 ) {                 // 1 child, descend if nearer than cur min-dist
                    iChild1 = (-iChild1) - 1;
                    double fChild1DistSqr = box_distance_sqr(iChild1, ref p);
                    if ( fChild1DistSqr <= fNearestSqr )
                        find_nearest_point(iChild1, p, ref fNearestSqr, ref tID);

                } else {                            // 2 children, descend closest first
                    iChild1 = iChild1 - 1;
                    int iChild2 = index_list[idx + 1] - 1;

                    double fChild1DistSqr = box_distance_sqr(iChild1, ref p);
                    double fChild2DistSqr = box_distance_sqr(iChild2, ref p);
                    if (fChild1DistSqr < fChild2DistSqr) {
                        if (fChild1DistSqr < fNearestSqr) {
                            find_nearest_point(iChild1, p, ref fNearestSqr, ref tID);
                            if (fChild2DistSqr < fNearestSqr)
                                find_nearest_point(iChild2, p, ref fNearestSqr, ref tID);
                        }
                    } else {
                        if (fChild2DistSqr < fNearestSqr) {
                            find_nearest_point(iChild2, p, ref fNearestSqr, ref tID);
                            if (fChild1DistSqr < fNearestSqr)
                                find_nearest_point(iChild1, p, ref fNearestSqr, ref tID);
                        }
                    }

                }
            }
        }




        /// <summary>
        /// Instances of this class can be passed in to the DoTraversal() function to implement your
        /// own tree-traversal queries.
        /// NextBoxF() is called for each box node. Return false from this function to halt terminate 
        /// that branch of the traversal, or true to descend into that box's children (boxes or points).
        /// NextPointF() is called for each point.
        /// </summary>
        public class TreeTraversal
        {
            // return false to terminate this branch
            // arguments are box and depth in tree
            public Func<AxisAlignedBox3d, int, bool> NextBoxF = (box,depth) => { return true; };

            public Action<int> NextPointF = (vID) => { };
        }


        /// <summary>
        /// Hierarchically descend through the tree nodes, calling the TreeTrversal functions at each level
        /// </summary>
        public virtual void DoTraversal(TreeTraversal traversal)
        {
            if (points_timestamp != points.Timestamp)
                throw new Exception("PointAABBTree3.FindNearestPoint: mesh has been modified since tree construction");

            tree_traversal(root_index, 0, traversal);
        }

        // traversal implementation. you can override to customize this if necessary.
        protected virtual void tree_traversal(int iBox, int depth, TreeTraversal traversal)
        {
            int idx = box_to_index[iBox];

            if ( idx < points_end ) {
                // point-list case, array is [N t1 t2 ... tN]
                int n = index_list[idx];
                for ( int i = 1; i <= n; ++i ) {
                    int ti = index_list[idx + i];
                    if (PointFilterF != null && PointFilterF(ti) == false)
                        continue;
                    traversal.NextPointF(ti);
                }
            } else {
                int i0 = index_list[idx];
                if ( i0 < 0 ) {
                    // negative index means we only have one 'child' box to descend into
                    i0 = (-i0) - 1;
                    if ( traversal.NextBoxF(get_box(i0), depth+1) )
                        tree_traversal(i0, depth+1, traversal);
                } else {
                    // positive index, two sequential child box indices to descend into
                    i0 = i0 - 1;
                    if ( traversal.NextBoxF(get_box(i0), depth+1) )
                        tree_traversal(i0, depth+1, traversal);
                    int i1 = index_list[idx + 1] - 1;
                    if ( traversal.NextBoxF(get_box(i1), depth+1) )
                        tree_traversal(i1, depth+1, traversal);
                }
            }
        }









        /*
         *  Fast Mesh Winding Number computation
         */

        /// <summary>
        /// FWN beta parameter - is 2.0 in paper
        /// </summary>
        public double FWNBeta = 2.0;

        /// <summary>
        /// FWN approximation order. can be 1 or 2. 2 is more accurate, obviously.
        /// </summary>
        public int FWNApproxOrder = 2;

        /// <summary>
        /// Replace this with function that returns proper area estimate
        /// </summary>
        public Func<int, double> FWNAreaEstimateF = (vid) => { return 1.0; };


        /// <summary>
        /// Fast approximation of winding number using far-field approximations
        /// </summary>
        public virtual double FastWindingNumber(Vector3d p)
        {
            if (points_timestamp != points.Timestamp)
                throw new Exception("PointAABBTree3.FindNearestPoint: mesh has been modified since tree construction");

            if (FastWindingCache == null || fast_winding_cache_timestamp != points.Timestamp) {
                build_fast_winding_cache();
                fast_winding_cache_timestamp = points.Timestamp;
            }

            double sum = branch_fast_winding_num(root_index, p);
            return sum;
        }

        // evaluate winding number contribution for all points below iBox
        protected double branch_fast_winding_num(int iBox, Vector3d p)
        {
            double branch_sum = 0;

            int idx = box_to_index[iBox];
            if (idx < points_end) {            // point-list case, array is [N t1 t2 ... tN]
                int num_pts = index_list[idx];
                for (int i = 1; i <= num_pts; ++i) {
                    int pi = index_list[idx + i];
                    Vector3d v = Points.GetVertex(pi);
                    Vector3d n = Points.GetVertexNormal(pi);
                    double a = FastWindingAreaCache[pi];
                    branch_sum += FastPointWinding.ExactEval(ref v, ref n, a, ref p);
                }

            } else {                                // internal node, either 1 or 2 child boxes
                int iChild1 = index_list[idx];
                if (iChild1 < 0) {                 // 1 child, descend if nearer than cur min-dist
                    iChild1 = (-iChild1) - 1;

                    // if we have winding cache, we can more efficiently compute contribution of all points
                    // below this box. Otherwise, recursively descend tree.
                    bool contained = box_contains(iChild1, p);
                    if (contained == false && can_use_fast_winding_cache(iChild1, ref p))
                        branch_sum += evaluate_box_fast_winding_cache(iChild1, ref p);
                    else
                        branch_sum += branch_fast_winding_num(iChild1, p);

                } else {                            // 2 children, descend closest first
                    iChild1 = iChild1 - 1;
                    int iChild2 = index_list[idx + 1] - 1;

                    bool contained1 = box_contains(iChild1, p);
                    if (contained1 == false && can_use_fast_winding_cache(iChild1, ref p))
                        branch_sum += evaluate_box_fast_winding_cache(iChild1, ref p);
                    else
                        branch_sum += branch_fast_winding_num(iChild1, p);

                    bool contained2 = box_contains(iChild2, p);
                    if (contained2 == false && can_use_fast_winding_cache(iChild2, ref p))
                        branch_sum += evaluate_box_fast_winding_cache(iChild2, ref p);
                    else
                        branch_sum += branch_fast_winding_num(iChild2, p);
                }
            }

            return branch_sum;
        }


        struct FWNInfo
        {
            public Vector3d Center;
            public double R;
            public Vector3d Order1Vec;
            public Matrix3d Order2Mat;
        }

        Dictionary<int, FWNInfo> FastWindingCache;
        double[] FastWindingAreaCache;
        int fast_winding_cache_timestamp = -1;

        protected void build_fast_winding_cache()
        {
            // set this to a larger number to ignore caches if number of points is too small.
            // (seems to be no benefit to doing this...is holdover from tree-decomposition FWN code)
            int WINDING_CACHE_THRESH = 1;

            FastWindingAreaCache = new double[Points.MaxVertexID];
            foreach (int vid in Points.VertexIndices())
                FastWindingAreaCache[vid] = FWNAreaEstimateF(vid);

            FastWindingCache = new Dictionary<int, FWNInfo>();
            HashSet<int> root_hash;
            build_fast_winding_cache(root_index, 0, WINDING_CACHE_THRESH, out root_hash);
        }
        protected int build_fast_winding_cache(int iBox, int depth, int pt_count_thresh, out HashSet<int> pts_hash)
        {
            pts_hash = null;

            int idx = box_to_index[iBox];
            if (idx < points_end) {            // point-list case, array is [N t1 t2 ... tN]
                int num_pts = index_list[idx];
                return num_pts;

            } else {                                // internal node, either 1 or 2 child boxes
                int iChild1 = index_list[idx];
                if (iChild1 < 0) {                 // 1 child, descend if nearer than cur min-dist
                    iChild1 = (-iChild1) - 1;
                    int num_child_pts = build_fast_winding_cache(iChild1, depth + 1, pt_count_thresh, out pts_hash);

                    // if count in child is large enough, we already built a cache at lower node
                    return num_child_pts;

                } else {                            // 2 children, descend closest first
                    iChild1 = iChild1 - 1;
                    int iChild2 = index_list[idx + 1] - 1;

                    // let each child build its own cache if it wants. If so, it will return the
                    // list of its child points
                    HashSet<int> child2_hash;
                    int num_pts_1 = build_fast_winding_cache(iChild1, depth + 1, pt_count_thresh, out pts_hash);
                    int num_pts_2 = build_fast_winding_cache(iChild2, depth + 1, pt_count_thresh, out child2_hash);
                    bool build_cache = (num_pts_1 + num_pts_2 > pt_count_thresh);

                    if (depth == 0)
                        return num_pts_1 + num_pts_2;  // cannot build cache at level 0...

                    // collect up the points we need. there are various cases depending on what children already did
                    if (pts_hash != null || child2_hash != null || build_cache) {
                        if (pts_hash == null && child2_hash != null) {
                            collect_points(iChild1, child2_hash);
                            pts_hash = child2_hash;
                        } else {
                            if (pts_hash == null) {
                                pts_hash = new HashSet<int>();
                                collect_points(iChild1, pts_hash);
                            }
                            if (child2_hash == null)
                                collect_points(iChild2, pts_hash);
                            else
                                pts_hash.UnionWith(child2_hash);
                        }
                    }
                    if (build_cache)
                        make_box_fast_winding_cache(iBox, pts_hash);

                    return (num_pts_1 + num_pts_2);
                }
            }
        }


        // check if we can use fwn 
        protected bool can_use_fast_winding_cache(int iBox, ref Vector3d q)
        {
            FWNInfo cacheInfo;
            if (FastWindingCache.TryGetValue(iBox, out cacheInfo) == false)
                return false;

            double dist_qp = cacheInfo.Center.Distance(ref q);
            if (dist_qp > FWNBeta * cacheInfo.R)
                return true;

            return false;
        }


        // compute FWN cache for all points underneath this box
        protected void make_box_fast_winding_cache(int iBox, IEnumerable<int> pointIndices)
        {
            Util.gDevAssert(FastWindingCache.ContainsKey(iBox) == false);

            // construct cache
            FWNInfo cacheInfo = new FWNInfo();
            FastPointWinding.ComputeCoeffs(points, pointIndices, FastWindingAreaCache,
                ref cacheInfo.Center, ref cacheInfo.R, ref cacheInfo.Order1Vec, ref cacheInfo.Order2Mat);

            FastWindingCache[iBox] = cacheInfo;
        }

        // evaluate the FWN cache for iBox
        protected double evaluate_box_fast_winding_cache(int iBox, ref Vector3d q)
        {
            FWNInfo cacheInfo = FastWindingCache[iBox];

            if (FWNApproxOrder == 2)
                return FastPointWinding.EvaluateOrder2Approx(ref cacheInfo.Center, ref cacheInfo.Order1Vec, ref cacheInfo.Order2Mat, ref q);
            else
                return FastPointWinding.EvaluateOrder1Approx(ref cacheInfo.Center, ref cacheInfo.Order1Vec, ref q);
        }


        // collect all the triangles below iBox in a hash
        protected void collect_points(int iBox, HashSet<int> points)
        {
            int idx = box_to_index[iBox];
            if (idx < points_end) {            // triange-list case, array is [N t1 t2 ... tN]
                int num_tris = index_list[idx];
                for (int i = 1; i <= num_tris; ++i)
                    points.Add(index_list[idx + i]);
            } else {
                int iChild1 = index_list[idx];
                if (iChild1 < 0) {                 // 1 child, descend if nearer than cur min-dist
                    collect_points((-iChild1) - 1, points);
                } else {                           // 2 children, descend closest first
                    collect_points(iChild1 - 1, points);
                    collect_points(index_list[idx + 1] - 1, points);
                }
            }
        }








        /// <summary>
        /// Total sum of volumes of all boxes in the tree. Mainly useful to evaluate tree quality.
        /// </summary>
        public double TotalVolume()
        {
            double volSum = 0;
            TreeTraversal t = new TreeTraversal() {
                NextBoxF = (box, depth) => {
                    volSum += box.Volume;
                    return true;
                }
            };
            DoTraversal(t);
            return volSum;
        }

        /// <summary>
        /// Total sum-of-extents over all boxes in the tree. Mainly useful to evaluate tree quality.
        /// </summary>
        public double TotalExtentSum()
        {
            double extSum = 0;
            TreeTraversal t = new TreeTraversal() {
                NextBoxF = (box, depth) => {
                    extSum += box.Extents.LengthL1;
                    return true;
                }
            };
            DoTraversal(t);
            return extSum;
        }


        /// <summary>
        /// Root bounding box of tree (note: tree must be generated by calling a query function first!)
        /// </summary>
        public AxisAlignedBox3d Bounds {
            get { return get_box(root_index); }
        }



        //
        // Internals - data structures, construction, etc
        //


        // storage for box nodes. 
        //   - box_to_index is a pointer into index_list
        //   - box_centers and box_extents are the centers/extents of the bounding boxes
        DVector<int> box_to_index;
        DVector<Vector3d> box_centers;
        DVector<Vector3d> box_extents;

        // list of indices for a given box. There is *no* marker/sentinel between
        // boxes, you have to get the starting index from box_to_index[]
        //
        // There are three kinds of records:
        //   - if i < points_end, then the list is a number of points,
        //       stored as [N t1 t2 t3 ... tN]
        //   - if i > points_end and index_list[i] < 0, this is a single-child
        //       internal box, with index (-index_list[i])-1     (shift-by-one in case actual value is 0!)
        //   - if i > points_end and index_list[i] > 0, this is a two-child
        //       internal box, with indices index_list[i]-1 and index_list[i+1]-1
        DVector<int> index_list;

        // index_list[i] for i < points_end is a point-index list, otherwise box-index pair/single
        int points_end = -1;

        // box_to_index[root_index] is the root node of the tree
        int root_index = -1;





        void build_top_down(bool bSorted)
        {
            // build list of valid elements. We skip any
            // elements that have infinite/garbage positoins...
            int i = 0;
            int N = points.VertexCount;
            int[] valid_indices = new int[N];
            Vector3d[] valid_points = new Vector3d[N];
            foreach ( int vi in points.VertexIndices()) {
                Vector3d pt = points.GetVertex(vi);
                double d2 = pt.LengthSquared;
                bool bInvalid = double.IsNaN(d2) || double.IsInfinity(d2);
                Debug.Assert(bInvalid == false);
                if (bInvalid == false) {
                    valid_indices[i] = vi;
                    valid_points[i] = pt;
                    i++;
                } // otherwise skip this element
            }

            boxes_set leafs = new boxes_set();
            boxes_set nodes = new boxes_set();
            AxisAlignedBox3d rootBox;
            int rootnode = (bSorted) ?
                split_point_set_sorted(valid_indices, valid_points, 0, N, 0, LeafMaxPointCount, leafs, nodes, out rootBox)
                : split_point_set_midpoint(valid_indices, valid_points, 0, N, 0, LeafMaxPointCount, leafs, nodes, out rootBox);

            box_to_index = leafs.box_to_index;
            box_centers = leafs.box_centers;
            box_extents = leafs.box_extents;
            index_list = leafs.index_list;
            points_end = leafs.iIndicesCur;
            int iIndexShift = points_end;
            int iBoxShift = leafs.iBoxCur;

            // ok now append internal node boxes & index ptrs
            for ( i = 0; i < nodes.iBoxCur; ++i ) {
                box_centers.insert(nodes.box_centers[i], iBoxShift + i);
                box_extents.insert(nodes.box_extents[i], iBoxShift + i);
                // internal node indices are shifted
                box_to_index.insert(iIndexShift + nodes.box_to_index[i], iBoxShift + i);
            }

            // now append index list
            for ( i = 0; i < nodes.iIndicesCur; ++i ) {
                int child_box = nodes.index_list[i];
                if ( child_box < 0 ) {        // this is a points box
                    child_box = (-child_box) - 1;
                } else {
                    child_box += iBoxShift;
                }
                child_box = child_box + 1;
                index_list.insert(child_box, iIndexShift + i);
            }

            root_index = rootnode + iBoxShift;
        }



        class AxisComp : IComparer<Vector3d>
        {
            public int Axis = 0;
            // Compares by Height, Length, and Width.
            public int Compare(Vector3d a, Vector3d b) {
                return a[Axis].CompareTo(b[Axis]);
            }
        }

        // returns box id

        class boxes_set
        {
            public DVector<int> box_to_index = new DVector<int>();
            public DVector<Vector3d> box_centers = new DVector<Vector3d>();
            public DVector<Vector3d> box_extents = new DVector<Vector3d>();
            public DVector<int> index_list = new DVector<int>();
            public int iBoxCur = 0;
            public int iIndicesCur = 0;
        }

        int split_point_set_sorted(int[] pt_indices, Vector3d[] positions, 
            int iStart, int iCount, int depth, int minIndexCount,
            boxes_set leafs, boxes_set nodes, out AxisAlignedBox3d box)
        {
            box = AxisAlignedBox3d.Empty;
            int iBox = -1;

            if ( iCount < minIndexCount ) {
                // append new points box
                iBox = leafs.iBoxCur++;
                leafs.box_to_index.insert(leafs.iIndicesCur, iBox);

                leafs.index_list.insert(iCount, leafs.iIndicesCur++);
                for (int i = 0; i < iCount; ++i) {
                    leafs.index_list.insert(pt_indices[iStart+i], leafs.iIndicesCur++);
                    box.Contain(points.GetVertex(pt_indices[iStart + i]));
                }

                leafs.box_centers.insert(box.Center, iBox);
                leafs.box_extents.insert(box.Extents, iBox);
                
                return -(iBox+1);
            }

            AxisComp c = new AxisComp() { Axis = depth % 3 };
            Array.Sort(positions, pt_indices, iStart, iCount, c);
            int mid = iCount / 2;
            int n0 = mid;
            int n1 = iCount - mid;

            // create child boxes
            AxisAlignedBox3d box1;
            int child0 = split_point_set_sorted(pt_indices, positions, iStart, n0, depth + 1, minIndexCount, leafs, nodes, out box);
            int child1 = split_point_set_sorted(pt_indices, positions, iStart+mid, n1, depth + 1, minIndexCount, leafs, nodes, out box1);
            box.Contain(box1);

            // append new box
            iBox = nodes.iBoxCur++;
            nodes.box_to_index.insert(nodes.iIndicesCur, iBox);

            nodes.index_list.insert(child0, nodes.iIndicesCur++);
            nodes.index_list.insert(child1, nodes.iIndicesCur++);

            nodes.box_centers.insert(box.Center, iBox);
            nodes.box_extents.insert(box.Extents, iBox);

            return iBox;
        }








        int split_point_set_midpoint(int[] pt_indices, Vector3d[] positions, 
            int iStart, int iCount, int depth, int minIndexCount,
            boxes_set leafs, boxes_set nodes, out AxisAlignedBox3d box)
        {
            box = AxisAlignedBox3d.Empty;
            int iBox = -1;

            if ( iCount < minIndexCount ) {
                // append new points box
                iBox = leafs.iBoxCur++;
                leafs.box_to_index.insert(leafs.iIndicesCur, iBox);

                leafs.index_list.insert(iCount, leafs.iIndicesCur++);
                for (int i = 0; i < iCount; ++i) {
                    leafs.index_list.insert(pt_indices[iStart+i], leafs.iIndicesCur++);
                    box.Contain(points.GetVertex(pt_indices[iStart + i]));
                }

                leafs.box_centers.insert(box.Center, iBox);
                leafs.box_extents.insert(box.Extents, iBox);
                
                return -(iBox+1);
            }

            //compute interval along an axis and find midpoint
            int axis = depth % 3;
            Interval1d interval = Interval1d.Empty;
            for ( int i = 0; i < iCount; ++i ) 
                interval.Contain(positions[iStart + i][axis]);
            double midpoint = interval.Center;

            int n0, n1;
            if (Math.Abs(interval.a - interval.b) > MathUtil.ZeroTolerance) {
                // we have to re-sort the centers & indices lists so that centers < midpoint
                // are first, so that we can recurse on the two subsets. We walk in from each side,
                // until we find two out-of-order locations, then we swap them.
                int l = 0;
                int r = iCount - 1;
                while (l < r) {
                    // [RMS] is <= right here? if v.axis == midpoint, then this loop
                    //   can get stuck unless one of these has an equality test. But
                    //   I did not think enough about if this is the right thing to do...
                    while (positions[iStart + l][axis] <= midpoint)
                        l++;
                    while (positions[iStart + r][axis] > midpoint)
                        r--;
                    if (l >= r)
                        break;      //done!
                    //swap
                    Vector3d tmpc = positions[iStart + l]; positions[iStart + l] = positions[iStart + r];  positions[iStart + r] = tmpc;
                    int tmpt = pt_indices[iStart + l]; pt_indices[iStart + l] = pt_indices[iStart + r]; pt_indices[iStart + r] = tmpt;
                }

                n0 = l;
                n1 = iCount - n0;
                Debug.Assert(n0 >= 1 && n1 >= 1);
            } else {
                // interval is near-empty, so no point trying to do sorting, just split half and half
                n0 = iCount / 2;
                n1 = iCount - n0;
            }

            // create child boxes
            AxisAlignedBox3d box1;
            int child0 = split_point_set_midpoint(pt_indices, positions, iStart, n0, depth + 1, minIndexCount, leafs, nodes, out box);
            int child1 = split_point_set_midpoint(pt_indices, positions, iStart+n0, n1, depth + 1, minIndexCount, leafs, nodes, out box1);
            box.Contain(box1);

            // append new box
            iBox = nodes.iBoxCur++;
            nodes.box_to_index.insert(nodes.iIndicesCur, iBox);

            nodes.index_list.insert(child0, nodes.iIndicesCur++);
            nodes.index_list.insert(child1, nodes.iIndicesCur++);

            nodes.box_centers.insert(box.Center, iBox);
            nodes.box_extents.insert(box.Extents, iBox);

            return iBox;
        }


        const double box_eps = 50.0 * MathUtil.Epsilon;


        AxisAlignedBox3d get_box(int iBox)
        {
            Vector3d c = box_centers[iBox];
            Vector3d e = box_extents[iBox];
            return new AxisAlignedBox3d(ref c, e.x, e.y, e.z);
        }


        double box_distance_sqr(int iBox, ref Vector3d p)
        {
            Vector3d c = box_centers[iBox];
            Vector3d e = box_extents[iBox];
            double dx = Math.Abs(p.x - c.x);
            dx = (dx < e.x) ? 0 : (dx - e.x);
            double dy = Math.Abs(p.y - c.y);
            dy = (dy < e.y) ? 0 : (dy - e.y);
            double dz = Math.Abs(p.z - c.z);
            dz = (dz < e.z) ? 0 : (dz - e.z);
            double d2 = dx * dx + dy * dy + dz * dz;
            return d2;
        }


        protected bool box_contains(int iBox, Vector3d p)
        {
            // [TODO] this could be way faster...
            Vector3d c = (Vector3d)box_centers[iBox];
            Vector3d e = box_extents[iBox];
            AxisAlignedBox3d box = new AxisAlignedBox3d(ref c, e.x + box_eps, e.y + box_eps, e.z + box_eps);
            return box.Contains(p);
        }



        // 1) make sure we can reach every point through tree (also demo of how to traverse tree...)
        // 2) make sure that points are contained in parent boxes
        public void TestCoverage()
        {
            int[] point_counts = new int[points.MaxVertexID];
            Array.Clear(point_counts, 0, point_counts.Length);
            int[] parent_indices = new int[box_to_index.Length];
            Array.Clear(parent_indices, 0, parent_indices.Length);

            test_coverage(point_counts, parent_indices, root_index);

            foreach (int ti in points.VertexIndices())
                if (point_counts[ti] != 1)
                    Util.gBreakToDebugger();
        }


        // accumulate point counts and track each box-parent index. 
        // also checks that points are contained in boxes
        private void test_coverage(int[] point_counts, int[] parent_indices, int iBox)
        {
            int idx = box_to_index[iBox];

            debug_check_child_points_in_box(iBox);

            if ( idx < points_end ) {
                // point-list case, array is [N t1 t2 ... tN]
                int n = index_list[idx];
                AxisAlignedBox3d box = get_box(iBox);
                for ( int i = 1; i <= n; ++i ) {
                    int vi = index_list[idx + i];
                    point_counts[vi]++;
                    Vector3d v = points.GetVertex(vi);
                    if (!box.Contains(v))
                        Util.gBreakToDebugger();
                }

            } else {
                int i0 = index_list[idx];
                if ( i0 < 0 ) {
                    // negative index means we only have one 'child' box to descend into
                    i0 = (-i0) - 1;
                    parent_indices[i0] = iBox;
                    test_coverage(point_counts, parent_indices, i0);
                } else {
                    // positive index, two sequential child box indices to descend into
                    i0 = i0 - 1;
                    parent_indices[i0] = iBox;
                    test_coverage(point_counts, parent_indices, i0);
                    int i1 = index_list[idx + 1];
                    i1 = i1 - 1;
                    parent_indices[i1] = iBox;
                    test_coverage(point_counts, parent_indices, i1);
                }
            }
        }
        // do full tree traversal below iBox and make sure that all points are further
        // than box-distance-sqr
        void debug_check_child_point_distances(int iBox, Vector3d p)
        {
            double fBoxDistSqr = box_distance_sqr(iBox, ref p);

            TreeTraversal t = new TreeTraversal() {
                NextPointF = (vID) => {
                    Vector3d v = points.GetVertex(vID);
                    double fDistSqr = p.DistanceSquared(v);
                    if (fDistSqr < fBoxDistSqr)
                        if ( Math.Abs(fDistSqr - fBoxDistSqr) > MathUtil.ZeroTolerance*100 )
                            Util.gBreakToDebugger();
                }
            };
            tree_traversal(iBox, 0, t);
        }

        // do full tree traversal below iBox to make sure that all child points are contained
        void debug_check_child_points_in_box(int iBox)
        {
            AxisAlignedBox3d box = get_box(iBox);
            TreeTraversal t = new TreeTraversal() {
                NextPointF = (vID) => {
                    Vector3d v = points.GetVertex(vID);
                    if (box.Contains(v) == false)
                        Util.gBreakToDebugger();
                }
            };
            tree_traversal(iBox, 0, t);
        }



    }
}
