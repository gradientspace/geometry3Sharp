using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace g3
{

    /// <summary>
    /// 2D dense grid of floating-point scalar values. 
    /// </summary>
    public class DenseGrid2f
    {
        public float[] Buffer;
        public int ni, nj;

        public DenseGrid2f()
        {
            ni = nj = 0;
        }

        public DenseGrid2f(int ni, int nj, float initialValue)
        {
            resize(ni, nj);
            assign(initialValue);
        }

        public DenseGrid2f(DenseGrid2f copy)
        {
            Buffer = new float[copy.Buffer.Length];
            Array.Copy(copy.Buffer, Buffer, Buffer.Length);
            ni = copy.ni; nj = copy.nj;
        }

        public void swap(DenseGrid2f g2)
        {
            Util.gDevAssert(ni == g2.ni && nj == g2.nj);
            var tmp = g2.Buffer;
            g2.Buffer = this.Buffer;
            this.Buffer = tmp;
        }

        public int size { get { return ni * nj; } }

        public void resize(int ni, int nj)
        {
            Buffer = new float[ni * nj];
            this.ni = ni; this.nj = nj;
        }

        public void assign(float value)
        {
            for (int i = 0; i < Buffer.Length; ++i)
                Buffer[i] = value;
        }

        public void assign_border(float value, int rings)
        {
            for ( int j = 0; j < rings; ++j ) {
                int jb = nj - 1 - j;
                for ( int i = 0; i < ni; ++i ) {
                    Buffer[i + ni * j] = value;
                    Buffer[i + ni * jb] = value;
                }
            }
            int stop = nj - 1 - rings;
            for ( int j = rings; j < stop; ++j ) {
                for ( int i = 0; i < rings; ++i ) {
                    Buffer[i + ni * j] = value;
                    Buffer[(ni - 1 - i) + ni * j] = value;
                }
            }
        }


        public void clear() {
            Array.Clear(Buffer, 0, Buffer.Length);
        }

        public void copy(DenseGrid2f copy)
        {
            Array.Copy(copy.Buffer, this.Buffer, this.Buffer.Length);
        }

        public float this[int i] {
            get { return Buffer[i]; }
            set { Buffer[i] = value; }
        }

        public float this[int i, int j] {
            get { return Buffer[i + ni * j]; }
            set { Buffer[i + ni * j] = value; }
        }

        public float this[Vector2i ijk] {
            get { return Buffer[ijk.x + ni * ijk.y]; }
            set { Buffer[ijk.x + ni * ijk.y] = value; }
        }

        public void get_x_pair(int i0, int j, out double a, out double b)
        {
            int offset = ni * j;
            a = Buffer[offset + i0];
            b = Buffer[offset + i0 + 1];
        }

        public void apply(Func<float, float> f)
        {
            for (int j = 0; j < nj; j++ ) {
                for ( int i = 0; i < ni; i++ ) {
                    int idx = i + ni * j;
                    Buffer[idx] = f(Buffer[idx]);
                }
            }
        }

        public void set_min(DenseGrid2f grid2)
        {
            for (int k = 0; k < Buffer.Length; ++k)
                Buffer[k] = Math.Min(Buffer[k], grid2.Buffer[k]);
        }
        public void set_max(DenseGrid2f grid2)
        {
            for (int k = 0; k < Buffer.Length; ++k)
                Buffer[k] = Math.Max(Buffer[k], grid2.Buffer[k]);
        }

        public AxisAlignedBox2i Bounds {
            get { return new AxisAlignedBox2i(0, 0, ni, nj); }
        }


        public IEnumerable<Vector2i> Indices()
        {
            for (int y = 0; y < nj; ++y) {
                for (int x = 0; x < ni; ++x)
                    yield return new Vector2i(x, y);
            }
        }


        public IEnumerable<Vector2i> InsetIndices(int border_width)
        {
            int stopy = nj - border_width, stopx = ni - border_width;
            for (int y = border_width; y < stopy; ++y) {
                for (int x = border_width; x < stopx; ++x)
                    yield return new Vector2i(x, y);
            }
        }


    }






    /// <summary>
    /// 2D dense grid of integers. 
    /// </summary>
    public class DenseGrid2i
    {
        public int[] Buffer;
        public int ni, nj;

        public DenseGrid2i()
        {
            ni = nj = 0;
        }

        public DenseGrid2i(int ni, int nj, int initialValue)
        {
            resize(ni, nj);
            assign(initialValue);
        }

        public DenseGrid2i(DenseGrid2i copy)
        {
            resize(copy.ni, copy.nj);
            Array.Copy(copy.Buffer, this.Buffer, this.Buffer.Length);
        }

        public int size { get { return ni * nj; } }

        public void resize(int ni, int nj)
        {
            Buffer = new int[ni * nj];
            this.ni = ni; this.nj = nj;
        }

        public void clear() {
            Array.Clear(Buffer, 0, Buffer.Length);
        }


        public void copy(DenseGrid2i copy)
        {
            Array.Copy(copy.Buffer, this.Buffer, this.Buffer.Length);
        }

        public void assign(int value)
        {
            for (int i = 0; i < Buffer.Length; ++i)
                Buffer[i] = value;
        }

        public int this[int i] {
            get { return Buffer[i]; }
            set { Buffer[i] = value; }
        }

        public int this[int i, int j] {
            get { return Buffer[i + ni * j]; }
            set { Buffer[i + ni * j] = value; }
        }

        public int this[Vector2i ijk] {
            get { return Buffer[ijk.x + ni * ijk.y]; }
            set { Buffer[ijk.x + ni * ijk.y] = value; }
        }

        public void increment(int i, int j)
        {
            Buffer[i + ni * j]++;
        }
        public void decrement(int i, int j)
        {
            Buffer[i + ni * j]--;
        }

        public void atomic_increment(int i, int j)
        {
            System.Threading.Interlocked.Increment(ref Buffer[i + ni * j]);
        }

        public void atomic_decrement(int i, int j)
        {
            System.Threading.Interlocked.Decrement(ref Buffer[i + ni * j]);
        }

        public void atomic_incdec(int i, int j, bool decrement = false) {
            if ( decrement )
                System.Threading.Interlocked.Decrement(ref Buffer[i + ni * j]);
            else
                System.Threading.Interlocked.Increment(ref Buffer[i + ni * j]);
        }

        public int sum() {
            int sum = 0;
            for (int i = 0; i < Buffer.Length; ++i)
                sum += Buffer[i];
            return sum;
        }


        public IEnumerable<Vector2i> Indices()
        {
            for (int y = 0; y < nj; ++y) {
                for (int x = 0; x < ni; ++x)
                    yield return new Vector2i(x, y);
            }
        }


        public IEnumerable<Vector2i> InsetIndices(int border_width)
        {
            int stopy = nj - border_width, stopx = ni - border_width;
            for (int y = border_width; y < stopy; ++y) {
                for (int x = border_width; x < stopx; ++x)
                    yield return new Vector2i(x, y);
            }
        }

    }



}
