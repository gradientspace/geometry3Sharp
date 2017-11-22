using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace g3
{

    public class DenseGrid3f
    {
        public float[] Buffer;
        public int ni, nj, nk;

        public void resize(int ni, int nj, int nk)
        {
            Buffer = new float[ni * nj * nk];
            this.ni = ni; this.nj = nj; this.nk = nk;
        }

        public void assign(float value)
        {
            for (int i = 0; i < Buffer.Length; ++i)
                Buffer[i] = value;
        }

        public float this[int i, int j, int k] {
            get { return Buffer[i + ni * (j + nj * k)]; }
            set { Buffer[i + ni * (j + nj * k)] = value; }
        }

        public void get_x_pair(int i0, int j, int k, out double a, out double b)
        {
            int offset = ni * (j + nj * k);
            a = Buffer[offset + i0];
            b = Buffer[offset + i0 + 1];
        }

    }


    




    public class DenseGrid3i
    {
        public int[] Buffer;
        public int ni, nj, nk;

        public DenseGrid3i(int ni, int nj, int nk, int initialValue)
        {
            resize(ni, nj, nk);
            assign(initialValue);
        }

        public void resize(int ni, int nj, int nk)
        {
            Buffer = new int[ni * nj * nk];
            this.ni = ni; this.nj = nj; this.nk = nk;
        }

        public void assign(int value)
        {
            for (int i = 0; i < Buffer.Length; ++i)
                Buffer[i] = value;
        }

        public int this[int i, int j, int k] {
            get { return Buffer[i + ni * (j + nj * k)]; }
            set { Buffer[i + ni * (j + nj * k)] = value; }
        }

        public void increment(int i, int j, int k)
        {
            Buffer[i + ni * (j + nj * k)]++;
        }
        public void decrement(int i, int j, int k)
        {
            Buffer[i + ni * (j + nj * k)]--;
        }

        public void atomic_increment(int i, int j, int k)
        {
            System.Threading.Interlocked.Increment(ref Buffer[i + ni * (j + nj * k)]);
        }

        public void atomic_decrement(int i, int j, int k)
        {
            System.Threading.Interlocked.Decrement(ref Buffer[i + ni * (j + nj * k)]);
        }

        public void atomic_incdec(int i, int j, int k, bool decrement = false) {
            if ( decrement )
                System.Threading.Interlocked.Decrement(ref Buffer[i + ni * (j + nj * k)]);
            else
                System.Threading.Interlocked.Increment(ref Buffer[i + ni * (j + nj * k)]);
        }
    }



}
