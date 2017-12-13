using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace g3
{
    /// <summary>
    /// Fast Approximate SVD of 3x3 matrix that returns quaternions. 
    /// Implemented based on https://github.com/benjones/quatSVD/blob/master/quatSVD.hpp
    /// which was re-implemented from http://pages.cs.wisc.edu/~sifakis/project_pages/svd.html
    /// 
    /// By default, only does a small number of diagonalization iterations (4), which limits
    /// the accuracy of the solution. Results are still orthonormal but error when reconstructing
    /// matrix will be larger. This is fine for many applications. Can increase accuracy
    /// by increasing NumJacobiIterations parameter
    /// 
    /// Note: does *not* produce same quaternions as running SingularValueDecomposition on
    /// matrix and converting resulting U/V to quaternions. The numbers will be similar
    /// but the signs will be different
    /// 
    /// Useful properties:
    ///   - quaternions are rotations, there are no mirrors like in normal SVD
    /// </summary>
    public class FastQuaternionSVD
    {
        int NumJacobiIterations = 4;   // increase this to get higher accuracy
                                       // TODO: characterize...

        public Quaterniond U;
        public Quaterniond V;
        public Vector3d S;


        public FastQuaternionSVD(Matrix3d matrix, double epsilon = MathUtil.Epsilon, int jacobiIters = 4)
        {
            NumJacobiIterations = jacobiIters;

            SymmetricMatrix3d ATA = SymmetricMatrix3d.MakeATA(matrix);
            Vector4d v = jacobiDiagonalize(ATA);
            double[] AV = computeAV(matrix, ref v);

            Vector4d u = Vector4d.Zero;
            QRFactorize(AV, ref v, epsilon, ref this.S, ref u);

            //u,v are quaternions in (s, x, y, z) order
            U = new Quaterniond(u[1], u[2], u[3], u[0]);
            V = new Quaterniond(v[1], v[2], v[3], v[0]);
        }




        /// <summary>
        /// Compute U * S * V^T, useful for error-checking
        /// </summary>
        public Matrix3d ReconstructMatrix()
        {
            Matrix3d svdS = new Matrix3d(S[0], S[1], S[2]);
            return U.ToRotationMatrix() * svdS * V.Conjugate().ToRotationMatrix();
        }




        Vector4d jacobiDiagonalize(SymmetricMatrix3d ATA)
        {
            Vector4d V = new Vector4d(1, 0, 0, 0);

            for (int i = 0; i < NumJacobiIterations; ++i) {
                Vector2d givens = givensAngles(ATA, 0, 1);
                ATA.quatConjugate01(givens.x, givens.y);
                quatTimesEqualCoordinateAxis(ref V, givens.x, givens.y, 2);

                givens = givensAngles(ATA, 1, 2);
                ATA.quatConjugate12(givens.x, givens.y);
                quatTimesEqualCoordinateAxis(ref V, givens.x, givens.y, 0);

                givens = givensAngles(ATA, 0, 2);
                ATA.quatConjugate02(givens.x, givens.y);
                quatTimesEqualCoordinateAxis(ref V, givens.x, givens.y, 1);
            }

            return V;
        }



        Vector2d givensAngles(SymmetricMatrix3d B, int p, int q)
        {
            double ch = 0;
            if (p == 0 && q == 1) {
                ch = B.entries[0] - B.entries[q];
            } else if (p == 0 && q == 2) {
                ch = B.entries[q] - B.entries[p];
            } else if (p == 1 && q == 2) {
                ch = B.entries[p] - B.entries[q];
            }

            double sh = 0.5 * B[p, q];

            // [TODO] can use fast reciprocal square root here...
            double omega = 1.0 / Math.Sqrt(ch * ch + sh * sh);
            ch *= omega;
            sh *= omega;

            bool approxValid = (gamma * sh * sh) < (ch * ch);

            ch = approxValid ? ch : cosBackup;
            sh = approxValid ? sh : sinBackup;

            return new Vector2d(ch, sh);
        }








        double[] computeAV(Matrix3d matrix, ref Vector4d V)
        {
            Quaterniond qV = new Quaterniond(V[1], V[2], V[3], V[0]);
            Matrix3d MV = qV.ToRotationMatrix();
            Matrix3d AV = matrix * MV;
            return AV.ToBuffer();
        }











        void QRFactorize(double[] AV, ref Vector4d V, double eps, ref Vector3d S, ref Vector4d U)
        {
            permuteColumns(AV, ref V);

            U = new Vector4d(1, 0, 0, 0);

            Vector2d givens10 = computeGivensQR(AV, eps, 1, 0);
            givensQTB2(AV, givens10.x, givens10.y);
            quatTimesEqualCoordinateAxis(ref U, givens10.x, givens10.y, 2);

            Vector2d givens20 = computeGivensQR(AV, eps, 2, 0);
            givensQTB1(AV, givens20.x, -givens20.y);
            quatTimesEqualCoordinateAxis(ref U, givens20.x, -givens20.y, 1);

            Vector2d givens21 = computeGivensQR(AV, eps, 2, 1);
            givensQTB0(AV, givens21.x, givens21.y);
            quatTimesEqualCoordinateAxis(ref U, givens21.x, givens21.y, 0);

            S = new Vector3d(AV[0], AV[4], AV[8]);
            //return new KeyValuePair<Vector3d, double[]>(S, U);
        }



        //returns the 2 components of the quaternion
        //such that Q^T * B has a 0 in element p, q
        Vector2d computeGivensQR(double[] B, double eps, int r, int c)
        {
            double app = B[4 * c];
            double apq = B[3 * r + c];

            double rho = Math.Sqrt(app * app + apq * apq);
            double sh = rho > eps ? apq : 0;
            double ch = Math.Abs(app) + Math.Max(rho, eps);

            if (app < 0) {
                double tmp = sh; sh = ch; ch = tmp;
            }

            double omega = 1.0 / Math.Sqrt(ch * ch + sh * sh);
            ch *= omega;
            sh *= omega;

            return new Vector2d(ch, sh);
        }


        //Q is the rot matrix defined by quaternion (ch, . . . sh .. . ) where sh is coord i
        void givensQTB2(double[] B, double ch, double sh)
        {
            //quat is (ch, 0, 0, sh), rotation around Z axis
            double c = ch * ch - sh * sh;
            double s = 2 * sh * ch;
            //Q = [ c -s 0; s c 0; 0 0 1]

            double newb00 = B[0] * c + B[3] * s;
            double newb01 = B[1] * c + B[4] * s;
            double newb02 = B[2] * c + B[5] * s;

            double newb10 = 0;//B[3]*c - B[0]*s; //should be 0... maybe don't compute?
            double newb11 = B[4] * c - B[1] * s;
            double newb12 = B[5] * c - B[2] * s;

            B[0] = newb00;
            B[1] = newb01;
            B[2] = newb02;

            B[3] = newb10;
            B[4] = newb11;
            B[5] = newb12;
        }

        //This will be called after givensQTB<2>, so we know that
        //B10 is 0... which actually doesn't matter since that row won't change
        void givensQTB1(double[] B, double ch, double sh)
        {
            double c = ch * ch - sh * sh;
            double s = 2 * sh * ch;
            //Q = [c 0 s; 0 1 0; -s 0 c];
            double newb00 = B[0] * c - B[6] * s;
            double newb01 = B[1] * c - B[7] * s;
            double newb02 = B[2] * c - B[8] * s;

            double newb20 = 0;// B[0]*s + B[6]*c; //should be 0... maybe don't compute?
            double newb21 = B[1] * s + B[7] * c;
            double newb22 = B[2] * s + B[8] * c;

            B[0] = newb00;
            B[1] = newb01;
            B[2] = newb02;

            B[6] = newb20;
            B[7] = newb21;
            B[8] = newb22;
        }

        //B10 and B20 are 0, so don't bother filling in/computing them :)
        void givensQTB0(double[] B, double ch, double sh)
        {
            double c = ch * ch - sh * sh;
            double s = 2 * ch * sh;

            /* we may not need to compute the off diags since B should be diagonal
               after this step */
            double newb11 = B[4] * c + B[7] * s;
            //double newb12 = B[5]*c + B[8]*s; 

            //double newb21 = B[7]*c - B[4]*s;
            double newb22 = B[8] * c - B[5] * s;

            B[4] = newb11;
            //B[5] = newb12;

            //B[7] = newb21;
            B[8] = newb22;
        }



        void quatTimesEqualCoordinateAxis(ref Vector4d lhs, double c, double s, int i)
        {
            //the quat we're multiplying by is (c, ? s ?)  where s is in slot i of the vector part,
            //and the other entries are 0
            double newS = lhs.x * c - lhs[i + 1] * s;

            Vector3d newVals = Vector3d.Zero;
            //the s2*v1 part
            newVals.x = c * lhs.y;
            newVals.y = c * lhs.z;
            newVals.z = c * lhs.w;
            //the s1*v2 part
            newVals[i] += lhs.x * s;
            //the cross product part
            newVals[(i + 1) % 3] += s * lhs[1 + ((i + 2) % 3)];
            newVals[(i + 2) % 3] -= s * lhs[1 + ((i + 1) % 3)];

            lhs.x = newS;
            lhs.y = newVals.x;
            lhs.z = newVals.y;
            lhs.w = newVals.z;
        }


        const double gamma = 3.0 + 2.0 * MathUtil.SqrtTwo;
        const double sinBackup = 0.38268343236508973; //0.5 * Math.Sqrt(2.0 - MathUtil.SqrtTwo);
        const double cosBackup = 0.92387953251128674; //0.5 * Math.Sqrt(2.0 + MathUtil.SqrtTwo);

        void permuteColumns(double[] B, ref Vector4d V)
        {
            double magx = B[0] * B[0] + B[3] * B[3] + B[6] * B[6];
            double magy = B[1] * B[1] + B[4] * B[4] + B[7] * B[7];
            double magz = B[2] * B[2] + B[5] * B[5] + B[8] * B[8];

            if (magx < magy) {
                swapColsNeg(B, 0, 1);
                quatTimesEqualCoordinateAxis(ref V, MathUtil.SqrtTwoInv, MathUtil.SqrtTwoInv, 2);
                double tmp = magx; magx = magy; magy = tmp;
            }

            if (magx < magz) {
                swapColsNeg(B, 0, 2);
                quatTimesEqualCoordinateAxis(ref V, MathUtil.SqrtTwoInv, -MathUtil.SqrtTwoInv, 1 );
                double tmp = magx; magx = magz; magz = tmp;
            }

            if (magy < magz) {
                swapColsNeg(B, 1, 2);
                quatTimesEqualCoordinateAxis(ref V, MathUtil.SqrtTwoInv, MathUtil.SqrtTwoInv, 0);
            }

        }



        void swapColsNeg(double[] B, int i, int j) {
            double tmp = -B[i];
            B[i] = B[j];
            B[j] = tmp;

            tmp = -B[i + 3];
            B[i + 3] = B[j + 3];
            B[j + 3] = tmp;

            tmp = -B[i + 6];
            B[i + 6] = B[j + 6];
            B[j + 6] = tmp;
        }


    }



    /// <summary>
    /// Simple 3x3 symmetric-matrix class. The 6 values are stored
    /// as [diag_00, diag_11, diag_22, upper_01, upper_02, upper_12]
    /// 
    /// This is a helper class for FastQuaternionSVD, currently not public
    /// </summary>
    class SymmetricMatrix3d
    {
        public double[] entries = new double[6];

        public SymmetricMatrix3d()
        {
        }

        public static SymmetricMatrix3d MakeATA(Matrix3d A)
        {
            SymmetricMatrix3d M = new SymmetricMatrix3d();
            Vector3d c0 = A.Column(0), c1 = A.Column(1), c2 = A.Column(2);
            M.entries[0] = c0.LengthSquared;
            M.entries[1] = c1.LengthSquared;
            M.entries[2] = c2.LengthSquared;
            M.entries[3] = c0.Dot(c1);
            M.entries[4] = c0.Dot(c2);
            M.entries[5] = c1.Dot(c2);
            return M;
        }


        public double this[int r, int c] {
            get {
                Debug.Assert(r <= c);
                if (r == c) { return entries[r]; } 
                else if (r == 0) { return entries[2 + c]; } 
                else return entries[5];
            }
            set {
                Debug.Assert(r <= c);
                if (r == c) { entries[r] = value; } 
                else if (r == 0) { entries[2 + c] = value; } 
                else entries[5] = value;
            }
        }


        /*
         * These functions compute Q^T * S * Q
         * where Q is represented as the quaterion with c as the scalar and s in the slot that's not p or q
         */

/*      // unused
        public void quatConjugateFull(double[] q){
	        //assume q is unit
	  
	        double a = 1 - 2*(q[2]*q[2] + q[3]*q[3]);
	        double b = 2*(q[1]*q[2] - q[3]*q[0]);
	        double c = 2*(q[1]*q[3] + q[2]*q[0]);

	        double d = 2*(q[1]*q[2] + q[3]*q[0]);
	        double e = 1 - 2*(q[1]*q[1] + q[3]*q[3]);
	        double f = 2*(q[2]*q[3] - q[1]*q[0]);

	        double g = 2*(q[1]*q[3] - q[2]*q[0]);
	        double h = 2*(q[2]*q[3] + q[1]*q[0]);
	        double i = 1 - 2*(q[1]*q[1] + q[2]*q[2]);

	        //B = Q^T S
	        double B11 = a*entries[0] + d*entries[3] + g*entries[4];
	        double B12 = a*entries[3] + d*entries[1] + g*entries[5];
	        double B13 = a*entries[4] + d*entries[5] + g*entries[2];
	  
	        double B21 = b*entries[0] + e*entries[3] + h*entries[4];
	        double B22 = b*entries[3] + e*entries[1] + h*entries[5];
	        double B23 = b*entries[4] + e*entries[5] + h*entries[2];
	  
	        double B31 = c*entries[0] + f*entries[3] + i*entries[4];
	        double B32 = c*entries[3] + f*entries[1] + i*entries[5];
	        double B33 = c*entries[4] + f*entries[5] + i*entries[2];

	        double new11 = a*B11 + d*B12 + g*B13;
	        double new22 = b*B21 + e*B22 + h*B23;
	        double new33 = c*B31 + f*B32 + i*B33;
	  
	        double new12 = a*B21 + d*B22 + g*B23;
	        double new13 = a*B31 + d*B32 + g*B33;
		
	        double new23 = b*B31 + e*B32 + h*B33;

	        entries[0] = new11;
	        entries[1] = new22;
	        entries[2] = new33;
	        entries[3] = new12;
	        entries[4] = new13;
	        entries[5] = new23;
  	    }
*/



        public void quatConjugate01(double c, double s){
            //rotatoin around z axis
            double realC = c*c - s*s;
	        double realS = 2*s*c;
	
	        double cs = realS*realC;
	        double cc = realC*realC;
	        double ss = realS*realS;

	        double newS11 = cc * entries[0] + 2*cs*entries[3] + ss*entries[1];
	        double newS22 = ss * entries[0] - 2*cs*entries[3] + cc*entries[1];
	        double newS12 = entries[3]*(cc - ss) + cs*( entries[1] - entries[0] );
	        double newS13 = realC*entries[4] + realS*entries[5];
	        double newS23 = realC*entries[5] - realS*entries[4];
	  
	        entries[0] = newS11;
	        entries[1] = newS22;
	        entries[3] = newS12;
	        entries[4] = newS13;
	        entries[5] = newS23;
        }


        public void quatConjugate02(double c, double s){
            //rotation around y axis
            //quat looks like (ch, 0, sh, 0)
            double realC = c*c - s*s;
	        double realS = 2*s*c;
	
	        double cs = realS*realC;
	        double cc = realC*realC;
	        double ss = realS*realS;

	        double newS11 = cc*entries[0] - 2*cs*entries[4] + ss*entries[2];
	        double newS33 = ss*entries[0] + 2*cs*entries[4] + cc*entries[2];
	        double newS12 = realC*entries[3] - realS*entries[5];
	        double newS13 = cs*(entries[0] - entries[2])  + (cc - ss)*entries[4];
	        double newS23 = realS*entries[3] + realC*entries[5];
	  
	        entries[0] = newS11;
	        entries[2] = newS33;
	        entries[3] = newS12;
	        entries[4] = newS13;
	        entries[5] = newS23;
        }
  

  
	    public void quatConjugate12(double c, double s){
	        //rotation around x axis
	        //quat looks like (ch, sh, 0, 0)
	        double realC = c*c - s*s;	
	        double realS = 2*s*c;
	
	        double cs = realS*realC;
	        double cc = realC*realC;
	        double ss = realS*realS;
	        double newS22 = cc*entries[1] + 2*cs*entries[5] + ss*entries[2];
	        double newS33 = ss*entries[1] - 2*cs*entries[5] + cc*entries[2];
	        double newS12 = realC*entries[3] + realS*entries[4];
	        double newS13 = -realS*entries[3] + realC*entries[4];
	        double newS23 = (cc - ss)*entries[5] + cs*(entries[2] - entries[1]);
	
	        entries[1] = newS22;
	        entries[2] = newS33;
	        entries[3] = newS12;
	        entries[4] = newS13;
	        entries[5] = newS23;
        }



    }

}
