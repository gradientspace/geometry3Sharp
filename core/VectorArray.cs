using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    //
    // This class is just a wrapper around a static array that provides convenient 3-element set/get access
    // Useful for things like treating a float array as a list of vectors
    //
    public class VectorArray3<T>
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
    }
    public class VectorArray3d : VectorArray3<double>
    {
        public VectorArray3d(int nCount) : base(nCount) { }
        public VectorArray3d(double[] data) : base(data) { }
        public Vector3d this[int i] {
            get { return new Vector3d(array[3 * i], array[3 * i + 1], array[3 * i + 2]); }
            set { Set(i, value[0], value[1], value[2]); }
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
    };


    //
    // Same as VectorArray3, but for 2D vectors/etc
    //
    public class VectorArray2<T>
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

        public void Resize(int Count)
        {
            array = new T[2 * Count];
        }

        public void Set(int i, T a, T b)
        {
            array[2 * i] = a;
            array[2 * i + 1] = b;
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


}
