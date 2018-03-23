using System;
using System.Collections.Generic;
using System.Threading;

namespace g3
{


    /// <summary>
    /// Hash Grid for 2D segments. You provide the 'segment' type. If you have an indexable
    /// set of segments this can just be int, or can be more complex segment data structure
    /// (but be careful w/ structs...)
    /// 
    /// Segments are stored in the grid cell that contains the segment center. We keep track
    /// of the extent of the *longest* segment that has been added. The search radius for
    /// distance queries is expanded by this extent. 
    /// 
    /// So, distance queries **ARE NOT EFFICIENT** if you even one very long segment.
    /// [TODO] make a multi-level hash
    /// 
    /// Does not actually store 2D segments. So, to remove a segment
    /// you must also know it's 2D center, so we can look up the cell coordinates.
    /// Hence, to 'update' a segment, you need to know both it's old and new 2D centers.
    /// </summary>
    public class SegmentHashGrid2d<T>
    {
        Dictionary<Vector2i, List<T>> Hash;
        ScaleGridIndexer2 Indexer;
        double MaxExtent;
        T invalidValue;

        SpinLock spinlock;

        /// <summary>
        /// "invalid" value will be returned by queries if no valid result is found (eg bounded-distance query)
        /// </summary>
        public SegmentHashGrid2d(double cellSize, T invalidValue)
        {
            Hash = new Dictionary<Vector2i, List<T>>();
            Indexer = new ScaleGridIndexer2() { CellSize = cellSize };
            MaxExtent = 0;
            spinlock = new SpinLock();
            this.invalidValue = invalidValue;
        }


        /// <summary>
        /// Insert segment at position. This function is thread-safe, uses a SpinLock internally
        /// </summary>
        public void InsertSegment(T value, Vector2d center, double extent)
        {
            Vector2i idx = Indexer.ToGrid(center);
            if (extent > MaxExtent)
                MaxExtent = extent;
            insert_segment(value, idx);
        }

        /// <summary>
        /// Insert segment without locking / thread-safety
        /// </summary>
        public void InsertSegmentUnsafe(T value, Vector2d center, double extent)
        {
            Vector2i idx = Indexer.ToGrid(center);
            if (extent > MaxExtent)
                MaxExtent = extent;
            insert_segment(value, idx, false);
        }


        /// <summary>
        /// Remove segment. This function is thread-safe, uses a SpinLock internally
        /// </summary>
        public bool RemoveSegment(T value, Vector2d center)
        {
            Vector2i idx = Indexer.ToGrid(center);
            return remove_segment(value, idx);
        }

        /// <summary>
        /// Remove segment without locking / thread-safety
        /// </summary>
        public bool RemoveSegmentUnsafe(T value, Vector2d center)
        {
            Vector2i idx = Indexer.ToGrid(center);
            return remove_segment(value, idx, false);
        }


        /// <summary>
        /// Move segment from old to new position. This function is thread-safe, uses a SpinLock internally
        /// </summary>
        public void UpdateSegment(T value, Vector2d old_center, Vector2d new_center, double new_extent)
        {
            if (new_extent > MaxExtent)
                MaxExtent = new_extent;
            Vector2i old_idx = Indexer.ToGrid(old_center);
            Vector2i new_idx = Indexer.ToGrid(new_center);
            if (old_idx == new_idx)
                return;
            bool ok = remove_segment(value, old_idx);
            Util.gDevAssert(ok);
            insert_segment(value, new_idx);
            return;
        }


        /// <summary>
        /// Move segment from old to new position without locking / thread-safety
        /// </summary>
        public void UpdateSegmentUnsafe(T value, Vector2d old_center, Vector2d new_center, double new_extent)
        {
            if (new_extent > MaxExtent)
                MaxExtent = new_extent;
            Vector2i old_idx = Indexer.ToGrid(old_center);
            Vector2i new_idx = Indexer.ToGrid(new_center);
            if (old_idx == new_idx)
                return;
            bool ok = remove_segment(value, old_idx, false);
            Util.gDevAssert(ok);
            insert_segment(value, new_idx, false);
            return;
        }


        /// <summary>
        /// Find nearest segment in grid, within radius, without locking / thread-safety
        /// You must provided distF which returns distance between query_pt and the segment argument
        /// You can ignore specific segments via ignoreF lambda - return true to ignore 
        /// Return value is pair (nearest_index,min_dist) or (invalidValue,double.MaxValue)
        /// </summary>
        public KeyValuePair<T, double> FindNearestInRadius(Vector2d query_pt, double radius, Func<T, double> distF, Func<T, bool> ignoreF = null)
        {
            double search_dist = radius + MaxExtent;
            Vector2i min_idx = Indexer.ToGrid(query_pt - search_dist * Vector2d.One);
            Vector2i max_idx = Indexer.ToGrid(query_pt + search_dist * Vector2d.One);

            double min_dist = double.MaxValue;
            T nearest = invalidValue;

            if (ignoreF == null)
                ignoreF = (pt) => { return false; };

            for (int yi = min_idx.y; yi <= max_idx.y; yi++) {
                for (int xi = min_idx.x; xi <= max_idx.x; xi++) {
                    Vector2i idx = new Vector2i(xi, yi);
                    List<T> values;
                    if (Hash.TryGetValue(idx, out values) == false)
                        continue;
                    foreach (T value in values) {
                        if (ignoreF(value))
                            continue;
                        double dist = distF(value);
                        if (dist < radius && dist < min_dist) {
                            nearest = value;
                            min_dist = dist;
                        }
                    }
                }
            }

            return new KeyValuePair<T, double>(nearest, min_dist);
        }





        /// <summary>
        /// Variant of FindNearestInRadius that works with squared-distances.
        /// Return value is pair (nearest_index,min_dist) or (invalidValue,double.MaxValue)
        /// </summary>
        public KeyValuePair<T, double> FindNearestInSquaredRadius(Vector2d query_pt, double radiusSqr, Func<T, double> distSqrF, Func<T, bool> ignoreF = null)
        {
            double search_dist = Math.Sqrt(radiusSqr) + MaxExtent;
            Vector2i min_idx = Indexer.ToGrid(query_pt - search_dist * Vector2d.One);
            Vector2i max_idx = Indexer.ToGrid(query_pt + search_dist * Vector2d.One);

            double min_dist_sqr = double.MaxValue;
            T nearest = invalidValue;

            if (ignoreF == null)
                ignoreF = (pt) => { return false; };

            for (int yi = min_idx.y; yi <= max_idx.y; yi++) {
                for (int xi = min_idx.x; xi <= max_idx.x; xi++) {
                    Vector2i idx = new Vector2i(xi, yi);
                    List<T> values;
                    if (Hash.TryGetValue(idx, out values) == false)
                        continue;
                    foreach (T value in values) {
                        if (ignoreF(value))
                            continue;
                        double distSqr = distSqrF(value);
                        if (distSqr < radiusSqr && distSqr < min_dist_sqr) {
                            nearest = value;
                            min_dist_sqr = distSqr;
                        }
                    }
                }
            }

            return new KeyValuePair<T, double>(nearest, min_dist_sqr);
        }




        void insert_segment(T value, Vector2i idx, bool threadsafe = true)
        {
            bool lockTaken = false;
            while (threadsafe == true && lockTaken == false)
                spinlock.Enter(ref lockTaken);

            List<T> values;
            if (Hash.TryGetValue(idx, out values)) {
                values.Add(value);
            } else {
                Hash[idx] = new List<T>() { value };
            }

            if (lockTaken)
                spinlock.Exit();
        }


        bool remove_segment(T value, Vector2i idx, bool threadsafe = true)
        {
            bool lockTaken = false;
            while (threadsafe == true && lockTaken == false)
                spinlock.Enter(ref lockTaken);

            List<T> values;
            bool result = false;
            if (Hash.TryGetValue(idx, out values)) {
                result = values.Remove(value);
            }

            if (lockTaken)
                spinlock.Exit();
            return result;
        }
    }
}
