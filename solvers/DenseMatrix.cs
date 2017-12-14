using System;


namespace g3
{
	/// <summary>
	/// Row-major dense matrix
	/// </summary>
    public class DenseMatrix : IMatrix
    {
        double[] d;
		int N;		// rows
		int M;		// columns


        public DenseMatrix(int Nrows, int Mcols)
        {
            d = new double[Nrows*Mcols];
            Array.Clear(d, 0, d.Length);
            N = Nrows;
            M = Mcols;
        }
        public DenseMatrix(DenseMatrix copy)
        {
            N = copy.N; 
            M = copy.M;
            d = new double[N*M];
			// is there a more efficient way to do this?!?
			Array.Copy(copy.d, d, copy.d.Length);
        }

		public double[] Buffer {
			get { return d; }
		}


        public void Set(int r, int c, double value)
        {
            d[r*M+c] = value;
        }


		public void Set(double[] values)
		{
			if (values.Length != N * M)
				throw new Exception("DenseMatrix.Set: incorrect length");
			Array.Copy(values, d, d.Length);
		}


        public int Rows { get { return N; } }
        public int Columns { get { return M; } }
        public Index2i Size { get { return new Index2i(N, M); } }

        public int Length { get { return M * N; } }

        public double this[int r, int c]
        {
            get { return d[r*M+c]; }
            set { d[r*M+c] = value; }
        }
        public double this[int i]
        {
			get { return d[i]; }
            set { d[i] = value; }
        }


        public DenseVector Row(int r)
        {
            DenseVector row = new DenseVector(M);
			int ii = r * M;
            for (int i = 0; i < M; ++i)
                row[i] = d[ii+i];
            return row;
        }
        public DenseVector Column(int c)
        {
            DenseVector col = new DenseVector(N);
            for (int i = 0; i < N; ++i)
                col[i] = d[i*M+c];
            return col;
        }

        public DenseVector Diagonal()
        {
            if (M != N)
                throw new Exception("DenseMatrix.Diagonal: matrix is not square!");
            DenseVector diag = new DenseVector(N);
            for (int i = 0; i < N; ++i)
                diag[i] = d[i*M+i];
            return diag;
        }


        public DenseMatrix Transpose()
        {
            DenseMatrix t = new DenseMatrix(M, N);
            for ( int r = 0; r < N; ++r ) {
                for (int c = 0; c < M; ++c)
                    t.d[c*M+r] = d[r*M+c];
            }
            return t;
        }

        public void TransposeInPlace()
        {
            if (N != M) {
				// [TODO]: do not need to make new matrix for this case anymore...right?
                double[] d2 = new double[M*N];
                for ( int r = 0; r < N; ++r ) {
                    for (int c = 0; c < M; ++c)
                        d2[c*M+r] = d[r*M+c];
                }
                d = d2;
                int k = M; M = N; N = k;
            } else {
                for (int r = 0; r < N; ++r) {
                    for (int c = 0; c < M; ++c) {
                        if (c != r) {
							int i0 = r * M + c, i1 = c * M + r;
                            double tmp = d[i0];
                            d[i0] = d[i1];
                            d[i1] = tmp;
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
                    if (Math.Abs(d[i*M+j] - d[j*M+i]) > dTolerance)
                        return false;
                }
            }
            return true;
        }


		public bool IsPositiveDefinite()
		{
			if (M != N)
				throw new Exception("DenseMatrix.IsPositiveDefinite: matrix is not square!");
			if (IsSymmetric() == false)
				throw new Exception("DenseMatrix.IsPositiveDefinite: matrix is not symmetric!");

			for (int i = 0; i < N; ++i) {
				double diag = d[i * M + i];
				double row_sum = 0;
				for (int j = 0; j < N; ++j) {
					if (j != i)
						row_sum += Math.Abs(d[i * M + j]);
				}
				if (diag < 0 || diag < row_sum)
					return false;
			}
			return true;
		}



		public bool EpsilonEquals(DenseMatrix m2, double epsilon = MathUtil.ZeroTolerance)
		{
			if (N != m2.N || M != m2.M)
				throw new Exception("DenseMatrix.Equals: matrices are not the same size!");
			for (int i = 0; i < d.Length; ++i) {
				if (Math.Abs(d[i] - m2.d[i]) > epsilon)
					return false;
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
				int ii = i * M;
                for ( int j = 0; j < M; ++j ) {
                    Result[i] += d[ii+j] * X[j];
                }
            }
        }



        public void Add(DenseMatrix M2)
        {
            if (N != M2.N|| M != M2.M)
                throw new Exception("DenseMatrix.Add: matrices have incompatible dimensions");
			for (int i = 0; i < d.Length; ++i)
				d[i] += M2.d[i];
        }
        public void Add(IMatrix M2)
        {
            if (N != M2.Rows || M != M2.Columns)
                throw new Exception("DenseMatrix.Add: matrices have incompatible dimensions");
            for (int ri = 0; ri < N; ++ri)
                for (int ci = 0; ci < M; ++ci)
                    d[ri*M+ci] += M2[ri, ci];
        }


        public void MulAdd(DenseMatrix M2, double s)
        {
            if (N != M2.N|| M != M2.M)
                throw new Exception("DenseMatrix.MulAdd: matrices have incompatible dimensions");
			for (int i = 0; i < d.Length; ++i)
				d[i] += s*M2.d[i];
        }
        public void MulAdd(IMatrix M2, double s)
        {
            if (N != M2.Rows || M != M2.Columns)
                throw new Exception("DenseMatrix.MulAdd: matrices have incompatible dimensions");
            for (int ri = 0; ri < N; ++ri)
                for (int ci = 0; ci < M; ++ci)
                    d[ri*M+ci] += s*M2[ri, ci];
        }



        public DenseMatrix Multiply(DenseMatrix M2, bool bParallel = true)
        {
            DenseMatrix R = new DenseMatrix(Rows, M2.Columns);
            Multiply(M2, ref R, bParallel);
            return R;
        }
        public void Multiply(DenseMatrix M2, ref DenseMatrix R, bool bParallel = true)
        {
            int rows1 = N, cols1 = M;
            int rows2 = M2.N, cols2 = M2.M;

            if (cols1 != rows2)
                throw new Exception("DenseMatrix.Multiply: matrices have incompatible dimensions");

            if ( R == null )
                R = new DenseMatrix(Rows, M2.Columns);

            if (R.Rows != rows1 || R.Columns != cols2)
                throw new Exception("DenseMatrix.Multiply: Result matrix has incorrect dimensions");

            if (bParallel) {
                DenseMatrix Rt = R;
                gParallel.ForEach(Interval1i.Range(0, rows1), (r1i) => {
                    int ii = r1i * M;
                    for (int c2i = 0; c2i < cols2; c2i++) {
                        double v = 0;
                        for (int k = 0; k < cols1; ++k)
                            v += d[ii + k] * M2.d[k * M + c2i];
                        Rt[ii + c2i] = v;
                    }
                });
            } else {
                for (int r1i = 0; r1i < rows1; r1i++) {
                    int ii = r1i * M;
                    for (int c2i = 0; c2i < cols2; c2i++) {
                        double v = 0;
                        for (int k = 0; k < cols1; ++k)
                            v += d[ii + k] * M2.d[k * M + c2i];
                        R[ii + c2i] = v;
                    }
                }
            }

        }

    }

}
