using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    /// <summary>
    /// SparseList provides a linear-indexing interface, but internally may use an
    /// alternate data structure to store the [index,value] pairs, if the list
    /// is very sparse. 
    /// 
    /// Currently uses Dictionary<> as sparse data structure
    /// </summary>
    public class SparseList<T> where T : IComparable<T>
    {
        T[] dense;
        Dictionary<int, T> sparse;
        T zeroValue;

        public SparseList(int MaxIndex, int SubsetCountEst, T ZeroValue)
        {
            zeroValue = ZeroValue;

            bool bSmall = MaxIndex < 1024;        // 16k in bits is a pretty small buffer?
            float fPercent = (float)SubsetCountEst / (float)MaxIndex;
            float fPercentThresh = 0.1f;

            if (bSmall || fPercent > fPercentThresh) {
                dense = new T[MaxIndex];
                for (int k = 0; k < MaxIndex; ++k)
                    dense[k] = ZeroValue;
            } else
                sparse = new Dictionary<int, T>();
        }


        public T this[int idx]
        {
            get {
                if (dense != null)
                    return dense[idx];
                T val;
                if (sparse.TryGetValue(idx, out val))
                    return val;
                return zeroValue;
            }
            set {
                if (dense != null) {
                    dense[idx] = value;
                } else {
                    sparse.Add(idx, value);
                }
            }
        }


        public int Count(Func<T,bool> CountF)
        {
            int count = 0;
            if ( dense != null ) {
                for (int i = 0; i < dense.Length; ++i)
                    if (CountF(dense[i]))
                        count++;
            } else {
                foreach (var v in sparse) {
                    if (CountF(v.Value))
                        count++;
                }
            }
            return count;
        }


        /// <summary>
        /// This enumeration will return pairs [index,0] for dense case
        /// </summary>
        public IEnumerable<KeyValuePair<int,T>> Values()
        {
            if ( dense != null ) {
                for (int i = 0; i < dense.Length; ++i)
                    yield return new KeyValuePair<int, T>(i, dense[i]);
            } else {
                foreach (var v in sparse)
                    yield return v;
            }
        }


        public IEnumerable<KeyValuePair<int,T>> NonZeroValues()
        {
            if ( dense != null ) {
                for (int i = 0; i < dense.Length; ++i) {
                    if ( dense[i].CompareTo(zeroValue) != 0 )
                        yield return new KeyValuePair<int, T>(i, dense[i]);
                }
            } else {
                foreach (var v in sparse)
                    yield return v;
            }
        }



    }
}
