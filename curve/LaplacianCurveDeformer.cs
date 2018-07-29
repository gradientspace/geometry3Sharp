using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace g3
{
    /// <summary>
    /// Variant of LaplacianMeshDeformer that can be applied to 3D curve.
    /// 
    /// Solve in each dimension can be disabled using .SolveX/Y/Z
    /// 
    /// Currently only supports uniform weights (in Initialize)
    /// 
    /// </summary>
    public class LaplacianCurveDeformer
    {
        public DCurve3 Curve;


        public bool SolveX = true;
        public bool SolveY = true;
        public bool SolveZ = true;


        // indicates that solve did not converge in at least one dimension
        public bool ConvergeFailed = false;


        // info that is fixed based on mesh
        PackedSparseMatrix PackedM;
        int N;
        int[] ToCurveV, ToIndex;
        double[] Px, Py, Pz;
        int[] nbr_counts;
        double[] MLx, MLy, MLz;

        // constraints
        public struct SoftConstraintV
        {
            public Vector3d Position;
            public double Weight;
            public bool PostFix;
        }
        Dictionary<int, SoftConstraintV> SoftConstraints = new Dictionary<int, SoftConstraintV>();
        bool HavePostFixedConstraints = false;


        // needs to be updated after constraints
        bool need_solve_update;
        DiagonalMatrix WeightsM;
        double[] Cx, Cy, Cz;
        double[] Bx, By, Bz;
        DiagonalMatrix Preconditioner;


        // Appendix C from http://sites.fas.harvard.edu/~cs277/papers/deformation_survey.pdf
        public bool UseSoftConstraintNormalEquations = true;


        // result
        double[] Sx, Sy, Sz;


        public LaplacianCurveDeformer(DCurve3 curve)
        {
            Curve = curve;
        }


        public void SetConstraint(int vID, Vector3d targetPos, double weight, bool bForceToFixedPos = false)
        {
            SoftConstraints[vID] = new SoftConstraintV() { Position = targetPos, Weight = weight, PostFix = bForceToFixedPos };
            HavePostFixedConstraints = HavePostFixedConstraints || bForceToFixedPos;
            need_solve_update = true;
        }

        public bool IsConstrained(int vID) {
            return SoftConstraints.ContainsKey(vID);
        }

        public void ClearConstraints()
        {
            SoftConstraints.Clear();
            HavePostFixedConstraints = false;
            need_solve_update = true;
        }


        public void Initialize()
        {
            int NV = Curve.VertexCount;
            ToCurveV = new int[NV];
            ToIndex = new int[NV];

            N = 0;
            for ( int k = 0; k < NV; k++) {
                int vid = k;
                ToCurveV[N] = vid;
                ToIndex[vid] = N;
                N++;
            }

            Px = new double[N];
            Py = new double[N];
            Pz = new double[N];
            nbr_counts = new int[N];
            SymmetricSparseMatrix M = new SymmetricSparseMatrix();

            for (int i = 0; i < N; ++i) {
                int vid = ToCurveV[i];
                Vector3d v = Curve.GetVertex(vid);
                Px[i] = v.x; Py[i] = v.y; Pz[i] = v.z;
                nbr_counts[i] = (i == 0 || i == N-1) ? 1 : 2;
            }

            // construct laplacian matrix
            for (int i = 0; i < N; ++i) {
                int vid = ToCurveV[i];
                int n = nbr_counts[i];

                Index2i nbrs = Curve.Neighbours(vid);
                
                double sum_w = 0;
                for ( int k = 0; k < 2; ++k ) {
                    int nbrvid = nbrs[k];
                    if (nbrvid == -1)
                        continue;
                    int j = ToIndex[nbrvid];
                    int n2 = nbr_counts[j];

                    // weight options
                    double w = -1;
                    //double w = -1.0 / Math.Sqrt(n + n2);
                    //double w = -1.0 / n;

                    M.Set(i, j, w);
                    sum_w += w;
                }
                sum_w = -sum_w;
                M.Set(vid, vid, sum_w);
            }

            // transpose(L) * L, but matrix is symmetric...
            if (UseSoftConstraintNormalEquations) {
                //M = M.Multiply(M);
                // only works if M is symmetric!!
                PackedM = M.SquarePackedParallel();
            } else {
                PackedM = new PackedSparseMatrix(M);
            }

            // compute laplacian vectors of initial mesh positions
            MLx = new double[N];
            MLy = new double[N];
            MLz = new double[N];
            PackedM.Multiply(Px, MLx);
            PackedM.Multiply(Py, MLy);
            PackedM.Multiply(Pz, MLz);

            // allocate memory for internal buffers
            Preconditioner = new DiagonalMatrix(N);
            WeightsM = new DiagonalMatrix(N);
            Cx = new double[N]; Cy = new double[N]; Cz = new double[N];
            Bx = new double[N]; By = new double[N]; Bz = new double[N];
            Sx = new double[N]; Sy = new double[N]; Sz = new double[N];

            need_solve_update = true;
            UpdateForSolve();
        }




        void UpdateForSolve()
        {
            if (need_solve_update == false)
                return;

            // construct constraints matrix and RHS
            WeightsM.Clear();
            Array.Clear(Cx, 0, N);
            Array.Clear(Cy, 0, N);
            Array.Clear(Cz, 0, N);
            foreach ( var constraint in SoftConstraints ) {
                int vid = constraint.Key;
                int i = ToIndex[vid];
                double w = constraint.Value.Weight;

                if (UseSoftConstraintNormalEquations)
                    w = w * w;

                WeightsM.Set(i, i, w);
                Vector3d pos = constraint.Value.Position;
                Cx[i] = w * pos.x;
                Cy[i] = w * pos.y;
                Cz[i] = w * pos.z;
            }

            // add RHS vectors
            for (int i = 0; i < N; ++i) {
                Bx[i] = MLx[i] + Cx[i];
                By[i] = MLy[i] + Cy[i];
                Bz[i] = MLz[i] + Cz[i];
            }

            // update basic preconditioner
            // [RMS] currently not using this...it actually seems to make things worse!! 
            for ( int i = 0; i < N; i++ ) {
                double diag_value = PackedM[i, i] + WeightsM[i, i];
                Preconditioner.Set(i, i, 1.0 / diag_value);
            }

            need_solve_update = false;
        }



        // Result must be as large as Mesh.MaxVertexID
        public bool SolveMultipleCG(Vector3d[] Result)
        {
            if (WeightsM == null)
                Initialize();       // force initialize...

            UpdateForSolve();

            // use initial positions as initial solution. 
            Array.Copy(Px, Sx, N);
            Array.Copy(Py, Sy, N);
            Array.Copy(Pz, Sz, N);


            Action<double[], double[]> CombinedMultiply = (X, B) => {
                //PackedM.Multiply(X, B);
                PackedM.Multiply_Parallel(X, B);

                for (int i = 0; i < N; ++i)
                    B[i] += WeightsM[i, i] * X[i];
            };

            List<SparseSymmetricCG> Solvers = new List<SparseSymmetricCG>();
            if (SolveX) {
                Solvers.Add(new SparseSymmetricCG() { B = Bx, X = Sx,
                    MultiplyF = CombinedMultiply, PreconditionMultiplyF = Preconditioner.Multiply,
                    UseXAsInitialGuess = true
                });
            }
            if (SolveY) {
                Solvers.Add(new SparseSymmetricCG() { B = By, X = Sy,
                    MultiplyF = CombinedMultiply, PreconditionMultiplyF = Preconditioner.Multiply,
                    UseXAsInitialGuess = true
                });
            }
            if (SolveZ) {
                Solvers.Add(new SparseSymmetricCG() { B = Bz, X = Sz,
                    MultiplyF = CombinedMultiply, PreconditionMultiplyF = Preconditioner.Multiply,
                    UseXAsInitialGuess = true
                });
            }
            bool[] ok = new bool[Solvers.Count];

            gParallel.ForEach(Interval1i.Range(Solvers.Count), (i) => {
                ok[i] = Solvers[i].Solve();
                // preconditioned solve is slower =\
                //ok[i] = solvers[i].SolvePreconditioned();
            });

            ConvergeFailed = false;
            foreach ( bool b in ok ) {
                if (b == false)
                    ConvergeFailed = true;
            }

            for ( int i = 0; i < N; ++i ) {
                int vid = ToCurveV[i];
                Result[vid] = new Vector3d(Sx[i], Sy[i], Sz[i]);
            }

            // apply post-fixed constraints
            if (HavePostFixedConstraints) {
                foreach (var constraint in SoftConstraints) {
                    if (constraint.Value.PostFix) {
                        int vid = constraint.Key;
                        Result[vid] = constraint.Value.Position;
                    }
                }
            }

            return true;
        }




        // Result must be as large as Mesh.MaxVertexID
        public bool SolveMultipleRHS(Vector3d[] Result)
        {
            if (WeightsM == null)
                Initialize();       // force initialize...

            UpdateForSolve();

            // use initial positions as initial solution. 
            double[][] B = BufferUtil.InitNxM(3, N, new double[][] { Bx, By, Bz });
            double[][] X = BufferUtil.InitNxM(3, N, new double[][] { Px, Py, Pz });

            Action<double[][], double[][]> CombinedMultiply = (Xt, Bt) => {
                PackedM.Multiply_Parallel_3(Xt, Bt);
                gParallel.ForEach(Interval1i.Range(3), (j) => {
                    BufferUtil.MultiplyAdd(Bt[j], WeightsM.D, Xt[j]);
                });
            };

            SparseSymmetricCGMultipleRHS Solver = new SparseSymmetricCGMultipleRHS() {
                B = B, X = X,
                MultiplyF = CombinedMultiply, PreconditionMultiplyF = null,
                UseXAsInitialGuess = true
            };

            bool ok = Solver.Solve();

            if (ok == false)
                return false;

            for (int i = 0; i < N; ++i) {
                int vid = ToCurveV[i];
                Result[vid] = new Vector3d(X[0][i], X[1][i], X[2][i]);
            }

            // apply post-fixed constraints
            if (HavePostFixedConstraints) {
                foreach (var constraint in SoftConstraints) {
                    if (constraint.Value.PostFix) {
                        int vid = constraint.Key;
                        Result[vid] = constraint.Value.Position;
                    }
                }
            }

            return true;
        }





        public bool Solve(Vector3d[] Result)
        {
            // for small problems, faster to use separate CGs?
            if ( Curve.VertexCount < 10000 )
                return SolveMultipleCG(Result);
            else
                return SolveMultipleRHS(Result);
        }



        public bool SolveAndUpdateCurve()
        {
            int N = Curve.VertexCount;
            Vector3d[] Result = new Vector3d[N];
            if ( Solve(Result) == false )
                return false;
            for (int i = 0; i < N; ++i) {
                Curve[i] = Result[i];
            }
            return true;
        }



    }
}
