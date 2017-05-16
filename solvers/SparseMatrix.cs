using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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


        public SymmetricSparseMatrix Multiply(SymmetricSparseMatrix M2)
        {
            SymmetricSparseMatrix R = new SymmetricSparseMatrix();
            Multiply(M2, ref R);
            return R;
        }
        public void Multiply(SymmetricSparseMatrix M2, ref SymmetricSparseMatrix R)
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
        double[] d;

        public DiagonalMatrix(int N)
        {
            d = new double[N];
        }

        public void Clear()
        {
            Array.Clear(d, 0, d.Length);
        }

        public void Set(int r, int c, double value)
        {
            if (r == c)
                d[r] = value;
            else
                throw new Exception("DiagonalMatrix.Set: tried to set off-diagonal entry!");
        }


        public int Rows { get { return d.Length; } }
        public int Columns { get { return d.Length; } }
        public Index2i Size { get { return new Index2i(d.Length, d.Length); } }


        public double this[int r, int c]
        {
            get {
                if (r != c)
                    throw new Exception("DiagonalMatrix.this[]: tried to get off-diagonal entry!");
                return d[r];
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
                Result[i] = d[i] * X[i];
        }
    }

}
