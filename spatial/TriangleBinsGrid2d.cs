using System;
using System.Collections.Generic;
using System.Threading;

namespace g3
{


    /// <summary>
    /// This class is a spatial data structure for 2D triangles. It is intended
    /// for point-containment and box-overlap queries. It does not store the
    /// triangles, only indices, so you must pass in the triangle vertices to add/remove
    /// functions, similar to PointHashGrid2d.
    /// 
    /// However, unlike the hash classes, this one is based on a grid of "bins" which 
    /// has a fixed size, so you must provide a bounding box on construction. 
    /// Each triangle is inserted into every bin that it overlaps. 
    /// 
    /// [TODO] currently each triangle is inserted into every bin that it's *bounding box*
    /// overlaps. Need conservative rasterization to improve this. Can implement by
    /// testing each bin bbox for intersection w/ triangle
    /// </summary>
    public class TriangleBinsGrid2d
    {
        ShiftGridIndexer2 indexer;
        AxisAlignedBox2d bounds;

        SmallListSet bins_list;
        int bins_x, bins_y;
        AxisAlignedBox2i grid_bounds;

        SpinLock spinlock = new SpinLock();

        /// <summary>
        /// "invalid" value will be returned by queries if no valid result is found (eg bounded-distance query)
        /// </summary>
        public TriangleBinsGrid2d(AxisAlignedBox2d bounds, int numCells) 
        {
            this.bounds = bounds;
            double cellsize = bounds.MaxDim / (double)numCells;
            Vector2d origin = bounds.Min - cellsize * 0.5 * Vector2d.One;
            indexer = new ShiftGridIndexer2(origin, cellsize);

            bins_x = (int)(bounds.Width / cellsize) + 2;
            bins_y = (int)(bounds.Height / cellsize) + 2;
            grid_bounds = new AxisAlignedBox2i(0, 0, bins_x-1, bins_y-1);
            bins_list = new SmallListSet();
            bins_list.Resize(bins_x * bins_y);
        }


        public AxisAlignedBox2d Bounds {
            get { return bounds; }
        }

        /// <summary>
        /// Insert triangle. This function is thread-safe, uses a SpinLock internally
        /// </summary>
        public void InsertTriangle(int triangle_id, ref Vector2d a, ref Vector2d b, ref Vector2d c)
        {
            insert_triangle(triangle_id, ref a, ref b, ref c, true);
        }

        /// <summary>
        /// Insert triangle without locking / thread-safety
        /// </summary>
        public void InsertTriangleUnsafe(int triangle_id, ref Vector2d a, ref Vector2d b, ref Vector2d c)
        {
            insert_triangle(triangle_id, ref a, ref b, ref c, false);
        }


        /// <summary>
        /// Remove triangle. This function is thread-safe, uses a SpinLock internally
        /// </summary>
        public void RemoveTriangle(int triangle_id, ref Vector2d a, ref Vector2d b, ref Vector2d c)
        {
            remove_triangle(triangle_id, ref a, ref b, ref c, true);
        }

        /// <summary>
        /// Remove triangle without locking / thread-safety
        /// </summary>
        public void RemoveTriangleUnsafe(int triangle_id, ref Vector2d a, ref Vector2d b, ref Vector2d c)
        {
            remove_triangle(triangle_id, ref a, ref b, ref c, false);
        }


        /// <summary>
        /// Find triangle that contains point. Not thread-safe.
        /// You provide containsF(), which does the containment check.
        /// If you provide ignoreF(), then tri is skipped if ignoreF(tid) == true
        /// </summary>
        public int FindContainingTriangle(Vector2d query_pt, Func<int, Vector2d, bool> containsF, Func<int, bool> ignoreF = null)
        {
            Vector2i grid_idx = indexer.ToGrid(query_pt);
            if (grid_bounds.Contains(grid_idx) == false)
                return DMesh3.InvalidID; ;

            int bin_i = grid_idx.y * bins_x + grid_idx.x;
            if (ignoreF == null) {
                foreach (int tid in bins_list.ValueItr(bin_i)) {
                    if (containsF(tid, query_pt))
                        return tid;
                }
            } else {
                foreach (int tid in bins_list.ValueItr(bin_i)) {
                    if (ignoreF(tid) == false && containsF(tid, query_pt))
                        return tid;
                }
            }

            return DMesh3.InvalidID;
        }




        /// <summary>
        /// find all triangles that overlap range
        /// </summary>
        public void FindTrianglesInRange(AxisAlignedBox2d range, HashSet<int> triangles)
        {
            Vector2i grid_min = indexer.ToGrid(range.Min);
            if (grid_bounds.Contains(grid_min) == false)
                throw new Exception("TriangleBinsGrid2d.FindTrianglesInRange: range.Min is out of bounds");
            Vector2i grid_max = indexer.ToGrid(range.Max);
            if (grid_bounds.Contains(grid_max) == false)
                throw new Exception("TriangleBinsGrid2d.FindTrianglesInRange: range.Max is out of bounds");

            for (int yi = grid_min.y; yi <= grid_max.y; ++yi) {
                for (int xi = grid_min.x; xi <= grid_max.x; ++xi) {
                    int bin_i = yi * bins_x + xi;
                    foreach (int tid in bins_list.ValueItr(bin_i))
                        triangles.Add(tid);
                }
            }

        }





        void insert_triangle(int triangle_id, ref Vector2d a, ref Vector2d b, ref Vector2d c, bool threadsafe = true)
        {
            bool lockTaken = false;
            while (threadsafe == true && lockTaken == false)
                spinlock.Enter(ref lockTaken);

            // [TODO] actually want to conservatively rasterize triangles here, not just
            // store in every cell in bbox!

            AxisAlignedBox2d bounds = BoundsUtil.Bounds(ref a, ref b, ref c);
            Vector2i imin = indexer.ToGrid(bounds.Min);
            Vector2i imax = indexer.ToGrid(bounds.Max);

            for ( int yi = imin.y; yi <= imax.y; ++yi ) {
                for (int xi = imin.x; xi <= imax.x; ++xi) {

                    // check if triangle overlaps this grid cell...

                    int bin_i = yi * bins_x + xi;
                    bins_list.Insert(bin_i, triangle_id);
                }
            }

            if (lockTaken)
                spinlock.Exit();
        }


        void remove_triangle(int triangle_id, ref Vector2d a, ref Vector2d b, ref Vector2d c, bool threadsafe = true)
        {
            bool lockTaken = false;
            while (threadsafe == true && lockTaken == false)
                spinlock.Enter(ref lockTaken);

            AxisAlignedBox2d bounds = BoundsUtil.Bounds(ref a, ref b, ref c);
            Vector2i imin = indexer.ToGrid(bounds.Min);
            Vector2i imax = indexer.ToGrid(bounds.Max);
            for (int yi = imin.y; yi <= imax.y; ++yi) {
                for (int xi = imin.x; xi <= imax.x; ++xi) {
                    int bin_i = yi * bins_x + xi;
                    bins_list.Remove(bin_i, triangle_id);
                }
            }

            if (lockTaken)
                spinlock.Exit();
        }
    }
}
