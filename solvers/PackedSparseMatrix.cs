using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace g3
{
    /// <summary>
    /// This is a sparse matrix where each row is an array of (column,value) pairs
    /// This is more efficient for Matrix*Vector multiply.
    /// </summary>
    public class PackedSparseMatrix
    {
        struct nonzero
        {
            public int j;
            public double d;
        }
        nonzero[][] Rows;

        int Columns;
        bool Sorted;

        public PackedSparseMatrix(SymmetricSparseMatrix m, bool bTranspose = false)
        {
            int numRows = (bTranspose) ? m.Columns : m.Rows;
            Columns = (bTranspose) ? m.Columns : m.Rows;

            Rows = new nonzero[numRows][];

            int[] counts = new int[numRows];
            foreach ( Index2i ij in m.NonZeroIndices() ) {
                counts[ij.a]++;
                if ( ij.a != ij.b )
                    counts[ij.b]++;
            }

            for (int k = 0; k < numRows; ++k)
                Rows[k] = new nonzero[counts[k]];

            int[] accum = new int[numRows];
            foreach ( KeyValuePair<Index2i,double> ijv in m.NonZeros() ) {
                int i = ijv.Key.a, j = ijv.Key.b;
                if ( bTranspose ) {
                    int tmp = i; i = j; j = tmp;
                }

                int k = accum[i]++;
                Rows[i][k].j = j; Rows[i][k].d = ijv.Value;

                if ( i != j ) {
                    k = accum[j]++;
                    Rows[j][k].j = i; Rows[j][k].d = ijv.Value;
                }
            }

            for (int k = 0; k < numRows; ++k)
                Debug.Assert(accum[k] == counts[k]);

            Sorted = false;
        }



        public void Sort()
        {
            gParallel.ForEach(Interval1i.Range(Rows.Length), (i) => {
                Array.Sort(Rows[i], (x, y) => { return x.j.CompareTo(y.j); });
            });
            //for ( int i = 0; i < Rows.Length; ++i ) 
            //    Array.Sort(Rows[i], (x, y) => { return x.j.CompareTo(y.j); }  );
            Sorted = true;
        }



        public void Multiply(double[] X, double[] Result)
        {
            Array.Clear(Result, 0, Result.Length);

            for ( int i = 0; i < Rows.Length; ++i ) {
                int n = Rows[i].Length;
                for ( int k = 0; k < n; ++k ) {
                    int j = Rows[i][k].j;
                    Result[i] += Rows[i][k].d * X[j];
                }
            }
        }



        /// <summary>
        /// Compute dot product of this.row[r] and M.col[c], where the
        /// column is stored as MTranspose.row[c]
        /// </summary>
        public double DotRowColumn(int r, int c, PackedSparseMatrix MTranspose)
        {
            Debug.Assert(Sorted && MTranspose.Sorted);
            Debug.Assert(Rows.Length == MTranspose.Rows.Length);

            int a = 0;
            int b = 0;
            nonzero[] Row = Rows[r];
            nonzero[] Col = MTranspose.Rows[c];
            int NA = Row.Length;
            int NB = Col.Length;

            double sum = 0;
            while (a < NA && b < NB) { 
                if ( Row[a].j == Col[b].j ) {
                    sum += Row[a].d * Col[b].d;
                    a++;
                    b++;
                } else if ( Row[a].j < Col[b].j ) {
                    a++;
                } else {
                    b++;
                }
            }

            return sum;
        }




        /// <summary>
        /// Compute dot product of this.row[r] with all columns of M,
        /// where columns are stored in MTranspose rows.
        /// In theory more efficient than doing DotRowColumn(r,c) for each c, 
        /// however so far the difference is negligible...perhaps because
        /// there are quite a few more branches in the inner loop
        /// </summary>
        public void DotRowAllColumns(int r, double[] sums, int[] col_indices, PackedSparseMatrix MTranspose)
        {
            Debug.Assert(Sorted && MTranspose.Sorted);
            Debug.Assert(Rows.Length == MTranspose.Rows.Length);

            int N = Rows.Length;
            int a = 0;
            nonzero[] Row = Rows[r];
            int NA = Row.Length;

            Array.Clear(sums, 0,  N);
            Array.Clear(col_indices, 0, N);

            while ( a < NA ) {
                int aj = Row[a].j;
                for ( int ci = 0; ci < N; ++ci ) {
                    nonzero[] Col = MTranspose.Rows[ci];

                    int b = col_indices[ci];
                    if (b >= Col.Length)
                        continue;

                    while (b < Col.Length && Col[b].j < aj ) 
                        b++;

                    if (b < Col.Length && aj == Col[b].j) {
                        sums[ci] += Row[a].d * Col[b].d;
                        b++;
                    }
                    col_indices[ci] = b;
                }
                a++;
            }

        }


    }
}
