using System;
using System.Collections.Generic;
using System.Threading;

namespace g3
{


    /// <summary>
    /// Hash Grid for 2D points. You provide the 'point' type. If you have an indexable
    /// set of points this can just be int, or can be more complex point data structure
    /// (but be careful w/ structs...)
    /// 
    /// Does not actually store 2D points. So, to remove a point
    /// you must also know it's 2D coordinate, so we can look up the cell coordinates.
    /// Hence, to 'update' a point, you need to know both it's old and new 2D coordinates.
    /// </summary>
    public class PointHashGrid2d<T>
    {
        Dictionary<Vector2i, List<T>> Hash;
        ScaleGridIndexer2 Indexer;
        T invalidValue;

        SpinLock spinlock;

        /// <summary>
        /// "invalid" value will be returned by queries if no valid result is found (eg bounded-distance query)
        /// </summary>
        public PointHashGrid2d(double cellSize, T invalidValue)
        {
            Hash = new Dictionary<Vector2i, List<T>>();
            Indexer = new ScaleGridIndexer2() { CellSize = cellSize };
            spinlock = new SpinLock();
            this.invalidValue = invalidValue;
        }

        public T InvalidValue {
            get { return invalidValue; }
        }

        /// <summary>
        /// Insert point at position. This function is thread-safe, uses a SpinLock internally
        /// </summary>
        public void InsertPoint(T value, Vector2d pos)
        {
            Vector2i idx = Indexer.ToGrid(pos);
            insert_point(value, idx);
        }

        /// <summary>
        /// Insert point without locking / thread-safety
        /// </summary>
        public void InsertPointUnsafe(T value, Vector2d pos)
        {
            Vector2i idx = Indexer.ToGrid(pos);
            insert_point(value, idx, false);
        }


        /// <summary>
        /// Insert point. This function is thread-safe, uses a SpinLock internally
        /// </summary>
        public bool RemovePoint(T value, Vector2d pos)
        {
            Vector2i idx = Indexer.ToGrid(pos);
            return remove_point(value, idx);
        }

        /// <summary>
        /// Remove point without locking / thread-safety
        /// </summary>
        public bool RemovePointUnsafe(T value, Vector2d pos)
        {
            Vector2i idx = Indexer.ToGrid(pos);
            return remove_point(value, idx, false);
        }


        /// <summary>
        /// Move point from old to new position. This function is thread-safe, uses a SpinLock internally
        /// </summary>
        public void UpdatePoint(T value, Vector2d old_pos, Vector2d new_pos)
        {
            Vector2i old_idx = Indexer.ToGrid(old_pos);
            Vector2i new_idx = Indexer.ToGrid(new_pos);
            if (old_idx == new_idx)
                return;
            bool ok = remove_point(value, old_idx);
            Util.gDevAssert(ok);
            insert_point(value, new_idx);
            return;
        }


        /// <summary>
        /// Move point from old to new position without locking / thread-safety
        /// </summary>
        public void UpdatePointUnsafe(T value, Vector2d old_pos, Vector2d new_pos)
        {
            Vector2i old_idx = Indexer.ToGrid(old_pos);
            Vector2i new_idx = Indexer.ToGrid(new_pos);
            if (old_idx == new_idx)
                return;
            bool ok = remove_point(value, old_idx, false);
            Util.gDevAssert(ok);
            insert_point(value, new_idx, false);
            return;
        }


        /// <summary>
        /// Find nearest point in grid, within radius, without locking / thread-safety
        /// You must provided distF which returns distance between query_pt and the point argument
        /// You can ignore specific points via ignoreF lambda
        /// </summary>
        public KeyValuePair<T, double> FindNearestInRadius(Vector2d query_pt, double radius, Func<T, double> distF, Func<T, bool> ignoreF = null)
        {
            Vector2i min_idx = Indexer.ToGrid(query_pt - radius * Vector2d.One);
            Vector2i max_idx = Indexer.ToGrid(query_pt + radius * Vector2d.One);

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





        void insert_point(T value, Vector2i idx, bool threadsafe = true)
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


        bool remove_point(T value, Vector2i idx, bool threadsafe = true)
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
