using System;
using System.Collections;
using System.Collections.Generic;


namespace g3
{
    /*
     * Utility generic iterators
     */

    /// <summary>
    /// Iterator that just returns a constant value N times
    /// </summary>
    public class ConstantItr<T> : IEnumerable<T>
    {
        public T ConstantValue = default(T);
        public int N;

        public ConstantItr(int count, T constant) {
            N = count; ConstantValue = constant;
        }
        public IEnumerator<T> GetEnumerator() {
            for (int i = 0; i < N; ++i)
                yield return ConstantValue;
        }
        IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
    }


    /// <summary>
    /// Iterator that re-maps iterated values via a Func
    /// </summary>
    public class RemapItr<T,T2> : IEnumerable<T>
    {
        public IEnumerable<T2> OtherItr;
        public Func<T2, T> ValueF;

        public RemapItr(IEnumerable<T2> otherIterator, Func<T2,T> valueFunction)
        {
            OtherItr = otherIterator; ValueF = valueFunction;
        }
        public IEnumerator<T> GetEnumerator() {
            foreach (T2 idx in OtherItr) 
                yield return ValueF(idx);
        }
        IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
    }



    /// <summary>
    /// IList wrapper that remaps values via a Func (eg for index maps)
    /// </summary>
    public class MappedList : IList<int>
    {
        public IList<int> BaseList;
        public Func<int, int> MapF = (i) => { return i; };

        public MappedList(IList<int> list, int[] map)
        {
            BaseList = list;
            MapF = (v) => { return map[v]; };
        }

        public int this[int index] {
            get { return MapF(BaseList[index]); }
            set { throw new NotImplementedException(); }
        }
        public int Count { get { return BaseList.Count; } }
        public bool IsReadOnly { get { return true; } }

        public void Add(int item) { throw new NotImplementedException(); }
        public void Clear() { throw new NotImplementedException(); }
        public void Insert(int index, int item) { throw new NotImplementedException(); }
        public bool Remove(int item) { throw new NotImplementedException(); }
        public void RemoveAt(int index) { throw new NotImplementedException(); }

        // could be implemented...
        public bool Contains(int item) { throw new NotImplementedException(); }
        public int IndexOf(int item) { throw new NotImplementedException(); }
        public void CopyTo(int[] array, int arrayIndex) { throw new NotImplementedException(); }

        public IEnumerator<int> GetEnumerator() {
            int N = BaseList.Count;
            for (int i = 0; i < N; ++i)
                yield return MapF(BaseList[i]);
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }


    

    /// <summary>
    /// IList wrapper for an Interval1i, ie sequential list of integers
    /// </summary>
    public struct IntSequence : IList<int>
    {
        Interval1i range;

        public IntSequence(Interval1i ival) {
            range = ival;
        }
        public IntSequence(int iStart, int iEnd) {
            range = new Interval1i(iStart, iEnd);
        }

        /// <summary> construct interval [0, N-1] </summary>
        static public IntSequence Range(int N) { return new IntSequence(0, N - 1); }

        /// <summary> construct interval [0, N-1] </summary>
        static public IntSequence RangeInclusive(int N) { return new IntSequence(0, N); }

        /// <summary> construct interval [start, start+N-1] </summary>
        static public IntSequence Range(int start, int N) { return new IntSequence(start, start + N - 1); }


        /// <summary> construct interval [a, b] </summary>
        static public IntSequence FromToInclusive(int a, int b) { return new IntSequence(a, b); }

        public int this[int index] {
            get { return range.a + index; }
            set { throw new NotImplementedException(); }
        }
        public int Count { get { return range.Length+1; } }
        public bool IsReadOnly { get { return true; } }

        public void Add(int item) { throw new NotImplementedException(); }
        public void Clear() { throw new NotImplementedException(); }
        public void Insert(int index, int item) { throw new NotImplementedException(); }
        public bool Remove(int item) { throw new NotImplementedException(); }
        public void RemoveAt(int index) { throw new NotImplementedException(); }

        // could be implemented...
        public bool Contains(int item) { return range.Contains(item); }
        public int IndexOf(int item) { throw new NotImplementedException(); }
        public void CopyTo(int[] array, int arrayIndex) { throw new NotImplementedException(); }

        public IEnumerator<int> GetEnumerator() {
            return range.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }
    }





}
