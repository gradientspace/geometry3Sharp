using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace g3
{
    public class LaplacianMeshDeformer
    {
        public DMesh3 Mesh;

        // info that is fixed based on mesh
        SymmetricSparseMatrix M;
        int N;
        int[] ToMeshV, ToIndex;
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


        // result
        double[] Sx, Sy, Sz;


        public LaplacianMeshDeformer(DMesh3 mesh)
        {
            Mesh = mesh;
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
            ToMeshV = new int[Mesh.MaxVertexID];
            ToIndex = new int[Mesh.MaxVertexID];
            N = 0;
            foreach (int vid in Mesh.VertexIndices()) {
                ToMeshV[N] = vid;
                ToIndex[vid] = N;
                N++;
            }

            Px = new double[N];
            Py = new double[N];
            Pz = new double[N];
            nbr_counts = new int[N];
            M = new SymmetricSparseMatrix();

            for (int i = 0; i < N; ++i) {
                int vid = ToMeshV[i];
                Vector3d v = Mesh.GetVertex(vid);
                Px[i] = v.x; Py[i] = v.y; Pz[i] = v.z;
                nbr_counts[i] = Mesh.GetVtxEdgeCount(vid);
            }

            // construct laplacian matrix
            for (int i = 0; i < N; ++i) {
                int vid = ToMeshV[i];
                int n = nbr_counts[i];

                double sum_w = 0;
                foreach (int nbrvid in Mesh.VtxVerticesItr(vid)) {
                    int j = ToIndex[nbrvid];
                    int n2 = nbr_counts[j];

                    // weight options
                    //double w = -1;
                    double w = -1.0 / Math.Sqrt(n + n2);
                    //double w = -1.0 / n;

                    M.Set(i, j, w);
                    sum_w += w;
                }
                sum_w = -sum_w;
                M.Set(vid, vid, sum_w);
            }

            // compute laplacian vectors of initial mesh positions
            MLx = new double[N];
            MLy = new double[N];
            MLz = new double[N];
            M.Multiply(Px, MLx);
            M.Multiply(Py, MLy);
            M.Multiply(Pz, MLz);

            // allocate memory for internal buffers
            Preconditioner = new DiagonalMatrix(N);
            WeightsM = new DiagonalMatrix(N);
            Cx = new double[N]; Cy = new double[N]; Cz = new double[N];
            Bx = new double[N]; By = new double[N]; Bz = new double[N];
            Sx = new double[N]; Sy = new double[N]; Sz = new double[N];

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
            // [RMS] currently not using this...it actually seems to make things
            //   worse!! 
            for ( int i = 0; i < N; i++ ) {
                //double diag_value = M[i, i] + WeightsM[i, i];
                double diag_value = M[i, i];
                //Preconditioner.Set(i, i, 1.0 / diag_value);
                Preconditioner.Set(i, i, diag_value);
            }

            need_solve_update = false;
        }



        // Result must be as large as Mesh.MaxVertexID
        public bool Solve(Vector3d[] Result)
        {
            UpdateForSolve();

            // use initial positions as initial solution. 
            Array.Copy(Px, Sx, N);
            Array.Copy(Py, Sy, N);
            Array.Copy(Pz, Sz, N);


            Action<double[], double[]> CombinedMultiply = (X, B) => {
                M.Multiply(X, B);
                for (int i = 0; i < N; ++i)
                    B[i] += WeightsM[i, i] * X[i];
            };

            SparseSymmetricCG SolverX = new SparseSymmetricCG() { B = Bx, X = Sx,
                MultiplyF = CombinedMultiply, PreconditionMultiplyF = Preconditioner.Multiply,
                UseXAsInitialGuess = true };
            SparseSymmetricCG SolverY = new SparseSymmetricCG() { B = By, X = Sy,
                MultiplyF = CombinedMultiply, PreconditionMultiplyF = Preconditioner.Multiply,
                UseXAsInitialGuess = true };
            SparseSymmetricCG SolverZ = new SparseSymmetricCG() { B = Bz, X = Sz,
                MultiplyF = CombinedMultiply, PreconditionMultiplyF = Preconditioner.Multiply,
                UseXAsInitialGuess = true };

            SparseSymmetricCG[] solvers = new SparseSymmetricCG[3] { SolverX, SolverY, SolverZ };
            bool[] ok = new bool[3];
            int[] indices = new int[3] { 0, 1, 2 };

            // preconditioned solve is slower =\
            //Action<int> SolveF = (i) => {  ok[i] = solvers[i].SolvePreconditioned(); };
            Action<int> SolveF = (i) => {  ok[i] = solvers[i].Solve(); };
            gParallel.ForEach(indices, SolveF);

            if (ok[0] == false || ok[1] == false || ok[2] == false)
                return false;

            for ( int i = 0; i < N; ++i ) {
                int vid = ToMeshV[i];
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



    }
}
