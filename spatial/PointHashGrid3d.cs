using System;
using System.Collections.Generic;
using System.Threading;

namespace g3
{


    /// <summary>
    /// Hash Grid for 3D points. You provide the 'point' type. If you have an indexable
    /// set of points this can just be int, or can be more complex point data structure
    /// (but be careful w/ structs...)
    /// 
    /// Does not actually store 3D points. So, to remove a point
    /// you must also know it's 3D coordinate, so we can look up the cell coordinates.
    /// Hence, to 'update' a point, you need to know both it's old and new 3D coordinates.
    /// 
    /// TODO: if a lot of points are in the same spot, this is still a disaster.
    /// What if we had a second level of hashing, where once a list at a level gets too
    /// big, we build a sub-hash there?
    /// 
    /// </summary>
    public class PointHashGrid3d<T>
    {
        Dictionary<Vector3i, List<T>> Hash;
        ScaleGridIndexer3 Indexer;
        T invalidValue;

        SpinLock spinlock;

        /// <summary>
        /// "invalid" value will be returned by queries if no valid result is found (eg bounded-distance query)
        /// </summary>
        public PointHashGrid3d(double cellSize, T invalidValue)
        {
            Hash = new Dictionary<Vector3i, List<T>>();
            Indexer = new ScaleGridIndexer3() { CellSize = cellSize };
            spinlock = new SpinLock();
            this.invalidValue = invalidValue;
        }

        public T InvalidValue {
            get { return invalidValue; }
        }

        /// <summary>
        /// Insert point at position. This function is thread-safe, uses a SpinLock internally
        /// </summary>
        public void InsertPoint(T value, Vector3d pos)
        {
            Vector3i idx = Indexer.ToGrid(pos);
            insert_point(value, idx);
        }

        /// <summary>
        /// Insert point without locking / thread-safety
        /// </summary>
        public void InsertPointUnsafe(T value, Vector3d pos)
        {
            Vector3i idx = Indexer.ToGrid(pos);
            insert_point(value, idx, false);
        }


        /// <summary>
        /// Insert point. This function is thread-safe, uses a SpinLock internally
        /// </summary>
        public bool RemovePoint(T value, Vector3d pos)
        {
            Vector3i idx = Indexer.ToGrid(pos);
            return remove_point(value, idx);
        }

        /// <summary>
        /// Remove point without locking / thread-safety
        /// </summary>
        public bool RemovePointUnsafe(T value, Vector3d pos)
        {
            Vector3i idx = Indexer.ToGrid(pos);
            return remove_point(value, idx, false);
        }


        /// <summary>
        /// Move point from old to new position. This function is thread-safe, uses a SpinLock internally
        /// </summary>
        public void UpdatePoint(T value, Vector3d old_pos, Vector3d new_pos)
        {
            Vector3i old_idx = Indexer.ToGrid(old_pos);
            Vector3i new_idx = Indexer.ToGrid(new_pos);
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
        public void UpdatePointUnsafe(T value, Vector3d old_pos, Vector3d new_pos)
        {
            Vector3i old_idx = Indexer.ToGrid(old_pos);
            Vector3i new_idx = Indexer.ToGrid(new_pos);
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
        /// You can ignore specific points via ignoreF lambda.
        /// returned key is InvalidValue if not found
        /// </summary>
        public KeyValuePair<T, double> FindNearestInRadius(Vector3d query_pt, double radius, Func<T, double> distF, Func<T, bool> ignoreF = null)
        {
            Vector3i min_idx = Indexer.ToGrid(query_pt - radius * Vector3d.One);
            Vector3i max_idx = Indexer.ToGrid(query_pt + radius * Vector3d.One);

            double min_dist = double.MaxValue;
            T nearest = invalidValue;

            if (ignoreF == null)
                ignoreF = (pt) => { return false; };

            for (int zi = min_idx.z; zi <= max_idx.z; zi++) {
                for (int yi = min_idx.y; yi <= max_idx.y; yi++) {
                    for (int xi = min_idx.x; xi <= max_idx.x; xi++) {
                        Vector3i idx = new Vector3i(xi, yi ,zi);
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
            }

            return new KeyValuePair<T, double>(nearest, min_dist);
        }



        void insert_point(T value, Vector3i idx, bool threadsafe = true)
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


        bool remove_point(T value, Vector3i idx, bool threadsafe = true)
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




        public void print_large_buckets()
        {
            foreach ( var entry in Hash ) {
                if (entry.Value.Count > 512)
                    System.Console.WriteLine("{0} : {1}", entry.Key, entry.Value.Count);
            }
        }


    }
}
