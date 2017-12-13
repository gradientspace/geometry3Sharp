using System;
using System.Collections.Generic;

namespace g3
{
    /// <summary>
    /// Singular Value Decomposition of arbitrary matrix A
    /// Computes U/S/V of  A = U * S * V^T
    /// 
    /// Useful Properties:
    ///  S = square-roots of eigenvalues of A
    ///  U = eigenvectors of A * A^T
    ///  V = eigenvectors of A^T * A
    ///  U * V^T = rotation matrix closest to A 
    ///  V * Inv(S) * U^T = psuedoinverse of A
    ///  
    /// U and/or V are rotation matrices but may also contain reflections
    /// Detection: det(U) or det(v) == -1
    /// Removal: if ( det(U) == -1 ) { U *= -1; S *= -1 }
    ///          if ( det(V) == -1 ) { V *= -1; S *= -1 }     (right? seems to work)
    ///  
    /// </summary>
    public class SingularValueDecomposition
    {
        // port of WildMagic5 SingularValueDecomposition class (which is a back-port
        // of GTEngine SVD class) see geometrictools.com


        // The solver processes MxN symmetric matrices, where M >= N > 1
        // ('numRows' is M and 'numCols' is N) and the matrix is stored in
        // row-major order.  The maximum number of iterations ('maxIterations')
        // must be specified for the reduction of a bidiagonal matrix to a
        // diagonal matrix.  The goal is to compute MxM orthogonal U, NxN
        // orthogonal V, and MxN matrix S for which U^T*A*V = S.  The only
        // nonzero entries of S are on the diagonal; the diagonal entries are
        // the singular values of the original matrix.
        public SingularValueDecomposition(int numRows, int numCols, int maxIterations)
        {
            mNumRows = mNumCols = mMaxIterations = 0;
            if (numCols > 1 && numRows >= numCols && maxIterations > 0) {
                mNumRows = numRows;
                mNumCols = numCols;
                mMaxIterations = maxIterations;
                mMatrix = new double[(numRows * numCols)];
                mDiagonal = new double[(numCols)];
                mSuperdiagonal = new double[(numCols - 1)];
                mRGivens = new List<GivensRotation>(maxIterations * (numCols - 1));
                mLGivens = new List<GivensRotation>(maxIterations * (numCols - 1));
                mFixupDiagonal = new double[(numCols)];
                mPermutation = new int[(numCols)];
                mVisited = new int[(numCols)];
                mTwoInvUTU = new double[(numCols)];
                mTwoInvVTV = new double[(numCols - 2)];
                mUVector = new double[(numRows)];
                mVVector = new double[(numCols)];
                mWVector = new double[(numRows)];
            }
        }

        // A copy of the MxN input is made internally.  The order of the singular
        // values is specified by sortType: -1 (decreasing), 0 (no sorting), or +1
        // (increasing).  When sorted, the columns of the orthogonal matrices
        // are ordered accordingly.  The return value is the number of iterations
        // consumed when convergence occurred, 0xFFFFFFFF when convergence did not
        // occur or 0 when N <= 1 or M < N was passed to theructor.
        public uint Solve(double[] input, int sortType = -1)
        {
            if (mNumRows > 0) {
                int numElements = mNumRows * mNumCols;
                Array.Copy(input, mMatrix, numElements);
                Bidiagonalize();

                // Compute 'threshold = multiplier*epsilon*|B|' as the threshold for
                // diagonal entries effectively zero; that is, |d| <= |threshold|
                // implies that d is (effectively) zero.  TODO: Allow the caller to
                // pass 'multiplier' to the constructor.
                //
                // We will use the L2-norm |B|, which is the length of the elements
                // of B treated as an NM-tuple.  The following code avoids overflow
                // when accumulating the squares of the elements when those elements
                // are large.
                double maxAbsComp = Math.Abs(input[0]);
                for (int i = 1; i < numElements; ++i) {
                    double absComp = Math.Abs(input[i]);
                    if (absComp > maxAbsComp) {
                        maxAbsComp = absComp;
                    }
                }

                double norm = (double)0;
                if (maxAbsComp > (double)0) {
                    double invMaxAbsComp = ((double)1) / maxAbsComp;
                    for (int i = 0; i < numElements; ++i) {
                        double ratio = input[i] * invMaxAbsComp;
                        norm += ratio * ratio;
                    }
                    norm = maxAbsComp * Math.Sqrt(norm);
                }

                double multiplier = (double)8;  // TODO: Expose to caller.
                double epsilon = double.Epsilon;
                double threshold = multiplier * epsilon * norm;

                mRGivens.Clear();
                mLGivens.Clear();
                for (uint j = 0; j < mMaxIterations; ++j) {
                    int imin = -1, imax = -1;
                    for (int i = mNumCols - 2; i >= 0; --i) {
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
                        EnsureNonnegativeDiagonal();
                        ComputePermutation(sortType);
                        return j;
                    }

                    // We need to test diagonal entries of B for zero.  For each zero
                    // diagonal entry, zero the superdiagonal.
                    if (DiagonalEntriesNonzero(imin, imax, threshold)) {
                        // Process the lower-right-most unreduced bidiagonal block.
                        DoGolubKahanStep(imin, imax);
                    }
                }
                return 0xFFFFFFFF;
            } else {
                return 0;
            }
        }

        // Get the singular values of the matrix passed to Solve(...).  The input
        // 'singularValues' must have N elements.
        public void GetSingularValues(double[] singularValues)
        {
            if (singularValues != null && mNumCols > 0) {
                if (mPermutation[0] >= 0) {
                    // Sorting was requested.
                    for (int i = 0; i < mNumCols; ++i) {
                        int p = mPermutation[i];
                        singularValues[i] = mDiagonal[p];
                    }
                } else {
                    // Sorting was not requested.
                    for (int i = 0; i < mNumCols; ++i)
                        singularValues[i] = mDiagonal[i];
                }
            }
        }

        // Accumulate the Householder reflections, the Givens rotations, and the
        // diagonal fix-up matrix to compute the orthogonal matrices U and V for
        // which U^T*A*V = S.  The input uMatrix must be MxM and the input vMatrix
        // must be NxN, both stored in row-major order.
        public void GetU(double[] uMatrix)
        {
            if (uMatrix == null || mNumCols == 0) {
                // Invalid input or the constructor failed.
                return;
            }

            // Start with the identity matrix.
            Array.Clear(uMatrix, 0, uMatrix.Length);
            for (int d = 0; d < mNumRows; ++d) {
                uMatrix[d + mNumRows * d] = (double)1;
            }

            // Multiply the Householder reflections using backward accumulation.
            int r, c;
            for (int i0 = mNumCols - 1, i1 = i0 + 1; i0 >= 0; --i0, --i1) {
                // Copy the u vector and 2/Dot(u,u) from the matrix.
                double twoinvudu = mTwoInvUTU[i0];
                //double const* column = &mMatrix[i0];
                mUVector[i0] = (double)1;
                for (r = i1; r < mNumRows; ++r) {
                    //mUVector[r] = column[mNumCols * r];
                    mUVector[r] = mMatrix[i0 + (mNumCols * r)];
                }

                // Compute the w vector.
                mWVector[i0] = twoinvudu;
                for (r = i1; r < mNumRows; ++r) {
                    mWVector[r] = (double)0;
                    for (c = i1; c < mNumRows; ++c) {
                        mWVector[r] += mUVector[c] * uMatrix[r + mNumRows * c];
                    }
                    mWVector[r] *= twoinvudu;
                }

                // Update the matrix, U <- U - u*w^T.
                for (r = i0; r < mNumRows; ++r) {
                    for (c = i0; c < mNumRows; ++c) {
                        uMatrix[c + mNumRows * r] -= mUVector[r] * mWVector[c];
                    }
                }
            }

            // Multiply the Givens rotations.
            foreach( GivensRotation givens in mLGivens ) { 
                int j0 = givens.index0;
                int j1 = givens.index1;
                for (r = 0; r < mNumRows; ++r, j0 += mNumRows, j1 += mNumRows) {
                    double q0 = uMatrix[j0];
                    double q1 = uMatrix[j1];
                    double prd0 = givens.cs * q0 - givens.sn * q1;
                    double prd1 = givens.sn * q0 + givens.cs * q1;
                    uMatrix[j0] = prd0;
                    uMatrix[j1] = prd1;
                }
            }

            if (mPermutation[0] >= 0) {
                // Sorting was requested.
                Array.Clear(mVisited, 0, mVisited.Length);
                for (c = 0; c < mNumCols; ++c) {
                    if (mVisited[c] == 0 && mPermutation[c] != c) {
                        // The item starts a cycle with 2 or more elements.
                        int start = c, current = c, next;
                        for (r = 0; r < mNumRows; ++r) {
                            mWVector[r] = uMatrix[c + mNumRows * r];
                        }
                        while ((next = mPermutation[current]) != start) {
                            mVisited[current] = 1;
                            for (r = 0; r < mNumRows; ++r) {
                                uMatrix[current + mNumRows * r] =
                                    uMatrix[next + mNumRows * r];
                            }
                            current = next;
                        }
                        mVisited[current] = 1;
                        for (r = 0; r < mNumRows; ++r) {
                            uMatrix[current + mNumRows * r] = mWVector[r];
                        }
                    }
                }
            }
        }


        public void GetV(double[] vMatrix)
        {
            if (vMatrix == null || mNumCols == 0) {
                // Invalid input or the constructor failed.
                return;
            }

            // Start with the identity matrix.
            Array.Clear(vMatrix, 0, vMatrix.Length);
            for (int d = 0; d < mNumCols; ++d) {
                vMatrix[d + mNumCols * d] = (double)1;
            }

            // Multiply the Householder reflections using backward accumulation.
            int i0 = mNumCols - 3;
            int i1 = i0 + 1;
            int i2 = i0 + 2;
            int r, c;
            for (/**/; i0 >= 0; --i0, --i1, --i2) {
                // Copy the v vector and 2/Dot(v,v) from the matrix.
                double twoinvvdv = mTwoInvVTV[i0];
                //double const* row = &mMatrix[mNumCols * i0];      // [RMS] port
                mVVector[i1] = (double)1;
                for (r = i2; r < mNumCols; ++r) {
                    //mVVector[r] = row[r];         // [RMS] port
                    mVVector[r] = mMatrix[mNumCols * i0 + r];
                }

                // Compute the w vector.
                mWVector[i1] = twoinvvdv;
                for (r = i2; r < mNumCols; ++r) {
                    mWVector[r] = (double)0;
                    for (c = i2; c < mNumCols; ++c) {
                        mWVector[r] += mVVector[c] * vMatrix[r + mNumCols * c];
                    }
                    mWVector[r] *= twoinvvdv;
                }

                // Update the matrix, V <- V - v*w^T.
                for (r = i1; r < mNumCols; ++r) {
                    for (c = i1; c < mNumCols; ++c) {
                        vMatrix[c + mNumCols * r] -= mVVector[r] * mWVector[c];
                    }
                }
            }

            // Multiply the Givens rotations.
            foreach ( GivensRotation givens in mRGivens) { 
                int j0 = givens.index0;
                int j1 = givens.index1;
                for (c = 0; c < mNumCols; ++c, j0 += mNumCols, j1 += mNumCols) {
                    double q0 = vMatrix[j0];
                    double q1 = vMatrix[j1];
                    double prd0 = givens.cs * q0 - givens.sn * q1;
                    double prd1 = givens.sn * q0 + givens.cs * q1;
                    vMatrix[j0] = prd0;
                    vMatrix[j1] = prd1;
                }
            }

            // Fix-up the diagonal.
            for (r = 0; r < mNumCols; ++r) {
                for (c = 0; c < mNumCols; ++c) {
                    vMatrix[c + mNumCols * r] *= mFixupDiagonal[c];
                }
            }

            if (mPermutation[0] >= 0) {
                // Sorting was requested.
                Array.Clear(mVisited, 0, mVisited.Length);
                for (c = 0; c < mNumCols; ++c) {
                    if (mVisited[c] == 0 && mPermutation[c] != c) {
                        // The item starts a cycle with 2 or more elements.
                        int start = c, current = c, next;
                        for (r = 0; r < mNumCols; ++r) {
                            mWVector[r] = vMatrix[c + mNumCols * r];
                        }
                        while ((next = mPermutation[current]) != start) {
                            mVisited[current] = 1;
                            for (r = 0; r < mNumCols; ++r) {
                                vMatrix[current + mNumCols * r] =
                                    vMatrix[next + mNumCols * r];
                            }
                            current = next;
                        }
                        mVisited[current] = 1;
                        for (r = 0; r < mNumCols; ++r) {
                            vMatrix[current + mNumCols * r] = mWVector[r];
                        }
                    }
                }
            }
        }



        //
        // internals
        //


        // Bidiagonalize using Householder reflections.  On input, mMatrix is a
        // copy of the input matrix and has one extra row.  On output, the
        // diagonal and superdiagonal contain the bidiagonalized results.  The
        // lower-triangular portion stores the essential parts of the Householder
        // u vectors (the elements of u after the leading 1-valued component) and
        // the upper-triangular portion stores the essential parts of the
        // Householder v vectors.  To avoid recomputing 2/Dot(u,u) and 2/Dot(v,v),
        // these quantities are stored in mTwoInvUTU and mTwoInvVTV.
        void Bidiagonalize()
        {
            int r, c;
            for (int i = 0, ip1 = 1; i < mNumCols; ++i, ++ip1) {
                // Compute the U-Householder vector.
                double length = (double)0;
                for (r = i; r < mNumRows; ++r) {
                    double ur = mMatrix[i + mNumCols * r];
                    mUVector[r] = ur;
                    length += ur * ur;
                }
                double udu = (double)1;
                length = Math.Sqrt(length);
                if (length > (double)0) {
                    double u1 = mUVector[i];
                    double sgn = (u1 >= (double)0 ? (double)1 : (double) - 1);
                    double invDenom = ((double)1) / (u1 + sgn * length);
                    mUVector[i] = (double)1;
                    for (r = ip1; r < mNumRows; ++r) {
                        mUVector[r] *= invDenom;
                        udu += mUVector[r] * mUVector[r];
                    }
                }

                // Compute the rank-1 offset u*w^T.
                double invudu = (double)1 / udu;
                double twoinvudu = invudu * (double)2;
                for (c = i; c < mNumCols; ++c) {
                    mWVector[c] = (double)0;
                    for (r = i; r < mNumRows; ++r) {
                        mWVector[c] += mMatrix[c + mNumCols * r] * mUVector[r];
                    }
                    mWVector[c] *= twoinvudu;
                }

                // Update the input matrix.
                for (r = i; r < mNumRows; ++r) {
                    for (c = i; c < mNumCols; ++c) {
                        mMatrix[c + mNumCols * r] -= mUVector[r] * mWVector[c];
                    }
                }

                if (i < mNumCols - 2) {
                    // Compute the V-Householder vectors.
                    length = (double)0;
                    for (c = ip1; c < mNumCols; ++c) {
                        double vc = mMatrix[c + mNumCols * i];
                        mVVector[c] = vc;
                        length += vc * vc;
                    }
                    double vdv = (double)1;
                    length = Math.Sqrt(length);
                    if (length > (double)0) {
                        double v1 = mVVector[ip1];
                        double sgn = (v1 >= (double)0 ? (double)1 : (double) - 1);
                        double invDenom = ((double)1) / (v1 + sgn * length);
                        mVVector[ip1] = (double)1;
                        for (c = ip1 + 1; c < mNumCols; ++c) {
                            mVVector[c] *= invDenom;
                            vdv += mVVector[c] * mVVector[c];
                        }
                    }

                    // Compute the rank-1 offset w*v^T.
                    double invvdv = (double)1 / vdv;
                    double twoinvvdv = invvdv * (double)2;
                    for (r = i; r < mNumRows; ++r) {
                        mWVector[r] = (double)0;
                        for (c = ip1; c < mNumCols; ++c) {
                            mWVector[r] += mMatrix[c + mNumCols * r] * mVVector[c];
                        }
                        mWVector[r] *= twoinvvdv;
                    }

                    // Update the input matrix.
                    for (r = i; r < mNumRows; ++r) {
                        for (c = ip1; c < mNumCols; ++c) {
                            mMatrix[c + mNumCols * r] -= mWVector[r] * mVVector[c];
                        }
                    }

                    mTwoInvVTV[i] = twoinvvdv;
                    for (c = i + 2; c < mNumCols; ++c) {
                        mMatrix[c + mNumCols * i] = mVVector[c];
                    }
                }

                mTwoInvUTU[i] = twoinvudu;
                for (r = ip1; r < mNumRows; ++r) {
                    mMatrix[i + mNumCols * r] = mUVector[r];
                }
            }

            // Copy the diagonal and subdiagonal for cache coherence in the
            // Golub-Kahan iterations.
            int k, ksup = mNumCols - 1, index = 0, delta = mNumCols + 1;
            for (k = 0; k < ksup; ++k, index += delta) {
                mDiagonal[k] = mMatrix[index];
                mSuperdiagonal[k] = mMatrix[index + 1];
            }
            mDiagonal[k] = mMatrix[index];
        }

        // A helper for generating Givens rotation sine and cosine robustly.
        void GetSinCos(double x, double y, out double cs, out double sn)
        {
            // Solves sn*x + cs*y = 0 robustly.
            double tau;
            if (y != (double)0) {
                if (Math.Abs(y) > Math.Abs(x)) {
                    tau = -x / y;
                    sn = ((double)1) / Math.Sqrt(((double)1) + tau * tau);
                    cs = sn * tau;
                } else {
                    tau = -y / x;
                    cs = ((double)1) / Math.Sqrt(((double)1) + tau * tau);
                    sn = cs * tau;
                }
            } else {
                cs = (double)1;
                sn = (double)0;
            }
        }

        // Test for (effectively) zero-valued diagonal entries (through all but
        // the last).  For each such entry, the B matrix decouples.  Perform
        // that decoupling.  If there are no zero-valued entries, then the
        // Golub-Kahan step must be performed.
        bool DiagonalEntriesNonzero(int imin, int imax, double threshold)
        {
            for (int i = imin; i <= imax; ++i) {
                if (Math.Abs(mDiagonal[i]) <= threshold) {
                    // Use planar rotations to case the superdiagonal entry out of
                    // the matrix, thus producing a row of zeros.
                    double x, z, cs, sn;
                    double y = mSuperdiagonal[i];
                    mSuperdiagonal[i] = (double)0;
                    for (int j = i + 1; j <= imax + 1; ++j) {
                        x = mDiagonal[j];
                        GetSinCos(x, y, out cs, out sn);
                        mLGivens.Add(new GivensRotation(i, j, cs, sn));
                        mDiagonal[j] = cs * x - sn * y;
                        if (j <= imax) {
                            z = mSuperdiagonal[j];
                            mSuperdiagonal[j] = cs * z;
                            y = sn * z;
                        }
                    }
                    return false;
                }
            }
            return true;
        }

        // This is Algorithm 8.3.1 in "Matrix Computations, 2nd edition" by
        // G. H. Golub and C. F. Van Loan.
        void DoGolubKahanStep(int imin, int imax)
        {
            // The implicit shift.  Compute the eigenvalue u of the lower-right 2x2
            // block of A = B^T*B that is closer to b11.
            double f0 = (imax >= (double)1 ? mSuperdiagonal[imax - 1] : (double)0);
            double d1 = mDiagonal[imax];
            double f1 = mSuperdiagonal[imax];
            double d2 = mDiagonal[imax + 1];
            double a00 = d1 * d1 + f0 * f0;
            double a01 = d1 * f1;
            double a11 = d2 * d2 + f1 * f1;
            double dif = (a00 - a11) * (double)0.5;
            double sgn = (dif >= (double)0 ? (double)1 : (double) - 1);
            double a01sqr = a01 * a01;
            double u = a11 - a01sqr / (dif + sgn * Math.Sqrt(dif * dif + a01sqr));
            double x = mDiagonal[imin] * mDiagonal[imin] - u;
            double y = mDiagonal[imin] * mSuperdiagonal[imin];

            double a12, a21, a22, a23, cs, sn;
            double a02 = (double)0;
            int i0 = imin - 1, i1 = imin, i2 = imin + 1;
            for (/**/; i1 <= imax; ++i0, ++i1, ++i2) {
                // Compute the Givens rotation G and save it for use in computing
                // V in U^T*A*V = S.
                GetSinCos(x, y, out cs, out sn);
                mRGivens.Add(new GivensRotation(i1, i2, cs, sn));

                // Update B0 = B*G.
                if (i1 > imin) {
                    mSuperdiagonal[i0] = cs * mSuperdiagonal[i0] - sn * a02;
                }

                a11 = mDiagonal[i1];
                a12 = mSuperdiagonal[i1];
                a22 = mDiagonal[i2];
                mDiagonal[i1] = cs * a11 - sn * a12;
                mSuperdiagonal[i1] = sn * a11 + cs * a12;
                mDiagonal[i2] = cs * a22;
                a21 = -sn * a22;

                // Update the parameters for the next Givens rotations.
                x = mDiagonal[i1];
                y = a21;

                // Compute the Givens rotation G and save it for use in computing
                // U in U^T*A*V = S.
                GetSinCos(x, y, out cs, out sn);
                mLGivens.Add(new GivensRotation(i1, i2, cs, sn));

                // Update B1 = G^T*B0.
                a11 = mDiagonal[i1];
                a12 = mSuperdiagonal[i1];
                a22 = mDiagonal[i2];
                mDiagonal[i1] = cs * a11 - sn * a21;
                mSuperdiagonal[i1] = cs * a12 - sn * a22;
                mDiagonal[i2] = sn * a12 + cs * a22;

                if (i1 < imax) {
                    a23 = mSuperdiagonal[i2];
                    a02 = -sn * a23;
                    mSuperdiagonal[i2] = cs * a23;

                    // Update the parameters for the next Givens rotations.
                    x = mSuperdiagonal[i1];
                    y = a02;
                }
            }
        }

        // The diagonal entries are not guaranteed to be nonnegative during the
        //ruction.  After convergence to a diagonal matrix S, test for
        // negative entries and build a diagonal matrix that reverses the sign
        // on the S-entry.
        void EnsureNonnegativeDiagonal()
        {
            for (int i = 0; i < mNumCols; ++i) {
                if (mDiagonal[i] >= 0) {
                    mFixupDiagonal[i] = 1.0;
                } else {
                    mDiagonal[i] = -mDiagonal[i];
                    mFixupDiagonal[i] = -1.0;
                }
            }
        }

        // Sort the singular values and compute the corresponding permutation of
        // the indices of the array storing the singular values.  The permutation
        // is used for reordering the singular values and the corresponding
        // columns of the orthogonal matrix in the calls to GetSingularValues(...)
        // and GetOrthogonalMatrices(...).
        void ComputePermutation(int sortType)
        {
            if (sortType == 0) {
                // Set a flag for GetSingularValues() and GetOrthogonalMatrices() to
                // know that sorted output was not requested.
                mPermutation[0] = -1;
                return;
            }

            double[] singularValues = new double[mNumCols];
            int[] indices = new int[mNumCols];
            for ( int i = 0; i < mNumCols; ++i ) {
                singularValues[i] = mDiagonal[i];
                indices[i] = i;
            }
            Array.Sort(singularValues, indices);
            if (sortType < 0)
                Array.Reverse(indices);
            mPermutation = indices;

            // GetOrthogonalMatrices() has nontrivial code for computing the
            // orthogonal U and V from the reflections and rotations.  To avoid
            // complicating the code further when sorting is requested, U and V are
            // computed as in the unsorted case.  We then need to swap columns of
            // U and V to be consistent with the sorting of the singular values.  To
            // minimize copying due to column swaps, we use permutation P.  The
            // minimum number of transpositions to obtain P from I is N minus the
            // number of cycles of P.  Each cycle is reordered with a minimum number
            // of transpositions; that is, the singular items are cyclically swapped,
            // leading to a minimum amount of copying.  For example, if there is a
            // cycle i0 -> i1 -> i2 -> i3, then the copying is
            //   save = singularitem[i0];
            //   singularitem[i1] = singularitem[i2];
            //   singularitem[i2] = singularitem[i3];
            //   singularitem[i3] = save;
        }

        // The number rows and columns of the matrices to be processed.
        int mNumRows, mNumCols;

        // The maximum number of iterations for reducing the bidiagonal matrix
        // to a diagonal matrix.
        int mMaxIterations;

        // The internal copy of a matrix passed to the solver.  See the comments
        // about function Bidiagonalize() about what is stored in the matrix.
        double[] mMatrix;  // MxN elements

        // After the initial bidiagonalization by Householder reflections, we no
        // longer need the full mMatrix.  Copy the diagonal and superdiagonal
        // entries to linear arrays in order to be cache friendly.
        double[] mDiagonal;  // N elements
        double[] mSuperdiagonal;  // N-1 elements

        // The Givens rotations used to reduce the initial bidiagonal matrix to
        // a diagonal matrix.  A rotation is the identity with the following
        // replacement entries:  R(index0,index0) = cs, R(index0,index1) = sn,
        // R(index1,index0) = -sn, and R(index1,index1) = cs.  If N is the
        // number of matrix columns and K is the maximum number of iterations, the
        // maximum number of right or left Givens rotations is K*(N-1).  The
        // maximum amount of memory is allocated to store these.  However, we also
        // potentially need left rotations to decouple the matrix when a diagonal
        // terms are zero.  Worst case is a number of matrices quadratic in N, so
        // for now we just use std::vector<Rotation> whose initial capacity is
        // K*(N-1).
        struct GivensRotation
        {
            public GivensRotation(int inIndex0, int inIndex1, double inCs, double inSn) {
                index0 = inIndex0; index1 = inIndex1; cs = inCs; sn = inSn;
            }
            public int index0, index1;
            public double cs, sn;
        };

        List<GivensRotation> mRGivens;
        List<GivensRotation> mLGivens;

        // The diagonal matrix that is used to convert S-entries to nonnegative.
        double[] mFixupDiagonal;  // N elements

        int[] mPermutation;  // N elements
        int[] mVisited;  // N elements

        // Temporary storage to compute Householder reflections and to support
        // sorting of columns of the orthogonal matrices.
        double[] mTwoInvUTU;  // N elements
        double[] mTwoInvVTV;  // N-2 elements
        double[] mUVector;  // M elements
        double[] mVVector;  // N elements
        double[] mWVector;  // max(M,N) elements
    }
}
