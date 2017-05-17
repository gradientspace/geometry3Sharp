using System;


namespace g3
{
    public class DenseMatrix : IMatrix
    {
        double[,] d;        // TODO: using multi-d array is a real pain...
        int N, M;


        public DenseMatrix(int Nrows, int Mcols)
        {
            d = new double[Nrows, Mcols];
            Array.Clear(d, 0, d.Length);
            N = Nrows;
            M = Mcols;
        }
        public DenseMatrix(DenseMatrix copy)
        {
            N = copy.N; 
            M = copy.M;
            d = new double[N, M];
            // is there a more efficient way to do this?!?
            for (int i = 0; i < N; ++i)
                for (int j = 0; j < M; ++j)
                    d[i, j] = copy.d[i, j];
        }


        public void Set(int r, int c, double value)
        {
            d[r, c] = value;
        }


        public int Rows { get { return N; } }
        public int Columns { get { return M; } }
        public Index2i Size { get { return new Index2i(N, M); } }

        public int Length { get { return M * N; } }

        public double this[int r, int c]
        {
            get { return d[r, c]; }
            set { d[r, c] = value; }
        }
        public double this[int i]
        {
            get { int r = i / M; return d[r, i-r*M]; }
            set { int r = i / M; d[r, i-r*M] = value; }
        }


        public DenseVector Row(int r)
        {
            DenseVector row = new DenseVector(M);
            for (int i = 0; i < M; ++i)
                row[i] = d[r, i];
            return row;
        }
        public DenseVector Column(int c)
        {
            DenseVector col = new DenseVector(N);
            for (int i = 0; i < N; ++i)
                col[i] = d[i, c];
            return col;
        }

        public DenseVector Diagonal()
        {
            if (M != N)
                throw new Exception("DenseMatrix.Diagonal: matrix is not square!");
            DenseVector diag = new DenseVector(N);
            for (int i = 0; i < N; ++i)
                diag[i] = d[i, i];
            return diag;
        }


        public DenseMatrix Transpose()
        {
            DenseMatrix t = new DenseMatrix(M, N);
            for ( int r = 0; r < N; ++r ) {
                for (int c = 0; c < M; ++c)
                    t.d[c, r] = d[r, c];
            }
            return t;
        }

        public void TransposeInPlace()
        {
            if (N != M) {
                double[,] d2 = new double[M, N];
                for ( int r = 0; r < N; ++r ) {
                    for (int c = 0; c < M; ++c)
                        d2[c, r] = d[r, c];
                }
                d = d2;
                int k = M; M = N; N = k;
            } else {
                for (int r = 0; r < N; ++r) {
                    for (int c = 0; c < M; ++c) {
                        if (c != r) {
                            double tmp = d[r, c];
                            d[r, c] = d[c, r];
                            d[c, r] = tmp;
                        }
                    }
                }
            }
        }




        public bool IsSymmetric(double dTolerance = MathUtil.Epsilon)
        {
            if (M != N)
                throw new Exception("DenseMatrix.IsSymmetric: matrix is not square!");
            for (int i = 0; i < N; ++i ) {
                for ( int j = 0; j < i; ++j ) {
                    if (Math.Abs(d[i, j] - d[j, i]) > dTolerance)
                        return false;
                }
            }
            return true;
        }





        public DenseVector Multiply(DenseVector X)
        {
            DenseVector R = new DenseVector(X.Length);
            Multiply(X.Buffer, R.Buffer);
            return R;
        }
        public void Multiply(DenseVector X, DenseVector R)
        {
            Multiply(X.Buffer, R.Buffer);
        }
        public void Multiply(double[] X, double[] Result)
        {
            for ( int i = 0; i < N; ++i ) {
                Result[i] = 0;
                for ( int j = 0; j < M; ++j ) {
                    Result[i] += d[i, j] * X[j];
                }
            }
        }



        public void Add(DenseMatrix M2)
        {
            if (N != M2.N|| M != M2.M)
                throw new Exception("DenseMatrix.Add: matrices have incompatible dimensions");
            for (int ri = 0; ri < N; ++ri)
                for (int ci = 0; ci < M; ++ci)
                    d[ri, ci] += M2.d[ri, ci];
        }
        public void Add(IMatrix M2)
        {
            if (N != M2.Rows || M != M2.Columns)
                throw new Exception("DenseMatrix.Add: matrices have incompatible dimensions");
            for (int ri = 0; ri < N; ++ri)
                for (int ci = 0; ci < M; ++ci)
                    d[ri, ci] += M2[ri, ci];
        }


        public void MulAdd(DenseMatrix M2, double s)
        {
            if (N != M2.N|| M != M2.M)
                throw new Exception("DenseMatrix.MulAdd: matrices have incompatible dimensions");
            for (int ri = 0; ri < N; ++ri)
                for (int ci = 0; ci < M; ++ci)
                    d[ri, ci] += s*M2.d[ri, ci];
        }
        public void MulAdd(IMatrix M2, double s)
        {
            if (N != M2.Rows || M != M2.Columns)
                throw new Exception("DenseMatrix.MulAdd: matrices have incompatible dimensions");
            for (int ri = 0; ri < N; ++ri)
                for (int ci = 0; ci < M; ++ci)
                    d[ri, ci] += s*M2[ri, ci];
        }



        public DenseMatrix Multiply(DenseMatrix M2)
        {
            DenseMatrix R = new DenseMatrix(Rows, M2.Columns);
            Multiply(M2, ref R);
            return R;
        }
        public void Multiply(DenseMatrix M2, ref DenseMatrix R)
        {
            int rows1 = N, cols1 = M;
            int rows2 = M2.N, cols2 = M2.M;

            if (cols1 != rows2)
                throw new Exception("DenseMatrix.Multiply: matrices have incompatible dimensions");

            if ( R == null )
                R = new DenseMatrix(Rows, M2.Columns);

            if (R.Rows != rows1 || R.Columns != cols2)
                throw new Exception("DenseMatrix.Multiply: Result matrix has incorrect dimensions");

            for ( int r1i = 0; r1i < rows1; r1i++ ) {
                for ( int c2i = 0; c2i < cols2; c2i++ ) {
                    double v = 0;
                    for (int k = 0; k < cols1; ++k)
                        v += d[r1i, k] * M2.d[k, c2i];
                    R[r1i, c2i] = v;
                }
            }

        }

    }
}
