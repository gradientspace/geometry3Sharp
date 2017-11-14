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
    public class SparseList<T>  where T : IEquatable<T>
    {
        T[] dense;
        Dictionary<int, T> sparse;
        T zeroValue;

        public SparseList(int MaxIndex, int SubsetCountEst, T ZeroValue)
        {
            zeroValue = ZeroValue;

            bool bSmall = MaxIndex > 0 && MaxIndex < 1024;
            float fPercent = (MaxIndex == 0) ? 0 : (float)SubsetCountEst / (float)MaxIndex;
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
                    sparse[idx] = value;
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
                    if ( dense[i].Equals(zeroValue) == false )
                        yield return new KeyValuePair<int, T>(i, dense[i]);
                }
            } else {
                foreach (var v in sparse)
                    yield return v;
            }
        }
    }










    /// <summary>
    /// variant of SparseList for class objects, then "zero" is null
    /// 
    /// TODO: can we combine these classes somehow?
    /// </summary>
    public class SparseObjectList<T>  where T : class
    {
        T[] dense;
        Dictionary<int, T> sparse;

        public SparseObjectList(int MaxIndex, int SubsetCountEst)
        {
            bool bSmall = MaxIndex < 1024;
            float fPercent = (float)SubsetCountEst / (float)MaxIndex;
            float fPercentThresh = 0.1f;

            if (bSmall || fPercent > fPercentThresh) {
                dense = new T[MaxIndex];
                for (int k = 0; k < MaxIndex; ++k)
                    dense[k] = null;
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
                return null;
            }
            set {
                if (dense != null) {
                    dense[idx] = value;
                } else {
                    sparse[idx] = value;
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
                    if ( dense[i] != null )
                        yield return new KeyValuePair<int, T>(i, dense[i]);
                }
            } else {
                foreach (var v in sparse)
                    yield return v;
            }
        }


        public void Clear()
        {
            if (dense != null) {
                Array.Clear(dense, 0, dense.Length);
            } else {
                sparse.Clear();
            }           
        }

    }







}
