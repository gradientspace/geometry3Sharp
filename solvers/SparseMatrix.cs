using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    public class SymmetricSparseMatrix
    {
        Dictionary<Index2i, double> d = new Dictionary<Index2i, double>();
        int N;

        public void Set(int r, int c, double value)
        {
            Index2i v = new Index2i(Math.Min(r, c), Math.Max(r, c));
            d[v] = value;
            if (r > N) N = r;
            if (c > N) N = c;
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
            Array.Clear(Result, 0, Result.Length);
            for (int i = 0; i < X.Length; ++i)
                Result[i] += d[i] * X[i];
        }
    }

}
