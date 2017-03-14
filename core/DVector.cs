using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{

    //
    // [RMS] AAAAHHHH usage of Blocks vs iCurBlock is not consistent!!
    //   - Should be supporting Capacity vs Size...
    //   - this[] operator does not check bounds, so it can write to any valid Block
    //   - some fns discard Blocks beyond iCurBlock
    //   - wtf...
    public class DVector<T>
    {
        List<T[]> Blocks;
        int nBlockSize;
        int iCurBlock;
        int iCurBlockUsed;

        public DVector() {
            nBlockSize = 2048;
            iCurBlock = 0;
            iCurBlockUsed = 0;
            Blocks = new List<T[]>();
            Blocks.Add(new T[nBlockSize]);
        }

        public DVector(DVector<T> copy)
        {
            nBlockSize = copy.nBlockSize;
            iCurBlock = copy.iCurBlock;
            iCurBlockUsed = copy.iCurBlockUsed;
            Blocks = new List<T[]>();
            for ( int i = 0; i < copy.Blocks.Count; ++i ) {
                Blocks.Add(new T[nBlockSize]);
                Array.Copy(copy.Blocks[i], Blocks[i], copy.Blocks[i].Length);
            }
        }

        public DVector(T[] data)
        {
            nBlockSize = 2048;
            Initialize(data);
        }

        public DVector(IEnumerable<T> init)
        {
            nBlockSize = 2048;
            iCurBlock = 0;
            iCurBlockUsed = 0;
            Blocks = new List<T[]>();
            Blocks.Add(new T[nBlockSize]);
            // AAAHHH this could be so more efficient...
            foreach (T v in init)
                Add(v);
        }



        //public int Capacity {
        //    get { return Blocks.Count * nBlockSize;  }
        //}

        public int Length {
            get { return iCurBlock * nBlockSize + iCurBlockUsed;  }
        }

        public int BlockCount {
            get { return nBlockSize; }
        }

        public int size {
            get { return Length; }
        }
                
        public bool empty 
        {
            get { return iCurBlock == 0 && iCurBlockUsed == 0; }
        }
            

        public void Add(T value)
        {
            if ( iCurBlockUsed == nBlockSize ) {
                Blocks.Add(new T[nBlockSize]);
                iCurBlock++;
                iCurBlockUsed = 0;
            }
            Blocks[iCurBlock][iCurBlockUsed] = value;
            iCurBlockUsed++;
        }

        public void Add(T value, int nRepeat) {
            for (int i = 0; i < nRepeat; i++)
                Add(value);
        }

        public void Add(T[] values)
        {
            // TODO make this more efficient?
            for (int i = 0; i < values.Length; ++i)
                Add(values[i]);
        }

        public void Add(T[] values, int nRepeat)
        {
            for (int i = 0; i < nRepeat; i++)
                for (int j = 0; j < values.Length; ++j)
                    Add(values[j]);
        }


        public void push_back(T value) {
            this.Add(value);
        }
        public void pop_back() {
            if (iCurBlockUsed > 0)
                iCurBlockUsed--;
            if (iCurBlockUsed == 0 && iCurBlock > 0)
            {
                iCurBlock--;
                iCurBlockUsed = nBlockSize;

                // remove block ??
            }
        }


        public void insert(T value, int index) {
            int s = size;
            if (index == s) {
                push_back( value );
            } else if ( index > s ) { 
                resize( index );
                push_back(value);
            } else {
                this[index] = value;
            }
        }


        public void resize(int count) {
            // figure out how many segments we need
            int nNumSegs = 1 + (int)count / nBlockSize;

            // figure out how many are currently allocated...
            int nCurCount = Blocks.Count;

            // erase extra segments memory
            for (int i = nNumSegs; i < nCurCount; ++i)
                Blocks[i] = null;

            // resize to right number of segments
            Blocks.Capacity = nNumSegs;

            // allocate new segments
            for (int i = (int)nCurCount; i < nNumSegs; ++i) {
                Blocks.Add(new T[nBlockSize]);
            }

            // mark last segment
            iCurBlockUsed = count - (nNumSegs-1)*nBlockSize;

            iCurBlock = nNumSegs-1;            
        }




        public T this[int i]
        {
            get {
                // [RMS] this is definitely marginally faster =)
                int bi = i / nBlockSize;
                return Blocks[bi][i - bi * nBlockSize];
                //return Blocks[i / nBlockSize][i % nBlockSize];
            }
            set {
                int bi = i / nBlockSize;
                Blocks[bi][i - bi * nBlockSize] = value;
                //Blocks[i / nBlockSize][i % nBlockSize] = value;
            }
        }


        public T back {
            get { return Blocks[iCurBlock][iCurBlockUsed-1]; }
            set { Blocks[iCurBlock][iCurBlockUsed-1] = value; }
        }
        public T front {
            get { return Blocks[0][0]; }
            set { Blocks[0][0] = value; }
        }



        // TODO: 
        //   - iterate through blocks in above to avoid div/mod for each element
        //   - provide function that takes lambda?


        // [RMS] slowest option, but only one that is completely generic
        public void GetBuffer(T[] data)
        {
            int nLen = this.Length;
            for (int k = 0; k < nLen; ++k)
                data[k] = this[k];
        }
        public T[] GetBuffer()      // todo: deprecate this...
        {
            T[] data = new T[this.Length];
            for (int k = 0; k < this.Length; ++k)
                data[k] = this[k];
            return data;
        }
        public T[] ToArray() {
            return GetBuffer();
        }

        // warning: this may be quite slow!
        public T2[] GetBufferCast<T2>()
        {
            T2[] data = new T2[this.Length];
            for (int k = 0; k < this.Length; ++k)
                data[k] = (T2)Convert.ChangeType(this[k], typeof(T2));
            return data;
        }


        public byte[] GetBytes()
        {
            Type type = typeof(T);
            int n = System.Runtime.InteropServices.Marshal.SizeOf(type);
            byte[] buffer = new byte[this.Length * n];
            int i = 0;
            int N = Blocks.Count;
            for ( int k = 0; k < N-1; ++k ) {
                Buffer.BlockCopy(Blocks[k], 0, buffer, i, nBlockSize * n);
                i += nBlockSize * n;
            }
            Buffer.BlockCopy(Blocks[N-1], 0, buffer, i, iCurBlockUsed * n);
            return buffer;
        }



        public void Initialize(T[] data)
        {
            int blocks = data.Length / nBlockSize;
            Blocks = new List<T[]>();
            int ai = 0;
            for (int i = 0; i < blocks; ++i) {
                T[] block = new T[nBlockSize];
                Array.Copy(data, ai, block, 0, nBlockSize);
                Blocks.Add(block);
                ai += nBlockSize;
            }
            iCurBlockUsed = data.Length - ai;
            if (iCurBlockUsed != 0) {
                T[] last = new T[nBlockSize];
                Array.Copy(data, ai, last, 0, iCurBlockUsed);
                Blocks.Add(last);
            } else {
                iCurBlockUsed = nBlockSize;
            }
            iCurBlock = Blocks.Count - 1;
        }



        /*
         * [RMS] C# resolves generics at compile-type, so we cannot call an overloaded
         *   function based on the generic type. Hence, we have these static helpers for
         *   common cases...
         */

        public static unsafe void FastGetBuffer(DVector<double> v, double * pBuffer)
        {
            IntPtr pCur = new IntPtr(pBuffer);
            int N = v.Blocks.Count;
            for (int k = 0; k < N - 1; k++) {
                System.Runtime.InteropServices.Marshal.Copy(v.Blocks[k], 0, pCur, v.nBlockSize);
                pCur = new IntPtr(
                    pCur.ToInt64() + v.nBlockSize * sizeof(double));
            }
            System.Runtime.InteropServices.Marshal.Copy(v.Blocks[N - 1], 0, pCur, v.iCurBlockUsed);
        }
        public static unsafe void FastGetBuffer(DVector<float> v, float * pBuffer)
        {
            IntPtr pCur = new IntPtr(pBuffer);
            int N = v.Blocks.Count;
            for (int k = 0; k < N - 1; k++) {
                System.Runtime.InteropServices.Marshal.Copy(v.Blocks[k], 0, pCur, v.nBlockSize);
                pCur = new IntPtr(
                    pCur.ToInt64() + v.nBlockSize * sizeof(float));
            }
            System.Runtime.InteropServices.Marshal.Copy(v.Blocks[N - 1], 0, pCur, v.iCurBlockUsed);
        }
        public static unsafe void FastGetBuffer(DVector<int> v, int * pBuffer)
        {
            IntPtr pCur = new IntPtr(pBuffer);
            int N = v.Blocks.Count;
            for (int k = 0; k < N - 1; k++) {
                System.Runtime.InteropServices.Marshal.Copy(v.Blocks[k], 0, pCur, v.nBlockSize);
                pCur = new IntPtr(
                    pCur.ToInt64() + v.nBlockSize * sizeof(int));
            }
            System.Runtime.InteropServices.Marshal.Copy(v.Blocks[N - 1], 0, pCur, v.iCurBlockUsed);
        }



        // block iterator
        public struct DBlock
        {
            public T[] data;
            public int usedCount;
        }
		public IEnumerable< DBlock > BlockIterator() {
            for (int i = 0; i < iCurBlock; ++i)
                yield return new DBlock() { data = Blocks[i], usedCount = nBlockSize };
            yield return new DBlock() { data = Blocks[iCurBlock], usedCount = iCurBlockUsed };
		}

    }
}
