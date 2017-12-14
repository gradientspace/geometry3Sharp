using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace g3
{
    public struct matrix_entry
    {
        public int r;
        public int c;
        public double value;
    }



    /// <summary>
    /// This is a sparse matrix where each row is an array of (column,value) pairs
    /// This is more efficient for Matrix*Vector multiply.
    /// </summary>
    public class PackedSparseMatrix
    {
        public struct nonzero
        {
            public int j;
            public double d;
        }
        public nonzero[][] Rows;

        public int Columns = 0;
        public bool Sorted = false;
        public int NumNonZeros = 0;

        public enum StorageModes
        {
            Full
        }
        public StorageModes StorageMode = StorageModes.Full;

        // [TODO] this should be enum w/ 3 states (yes, no, unknown)
        public bool IsSymmetric = false;


        public PackedSparseMatrix(PackedSparseMatrix copy)
        {
            int N = copy.Rows.Length;
            Rows = new nonzero[N][];
            for ( int r = 0; r < N; ++r ) {
                Rows[r] = new nonzero[copy.Rows[r].Length];
                Array.Copy(copy.Rows[r], Rows[r], Rows[r].Length);
            }
            Columns = copy.Columns;
            Sorted = copy.Sorted;
            NumNonZeros = copy.NumNonZeros;
            StorageMode = copy.StorageMode;
            IsSymmetric = copy.IsSymmetric;
        }


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

            NumNonZeros = 0;
            for (int k = 0; k < numRows; ++k) {
                Rows[k] = new nonzero[counts[k]];
                NumNonZeros += counts[k];
            }


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

            //for (int k = 0; k < numRows; ++k)
            //    Debug.Assert(accum[k] == counts[k]);

            Sorted = false;
            IsSymmetric = true;
            StorageMode = StorageModes.Full;
        }




        public PackedSparseMatrix(DVector<matrix_entry> entries, int numRows, int numCols, bool bSymmetric = true)
        {
            Columns = numCols;
            Rows = new nonzero[numRows][];

            int N = entries.size;
            int[] counts = new int[numRows];
            for ( int i = 0; i < N; ++i ) { 
                counts[entries[i].r]++;
                if (bSymmetric && entries[i].r != entries[i].c)
                    counts[entries[i].c]++;
            }

            NumNonZeros = 0;
            for (int k = 0; k < numRows; ++k) {
                Rows[k] = new nonzero[counts[k]];
                NumNonZeros += counts[k];
            }

            int[] accum = new int[numRows];
            for ( int i = 0; i < N; ++i ) {
                matrix_entry e = entries[i];
                int k = accum[e.r]++;
                Rows[e.r][k].j = e.c;
                Rows[e.r][k].d = e.value;

                if (bSymmetric && e.c != e.r ) {
                    k = accum[e.c]++;
                    Rows[e.c][k].j = e.r;
                    Rows[e.c][k].d = e.value;
                }
            }

            //for (int k = 0; k < numRows; ++k)
            //    Debug.Assert(accum[k] == counts[k]);

            Sorted = false;
            IsSymmetric = bSymmetric;
            StorageMode = StorageModes.Full;
        }



        public static PackedSparseMatrix FromDense(DenseMatrix m, bool bSymmetric)
        {
            DVector<matrix_entry> nonzeros = new DVector<matrix_entry>();
            for ( int r = 0; r < m.Rows; ++r) {
                int nStop = (bSymmetric) ? r + 1 : m.Columns;
                for ( int c = 0; c < nStop; ++c ) {
                    if ( m[r,c] != 0 ) 
                        nonzeros.Add(new matrix_entry() { r = r, c = c, value = m[r, c] });
                }
            }
            return new PackedSparseMatrix(nonzeros, m.Rows, m.Columns, bSymmetric);
        }





        public double this[int r, int c] {
            get {
                nonzero[] row = Rows[r];
                int n = row.Length;
                for (int k = 0; k < n; ++k) {
                    if (row[k].j == c)
                        return row[k].d;
                }
                return 0;
            }
            set {
                nonzero[] row = Rows[r];
                int n = row.Length;
                for (int k = 0; k < n; ++k) {
                    if (row[k].j == c) {
                        row[k].d = value;
                        return;
                    }
                }
                throw new Exception("PackedSparseMatrix[r,c]: value at index " + r.ToString() + "," + c.ToString() + " does not exist!");
            }
        }


        /// <summary>
        /// sort each row
        /// </summary>
        public void Sort(bool bParallel = true) {
            if (bParallel) {
                gParallel.BlockStartEnd(0, Rows.Length - 1, (a, b) => {
                    for (int i = a; i <= b; i++) {
                        Array.Sort(Rows[i], (x, y) => { return x.j.CompareTo(y.j); });
                    }
                });
            } else {
                for (int i = 0; i < Rows.Length; ++i)
                    Array.Sort(Rows[i], (x, y) => { return x.j.CompareTo(y.j); });
            }
            Sorted = true;
        }



        /// <summary>
        /// For row r, find interval that nonzeros lie in
        /// </summary>
        public Interval1i NonZerosRange(int r)
        {
            nonzero[] Row = Rows[r];
            if (Row.Length == 0)
                return Interval1i.Empty;
            if (Sorted == false) {
                Interval1i range = Interval1i.Empty;
                for (int i = 0; i < Row.Length; ++i)
                    range.Contain(Row[i].j);
                return range;
            } else {
                return new Interval1i(Row[0].j, Row[Row.Length - 1].j);
            }
        }


        public IEnumerable<Vector2i> NonZeroIndicesByRow(bool bWantSorted = true)
        {
            if (bWantSorted && Sorted == false)
                throw new Exception("PackedSparseMatrix.NonZeroIndicesByRow: sorting requested but not available");
            int N = Rows.Length;
            for ( int r = 0; r < N; ++r ) {
                nonzero[] Row = Rows[r];
                for (int i = 0; i < Row.Length; ++i)
                    yield return new Vector2i(r, Row[i].j);
            }
        }



        public IEnumerable<Vector2i> NonZeroIndicesForRow(int r, bool bWantSorted = true)
        {
            if (bWantSorted && Sorted == false)
                throw new Exception("PackedSparseMatrix.NonZeroIndicesByRow: sorting requested but not available");
            nonzero[] Row = Rows[r];
            for (int i = 0; i < Row.Length; ++i)
                yield return new Vector2i(r, Row[i].j);
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


        public void Multiply_Parallel(double[] X, double[] Result)
        {
            gParallel.BlockStartEnd(0, Rows.Length - 1, (i_start, i_end) => {
                for (int i = i_start; i <= i_end; ++i) {
                    Result[i] = 0;
                    nonzero[] row = Rows[i];
                    int n = row.Length;
                    for (int k = 0; k < n; ++k) {
                        Result[i] += row[k].d * X[row[k].j];
                    }
                }
            });
        }


        /// <summary>
        /// Hardcoded variant for 3 RHS vectors, much faster
        /// </summary>
        public void Multiply_Parallel_3(double[][] X, double[][] Result)
        {
            int j = X.Length;
            gParallel.BlockStartEnd(0, Rows.Length - 1, (i_start, i_end) => {
                for (int i = i_start; i <= i_end; ++i) {
                    Result[0][i] = Result[1][i] = Result[2][i] = 0;
                    nonzero[] row = Rows[i];
                    int n = row.Length;
                    for (int k = 0; k < n; ++k) {
                        int rowkj = row[k].j;
                        double d = row[k].d;
                        Result[0][i] += d * X[0][rowkj];
                        Result[1][i] += d * X[1][rowkj];
                        Result[2][i] += d * X[2][rowkj];
                    }
                }
            });
        }


        /// <summary>
        /// Compute dot product of this.row[r] and M.col[c], where the
        /// column is stored as MTranspose.row[c]
        /// </summary>
        public double DotRowColumn(int r, int c, PackedSparseMatrix MTranspose)
        {
            if (Sorted == false || MTranspose.Sorted == false)
                throw new Exception("PackedSparseMatrix.DotRowColumn: matrices must be sorted!");
            if (Rows.Length != MTranspose.Rows.Length )
                throw new Exception("PackedSparseMatrix.DotRowColumn: matrices are not the same size!");

            Debug.Assert(Sorted && MTranspose.Sorted);
            Debug.Assert(Rows.Length == MTranspose.Rows.Length);

            int ri = 0;
            int ci = 0;
            nonzero[] Row = Rows[r];
            nonzero[] Col = MTranspose.Rows[c];
            int NR = Row.Length;
            int NC = Col.Length;
            int last_col_j = Col[NC-1].j;
            int last_row_j = Row[NR-1].j;

            double sum = 0;
            while (ri < NR && ci < NC) {
                // early out if we passed last nonzero in other array
                if (Row[ri].j > last_col_j || Col[ci].j > last_row_j)
                    break;
                if (Row[ri].j == Col[ci].j) {
                    sum += Row[ri].d * Col[ci].d;
                    ri++;
                    ci++;
                } else if (Row[ri].j < Col[ci].j) {
                    ri++;
                } else {
                    ci++;
                }
            }

            return sum;
        }


        /// <summary>
        /// Dot product of this.row[r] with itself
        /// </summary>
        public double DotRowSelf(int r)
        {
            nonzero[] Row = Rows[r];
            double sum = 0;
            for (int ri = 0; ri < Row.Length; ri++)
                sum += Row[ri].d * Row[ri].d;
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




        /// <summary>
        /// Compute dot product of this.row[r1] and this.row[r2], up to N elements
        /// </summary>
        public double DotRows(int r1, int r2, int MaxCol = int.MaxValue)
        {
            if (Sorted == false)
                throw new Exception("PackedSparseMatrix.DotRows: matrices must be sorted!");
            Debug.Assert(Sorted);

            MaxCol = Math.Min(MaxCol, Columns);

            int r1i = 0;
            int r2i = 0;
            nonzero[] Row1 = Rows[r1];
            nonzero[] Row2 = Rows[r2];
            int N1 = Row1.Length;
            int N2 = Row2.Length;
            //int last_col_1 = Col[MaxCol - 1].j;
            //int last_col_2 = Row[NR - 1].j;

            double sum = 0;
            while (r1i < N1 && r2i < N2) {
                if (Row1[r1i].j > MaxCol || Row2[r2i].j > MaxCol)
                    break;
                // early out if we passed last nonzero in other array
                //if (Row[ri].j > last_col_j || Col[ci].j > last_row_j)
                //    break;
                if (Row1[r1i].j == Row2[r2i].j) {
                    sum += Row1[r1i].d * Row2[r2i].d;
                    r1i++;
                    r2i++;
                } else if (Row1[r1i].j < Row2[r2i].j) {
                    r1i++;
                } else {
                    r2i++;
                }
            }

            return sum;
        }






        /// <summary>
        /// Compute dot product of this.row[r] and vec, up to N elements
        /// </summary>
        public double DotRowVector(int r, double[] vec, int MaxCol = int.MaxValue)
        {
            if (Sorted == false && MaxCol < int.MaxValue)
                throw new Exception("PackedSparseMatrix.DotRows: matrices must be sorted if MaxCol is specified!");

            MaxCol = Math.Min(MaxCol, Columns);

            nonzero[] Row = Rows[r];

            double sum = 0;
            for ( int ri = 0; ri < Row.Length; ++ri ) { 
                if (Row[ri].j > MaxCol)
                    break;
                sum += Row[ri].d * vec[Row[ri].j];
            }

            return sum;
        }




        /// <summary>
        /// Compute dot product of this.row[r1] and this.row[r2], up to N elements
        /// </summary>
        public double DotColumnVector(int c, double[] vec, int start_row = 0, int end_row = int.MaxValue)
        {
            int Nr = Rows.Length;

            double sum = 0;
            if (Sorted) {
                for (int ri = start_row; ri <= end_row; ri++) {
                    nonzero[] row = Rows[ri];
                    for (int k = 0; k < row.Length; ++k) {
                        if (row[k].j == c) {
                            sum += row[k].d * vec[ri];
                            break;
                        } else if (row[k].j > c) {
                            break;
                        }
                    }
                }
            } else {
                for (int ri = start_row; ri <= end_row; ri++) {
                    nonzero[] row = Rows[ri];
                    for (int k = 0; k < row.Length; ++k) {
                        if (row[k].j == c) {
                            sum += row[k].d * vec[ri];
                            break;
                        }
                    }
                }
            }

            return sum;
        }






        public PackedSparseMatrix Square()
        {
            if (Rows.Length != Columns)
                throw new Exception("PackedSparseMatrix.Square: matrix is not square!");
            int N = Columns;

            DVector<matrix_entry> entries = new DVector<matrix_entry>();
            SpinLock entries_lock = new SpinLock();

            gParallel.BlockStartEnd(0, N - 1, (r_start, r_end) => {
                for (int r1i = r_start; r1i <= r_end; r1i++) {

                    // determine which entries of squared matrix might be nonzeros
                    HashSet<int> nbrs = new HashSet<int>();
                    nbrs.Add(r1i);
                    PackedSparseMatrix.nonzero[] row = Rows[r1i];
                    for (int k = 0; k < row.Length; ++k) {
                        if (row[k].j > r1i)
                            nbrs.Add(row[k].j);
                        PackedSparseMatrix.nonzero[] row2 = Rows[row[k].j];
                        for (int j = 0; j < row2.Length; ++j) {
                            if (row2[j].j > r1i)     // only compute lower-triangular entries
                                nbrs.Add(row2[j].j);
                        }
                    }

                    // compute them!
                    foreach (int c2i in nbrs) {
                        double v = DotRowColumn(r1i, c2i, this);
                        if (Math.Abs(v) > MathUtil.ZeroTolerance) {
                            bool taken = false;
                            entries_lock.Enter(ref taken);
                            entries.Add(new matrix_entry() { r = r1i, c = c2i, value = v });
                            entries_lock.Exit();
                        }
                    }
                }
            });

            PackedSparseMatrix R = new PackedSparseMatrix(entries, N, N, true);
            return R;
        }





        public double FrobeniusNorm {
            get {
                double sum = 0;
                for (int i = 0; i < Rows.Length; ++i) {
                    nonzero[] row = Rows[i];
                    for (int j = 0; j < row.Length; ++j)
                        sum += row[j].d * row[j].d;
                }
                return Math.Sqrt(sum);
            }
        }


        public double MaxNorm {
            get {
                double max = 0;
                for (int i = 0; i < Rows.Length; ++i) {
                    nonzero[] row = Rows[i];
                    for (int j = 0; j < row.Length; ++j) {
                        if (row[j].d > max)
                            max = row[j].d;
                    }
                }
                return max;
            }
        }


        public double Trace {
            get {
                double sum = 0;
                for (int i = 0; i < Rows.Length; ++i) {
                    nonzero[] row = Rows[i];
                    for (int j = 0; j < row.Length; ++j) {
                        if (row[j].j == i)
                            sum += row[j].d;
                    }
                }
                return sum;
            }
        }




        public string MatrixInfo(bool bExtended = false) {
            string s = string.Format("Rows {0}  Cols {1}   NonZeros {2}  Sorted {3}", Rows.Length, Columns, NumNonZeros, Sorted);
            if ( bExtended ) {
                double sum = 0; 
                foreach ( var row in Rows ) {
                    foreach (var val in row)
                        sum += val.d;
                }
                s = s + string.Format("  Sum {0}  Frobenius {1}  Max {2}  Trace {3}", sum, FrobeniusNorm, MaxNorm, Trace);
            }
            return s;
        }


    }
}
