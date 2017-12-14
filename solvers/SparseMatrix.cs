using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading;

namespace g3
{
    /// <summary>
    /// Basic sparse-symmetric-matrix class. Stores upper-triangular portion.
    /// Uses Dictionary as sparsifying data structure, which is probably
    /// not a good option. But it is easy.
    /// </summary>
    public class SymmetricSparseMatrix : IMatrix
    {
        Dictionary<Index2i, double> d = new Dictionary<Index2i, double>();
        int N;


        public SymmetricSparseMatrix(int setN = 0) {
            N = setN;
        }

        public SymmetricSparseMatrix(DenseMatrix m)
        {
            if (m.Rows != m.Columns)
                throw new Exception("SymmetricSparseMatrix(DenseMatrix): Matrix is not square!");
            if (m.IsSymmetric() == false)
                throw new Exception("SymmetricSparseMatrix(DenseMatrix): Matrix is not symmetric!");
            N = m.Rows;
            for ( int i = 0; i < N; ++i ) {
                for (int j = i; j < N; ++j)
                    Set(i, j, m[i, j]);
            }
        }


        public SymmetricSparseMatrix(SymmetricSparseMatrix m)
        {
            N = m.N;
            d = new Dictionary<Index2i, double>(m.d);
        }


        public void Set(int r, int c, double value)
        {
            Index2i v = new Index2i(Math.Min(r, c), Math.Max(r, c));
            d[v] = value;
            if (r >= N) N = r+1;
            if (c >= N) N = c+1;
        }


        public int Rows { get { return N; } }
        public int Columns { get { return N; } }
        public Index2i Size { get { return new Index2i(N, N); } }


        public double this[int r, int c]
        {
            get {
                Index2i v = new Index2i(Math.Min(r, c), Math.Max(r, c));
                double value;
                if (d.TryGetValue(v, out value))
                    return value;
                return 0;
            }
            set {
                Set(r, c, value);
            }
        }


        public void Multiply(double[] X, double[] Result)
        {
            Array.Clear(Result, 0, Result.Length);

            foreach ( KeyValuePair<Index2i,double> v in d ) {
                int i = v.Key.a;
                int j = v.Key.b;
                Result[i] += v.Value * X[j];
                if (i != j)
                    Result[j] += v.Value * X[i];
            }
        }



        // returns this*this (requires less memory)
        public SymmetricSparseMatrix Square(bool bParallel = true)
        {
            SymmetricSparseMatrix R = new SymmetricSparseMatrix();
            PackedSparseMatrix M = new PackedSparseMatrix(this);
            M.Sort();

            // Parallel variant is vastly faster, uses spinlock to control access to R
            if (bParallel) {

                // goddamn SpinLock is in .Net 4
                //SpinLock spin = new SpinLock();
                gParallel.ForEach(Interval1i.Range(N), (r1i) => {
                    for (int c2i = r1i; c2i < N; c2i++) {
                        double v = M.DotRowColumn(r1i, c2i, M);
                        if (Math.Abs(v) > MathUtil.ZeroTolerance) {
                            //bool taken = false;
                            //spin.Enter(ref taken);
                            //Debug.Assert(taken);
                            //R[r1i, c2i] = v;
                            //spin.Exit();
                            lock(R) {
                                R[r1i, c2i] = v;
                            }
                        }
                    }
                });

            } else {
                for (int r1i = 0; r1i < N; r1i++) {
                    for (int c2i = r1i; c2i < N; c2i++) {
                        double v = M.DotRowColumn(r1i, c2i, M);
                        if (Math.Abs(v) > MathUtil.ZeroTolerance)
                            R[r1i, c2i] = v;
                    }
                }
            }

            return R;
        }







        /// <summary>
        /// Returns this*this, as a packed sparse matrix. Computes in parallel.
        /// </summary>
        public PackedSparseMatrix SquarePackedParallel()
        {
            PackedSparseMatrix M = new PackedSparseMatrix(this);
            M.Sort();
            return M.Square();
        }





        public SymmetricSparseMatrix Multiply(SymmetricSparseMatrix M2)
        {
            SymmetricSparseMatrix R = new SymmetricSparseMatrix();
            Multiply(M2, ref R);
            return R;
        }
        public void Multiply(SymmetricSparseMatrix M2, ref SymmetricSparseMatrix R, bool bParallel = true)
        {
            // testing code
            //multiply_slow(M2, ref R);
            //SymmetricSparseMatrix R2 = new SymmetricSparseMatrix();
            //multiply_fast(M2, ref R2);
            //Debug.Assert(R.EpsilonEqual(R2));

            multiply_fast(M2, ref R, bParallel);
        }


        /// <summary>
        /// Construct packed versions of input matrices, and then use sparse row/column dot
        /// to compute elements of output matrix. This is faster. But still relatively expensive.
        /// </summary>
        void multiply_fast(SymmetricSparseMatrix M2in, ref SymmetricSparseMatrix Rin, bool bParallel)
        {
            int N = Rows;
            if (M2in.Rows != N)
                throw new Exception("SymmetricSparseMatrix.Multiply: matrices have incompatible dimensions");

            if ( Rin == null )
                Rin = new SymmetricSparseMatrix();
            SymmetricSparseMatrix R = Rin;      // require alias for use in lambda below

            PackedSparseMatrix M = new PackedSparseMatrix(this);
            M.Sort();
            PackedSparseMatrix M2 = new PackedSparseMatrix(M2in, true);
            M2.Sort();

            // Parallel variant is vastly faster, uses spinlock to control access to R
            if (bParallel) {

                // goddamn SpinLock is in .Net 4
                //SpinLock spin = new SpinLock();
                gParallel.ForEach(Interval1i.Range(N), (r1i) => {
                    for (int c2i = r1i; c2i < N; c2i++) {
                        double v = M.DotRowColumn(r1i, c2i, M2);
                        if (Math.Abs(v) > MathUtil.ZeroTolerance) {
                            //bool taken = false;
                            //spin.Enter(ref taken);
                            //Debug.Assert(taken);
                            //R[r1i, c2i] = v;
                            //spin.Exit();
                            lock(R) {
                                R[r1i, c2i] = v;
                            }
                        }
                    }
                });

            } else {

                for (int r1i = 0; r1i < N; r1i++) {
                    for (int c2i = r1i; c2i < N; c2i++) {
                        double v = M.DotRowColumn(r1i, c2i, M2);
                        if (Math.Abs(v) > MathUtil.ZeroTolerance)
                            R[r1i, c2i] = v;
                    }
                }
            }
        }


        /// <summary>
        /// directly multiply the matrices. This is very slow as the matrix gets
        /// larger because we are iterating over all indices to find nonzeros
        /// </summary>
        void multiply_slow(SymmetricSparseMatrix M2, ref SymmetricSparseMatrix R)
        {
            // this multiply is probably not ideal....

            int N = Rows;
            if (M2.Rows != N)
                throw new Exception("SymmetricSparseMatrix.Multiply: matrices have incompatible dimensions");

            if ( R == null )
                R = new SymmetricSparseMatrix();

            List<mval> row = new List<mval>(128);
            for ( int r1i = 0; r1i < N; r1i++ ) {

                row.Clear();
                this.get_row_nonzeros(r1i, row);
                int rN = row.Count;

                for ( int c2i = r1i; c2i < N; c2i++ ) {
                    double v = 0;

                    // would it be faster to convert cols to mval lists??
                    for (int ri = 0; ri < rN; ++ri) {
                        int k = row[ri].k;
                        v += row[ri].v * M2[k, c2i];
                    }

                    if (Math.Abs(v) > MathUtil.ZeroTolerance)
                        R[r1i, c2i] = v;
                }
            }
        }





        public IEnumerable<KeyValuePair<Index2i,double>> NonZeros()
        {
            return d;
        }
        public IEnumerable<Index2i> NonZeroIndices()
        {
            return d.Keys;
        }


        public bool EpsilonEqual(SymmetricSparseMatrix B, double eps = MathUtil.Epsilon)
        {
            foreach ( var val in d ) {
                if (Math.Abs(B[val.Key.a, val.Key.b] - val.Value) > eps)
                    return false;
            }
            foreach ( var val in B.d) {
                if (Math.Abs(this[val.Key.a, val.Key.b] - val.Value) > eps)
                    return false;
            }
            return true;
        }



        struct mval
        {
            public int k;
            public double v;
        }
        void get_row_nonzeros(int r, List<mval> buf)
        {
            // TODO: optimize this - exploit symmetry, etc
            int N = Rows;
            for ( int i = 0; i < N; ++i ) {
                double d = this[r, i];
                if (d != 0) {
                    buf.Add(new mval() { k = i, v = d });
                }
            }
        }


    }






    public class DiagonalMatrix
    {
        public double[] D;

        public DiagonalMatrix(int N)
        {
            D = new double[N];
        }

        public void Clear()
        {
            Array.Clear(D, 0, D.Length);
        }

        public void Set(int r, int c, double value)
        {
            if (r == c)
                D[r] = value;
            else
                throw new Exception("DiagonalMatrix.Set: tried to set off-diagonal entry!");
        }


        public int Rows { get { return D.Length; } }
        public int Columns { get { return D.Length; } }
        public Index2i Size { get { return new Index2i(D.Length, D.Length); } }


        public double this[int r, int c]
        {
            get {
                Debug.Assert(r == c);
                return D[r];
            }
            set {
                Set(r, c, value);
            }
        }


        public void Multiply(double[] X, double[] Result)
        {
            //Array.Clear(Result, 0, Result.Length);
            for (int i = 0; i < X.Length; ++i)
                //Result[i] += d[i] * X[i];
                Result[i] = D[i] * X[i];
        }
    }

}
