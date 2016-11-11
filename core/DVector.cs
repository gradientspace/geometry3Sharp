using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
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

        public int Length
        {
            get { return (Blocks.Count - 1) * nBlockSize + iCurBlockUsed;  }
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
                return Blocks[i / nBlockSize][i % nBlockSize];
            }
            set {
                Blocks[i / nBlockSize][i % nBlockSize] = value;
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
        public T[] GetBuffer()
        {
            T[] data = new T[this.Length];
            for (int k = 0; k < this.Length; ++k)
                data[k] = this[k];
            return data;
        }

        // warning: this may be quite slow!
        public T2[] GetBufferCast<T2>()
        {
            T2[] data = new T2[this.Length];
            for (int k = 0; k < this.Length; ++k)
                data[k] = (T2)Convert.ChangeType(this[k], typeof(T2));
            return data;
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

    }
}
