using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    //
    // This class is just a wrapper around a static array that provides convenient 3-element set/get access
    // Useful for things like treating a float array as a list of vectors
    //
    public class VectorArray3<T> : IEnumerable<T>
    {
        public T[] array;

        public VectorArray3(int nCount = 0) {
            array = new T[nCount*3];
        }

        public VectorArray3(T[] data)
        {
            array = data;
        }

        public int Count {
            get { return array.Length/3; }
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < array.Length; ++i)
                yield return array[i];
        }

        public void Resize(int Count)
        {
            array = new T[3 * Count];
        }

        public void Set(int i, T a, T b, T c)
        {
            array[3 * i] = a;
            array[3 * i+1] = b;
            array[3 * i+2] = c;
        }

		public void Set(int iStart, int iCount, VectorArray3<T> source) {
			Array.Copy(source.array, 0, array, 3*iStart, 3*iCount);
		}

        IEnumerator IEnumerable.GetEnumerator()
        {
            return array.GetEnumerator();
        }
    }


    public class VectorArray3d : VectorArray3<double>
    {
        const double invalid_value = -99999999.0;

#if DEBUG  
        bool __debug = false;
#endif
        public VectorArray3d(int nCount, bool debug = false) : base(nCount)
        {
#if DEBUG  
            __debug = debug;
            if (__debug)
                for (int i = 0; i < Count; ++i)
                    Set(i, invalid_value, invalid_value, invalid_value);
#endif
        }
        public VectorArray3d(double[] data) : base(data) { }
        public Vector3d this[int i] {
            get { return new Vector3d(array[3 * i], array[3 * i + 1], array[3 * i + 2]); }
            set {
#if DEBUG  
                if (__debug && this[i][0] != invalid_value)
                    throw new InvalidOperationException(string.Format("VectorArray3d.set - value {0} is already set!",i));
#endif
                Set(i, value[0], value[1], value[2]);
            }
        }

        public IEnumerable<Vector3d> AsVector3d()
        {
            for (int i = 0; i < Count; ++i)
                yield return this[i];
        }
    };


    public class VectorArray3f : VectorArray3<float>
    {
        public VectorArray3f(int nCount) : base(nCount) { }
        public VectorArray3f(float[] data) : base(data) { }
        public Vector3f this[int i] {
            get { return new Vector3f(array[3 * i], array[3 * i + 1], array[3 * i + 2]); }
            set { Set(i, value[0], value[1], value[2]); }
        }

        public IEnumerable<Vector3f> AsVector3f()
        {
            for (int i = 0; i < Count; ++i)
                yield return this[i];
        }
    };


    public class VectorArray3i : VectorArray3<int>
    {
        public VectorArray3i(int nCount) : base(nCount) { }
        public VectorArray3i(int[] data) : base(data) { }
        public Vector3i this[int i] {
            get { return new Vector3i(array[3 * i], array[3 * i + 1], array[3 * i + 2]); }
            set { Set(i, value[0], value[1], value[2]); }
        }
        // [RMS] for CW/CCW codes
        public void Set(int i, int a, int b, int c, bool bCycle = false) {
            array[3 * i] = a;
            if (bCycle) {
                array[3 * i + 1] = c;
                array[3 * i + 2] = b;
            } else {
                array[3 * i + 1] = b;
                array[3 * i + 2] = c;
            }
        }

        public IEnumerable<Vector3i> AsVector3i()
        {
            for (int i = 0; i < Count; ++i)
                yield return this[i];
        }
    };



    public class IndexArray3i : VectorArray3<int>
    {
        public IndexArray3i(int nCount) : base(nCount) { }
        public IndexArray3i(int[] data) : base(data) { }
        public Index3i this[int i] {
            get { return new Index3i(array[3 * i], array[3 * i + 1], array[3 * i + 2]); }
            set { Set(i, value[0], value[1], value[2]); }
        }
        // [RMS] for CW/CCW codes
        public void Set(int i, int a, int b, int c, bool bCycle = false) {
            array[3 * i] = a;
            if (bCycle) {
                array[3 * i + 1] = c;
                array[3 * i + 2] = b;
            } else {
                array[3 * i + 1] = b;
                array[3 * i + 2] = c;
            }
        }

        public IEnumerable<Index3i> AsIndex3i()
        {
            for (int i = 0; i < Count; ++i)
                yield return new Index3i(array[3 * i], array[3 * i + 1], array[3 * i + 2]);
        }
    };





    //
    // Same as VectorArray3, but for 2D vectors/etc
    //
    public class VectorArray2<T> : IEnumerable<T>
    {
        public T[] array;

        public VectorArray2(int nCount = 0)
        {
            array = new T[nCount * 2];
        }

        public VectorArray2(T[] data)
        {
            array = data;
        }

        public int Count
        {
            get { return array.Length / 2; }
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < array.Length; ++i)
                yield return array[i];
        }

        public void Resize(int Count)
        {
            array = new T[2 * Count];
        }

        public void Set(int i, T a, T b)
        {
            array[2 * i] = a;
            array[2 * i + 1] = b;
        }

		public void Set(int iStart, int iCount, VectorArray2<T> source) {
			Array.Copy(source.array, 0, array, 2*iStart, 2*iCount);
		}

        IEnumerator IEnumerable.GetEnumerator()
        {
            return array.GetEnumerator();
        }
    }
    public class VectorArray2d : VectorArray2<double>
    {
        public VectorArray2d(int nCount) : base(nCount) { }
        public VectorArray2d(double[] data) : base(data) { }
        public Vector2d this[int i] {
            get { return new Vector2d(array[2 * i], array[2 * i + 1]); }
            set { Set(i, value[0], value[1]); }
        }

        public IEnumerable<Vector2d> AsVector2d() {
            for (int i = 0; i < Count; ++i)
                yield return this[i];
        }
    };
    public class VectorArray2f : VectorArray2<float>
    {
        public VectorArray2f(int nCount) : base(nCount) { }
        public VectorArray2f(float[] data) : base(data) { }
        public Vector2f this[int i] {
            get { return new Vector2f(array[2 * i], array[2 * i + 1]); }
            set { Set(i, value[0], value[1]); }
        }

        public IEnumerable<Vector2d> AsVector2f() {
            for (int i = 0; i < Count; ++i)
                yield return this[i];
        }
    };



    public class IndexArray2i : VectorArray2<int>
    {
        public IndexArray2i(int nCount) : base(nCount) { }
        public IndexArray2i(int[] data) : base(data) { }
        public Index2i this[int i] {
            get { return new Index2i(array[2 * i], array[2 * i + 1]); }
            set { Set(i, value[0], value[1]); }
        }

        public IEnumerable<Index2i> AsIndex2i() {
            for (int i = 0; i < Count; ++i)
                yield return new Index2i(array[2 * i], array[2 * i + 1]);
        }
    };











    public class VectorArray4<T> : IEnumerable<T>
    {
        public T[] array;

        public VectorArray4(int nCount = 0)
        {
            array = new T[nCount * 4];
        }

        public VectorArray4(T[] data)
        {
            array = data;
        }

        public int Count {
            get { return array.Length / 4; }
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < array.Length; ++i)
                yield return array[i];
        }

        public void Resize(int Count)
        {
            array = new T[4 * Count];
        }

        public void Set(int i, T a, T b, T c, T d)
        {
            int j = 4 * i;
            array[j] = a;
            array[j+1] = b;
            array[j+2] = c;
            array[j+3] = d;
        }

        public void Set(int iStart, int iCount, VectorArray4<T> source)
        {
            Array.Copy(source.array, 0, array, 4 * iStart, 4 * iCount);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return array.GetEnumerator();
        }
    }



    public class IndexArray4i : VectorArray4<int>
    {
        public IndexArray4i(int nCount) : base(nCount) { }
        public IndexArray4i(int[] data) : base(data) { }
        public Index4i this[int i] {
            get { int j = 4 * i;  return new Index4i(array[j], array[j + 1], array[j + 2], array[j+3]); }
            set { Set(i, value[0], value[1], value[2], value[4]); }
        }
        public IEnumerable<Index4i> AsIndex4i()
        {
            for (int i = 0; i < Count; ++i)
                yield return this[i];
        }
    };



}
