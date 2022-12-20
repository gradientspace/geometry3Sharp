using System;
using System.Collections;

namespace g3
{

    /// <summary>
    /// RefCountedVector is used to keep track of which indices in a linear index list are in use/referenced.
    /// A free list is tracked so that unreferenced indices can be re-used.
    ///
    /// The enumerator iterates over valid indices (ie where refcount > 0)
    /// 
    /// **refcounts are shorts** so the maximum count is 65536. 
    /// No overflow checking is done in release builds.
    /// 
    /// </summary>
    public class RefCountVector : System.Collections.IEnumerable
    {
        public static readonly short invalid = -1;

        DVector<short> ref_counts;
        DVector<int> free_indices;
        int used_count;

        public RefCountVector()
        {
            ref_counts = new DVector<short>();
            free_indices = new DVector<int>();
            used_count = 0;
        }

        public RefCountVector(RefCountVector copy)
        {
            ref_counts = new DVector<short>(copy.ref_counts);
            free_indices = new DVector<int>(copy.free_indices);
            used_count = copy.used_count;
        }

        public RefCountVector(short[] raw_ref_counts, bool build_free_list = false)
        {
            ref_counts = new DVector<short>(raw_ref_counts);
            free_indices = new DVector<int>();
            used_count = 0;
            if (build_free_list)
                rebuild_free_list();
        }

        public DVector<short> RawRefCounts {
            get { return ref_counts; }
        }

        public bool empty {
            get { return used_count == 0; }
        }
        public int count {
            get { return used_count; }
        }
        public int max_index {
            get { return ref_counts.size; }
        }
        public bool is_dense {
            get { return free_indices.Length == 0; }
        }


        public bool isValid(int index) {
            return ( index >= 0 && index < ref_counts.size && ref_counts[index] > 0 );
        }
        public bool isValidUnsafe(int index) {
            return ref_counts[index] > 0;
        }


        public int refCount(int index) {
            int n = ref_counts[index];
            return (n == invalid) ? 0 : n;
        }
        public int rawRefCount(int index) {
            return ref_counts[index];
        }


        public int allocate() {
            used_count++;
            if (free_indices.empty) {
                // [RMS] do we need this branch anymore? 
                ref_counts.push_back(1);
                return ref_counts.size - 1;
            } else {
                int iFree = invalid;
                while (iFree == invalid && free_indices.empty == false) {
                    iFree = free_indices.back;
                    free_indices.pop_back();
                }
                if (iFree != invalid) {
                    ref_counts[iFree] = 1;
                    return iFree;
                } else {
                    ref_counts.push_back(1);
                    return ref_counts.size - 1;
                }
            }
        }



        public int increment(int index, short increment = 1) {
            Util.gDevAssert( isValid(index)  );
            // debug check for overflow...
            Util.gDevAssert(  (short)(ref_counts[index] + increment) > 0 );
            ref_counts[index] += increment;
            return ref_counts[index];       
        }

        public void decrement(int index, short decrement = 1) {
            Util.gDevAssert( isValid(index) );
            ref_counts[index] -= decrement;
            Util.gDevAssert(ref_counts[index] >= 0);
            if (ref_counts[index] == 0) {
                free_indices.push_back(index);
                ref_counts[index] = invalid;
                used_count--;
            }
        }



        /// <summary>
        /// allocate at specific index, which must either be larger than current max index,
        /// or on the free list. If larger, all elements up to this one will be pushed onto
        /// free list. otherwise we have to do a linear search through free list.
        /// If you are doing many of these, it is likely faster to use 
        /// allocate_at_unsafe(), and then rebuild_free_list() after you are done.
        /// </summary>
        public bool allocate_at(int index)
        {
            if (index >= ref_counts.size) {
                int j = ref_counts.size;
                while (j < index) {
                    ref_counts.push_back(invalid);
                    free_indices.push_back(j);
                    ++j;
                }
                ref_counts.push_back(1);
                used_count++;
                return true;

            } else {
                if (ref_counts[index] > 0)
                    return false;

                int N = free_indices.size;
                for (int i = 0; i < N; ++i) {
                    if ( free_indices[i] == index ) {
                        free_indices[i] = invalid;
                        ref_counts[index] = 1;
                        used_count++;
                        return true;
                    }
                }
                return false;
            }
        }


        /// <summary>
        /// allocate at specific index, which must be free or larger than current max index.
        /// However, we do not update free list. So, you probably need to do 
        /// rebuild_free_list() after calling this.
        /// </summary>
        public bool allocate_at_unsafe(int index)
        {
            if (index >= ref_counts.size) {
                int j = ref_counts.size;
                while (j < index) {
                    ref_counts.push_back(invalid);
                    ++j;
                }
                ref_counts.push_back(1);
                used_count++;
                return true;

            } else {
                if (ref_counts[index] > 0)
                    return false;
                ref_counts[index] = 1;
                used_count++;
                return true;
            }
        }



        // [RMS] really should not use this!!
        public void set_Unsafe(int index, short count)
        {
            ref_counts[index] = count;
        }

        // todo:
        //   remove
        //   clear


        public void rebuild_free_list()
        {
            free_indices = new DVector<int>();
            used_count = 0;

            int N = ref_counts.Length;
            for ( int i = 0; i < N; ++i ) {
                if (ref_counts[i] > 0)
                    used_count++;
                else
                    free_indices.Add(i);
            }
        }


        public void trim(int maxIndex)
        {
            free_indices = new DVector<int>();
            ref_counts.resize(maxIndex);
            used_count = maxIndex;
        }




        public System.Collections.IEnumerator GetEnumerator()
        {
            int nIndex = 0;
            int nLast = max_index;

            // skip leading empties
            while (nIndex != nLast && ref_counts[nIndex] <= 0)
                nIndex++;

            while (nIndex != nLast) {
                yield return nIndex;

                if (nIndex != nLast)
                    nIndex++;
                while (nIndex != nLast && ref_counts[nIndex] <= 0)
                    nIndex++;
            }
        }


        public string UsageStats {
            get { return string.Format("RefCountSize {0}  FreeSize {1} FreeMem {2}kb", ref_counts.size, free_indices.size, free_indices.MemoryUsageBytes/1024); }
        }



        public string debug_print()
        {
            string s = string.Format("size {0} used {1} free_size {2}\n", ref_counts.size, used_count, free_indices.size);
            for (int i = 0; i < ref_counts.size; ++i)
                s += string.Format("{0}:{1} ", i, ref_counts[i]);
            s += "\nfree:\n";
            for (int i = 0; i < free_indices.size; ++i)
                s += free_indices[i].ToString() + " ";
            return s;
        }


	}
}

