using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    // port of WildMagic5 Wm5SymmetricEigensolverGTE class (which is a back-port
    // of GTEngine Symmetric Eigensolver class) see geometrictools.com

    // The SymmetricEigensolver class is an implementation of Algorithm 8.2.3
    // (Symmetric QR Algorithm) described in "Matrix Computations, 2nd edition"
    // by G. H. Golub and C. F. Van Loan, The Johns Hopkins University Press,
    // Baltimore MD, Fourth Printing 1993.  Algorithm 8.2.1 (Householder
    // Tridiagonalization) is used to reduce matrix A to tridiagonal T.
    // Algorithm 8.2.2 (Implicit Symmetric QR Step with Wilkinson Shift) is
    // used for the iterative reduction from tridiagonal to diagonal.  If A is
    // the original matrix, D is the diagonal matrix of eigenvalues, and Q is
    // the orthogonal matrix of eigenvectors, then theoretically Q^T*A*Q = D.
    // Numerically, we have errors E = Q^T*A*Q - D.  Algorithm 8.2.3 mentions
    // that one expects |E| is approximately u*|A|, where |M| denotes the
    // Frobenius norm of M and where u is the unit roundoff for the
    // floating-point arithmetic: 2^{-23} for 'float', which is FLT_EPSILON
    // = 1.192092896e-7f, and 2^{-52} for'double', which is DBL_EPSILON
    // = 2.2204460492503131e-16.
    //
    // The condition |a(i,i+1)| <= epsilon*(|a(i,i) + a(i+1,i+1)|) used to
    // determine when the reduction decouples to smaller problems is implemented
    // as:  sum = |a(i,i)| + |a(i+1,i+1)|; sum + |a(i,i+1)| == sum.  The idea is
    // that the superdiagonal term is small relative to its diagonal neighbors,
    // and so it is effectively zero.  The unit tests have shown that this
    // interpretation of decoupling is effective.
    //
    // The authors suggest that once you have the tridiagonal matrix, a practical
    // implementation will store the diagonal and superdiagonal entries in linear
    // arrays, ignoring the theoretically zero values not in the 3-band.  This is
    // good for cache coherence.  The authors also suggest storing the Householder
    // vectors in the lower-triangular portion of the matrix to save memory.  The
    // implementation uses both suggestions.

    public class SymmetricEigenSolver
    {
        // The solver processes NxN symmetric matrices, where N > 1 ('size' is N)
        // and the matrix is stored in row-major order.  The maximum number of
        // iterations ('maxIterations') must be specified for the reduction of a
        // tridiagonal matrix to a diagonal matrix.  The goal is to compute
        // NxN orthogonal Q and NxN diagonal D for which Q^T*A*Q = D.
        public SymmetricEigenSolver(int size, int maxIterations)
        {
            mSize = mMaxIterations = 0;
            mIsRotation = -1;
            if (size > 1 && maxIterations > 0) {
                mSize = size;
                mMaxIterations = maxIterations;
                mMatrix = new double[size*size];
                mDiagonal = new double[size];
                mSuperdiagonal = new double[size - 1];
                mGivens = new List<GivensRotation>(maxIterations * (size - 1));
                mPermutation = new int[size];
                mVisited = new int[size];
                mPVector = new double[size];
                mVVector = new double[size];
                mWVector = new double[size];
            }
        }

        // A copy of the NxN symmetric input is made internally.  The order of
        // the eigenvalues is specified by sortType: -1 (decreasing), 0 (no
        // sorting), or +1 (increasing).  When sorted, the eigenvectors are
        // ordered accordingly.  The return value is the number of iterations
        // consumed when convergence occurred, 0xFFFFFFFF when convergence did
        // not occur, or 0 when N <= 1 was passed to the constructor.
        public enum SortType
        {
            Decreasing = -1,
            NoSorting = 0,
            Increasing = 1
        }
        public const int NO_CONVERGENCE = int.MaxValue;
        public int Solve(double [] input, SortType eSort)
        {
            int sortType = (int)eSort;
            if (mSize > 0) {
                Array.Copy(input, mMatrix, mSize * mSize);
                Tridiagonalize();

                mGivens.Clear();
                for (int j = 0; j < mMaxIterations; ++j) {
                    int imin = -1, imax = -1;
                    for (int i = mSize - 2; i >= 0; --i) {
                        // When a01 is much smaller than its diagonal neighbors, it is
                        // effectively zero.
                        double a00 = mDiagonal[i];
                        double a01 = mSuperdiagonal[i];
                        double a11 = mDiagonal[i + 1];
                        double sum = Math.Abs(a00) + Math.Abs(a11);
                        if (sum + Math.Abs(a01) != sum) {
                            if (imax == -1) {
                                imax = i;
                            }
                            imin = i;
                        } else {
                            // The superdiagonal term is effectively zero compared to
                            // the neighboring diagonal terms.
                            if (imin >= 0) {
                                break;
                            }
                        }
                    }

                    if (imax == -1) {
                        // The algorithm has converged.
                        ComputePermutation(sortType);
                        return j;
                    }

                    // Process the lower-right-most unreduced tridiagonal block.
                    DoQRImplicitShift(imin, imax);
                }
                return NO_CONVERGENCE;
            } else {
                return 0;
            }
        }

        // Get the eigenvalues of the matrix passed to Solve(...).  The input
        // 'eigenvalues' must have N elements.
        public void GetEigenvalues(double[] eigenvalues)
        {
            if (eigenvalues != null && mSize > 0) {
                if (mPermutation[0] >= 0) {
                    // Sorting was requested.
                    for (int i = 0; i < mSize; ++i) {
                        int p = mPermutation[i];
                        eigenvalues[i] = mDiagonal[p];
                    }
                } else {
                    // Sorting was not requested.
                    Array.Copy(mDiagonal, eigenvalues, mSize);
                }
            }
        }
        public double[] GetEigenvalues()
        {
            double[] eigenvalues = new double[mSize];
            GetEigenvalues(eigenvalues);
            return eigenvalues;
        }
		public double GetEigenvalue(int c) 
		{
			if (mSize > 0) {
				if (mPermutation[0] >= 0) {
					// Sorting was requested.
					return mDiagonal[mPermutation[c]];
				} else {
					// Sorting was not requested.
					return mDiagonal[c];
				}
			} else {
				return double.MaxValue;
			}			
		}

        // Accumulate the Householder reflections and Givens rotations to produce
        // the orthogonal matrix Q for which Q^T*A*Q = D.  The input
        // 'eigenvectors' must be NxN and stored in row-major order.
        public void GetEigenvectors(double[] eigenvectors)
        {
            if (eigenvectors != null && mSize > 0) {
                // Start with the identity matrix.
                Array.Clear(eigenvectors, 0, mSize * mSize);
                for (int d = 0; d < mSize; ++d) {
                    eigenvectors[d + mSize * d] = 1;
                }

                // Multiply the Householder reflections using backward accumulation.
                int r, c;
                for (int i = mSize - 3, rmin = i + 1; i >= 0; --i, --rmin) {
                    // Copy the v vector and 2/Dot(v,v) from the matrix.
                    //double const* column = &mMatrix[i];
                    ArrayAlias<double> column = new ArrayAlias<double>(mMatrix, i);
                    double twoinvvdv = column[mSize * (i + 1)];
                    for (r = 0; r < i + 1; ++r) {
                        mVVector[r] = 0;
                    }
                    mVVector[r] = 1;
                    for (++r; r < mSize; ++r) {
                        mVVector[r] = column[mSize * r];
                    }

                    // Compute the w vector.
                    for (r = 0; r < mSize; ++r) {
                        mWVector[r] = 0;
                        for (c = rmin; c < mSize; ++c) {
                            mWVector[r] += mVVector[c] * eigenvectors[r + mSize * c];
                        }
                        mWVector[r] *= twoinvvdv;
                    }

                    // Update the matrix, Q <- Q - v*w^T.
                    for (r = rmin; r < mSize; ++r) {
                        for (c = 0; c < mSize; ++c) {
                            eigenvectors[c + mSize * r] -= mVVector[r] * mWVector[c];
                        }
                    }
                }

                // Multiply the Givens rotations.
                foreach ( GivensRotation givens in mGivens ) { 
                    for (r = 0; r < mSize; ++r) {
                        int j = givens.index + mSize * r;
                        double q0 = eigenvectors[j];
                        double q1 = eigenvectors[j + 1];
                        double prd0 = givens.cs * q0 - givens.sn * q1;
                        double prd1 = givens.sn * q0 + givens.cs * q1;
                        eigenvectors[j] = prd0;
                        eigenvectors[j + 1] = prd1;
                    }
                }

                mIsRotation = 1 - (mSize & 1);
                if (mPermutation[0] >= 0) {
                    // Sorting was requested.
                    Array.Clear(mVisited, 0, mVisited.Length);
                    for (int i = 0; i < mSize; ++i) {
                        if (mVisited[i] == 0 && mPermutation[i] != i) {
                            // The item starts a cycle with 2 or more elements.
                            mIsRotation = 1 - mIsRotation;
                            int start = i, current = i, j, next;
                            for (j = 0; j < mSize; ++j) {
                                mPVector[j] = eigenvectors[i + mSize * j];
                            }
                            while ((next = mPermutation[current]) != start) {
                                mVisited[current] = 1;
                                for (j = 0; j < mSize; ++j) {
                                    eigenvectors[current + mSize * j] =
                                        eigenvectors[next + mSize * j];
                                }
                                current = next;
                            }
                            mVisited[current] = 1;
                            for (j = 0; j < mSize; ++j) {
                                eigenvectors[current + mSize * j] = mPVector[j];
                            }
                        }
                    }
                }
            }
        }
        public double[] GetEigenvectors()
        {
            double[] eigenvectors = new double[mSize*mSize];
            GetEigenvectors(eigenvectors);
            return eigenvectors;
        }

        // With no sorting, when N is odd the matrix returned by GetEigenvectors
        // is a reflection and when N is even it is a rotation.  With sorting
        // enabled, the type of matrix returned depends on the permutation of
        // columns.  If the permutation has C cycles, the minimum number of column
        // transpositions is T = N-C.  Thus, when C is odd the matrix is a
        // reflection and when C is even the matrix is a rotation.
        public bool IsRotation()
        {
            if (mSize > 0) {
                if (mIsRotation == -1) {
                    // Without sorting, the matrix is a rotation when size is even.
                    mIsRotation = 1 - (mSize & 1);
                    if (mPermutation[0] >= 0) {
                        // With sorting, the matrix is a rotation when the number of
                        // cycles in the permutation is even.
                        Array.Clear(mVisited, 0, mVisited.Length);
                        for (int i = 0; i < mSize; ++i) {
                            if (mVisited[i] == 0 && mPermutation[i] != i) {
                                // The item starts a cycle with 2 or more elements.
                                int start = i, current = i, next;
                                while ((next = mPermutation[current]) != start) {
                                    mVisited[current] = 1;
                                    current = next;
                                }
                                mVisited[current] = 1;
                            }
                        }
                    }
                }
                return mIsRotation == 1;
            } else {
                return false;
            }
        }

        // Compute a single eigenvector, which amounts to computing column c
        // of matrix Q.  The reflections and rotations are applied incrementally.
        // This is useful when you want only a small number of the eigenvectors.
        public void GetEigenvector(int c, double[] eigenvector)
        {
            if (0 <= c && c < mSize) {
                // y = H*x, then x and y are swapped for the next H
                double[] x = eigenvector;
                double[] y = mPVector;

                // Start with the Euclidean basis vector.
                Array.Clear(x, 0, mSize);
                if (mPermutation[c] >= 0) {
                    x[mPermutation[c]] = 1;
                } else {
                    x[c] = 1;
                }

                // Apply the Givens rotations.
                // [RMS] C# doesn't support reverse iterator so I replaced w/ loop...right?
                //typename std::vector < GivensRotation >::const_reverse_iterator givens = mGivens.rbegin();
                //for (/**/; givens != mGivens.rend(); ++givens) {
                for ( int i = mGivens.Count-1; i >= 0; --i) {
                    GivensRotation givens = mGivens[i];
                    double xr = x[givens.index];
                    double xrp1 = x[givens.index + 1];
                    double tmp0 = givens.cs * xr + givens.sn * xrp1;
                    double tmp1 = -givens.sn * xr + givens.cs * xrp1;
                    x[givens.index] = tmp0;
                    x[givens.index + 1] = tmp1;
                }

                // Apply the Householder reflections.
                for (int i = mSize - 3; i >= 0; --i) {
                    // Get the Householder vector v.
                    //double const* column = &mMatrix[i];
                    ArrayAlias<double> column = new ArrayAlias<double>(mMatrix, i);
                    double twoinvvdv = column[mSize * (i + 1)];
                    int r;
                    for (r = 0; r < i + 1; ++r) {
                        y[r] = x[r];
                    }

                    // Compute s = Dot(x,v) * 2/v^T*v.
                    double s = x[r];  // r = i+1, v[i+1] = 1
                    for (int j = r + 1; j < mSize; ++j) {
                        s += x[j] * column[mSize * j];
                    }
                    s *= twoinvvdv;

                    y[r] = x[r] - s;  // v[i+1] = 1

                    // Compute the remaining components of y.
                    for (++r; r < mSize; ++r) {
                        y[r] = x[r] - s * column[mSize * r];
                    }

                    //std::swap(x, y);
                    var tmp = x; x = y; y = tmp;
                }
                // The final product is stored in x.

                if (x != eigenvector) {
                    Array.Copy(x, eigenvector, mSize);
                }
            }
        }
        public double[] GetEigenvector(int c)
        {
            double[] eigenvector = new double[mSize];
            GetEigenvector(c, eigenvector);
            return eigenvector;
        }

        // Tridiagonalize using Householder reflections.  On input, mMatrix is a
        // copy of the input matrix.  On output, the upper-triangular part of
        // mMatrix including the diagonal stores the tridiagonalization.  The
        // lower-triangular part contains 2/Dot(v,v) that are used in computing
        // eigenvectors and the part below the subdiagonal stores the essential
        // parts of the Householder vectors v (the elements of v after the
        // leading 1-valued component).
        private void Tridiagonalize()
        {
            int r, c;
            for (int i = 0, ip1 = 1; i < mSize - 2; ++i, ++ip1) {
                // Compute the Householder vector.  Read the initial vector from the
                // row of the matrix.
                double length = 0;
                for (r = 0; r < ip1; ++r) {
                    mVVector[r] = 0;
                }
                for (r = ip1; r < mSize; ++r) {
                    double vr = mMatrix[r + mSize * i];
                    mVVector[r] = vr;
                    length += vr * vr;
                }
                double vdv = 1;
                length = Math.Sqrt(length);
                if (length > 0) {
                    double v1 = mVVector[ip1];
                    double sgn = (v1 >= 0 ? 1 : -1);
                    double invDenom = 1 / (v1 + sgn * length);
                    mVVector[ip1] = (double)1;
                    for (r = ip1 + 1; r < mSize; ++r) {
                        mVVector[r] *= invDenom;
                        vdv += mVVector[r] * mVVector[r];
                    }
                }

                // Compute the rank-1 offsets v*w^T and w*v^T.
                double invvdv = 1 / vdv;
                double twoinvvdv = invvdv * 2;
                double pdvtvdv = 0;
                for (r = i; r < mSize; ++r) {
                    mPVector[r] = 0;
                    for (c = i; c < r; ++c) {
                        mPVector[r] += mMatrix[r + mSize * c] * mVVector[c];
                    }
                    for (/**/; c < mSize; ++c) {
                        mPVector[r] += mMatrix[c + mSize * r] * mVVector[c];
                    }
                    mPVector[r] *= twoinvvdv;
                    pdvtvdv += mPVector[r] * mVVector[r];
                }

                pdvtvdv *= invvdv;
                for (r = i; r < mSize; ++r) {
                    mWVector[r] = mPVector[r] - pdvtvdv * mVVector[r];
                }

                // Update the input matrix.
                for (r = i; r < mSize; ++r) {
                    double vr = mVVector[r];
                    double wr = mWVector[r];
                    double offset = vr * wr * 2;
                    mMatrix[r + mSize * r] -= offset;
                    for (c = r + 1; c < mSize; ++c) {
                        offset = vr * mWVector[c] + wr * mVVector[c];
                        mMatrix[c + mSize * r] -= offset;
                    }
                }

                // Copy the vector to column i of the matrix.  The 0-valued components
                // at indices 0 through i are not stored.  The 1-valued component at
                // index i+1 is also not stored; instead, the quantity 2/Dot(v,v) is
                // stored for use in eigenvector construction. That construction must
                // take into account the implied components that are not stored.
                mMatrix[i + mSize * ip1] = twoinvvdv;
                for (r = ip1 + 1; r < mSize; ++r) {
                    mMatrix[i + mSize * r] = mVVector[r];
                }
            }

            // Copy the diagonal and subdiagonal entries for cache coherence in
            // the QR iterations.
            int k, ksup = mSize - 1, index = 0, delta = mSize + 1;
            for (k = 0; k < ksup; ++k, index += delta) {
                mDiagonal[k] = mMatrix[index];
                mSuperdiagonal[k] = mMatrix[index + 1];
            }
            mDiagonal[k] = mMatrix[index];
        }

        // A helper for generating Givens rotation sine and cosine robustly.
        private void GetSinCos(double x, double y, ref double cs, ref double sn)
        {
            // Solves sn*x + cs*y = 0 robustly.
            double tau;
            if (y != 0) {
                if (Math.Abs(y) > Math.Abs(x)) {
                    tau = -x / y;
                    sn = (1) / Math.Sqrt((1) + tau * tau);
                    cs = sn * tau;
                } else {
                    tau = -y / x;
                    cs = (1) / Math.Sqrt((1) + tau * tau);
                    sn = cs * tau;
                }
            } else {
                cs = 1;
                sn = 0;
            }
        }

        // The QR step with implicit shift.  Generally, the initial T is unreduced
        // tridiagonal (all subdiagonal entries are nonzero).  If a QR step causes
        // a superdiagonal entry to become zero, the matrix decouples into a block
        // diagonal matrix with two tridiagonal blocks.  These blocks can be
        // reduced independently of each other, which allows for parallelization
        // of the algorithm.  The inputs imin and imax identify the subblock of T
        // to be processed.   That block has upper-left element T(imin,imin) and
        // lower-right element T(imax,imax).
        private void DoQRImplicitShift(int imin, int imax)
        {
            // The implicit shift.  Compute the eigenvalue u of the lower-right 2x2
            // block that is closer to a11.
            double a00 = mDiagonal[imax];
            double a01 = mSuperdiagonal[imax];
            double a11 = mDiagonal[imax + 1];
            double dif = (a00 - a11) * 0.5;
            double sgn = (dif >= 0 ? 1 : -1);
            double a01sqr = a01 * a01;
            double u = a11 - a01sqr / (dif + sgn * Math.Sqrt(dif * dif + a01sqr));
            double x = mDiagonal[imin] - u;
            double y = mSuperdiagonal[imin];

            double a12, a22, a23, tmp11, tmp12, tmp21, tmp22, cs = 0, sn = 0;
            double a02 = 0;
            int i0 = imin - 1, i1 = imin, i2 = imin + 1;
            for (/**/; i1 <= imax; ++i0, ++i1, ++i2) {
                // Compute the Givens rotation and save it for use in computing the
                // eigenvectors.
                GetSinCos(x, y, ref cs, ref sn);
                mGivens.Add(new GivensRotation(i1, cs, sn));

                // Update the tridiagonal matrix.  This amounts to updating a 4x4
                // subblock,
                //   b00 b01 b02 b03
                //   b01 b11 b12 b13
                //   b02 b12 b22 b23
                //   b03 b13 b23 b33
                // The four corners (b00, b03, b33) do not change values.  The
                // The interior block {{b11,b12},{b12,b22}} is updated on each pass.
                // For the first pass, the b0c values are out of range, so only
                // the values (b13, b23) change.  For the last pass, the br3 values
                // are out of range, so only the values (b01, b02) change.  For
                // passes between first and last, the values (b01, b02, b13, b23)
                // change.
                if (i1 > imin) {
                    mSuperdiagonal[i0] = cs * mSuperdiagonal[i0] - sn * a02;
                }

                a11 = mDiagonal[i1];
                a12 = mSuperdiagonal[i1];
                a22 = mDiagonal[i2];
                tmp11 = cs * a11 - sn * a12;
                tmp12 = cs * a12 - sn * a22;
                tmp21 = sn * a11 + cs * a12;
                tmp22 = sn * a12 + cs * a22;
                mDiagonal[i1] = cs * tmp11 - sn * tmp12;
                mSuperdiagonal[i1] = sn * tmp11 + cs * tmp12;
                mDiagonal[i2] = sn * tmp21 + cs * tmp22;

                if (i1 < imax) {
                    a23 = mSuperdiagonal[i2];
                    a02 = -sn * a23;
                    mSuperdiagonal[i2] = cs * a23;

                    // Update the parameters for the next Givens rotation.
                    x = mSuperdiagonal[i1];
                    y = a02;
                }
            }
        }

        // Sort the eigenvalues and compute the corresponding permutation of the
        // indices of the array storing the eigenvalues.  The permutation is used
        // for reordering the eigenvalues and eigenvectors in the calls to
        // GetEigenvalues(...) and GetEigenvectors(...).
        private void ComputePermutation(int sortType)
        {
            mIsRotation = -1;

            if (sortType == 0) {
                // Set a flag for GetEigenvalues() and GetEigenvectors() to know
                // that sorted output was not requested.
                mPermutation[0] = -1;
                return;
            }

            // Compute the permutation induced by sorting.  Initially, we start with
            // the identity permutation I = (0,1,...,N-1).
            SortItem[] items = new SortItem[mSize];
            for (int i = 0; i < mSize; ++i) {
                items[i].eigenvalue = mDiagonal[i];
                items[i].index = i;
            }

            if (sortType > 0) {
                //std::sort(items.begin(), items.end(), std::less<SortItem>());
                Array.Sort(items, (a,b) => { return a.eigenvalue == b.eigenvalue ? 0 : a.eigenvalue < b.eigenvalue ? -1 : 1; }  );
            } else {
                //std::sort(items.begin(), items.end(), std::greater<SortItem>());
                Array.Sort(items, (a,b) => { return a.eigenvalue == b.eigenvalue ? 0 : a.eigenvalue > b.eigenvalue ? -1 : 1; }  );
            }

            for (int i = 0; i < mSize; ++i)
                mPermutation[i] = items[i].index;
            //typename std::vector < SortItem >::const_iterator item = items.begin();
            //for (i = 0; item != items.end(); ++item, ++i) {
            //    mPermutation[i] = item->index;
            //}

            // GetEigenvectors() has nontrivial code for computing the orthogonal Q
            // from the reflections and rotations.  To avoid complicating the code
            // further when sorting is requested, Q is computed as in the unsorted
            // case.  We then need to swap columns of Q to be consistent with the
            // sorting of the eigenvalues.  To minimize copying due to column swaps,
            // we use permutation P.  The minimum number of transpositions to obtain
            // P from I is N minus the number of cycles of P.  Each cycle is reordered
            // with a minimum number of transpositions; that is, the eigenitems are
            // cyclically swapped, leading to a minimum amount of copying.  For
            // example, if there is a cycle i0 -> i1 -> i2 -> i3, then the copying is
            //   save = eigenitem[i0];
            //   eigenitem[i1] = eigenitem[i2];
            //   eigenitem[i2] = eigenitem[i3];
            //   eigenitem[i3] = save;
        }

        // The number N of rows and columns of the matrices to be processed.
        private int mSize;

        // The maximum number of iterations for reducing the tridiagonal mtarix
        // to a diagonal matrix.
        private int mMaxIterations;

        // The internal copy of a matrix passed to the solver.  See the comments
        // about function Tridiagonalize() about what is stored in the matrix.
        private double[] mMatrix;  // NxN elements

        // After the initial tridiagonalization by Householder reflections, we no
        // longer need the full mMatrix.  Copy the diagonal and superdiagonal
        // entries to linear arrays in order to be cache friendly.
        private double[] mDiagonal;  // N elements
        private double[] mSuperdiagonal;  // N-1 elements

        // The Givens rotations used to reduce the initial tridiagonal matrix to
        // a diagonal matrix.  A rotation is the identity with the following
        // replacement entries:  R(index,index) = cs, R(index,index+1) = sn,
        // R(index+1,index) = -sn, and R(index+1,index+1) = cs.  If N is the
        // matrix size and K is the maximum number of iterations, the maximum
        // number of Givens rotations is K*(N-1).  The maximum amount of memory
        // is allocated to store these.
        private struct GivensRotation
        {
            public GivensRotation(int inIndex, double inCs, double inSn)
            {
                index = inIndex;
                cs = inCs;
                sn = inSn;
            }
            public int index;
            public double cs, sn;
        };

        private List<GivensRotation> mGivens;  // K*(N-1) elements

        // When sorting is requested, the permutation associated with the sort is
        // stored in mPermutation.  When sorting is not requested, mPermutation[0]
        // is set to -1.  mVisited is used for finding cycles in the permutation.
        private struct SortItem
        {
            public double eigenvalue;
            public int index;
        };
        private int[] mPermutation;  // N elements
        private int[] mVisited;  // N elements
        private int mIsRotation;  // 1 = rotation, 0 = reflection, -1 = unknown

        // Temporary storage to compute Householder reflections and to support
        // sorting of eigenvectors.
        private double[] mPVector;  // N elements
        private double[] mVVector;  // N elements
        private double[] mWVector;  // N elements

    }
}
