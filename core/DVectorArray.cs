using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    //
    // This class is just a wrapper around a dvector that provides convenient 3-element set/get access
    // Useful for things like treating a float array as a list of vectors
    //
    public class DVectorArray3<T> : IEnumerable<T>
    {
        public DVector<T> vector;

        public DVectorArray3(int nCount = 0) {
            vector = new DVector<T>();
            if (nCount > 0)
                vector.resize(nCount*3);

        }

        public DVectorArray3(T[] data) {
            vector = new DVector<T>(data);
        }

        public int Count {
            get { return vector.Length/3; }
        }

        // we should just be passing back DVector enumerator, but it doesn't have one??
        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < vector.Length; ++i)
                yield return vector[i];
        }

        public void Resize(int count) {
            vector.resize(3 * count);
        }

        public void Set(int i, T a, T b, T c) {
            vector.insert(a, 3 * i);
            vector.insert(b, 3 * i+1);
            vector.insert(c, 3 * i+2);
        }

        public void Append(T a, T b, T c) {
            vector.push_back(a);
            vector.push_back(b);
            vector.push_back(c);
        }

        public void Clear() {
            vector.Clear();
        }

        //public void Set(int iStart, int iCount, DVectorArray3<T> source) {
        //	Array.Copy(source.vector, 0, vector, 3*iStart, 3*iCount);
        //}

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();// vector.GetEnumerator();
        }
    }


    public class DVectorArray3d : DVectorArray3<double>
    {
        const double invalid_value = -99999999.0;

        public DVectorArray3d(int nCount = 0) : base(nCount)
        {
        }
        public DVectorArray3d(double[] data) : base(data) { }
        public Vector3d this[int i] {
            get { return new Vector3d(vector[3 * i], vector[3 * i + 1], vector[3 * i + 2]); }
            set { Set(i, value[0], value[1], value[2]); }
        }

        public IEnumerable<Vector3d> AsVector3d() {
            for (int i = 0; i < Count; ++i)
                yield return this[i];
        }
    };


    public class DVectorArray3f : DVectorArray3<float>
    {
        public DVectorArray3f(int nCount = 0) : base(nCount) { }
        public DVectorArray3f(float[] data) : base(data) { }
        public Vector3f this[int i] {
            get { return new Vector3f(vector[3 * i], vector[3 * i + 1], vector[3 * i + 2]); }
            set { Set(i, value[0], value[1], value[2]); }
        }

        public IEnumerable<Vector3f> AsVector3f()
        {
            for (int i = 0; i < Count; ++i)
                yield return this[i];
        }
    };


    public class DVectorArray3i : DVectorArray3<int>
    {
        public DVectorArray3i(int nCount = 0) : base(nCount) { }
        public DVectorArray3i(int[] data) : base(data) { }
        public Vector3i this[int i] {
            get { return new Vector3i(vector[3 * i], vector[3 * i + 1], vector[3 * i + 2]); }
            set { Set(i, value[0], value[1], value[2]); }
        }
        // [RMS] for CW/CCW codes
        public void Set(int i, int a, int b, int c, bool bCycle = false) {
            vector[3 * i] = a;
            if (bCycle) {
                vector[3 * i + 1] = c;
                vector[3 * i + 2] = b;
            } else {
                vector[3 * i + 1] = b;
                vector[3 * i + 2] = c;
            }
        }

        public IEnumerable<Vector3i> AsVector3i()
        {
            for (int i = 0; i < Count; ++i)
                yield return this[i];
        }
    };



    public class DIndexArray3i : DVectorArray3<int>
    {
        public DIndexArray3i(int nCount = 0) : base(nCount) { }
        public DIndexArray3i(int[] data) : base(data) { }
        public Index3i this[int i] {
            get { return new Index3i(vector[3 * i], vector[3 * i + 1], vector[3 * i + 2]); }
            set { Set(i, value[0], value[1], value[2]); }
        }
        // [RMS] for CW/CCW codes
        public void Set(int i, int a, int b, int c, bool bCycle = false) {
            vector[3 * i] = a;
            if (bCycle) {
                vector[3 * i + 1] = c;
                vector[3 * i + 2] = b;
            } else {
                vector[3 * i + 1] = b;
                vector[3 * i + 2] = c;
            }
        }

        public IEnumerable<Index3i> AsIndex3i()
        {
            for (int i = 0; i < Count; ++i)
                yield return new Index3i(vector[3 * i], vector[3 * i + 1], vector[3 * i + 2]);
        }
    };





    //
    // Same as DVectorArray3, but for 2D vectors/etc
    //
    public class DVectorArray2<T> : IEnumerable<T>
    {
        public DVector<T> vector;

        public DVectorArray2(int nCount = 0)
        {
            vector = new DVector<T>();
            vector.resize(nCount * 2);
        }

        public DVectorArray2(T[] data)
        {
            vector = new DVector<T>(data);
        }

        public int Count
        {
            get { return vector.Length / 2; }
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < vector.Length; ++i)
                yield return vector[i];
        }

        public void Resize(int count) {
            vector.resize(2* count);
        }

        public void Set(int i, T a, T b) {
            vector.insert(a, 2 * i);
            vector.insert(b, 2 * i+1);
        }

        public void Append(T a, T b) {
            vector.push_back(a);
            vector.push_back(b);
        }

        //public void Set(int iStart, int iCount, DVectorArray2<T> source) {
        //	Array.Copy(source.vector, 0, vector, 2*iStart, 2*iCount);
        //}

        IEnumerator IEnumerable.GetEnumerator()
        {
            //return vector.GetEnumerator();
            return this.GetEnumerator();
        }
    }
    public class DVectorArray2d : DVectorArray2<double>
    {
        public DVectorArray2d(int nCount = 0) : base(nCount) { }
        public DVectorArray2d(double[] data) : base(data) { }
        public Vector2d this[int i] {
            get { return new Vector2d(vector[2 * i], vector[2 * i + 1]); }
            set { Set(i, value[0], value[1]); }
        }

        public IEnumerable<Vector2d> AsVector2d() {
            for (int i = 0; i < Count; ++i)
                yield return this[i];
        }
    };
    public class DVectorArray2f : DVectorArray2<float>
    {
        public DVectorArray2f(int nCount = 0) : base(nCount) { }
        public DVectorArray2f(float[] data) : base(data) { }
        public Vector2f this[int i] {
            get { return new Vector2f(vector[2 * i], vector[2 * i + 1]); }
            set { Set(i, value[0], value[1]); }
        }

        public IEnumerable<Vector2d> AsVector2f() {
            for (int i = 0; i < Count; ++i)
                yield return this[i];
        }
    };



    public class DIndexArray2i : DVectorArray2<int>
    {
        public DIndexArray2i(int nCount = 0) : base(nCount) { }
        public DIndexArray2i(int[] data) : base(data) { }
        public Index2i this[int i] {
            get { return new Index2i(vector[2 * i], vector[2 * i + 1]); }
            set { Set(i, value[0], value[1]); }
        }

        public IEnumerable<Index2i> AsIndex2i() {
            for (int i = 0; i < Count; ++i)
                yield return new Index2i(vector[2 * i], vector[2 * i + 1]);
        }
    };


}
