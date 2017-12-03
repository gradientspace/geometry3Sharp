using System;


namespace g3
{

	/// <summary>
	/// Computes Cholesky decomposition/factorization L of matrix A
	/// A must be symmetric and positive-definite
	/// computed lower-triangular matrix L satisfies L*L^T = A.
	/// https://en.wikipedia.org/wiki/Cholesky_decomposition
	/// 
	/// 
	/// </summary>
    public class CholeskyDecomposition
    {
		public DenseMatrix A;

		public DenseMatrix L;


		public CholeskyDecomposition(DenseMatrix m)
		{
			A = m;
		}


		public bool Compute()
		{
			if (A.Rows != A.Columns)
				throw new Exception("CholeskyDecomposition.Compute(): cannot be applied to non-square matrix");

			int N = A.Rows;
			L = new DenseMatrix(N, N);
			double[] Lbuf = L.Buffer;

			L[0, 0] = Math.Sqrt(A[0, 0]);
			for (int r = 1; r < N; ++r) {

				L[r, 0] = A[r,0] / L[0,0];

				// fill in row up to diagonal element
				double diag_dot = L[r,0]*L[r,0];
				for (int j = 1; j < r; j++) {
					
					double row_dot = 0;
					int rk = r * N, jk = j * N;
					int jk_stop = jk + j;
					while ( jk < jk_stop ) {
						row_dot += Lbuf[rk++] * Lbuf[jk++];
					}

					L[r,j] = (1.0/L[j,j]) * (A[r,j] - row_dot);

					diag_dot += L[r, j] * L[r, j];
				}

				// now do diagonal element
				//double diag_dot = 0;
				//for (int k = 0; k < r; ++k)
					//diag_dot += L[r,k] * L[r,k];
				L[r,r] = Math.Sqrt( A[r,r] - diag_dot);
			}

			return true;
		}



    }
}
