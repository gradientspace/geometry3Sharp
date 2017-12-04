using System;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;

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
					while ( jk < jk_stop ) {    				// k from 0 to j-1
						row_dot += Lbuf[rk++] * Lbuf[jk++];   	// L[r,k] * L[j,k]
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


        /// <summary>
        /// Parallel version of Cholesky Decomposition, is about 2x faster on 4-cpu/8-HT
        /// </summary>
		public bool ComputeParallel()
		{
			if (A.Rows != A.Columns)
				throw new Exception("CholeskyDecomposition.ComputeParallel(): cannot be applied to non-square matrix");

			int N = A.Rows;
			L = new DenseMatrix(N, N);
			double[] Lbuf = L.Buffer;

            Action<int> compute_diag = (r) => {
                double diag_dot = 0;
                int ck = r * N; int ck_stop = ck + r;
                do {
                    diag_dot += Lbuf[ck] * Lbuf[ck];
                } while (ck++ < ck_stop);
                L[r, r] = Math.Sqrt(A[r, r] - diag_dot);
            };

            // compute initial column
            L[0, 0] = Math.Sqrt(A[0, 0]);
            for (int r = 1; r < N; ++r) {
                L[r, 0] = A[r, 0] / L[0, 0];
            }

            // fill diag (1,1)
            compute_diag(1);

            // once we have computed a diagonal element, we can parallel-fill
            // the column below it
            for (int c = 1; c < N; ++c) {

                int num_rows = (N-1) - (c+1);
                gParallel.BlockStartEnd(c + 1, N - 1, (a, b) => {
                    for (int r = a; r <= b; ++r) {
                        double row_dot = 0;

                        int rk = r * N, jk = c * N;
                        int jk_stop = jk + c;
                        while (jk < jk_stop) {                  // k from 0 to c-1
                            row_dot += Lbuf[rk++] * Lbuf[jk++];     // L[r,k] * L[c,k]
                        }

                        L[r, c] = (1.0 / L[c, c]) * (A[r, c] - row_dot);

                        if (r == c + 1)
                            compute_diag(r);   // compute diagonal for next column
                    }
                }, Math.Max(num_rows/20, 1) );     
            }


            //
            // Alternative algorithm that fills along upward-diagonals. 
            // This requires some spinlock-style waiting if we require an element
            // that has not been computed. The overhead of this seems to basically
            // make it slower than the down-columns version.
            //

            //int[] row_progress = new int[N];
            //for (int r = 0; r < N; ++r)
            //    row_progress[r] = 1;

            //Action<Vector2i> elemF = (rj) => {
            //    int r = rj.x, j = rj.y;

            //    // diagonal element
            //    if (j == r) {
            //        double diag_dot = 0;
            //        while (row_progress[r] < r - 1) { }
            //        for (int k = 0; k < r; ++k) {
            //            diag_dot += L[r, k] * L[r, k];
            //        }
            //        L[r, r] = Math.Sqrt(A[r, r] - diag_dot);
            //        Interlocked.Increment(ref row_progress[r]);
            //        return;
            //    }

            //    // interior-row element
            //    double row_dot = 0;
            //    while (row_progress[r] < j - 1 && row_progress[j] < j - 1) { }
            //    for (int k = 0; k < j; k++) {
            //        row_dot += L[r, k] * L[j, k];
            //    }
            //    L[r, j] = (1.0 / L[j, j]) * (A[r, j] - row_dot);
            //    Interlocked.Increment(ref row_progress[r]);
            //};
            //gParallel.ForEach(diag_itr(), elemF);

            return true;
		}


        // up-to-the-right diagonal iteration throuh matrix indices (below or on diagonal)
		IEnumerable<Vector2i> diag_itr()
		{
			int N = A.Rows;
            // first we go down the rows, and from each (row,0) we go up-right
            // (in cholesky context we skip (row,0) though, as it is already set)
			for (int r = 2; r < N; r++) {
				Vector2i rj = new Vector2i(r - 1, 1);
				while (rj.y <= rj.x) {
					yield return rj;
					rj.x--; 
					rj.y++;
				}
			}
            // once we hit bottom of matrix, we walk along bottom row, going up/right
            // at each element
			for (int j = 1; j < N; j++) {
				Vector2i rj = new Vector2i(N - 1, j);
				while (rj.y <= rj.x) {
					yield return rj;
					rj.x--;
					rj.y++;
				}
			}
		}





        /// <summary>
        /// Solve A*X=B via backsubstitution in cholesky factorization
        /// Requires temporary vector Y
        /// </summary>
        public void Solve(double[] B, double[] X, double[] Y)
        {
            int N = A.Rows;
            if (Y == null)
                Y = new double[N];

            // first solve L * Y = B  (store Y in X)   via forward substitution
            Y[0] = B[0] / L[0, 0];
            for ( int i = 1; i < N; ++i ) {
                double sum = 0;
                for ( int j = 0; j < i; ++j ) {
                    sum += L[i, j] * Y[j];
                }
                Y[i] = (B[i] - sum) / L[i, i];
            }

            // now solve L^T * X = Y  via backsubstutition
            X[N-1] = Y[N-1] / L[N-1,N-1];
            for ( int i = N-2; i >= 0; --i ) {
                double sum = 0;
                for ( int j = i+1; j < N; ++j ) {
                    sum += L[j,i] * X[j];
                }
                X[i] = (Y[i] - sum) / L[i, i];
            }

        }

    }
}
