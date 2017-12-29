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

        public float this[Vector3i ijk] {
            get { return Buffer[ijk.x + ni * (ijk.y + nj * ijk.z)]; }
            set { Buffer[ijk.x + ni * (ijk.y + nj * ijk.z)] = value; }
        }

        public void get_x_pair(int i0, int j, int k, out double a, out double b)
        {
            int offset = ni * (j + nj * k);
            a = Buffer[offset + i0];
            b = Buffer[offset + i0 + 1];
        }

        public void apply(Func<float, float> f)
        {
            for ( int k = 0; k < nk; k++ ) {
                for (int j = 0; j < nj; j++ ) {
                    for ( int i = 0; i < ni; i++ ) {
                        int idx = i + ni * (j + nj * k);
                        Buffer[idx] = f(Buffer[idx]);
                    }
                }
            }
        }

        public AxisAlignedBox3i Bounds {
            get { return new AxisAlignedBox3i(0, 0, 0, ni, nj, nk); }
        }


        public IEnumerable<Vector3i> Indices()
        {
            for (int z = 0; z < nk; ++z) {
                for (int y = 0; y < nj; ++y) {
                    for (int x = 0; x < ni; ++x)
                        yield return new Vector3i(x, y, z);
                }
            }
        }


        public IEnumerable<Vector3i> InsetIndices(int border_width)
        {
            int stopy = nj - border_width, stopx = ni - border_width;
            for (int z = border_width; z < nk-border_width; ++z) {
                for (int y = border_width; y < stopy; ++y) {
                    for (int x = border_width; x < stopx; ++x)
                        yield return new Vector3i(x, y, z);
                }
            }
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
