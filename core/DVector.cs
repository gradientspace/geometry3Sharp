using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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


        public T this[int i]
        {
            get {
                return Blocks[i / nBlockSize][i % nBlockSize];
            }
            set {
                Blocks[i / nBlockSize][i % nBlockSize] = value;
            }
        }



        // [RMS] slowest option, but only one that is completely generic
        public void GetBuffer(T[] data)
        {
            int nLen = this.Length;
            for (int k = 0; k < nLen; ++k)
                data[k] = this[k];
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
                pCur = IntPtr.Add(pCur, v.nBlockSize * sizeof(double));
            }
            System.Runtime.InteropServices.Marshal.Copy(v.Blocks[N - 1], 0, pCur, v.iCurBlockUsed);
        }
        public static unsafe void FastGetBuffer(DVector<float> v, float * pBuffer)
        {
            IntPtr pCur = new IntPtr(pBuffer);
            int N = v.Blocks.Count;
            for (int k = 0; k < N - 1; k++) {
                System.Runtime.InteropServices.Marshal.Copy(v.Blocks[k], 0, pCur, v.nBlockSize);
                pCur = IntPtr.Add(pCur, v.nBlockSize * sizeof(float));
            }
            System.Runtime.InteropServices.Marshal.Copy(v.Blocks[N - 1], 0, pCur, v.iCurBlockUsed);
        }
        public static unsafe void FastGetBuffer(DVector<int> v, int * pBuffer)
        {
            IntPtr pCur = new IntPtr(pBuffer);
            int N = v.Blocks.Count;
            for (int k = 0; k < N - 1; k++) {
                System.Runtime.InteropServices.Marshal.Copy(v.Blocks[k], 0, pCur, v.nBlockSize);
                pCur = IntPtr.Add(pCur, v.nBlockSize * sizeof(int));
            }
            System.Runtime.InteropServices.Marshal.Copy(v.Blocks[N - 1], 0, pCur, v.iCurBlockUsed);
        }

    }
}
