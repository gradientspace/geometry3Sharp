using System;
using System.Collections.Generic;
using System.Threading;


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




		class SetMap
		{
			int[] buf;
			int N;
			public SetMap(int N)
			{
				buf = new int[N * N];
				this.N = N;
				for (int k = 0; k < N * N; ++k)
					buf[k] = 0;
			}

			public int this[int x, int y] {
				get {
					return buf[y * N + x];
				}
				set {
					int i = y * N + x;
					Interlocked.Increment(ref buf[i]);
				}
			}
		}



		public bool ComputeParallel()
		{
			if (A.Rows != A.Columns)
				throw new Exception("CholeskyDecomposition.ComputeParallel(): cannot be applied to non-square matrix");

			int N = A.Rows;
			L = new DenseMatrix(N, N);
			double[] Lbuf = L.Buffer;

			//Bitmap2 isSet = new Bitmap2(N, N);
			SetMap isSet = new SetMap(N);

			L[0, 0] = Math.Sqrt(A[0, 0]);
			isSet[0, 0] = 1;
			for (int r = 1; r < N; ++r) {
				L[r, 0] = A[r, 0] / L[0, 0];
				isSet[r, 0] = 1;
			}


			//gParallel.ForEach_Sequential(Interval1i.FromToInclusive(1,N-1), (r) => {

			//	double diag_dot = L[r, 0] * L[r, 0];
			//	for (int j = 1; j < r; ++j) {
			//		double row_dot = 0;
			//		for (int k = 0; k < j; k++) {
			//			while (isSet[j, k] == 0) { }   // wait for elem jk
			//			row_dot += L[r, k] * L[j, k];
			//		}
			//		L[r, j] = (1.0 / L[j, j]) * (A[r, j] - row_dot);
			//		isSet[r, j] = 1;
			//		diag_dot += L[r, j] * L[r, j];
			//	}

			//	L[r, r] = Math.Sqrt(A[r, r] - diag_dot);
			//	isSet[r, r] = 1;

			//});



			Action<Vector2i> elemF = (rj) => {
				int r = rj.x, j = rj.y;

				// diagonal element
				if (j == r) {
					double diag_dot = 0;
					for (int k = 0; k < r; ++k) {
						while (isSet[r, k] == 0) { }

						diag_dot += L[r, k] * L[r, k];
					}
					L[r, r] = Math.Sqrt(A[r, r] - diag_dot);
					isSet[r, r] = 1;
					return;
				}

				// interior-row element
				double row_dot = 0;
				for (int k = 0; k < j; k++) {
					while (isSet[r, k] == 0 && isSet[j, k] == 0) { }

					row_dot += L[r, k] * L[j, k];
				}
				L[r, j] = (1.0 / L[j, j]) * (A[r, j] - row_dot);
				isSet[r, j] = 1;
			};



//			gParallel.ForEach_Sequential(diag_itr(), elemF);
			gParallel.ForEach_Sequential(row_itr(), elemF);

			return true;
		}



		IEnumerable<Vector2i> row_itr()
		{
			int N = A.Rows;
			for (int r = 1; r < N; ++r) {
				for (int j = 1; j < r; ++j)
					yield return new Vector2i(r, j);
				yield return new Vector2i(r, r);
			}
		}


		IEnumerable<Vector2i> diag_itr()
		{
			int N = A.Rows;
			for (int r = 2; r < N; r++) {
				Vector2i rj = new Vector2i(r - 1, 1);
				while (rj.y <= rj.x) {
					yield return rj;
					rj.x--; 
					rj.y++;
				}
			}
			for (int j = 1; j < N; j++) {
				Vector2i rj = new Vector2i(N - 1, j);
				while (rj.y <= rj.x) {
					yield return rj;
					rj.x--;
					rj.y++;
				}
			}
		}

    }
}
