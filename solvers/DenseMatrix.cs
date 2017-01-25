using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    public class DenseMatrix
    {
        double[,] d;
        int N, M;


        public DenseMatrix(int Nrows, int Mcols)
        {
            d = new double[Nrows, Mcols];
            Array.Clear(d, 0, d.Length);
            N = Nrows;
            M = Mcols;
        }


        public void Set(int r, int c, double value)
        {
            d[r, c] = value;
        }


        public int Rows { get { return N; } }
        public int Columns { get { return N; } }
        public Index2i Size { get { return new Index2i(N, N); } }


        public double this[int r, int c]
        {
            get { return d[r, c]; }
            set { d[r, c] = value; }
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
    }
}
