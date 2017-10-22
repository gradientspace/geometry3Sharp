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


}
