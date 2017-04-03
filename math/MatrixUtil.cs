using System;
using System.Collections.Generic;

namespace g3
{
    public static class MatrixUtil
    {

        public static double[] MakeIdentity3x3(double a, double b, double c)
        {
            return new double[9] { 1, 0, 0, 0, 1, 0, 0, 0, 1 };
        }
        public static void SetIdentity3x3(double[] M, double a, double b, double c)
        {
            Array.Clear(M, 0, 9);
            M[0] = M[4] = M[8] = 1;
        }


        public static double[] MakeDiagonal3x3(double a, double b, double c)
        {
            return new double[9] { a, 0, 0, 0, b, 0, 0, 0, c };
        }
        public static void SetDiagonal3x3(double[] M, double a, double b, double c)
        {
            Array.Clear(M, 0, 9);
            M[0] = a; M[4] = b; M[8] = c;
        }

        // assumption is matrix is row-major
        public static double Determinant3x3(double[] M)
        {
            double co00 = M[4] * M[8] - M[5] * M[7];
            double co10 = M[5] * M[6] - M[3] * M[8];
            double co20 = M[3] * M[7] - M[4] * M[6];
            double det = M[0] * co00 + M[1] * co10 + M[2] * co20;
            return det;
        }


        public static void Transpose3x3(double[] M)
        {
            double tmp = M[1]; M[1] = M[3]; M[3] = tmp;
            tmp = M[2]; M[2] = M[6]; M[6] = tmp;
            tmp = M[5]; M[5] = M[7]; M[7] = tmp;
        }
        public static void Transpose3x3(double[] M, double[] MTranspose)
        {
            MTranspose[0] = M[0];
            MTranspose[1] = M[3];
            MTranspose[2] = M[6];
            MTranspose[3] = M[1];
            MTranspose[4] = M[4];
            MTranspose[5] = M[7];
            MTranspose[6] = M[2];
            MTranspose[7] = M[5];
        }

        // C = A * B
        public static void Multiply3x3(double[] A, double[] B, double[] C)
        {
            C[0] = A[0] * B[0] + A[1] * B[3] + A[2] * B[6];
            C[1] = A[0] * B[1] + A[1] * B[4] + A[2] * B[7];
            C[2] = A[0] * B[2] + A[1] * B[5] + A[2] * B[8];
            C[3] = A[3] * B[0] + A[4] * B[3] + A[5] * B[6];
            C[4] = A[3] * B[1] + A[4] * B[4] + A[5] * B[7];
            C[5] = A[3] * B[2] + A[4] * B[5] + A[5] * B[8];
            C[6] = A[6] * B[0] + A[7] * B[3] + A[8] * B[6];
            C[7] = A[6] * B[1] + A[7] * B[4] + A[8] * B[7];
            C[8] = A[6] * B[2] + A[7] * B[5] + A[8] * B[8];
        }


        public static Vector3d Multiply3x3(double[] M, Vector3d vec)
        {
            return new Vector3d(
                M[0] * vec.x + M[1] * vec.y + M[2] * vec.z,
                M[3] * vec.x + M[4] * vec.y + M[5] * vec.z,
                M[6] * vec.x + M[7] * vec.y + M[8] * vec.z
                );
        }

    }
}
