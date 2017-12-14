using System;

namespace g3
{
    // ported from WildMagic5 Wm5LinearSystem.cpp
    public class SparseSymmetricCG
    {
        // Compute B = A*X, where inputs are ordered <X,B>
        public Action<double[], double[]> MultiplyF;

        // Compute B = A*X, where inputs are ordered <X,B>
        public Action<double[], double[]> PreconditionMultiplyF;

        // B is not modified!
        public double[] B;

        // X will be used as initial guess if non-null and UseXAsInitialGuess is true
        // After Solve(), solution will be available in X
        public double[] X;
        public bool UseXAsInitialGuess = true;

        public int MaxIterations = 1024;
        public int Iterations;

        // internal
        double[] R, P, AP, Z;




        public bool Solve()
        {
            Iterations = 0;
            int size = B.Length;

            // Based on the algorithm in "Matrix Computations" by Golum and Van Loan.
            R = new double[size];
            P = new double[size];
            AP = new double[size];

            if ( X == null || UseXAsInitialGuess == false ) {
                if ( X == null )
                    X = new double[size];
                Array.Clear(X, 0, X.Length);
                Array.Copy(B, R, B.Length);
            } else {
                // hopefully is X is a decent initialization...
                InitializeR(R);
            }

            // [RMS] these were inside loop but they are constant!
            double norm = BufferUtil.Dot(B, B);
            double root1 = Math.Sqrt(norm);

            // The first iteration. 
            double rho0 = BufferUtil.Dot(R, R);

            // [RMS] If we were initialized w/ constraints already satisfied, 
            //   then we are done! (happens for example in mesh deformations)
            if (rho0 < MathUtil.ZeroTolerance * root1)
                return true;

            Array.Copy(R, P, R.Length);

            MultiplyF(P, AP);

            double alpha = rho0 / BufferUtil.Dot(P, AP);
            BufferUtil.MultiplyAdd(X, alpha, P);
            BufferUtil.MultiplyAdd(R, -alpha, AP);
            double rho1 = BufferUtil.Dot(R, R);

            // The remaining iterations.
            int iter;
            for (iter = 1; iter < MaxIterations; ++iter) {
                double root0 = Math.Sqrt(rho1);
                if (root0 <= MathUtil.ZeroTolerance * root1) {
                    break;
                }

                double beta = rho1 / rho0;
                UpdateP(P, beta, R);

                MultiplyF(P, AP);

                alpha = rho1 / BufferUtil.Dot(P, AP);

                // can compute these two steps simultaneously
                double RdotR = 0;
                gParallel.Evaluate(
                    () => { BufferUtil.MultiplyAdd(X, alpha, P); },
                    () => { RdotR = BufferUtil.MultiplyAdd_GetSqrSum(R, -alpha, AP); } 
                );

                rho0 = rho1;
                rho1 = RdotR; // BufferUtil.Dot(R, R);
            }

            //System.Console.WriteLine("{0} iterations", iter);
            Iterations = iter;
            return iter < MaxIterations;
        }


        void UpdateP(double[] P, double beta, double[] R)
        {
            for (int i = 0; i < P.Length; ++i)
                P[i] = R[i] + beta * P[i];
        }


        void InitializeR(double[] R)
        {
            // R = B - A*X
            MultiplyF(X, R);
            for (int i = 0; i < X.Length; ++i)
                R[i] = B[i] - R[i];
        }







        public bool SolvePreconditioned()
        {
            Iterations = 0;
            int n = B.Length;

            R = new double[n];
            P = new double[n];
            AP = new double[n];
            Z = new double[n];

            if (X == null || UseXAsInitialGuess == false) {
                if (X == null)
                    X = new double[n];
                Array.Clear(X, 0, X.Length);
                Array.Copy(B, R, B.Length);
            } else {
                // hopefully is X is a decent initialization...
                InitializeR(R);
            }

            // [RMS] for convergence test?
            double norm = BufferUtil.Dot(B, B);
            double root1 = Math.Sqrt(norm);

            // r_0 = b - A*x_0
            MultiplyF(X, R);
            for (int i = 0; i < n; ++i)
                R[i] = B[i] - R[i];

            // z0 = M_inverse * r_0
            PreconditionMultiplyF(R, Z);

            // p0 = z0
            Array.Copy(Z, P, n);

            double RdotZ_k = BufferUtil.Dot(R, Z);

            int iter = 0;
            while (iter++ < MaxIterations) {

                // convergence test
                double root0 = Math.Sqrt(RdotZ_k);
                if (root0 <= MathUtil.ZeroTolerance * root1) {
                    break;
                }

                MultiplyF(P, AP);
                double alpha_k = RdotZ_k / BufferUtil.Dot(P, AP);

                gParallel.Evaluate(
                    // x_k+1 = x_k + alpha_k * p_k
                    () => { BufferUtil.MultiplyAdd(X, alpha_k, P); },
                    // r_k+1 = r_k - alpha_k * A * p_k
                    () => { BufferUtil.MultiplyAdd(R, -alpha_k, AP); }
                );

                // z_k+1 = M_inverse * r_k+1
                PreconditionMultiplyF(R, Z);

                // beta_k = (z_k+1 * r_k+1) / (z_k * r_k)
                double beta_k = BufferUtil.Dot(Z, R) / RdotZ_k;

                gParallel.Evaluate(
                    // p_k+1 = z_k+1 + beta_k * p_k
                    () => {
                        for (int i = 0; i < n; ++i)
                            P[i] = Z[i] + beta_k * P[i];
                    },
                    () => { RdotZ_k = BufferUtil.Dot(R, Z); }
                );
            }


            //System.Console.WriteLine("{0} iterations", iter);
            Iterations = iter;
            return iter < MaxIterations;
        }


    }











    /// <summary>
    /// [RMS] this is a variant of SparseSymmetricCG that supports multiple right-hand-sides.
    /// Makes quite a big difference as matrix gets bigger, because MultiplyF can
    /// unroll inner loops (as long as you actually do that)
    /// 
    /// However, if this is done then it is not really possible to do different numbers
    /// of iterations for different RHS's. We will not update that RHS once it has 
    /// converged, however we still have to do the multiplies!
    /// 
    /// </summary>
    public class SparseSymmetricCGMultipleRHS
    {
        // Compute B = A*X, where inputs are ordered <X,B>
        public Action<double[][], double[][]> MultiplyF;

        public Action<double[][], double[][]> PreconditionMultiplyF;

        // B is not modified!
        public double[][] B;

        public double ConvergeTolerance = MathUtil.ZeroTolerance;

        // X will be used as initial guess if non-null and UseXAsInitialGuess is true
        // After Solve(), solution will be available in X
        public double[][] X;
        public bool UseXAsInitialGuess = true;

        public int MaxIterations = 1024;
        public int Iterations;

        // internal
        double[][] R, P, W, AP, Z;


        /// <summary>
        /// standard CG solve
        /// </summary>
        public bool Solve()
        {
            Iterations = 0;
            if (B == null || MultiplyF == null)
                throw new Exception("SparseSymmetricCGMultipleRHS.Solve(): Must set B and MultiplyF!");
            int NRHS = B.Length;
            if ( NRHS == 0 )
                throw new Exception("SparseSymmetricCGMultipleRHS.Solve(): Need at least one RHS vector in B");
            int size = B[0].Length;

            // Based on the algorithm in "Matrix Computations" by Golum and Van Loan.
            R = BufferUtil.AllocNxM(NRHS, size);
            P = BufferUtil.AllocNxM(NRHS, size);
            W = BufferUtil.AllocNxM(NRHS, size);

            if (X == null || UseXAsInitialGuess == false) {
                if (X == null)
                    X = BufferUtil.AllocNxM(NRHS, size);
                for (int j = 0; j < NRHS; ++j) {
                    Array.Clear(X[j], 0, size);
                    Array.Copy(B[j], R[j], size);
                }
            } else {
                // hopefully is X is a decent initialization...
                InitializeR(R);
            }

            // [RMS] these were inside loop but they are constant!
            double[] norm = new double[NRHS];
            for ( int j = 0; j < NRHS; ++j )
                norm[j] = BufferUtil.Dot(B[j], B[j]);
            double[] root1 = new double[NRHS];
            for (int j = 0; j < NRHS; ++j)
                root1[j] = Math.Sqrt(norm[j]);

            // The first iteration. 
            double[] rho0 = new double[NRHS];
            for (int j = 0; j < NRHS; ++j)
                rho0[j] = BufferUtil.Dot(R[j], R[j]);

            // [RMS] If we were initialized w/ constraints already satisfied, 
            //   then we are done! (happens for example in mesh deformations)
            bool[] converged = new bool[NRHS];
            int nconverged = 0;
            for (int j = 0; j < NRHS; ++j) {
                converged[j] = rho0[j] < (ConvergeTolerance * root1[j]);
                if (converged[j])
                    nconverged++;
            }
            if (nconverged == NRHS)
                return true;

            for ( int j = 0; j < NRHS; ++j )
                Array.Copy(R[j], P[j], size);

            MultiplyF(P, W);

            double[] alpha = new double[NRHS];
            for ( int j = 0; j < NRHS; ++j )
                alpha[j] = rho0[j] / BufferUtil.Dot(P[j], W[j]);

            for (int j = 0; j < NRHS; ++j)
                BufferUtil.MultiplyAdd(X[j], alpha[j], P[j]);
            for (int j = 0; j < NRHS; ++j)
                BufferUtil.MultiplyAdd(R[j], -alpha[j], W[j]);

            double[] rho1 = new double[NRHS];
            for (int j = 0; j < NRHS; ++j)
                rho1[j] = BufferUtil.Dot(R[j], R[j]);

            double[] beta = new double[NRHS];

            Interval1i rhs = Interval1i.Range(NRHS);

            // The remaining iterations.
            int iter;
            for (iter = 1; iter < MaxIterations; ++iter) {

                bool done = true;
                for (int j = 0; j < NRHS; ++j) {
                    if (converged[j] == false) {
                        double root0 = Math.Sqrt(rho1[j]);
                        if (root0 <= ConvergeTolerance * root1[j])
                            converged[j] = true;
                    }
                    if (converged[j] == false)
                        done = false;
                }
                if (done)
                    break;

                for (int j = 0; j < NRHS; ++j) 
                    beta[j] = rho1[j] / rho0[j];
                UpdateP(P, beta, R, converged);

                MultiplyF(P, W);

                gParallel.ForEach(rhs, (j) => {
                    if ( converged[j] == false )
                        alpha[j] = rho1[j] / BufferUtil.Dot(P[j], W[j]);
                });

                // can do all these in parallel, but improvement is minimal
                gParallel.ForEach(rhs, (j) => {
                    if (converged[j] == false)
                        BufferUtil.MultiplyAdd(X[j], alpha[j], P[j]);
                });
                gParallel.ForEach(rhs, (j) => {
                    if (converged[j] == false) {
                        rho0[j] = rho1[j];
                        rho1[j] = BufferUtil.MultiplyAdd_GetSqrSum(R[j], -alpha[j], W[j]);
                    }
                });
            }

            //System.Console.WriteLine("{0} iterations", iter);
            Iterations = iter;
            return iter < MaxIterations;
        }





        /// <summary>
        /// Preconditioned variant
        /// Similar to non-preconditioned version, this can suffer if one solution converges
        /// much slower than others, as we can't skip matrix multiplies in that case.
        /// </summary>
        public bool SolvePreconditioned()
        {
            Iterations = 0;
            if (B == null || MultiplyF == null || PreconditionMultiplyF == null)
                throw new Exception("SparseSymmetricCGMultipleRHS.SolvePreconditioned(): Must set B and MultiplyF and PreconditionMultiplyF!");
            int NRHS = B.Length;
            if (NRHS == 0)
                throw new Exception("SparseSymmetricCGMultipleRHS.SolvePreconditioned(): Need at least one RHS vector in B");
            int n = B[0].Length;

            R = BufferUtil.AllocNxM(NRHS, n);
            P = BufferUtil.AllocNxM(NRHS, n);
            AP = BufferUtil.AllocNxM(NRHS, n);
            Z = BufferUtil.AllocNxM(NRHS, n);

            if (X == null || UseXAsInitialGuess == false) {
                if (X == null)
                    X = BufferUtil.AllocNxM(NRHS, n);
                for (int j = 0; j < NRHS; ++j) {
                    Array.Clear(X[j], 0, n);
                    Array.Copy(B[j], R[j], n);
                }
            } else {
                // hopefully is X is a decent initialization...
                InitializeR(R);
            }

            // [RMS] for convergence test?
            double[] norm = new double[NRHS];
            for (int j = 0; j < NRHS; ++j)
                norm[j] = BufferUtil.Dot(B[j], B[j]);
            double[] root1 = new double[NRHS];
            for (int j = 0; j < NRHS; ++j)
                root1[j] = Math.Sqrt(norm[j]);


            // r_0 = b - A*x_0
            MultiplyF(X, R);
            for (int j = 0; j < NRHS; ++j) {
                for (int i = 0; i < n; ++i)
                    R[j][i] = B[j][i] - R[j][i];
            }

            // z0 = M_inverse * r_0
            PreconditionMultiplyF(R, Z);

            // p0 = z0
            for (int j = 0; j < NRHS; ++j)
                Array.Copy(Z[j], P[j], n);

            // compute initial R*Z
            double[] RdotZ_k = new double[NRHS];
            for (int j = 0; j < NRHS; ++j)
                RdotZ_k[j] = BufferUtil.Dot(R[j], Z[j]);

            double[] alpha_k = new double[NRHS];
            double[] beta_k = new double[NRHS];
            bool[] converged = new bool[NRHS];
            Interval1i rhs = Interval1i.Range(NRHS);

            int iter = 0;
            while (iter++ < MaxIterations) {

                // convergence test
                bool done = true;
                for (int j = 0; j < NRHS; ++j) {
                    if (converged[j] == false) {
                        double root0 = Math.Sqrt(RdotZ_k[j]);
                        if (root0 <= ConvergeTolerance * root1[j])
                            converged[j] = true;
                    }
                    if (converged[j] == false)
                        done = false;
                }
                if (done)
                    break;

                MultiplyF(P, AP);

                gParallel.ForEach(rhs, (j) => {
                    if ( converged[j] == false )
                        alpha_k[j] = RdotZ_k[j] / BufferUtil.Dot(P[j], AP[j]);
                });

                // x_k+1 = x_k + alpha_k * p_k
                gParallel.ForEach(rhs, (j) => {
                    if (converged[j] == false) {
                        BufferUtil.MultiplyAdd(X[j], alpha_k[j], P[j]);
                    }
                });

                // r_k+1 = r_k - alpha_k * A * p_k
                gParallel.ForEach(rhs, (j) => {
                    if (converged[j] == false) {
                        BufferUtil.MultiplyAdd(R[j], -alpha_k[j], AP[j]);
                    }
                });

                // z_k+1 = M_inverse * r_k+1
                PreconditionMultiplyF(R, Z);

                // beta_k = (z_k+1 * r_k+1) / (z_k * r_k)
                gParallel.ForEach(rhs, (j) => {
                    if (converged[j] == false)
                        beta_k[j] = BufferUtil.Dot(Z[j], R[j]) / RdotZ_k[j];
                });

                // can do these in parallel but improvement is minimal

                // p_k+1 = z_k+1 + beta_k * p_k
                gParallel.ForEach(rhs, (j) => {
                    if (converged[j] == false) {
                        for (int i = 0; i < n; ++i)
                            P[j][i] = Z[j][i] + beta_k[j] * P[j][i];
                    }
                });

                gParallel.ForEach(rhs, (j) => {
                    if (converged[j] == false) {
                        RdotZ_k[j] = BufferUtil.Dot(R[j], Z[j]);
                    }
                });
            }


            //System.Console.WriteLine("{0} iterations", iter);
            Iterations = iter;
            return iter < MaxIterations;
        }











        void UpdateP(double[][] P, double[] beta, double[][] R, bool[] converged)
        {
            Interval1i rhs = Interval1i.Range(P.Length);
            gParallel.ForEach(rhs, (j) => {
                if (converged[j] == false) {
                    int n = P[j].Length;
                    for (int i = 0; i < n; ++i)
                        P[j][i] = R[j][i] + beta[j] * P[j][i];
                }
            });
        }


        void InitializeR(double[][] R)
        {
            // R = B - A*X
            MultiplyF(X, R);
            for (int j = 0; j < X.Length; ++j) {
                int n = R[j].Length;
                for (int i = 0; i < n; ++i)
                    R[j][i] = B[j][i] - R[j][i];
            }
        }




    }


}
