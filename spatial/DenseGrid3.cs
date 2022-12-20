using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace g3
{
    /// <summary>
    /// 3D dense grid of floating-point scalar values. 
    /// </summary>
    public class DenseGrid3f
    {
        public float[] Buffer;
        public int ni, nj, nk;

        public DenseGrid3f()
        {
            ni = nj = nk = 0;
        }

        public DenseGrid3f(int ni, int nj, int nk, float initialValue)
        {
            resize(ni, nj, nk);
            assign(initialValue);
        }

        public DenseGrid3f(DenseGrid3f copy)
        {
            Buffer = new float[copy.Buffer.Length];
            Array.Copy(copy.Buffer, Buffer, Buffer.Length);
            ni = copy.ni; nj = copy.nj; nk = copy.nk;
        }

        public void swap(DenseGrid3f g2)
        {
            Util.gDevAssert(ni == g2.ni && nj == g2.nj && nk == g2.nk);
            var tmp = g2.Buffer;
            g2.Buffer = this.Buffer;
            this.Buffer = tmp;
        }

        public int size { get { return ni * nj * nk; } }

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

        public void set_min(ref Vector3i ijk, float f)
        {
            int idx = ijk.x + ni * (ijk.y + nj * ijk.z);
            if (f < Buffer[idx])
                Buffer[idx] = f;
        }
        public void set_max(ref Vector3i ijk, float f)
        {
            int idx = ijk.x + ni * (ijk.y + nj * ijk.z);
            if (f > Buffer[idx])
                Buffer[idx] = f;
        }

        public float this[int i] {
            get { return Buffer[i]; }
            set { Buffer[i] = value; }
        }

        public float this[int i, int j, int k] {
            get { return Buffer[i + ni * (j + nj * k)]; }
            set { Buffer[i + ni * (j + nj * k)] = value; }
        }

        public float this[Vector3i ijk] {
            get { return Buffer[ijk.x + ni * (ijk.y + nj * ijk.z)]; }
            set { Buffer[ijk.x + ni * (ijk.y + nj * ijk.z)] = value; }
        }

        public void get_x_pair(int i0, int j, int k, out float a, out float b)
        {
            int offset = ni * (j + nj * k);
            a = Buffer[offset + i0];
            b = Buffer[offset + i0 + 1];
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


        public DenseGrid2f get_slice(int slice_i, int dimension)
        {
            DenseGrid2f slice;
            if (dimension == 0) {
                slice = new DenseGrid2f(nj, nk, 0);
                for (int k = 0; k < nk; ++k)
                    for (int j = 0; j < nj; ++j)
                        slice[j, k] = Buffer[slice_i + ni * (j + nj * k)];
            } else if (dimension == 1) {
                slice = new DenseGrid2f(ni, nk, 0);
                for (int k = 0; k < nk; ++k)
                    for (int i = 0; i < ni; ++i)
                        slice[i, k] = Buffer[i + ni * (slice_i + nj * k)];
            } else {
                slice = new DenseGrid2f(ni, nj, 0);
                for (int j = 0; j < nj; ++j)
                    for (int i = 0; i < ni; ++i)
                        slice[i, j] = Buffer[i + ni * (j + nj * slice_i)];
            }
            return slice;
        }


        public void set_slice(DenseGrid2f slice, int slice_i, int dimension)
        {
            if (dimension == 0) {
                for (int k = 0; k < nk; ++k)
                    for (int j = 0; j < nj; ++j)
                        Buffer[slice_i + ni * (j + nj * k)] = slice[j, k];
            } else if (dimension == 1) {
                for (int k = 0; k < nk; ++k)
                    for (int i = 0; i < ni; ++i)
                        Buffer[i + ni * (slice_i + nj * k)] = slice[i, k];
            } else {
                for (int j = 0; j < nj; ++j)
                    for (int i = 0; i < ni; ++i)
                        Buffer[i + ni * (j + nj * slice_i)] = slice[i, j];
            }
        }



        public AxisAlignedBox3i Bounds {
            get { return new AxisAlignedBox3i(0, 0, 0, ni, nj, nk); }
        }
        public AxisAlignedBox3i BoundsInclusive {
            get { return new AxisAlignedBox3i(0, 0, 0, ni-1, nj-1, nk-1); }
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


        public Vector3i to_index(int idx) {
            int x = idx % ni;
            int y = (idx / ni) % nj;
            int z = idx / (ni * nj);
            return new Vector3i(x, y, z);
        }
        public int to_linear(int i, int j, int k)
        {
            return i + ni * (j + nj * k);
        }
        public int to_linear(ref Vector3i ijk)
        {
            return ijk.x + ni * (ijk.y + nj * ijk.z);
        }
        public int to_linear(Vector3i ijk)
        {
            return ijk.x + ni * (ijk.y + nj * ijk.z);
        }

    }






    /// <summary>
    /// 3D dense grid of integers. 
    /// </summary>
    public class DenseGrid3i
    {
        public int[] Buffer;
        public int ni, nj, nk;

        public DenseGrid3i()
        {
            ni = nj = nk = 0;
        }

        public DenseGrid3i(int ni, int nj, int nk, int initialValue)
        {
            resize(ni, nj, nk);
            assign(initialValue);
        }

        public int size { get { return ni * nj * nk; } }

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

        public int this[int i] {
            get { return Buffer[i]; }
            set { Buffer[i] = value; }
        }

        public int this[int i, int j, int k] {
            get { return Buffer[i + ni * (j + nj * k)]; }
            set { Buffer[i + ni * (j + nj * k)] = value; }
        }

        public int this[Vector3i ijk] {
            get { return Buffer[ijk.x + ni * (ijk.y + nj * ijk.z)]; }
            set { Buffer[ijk.x + ni * (ijk.y + nj * ijk.z)] = value; }
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



        public DenseGrid2i get_slice(int slice_i, int dimension)
        {
            DenseGrid2i slice;
            if ( dimension == 0 ) {
                slice = new DenseGrid2i(nj, nk, 0);
                for (int k = 0; k < nk; ++k)
                    for (int j = 0; j < nj; ++j)
                        slice[j, k] = Buffer[slice_i + ni * (j + nj * k)];
            } else if (dimension == 1) {
                slice = new DenseGrid2i(ni, nk, 0);
                for (int k = 0; k < nk; ++k)
                    for (int i = 0; i < ni; ++i)
                        slice[i, k] = Buffer[i + ni * (slice_i + nj * k)];
            } else {
                slice = new DenseGrid2i(ni, nj, 0);
                for (int j = 0; j < nj; ++j)
                    for (int i = 0; i < ni; ++i)
                        slice[i, j] = Buffer[i + ni * (j + nj * slice_i)];
            }
            return slice;
        }


        /// <summary>
        /// convert to binary bitmap
        /// </summary>
        public Bitmap3 get_bitmap(int thresh = 0)
        {
            Bitmap3 bmp = new Bitmap3(new Vector3i(ni, nj, nk));
            for (int i = 0; i < Buffer.Length; ++i)
                bmp[i] = (Buffer[i] > thresh) ? true : false;
            return bmp;
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
            for (int z = border_width; z < nk - border_width; ++z) {
                for (int y = border_width; y < stopy; ++y) {
                    for (int x = border_width; x < stopx; ++x)
                        yield return new Vector3i(x, y, z);
                }
            }
        }




    }



}
