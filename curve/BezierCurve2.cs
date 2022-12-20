using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    /// <summary>
    /// 2D Bezier curve of arbitrary degree
    /// Ported from WildMagic5 Wm5BezierCurve2
    /// </summary>
    public class BezierCurve2 : BaseCurve2, IParametricCurve2d
    {
        int mDegree;
        int mNumCtrlPoints;
        Vector2d[] mCtrlPoint;
        Vector2d[] mDer1CtrlPoint;
        Vector2d[] mDer2CtrlPoint;
        Vector2d[] mDer3CtrlPoint;
        DenseMatrix mChoose;


        public int Degree { get { return mDegree; } }
        public Vector2d[] ControlPoints { get { return mCtrlPoint; } }


        public BezierCurve2(int degree, Vector2d[] ctrlPoint, bool bTakeOwnership = false) : base(0, 1)
        {
            if ( degree < 2 )
                throw new Exception("BezierCurve2() The degree must be three or larger\n");

            int i, j;

            mDegree = degree;
            mNumCtrlPoints = mDegree + 1;
            if (bTakeOwnership) {
                mCtrlPoint = ctrlPoint;
            } else {
                mCtrlPoint = new Vector2d[ctrlPoint.Length];
                Array.Copy(ctrlPoint, mCtrlPoint, ctrlPoint.Length);
            }

            // Compute first-order differences.
            mDer1CtrlPoint = new Vector2d[mNumCtrlPoints - 1];
            for (i = 0; i < mNumCtrlPoints - 1; ++i) {
                mDer1CtrlPoint[i] = mCtrlPoint[i + 1] - mCtrlPoint[i];
            }

            // Compute second-order differences.
            mDer2CtrlPoint = new Vector2d[mNumCtrlPoints - 2];
            for (i = 0; i < mNumCtrlPoints - 2; ++i) {
                mDer2CtrlPoint[i] = mDer1CtrlPoint[i + 1] - mDer1CtrlPoint[i];
            }

            // Compute third-order differences.
            if (degree >= 3) {
                mDer3CtrlPoint = new Vector2d[mNumCtrlPoints - 3];
                for (i = 0; i < mNumCtrlPoints - 3; ++i) {
                    mDer3CtrlPoint[i] = mDer2CtrlPoint[i + 1] - mDer2CtrlPoint[i];
                }
            } else {
                mDer3CtrlPoint = null;
            }

            // Compute combinatorial values Choose(N,K), store in mChoose[N,K].
            // The values mChoose[r,c] are invalid for r < c (use only the
            // entries for r >= c).
            mChoose = new DenseMatrix(mNumCtrlPoints, mNumCtrlPoints);

            mChoose[0,0] = 1.0;
            mChoose[1,0] = 1.0;
            mChoose[1,1] = 1.0;
            for (i = 2; i <= mDegree; ++i) {
                mChoose[i,0] = 1.0;
                mChoose[i,i] = 1.0;
                for (j = 1; j < i; ++j) {
                    mChoose[i,j] = mChoose[i - 1,j - 1] + mChoose[i - 1,j];
                }
            }
        }


        // used in Clone()
        protected BezierCurve2() : base(0, 1)
        {
        }


        public override Vector2d GetPosition(double t)
        {
            double oneMinusT = 1 - t;
            double powT = t;
            Vector2d result = oneMinusT * mCtrlPoint[0];

            for (int i = 1; i < mDegree; ++i) {
                double coeff = mChoose[mDegree,i] * powT;
                result = (result + mCtrlPoint[i] * coeff) * oneMinusT;
                powT *= t;
            }

            result += mCtrlPoint[mDegree] * powT;

            return result;
        }


        public override Vector2d GetFirstDerivative(double t)
        {
            double oneMinusT = 1 - t;
            double powT = t;
            Vector2d result = oneMinusT * mDer1CtrlPoint[0];

            int degreeM1 = mDegree - 1;
            for (int i = 1; i < degreeM1; ++i) {
                double coeff = mChoose[degreeM1,i] * powT;
                result = (result + mDer1CtrlPoint[i] * coeff) * oneMinusT;
                powT *= t;
            }

            result += mDer1CtrlPoint[degreeM1] * powT;
            result *= (double)mDegree;

            return result;
        }


        public override Vector2d GetSecondDerivative(double t)
        {
            double oneMinusT = 1 - t;
            double powT = t;
            Vector2d result = oneMinusT * mDer2CtrlPoint[0];

            int degreeM2 = mDegree - 2;
            for (int i = 1; i < degreeM2; ++i) {
                double coeff = mChoose[degreeM2,i] * powT;
                result = (result + mDer2CtrlPoint[i] * coeff) * oneMinusT;
                powT *= t;
            }

            result += mDer2CtrlPoint[degreeM2] * powT;
            result *= (double)(mDegree * (mDegree - 1));

            return result;
        }


        public override Vector2d GetThirdDerivative(double t)
        {
            if (mDegree < 3) {
                return Vector2d.Zero;
            }

            double oneMinusT = 1 - t;
            double powT = t;
            Vector2d result = oneMinusT * mDer3CtrlPoint[0];

            int degreeM3 = mDegree - 3;
            for (int i = 1; i < degreeM3; ++i) {
                double coeff = mChoose[degreeM3,i] * powT;
                result = (result + mDer3CtrlPoint[i] * coeff) * oneMinusT;
                powT *= t;
            }

            result += mDer3CtrlPoint[degreeM3] * powT;
            result *= (double)(mDegree * (mDegree - 1) * (mDegree - 2));

            return result;
        }



        /*
         * IParametricCurve2d implementation
         */

        // TODO: could support closed bezier?
        public bool IsClosed {
            get { return false; }
        }

        // can call SampleT in range [0,ParamLength]
        public double ParamLength {
            get { return mTMax - mTMin; }
        }
        public Vector2d SampleT(double t)
        {
            return GetPosition(t);
        }

        public Vector2d TangentT(double t)
        {
            return GetFirstDerivative(t).Normalized;
        }

        public bool HasArcLength {
            get { return true; }
        }
        public double ArcLength {
            get { return GetTotalLength(); }
        }
        public Vector2d SampleArcLength(double a)
        {
            double t = GetTime(a);
            return GetPosition(t);
        }

        public void Reverse()
        {
            throw new NotSupportedException("NURBSCurve2.Reverse: how to reverse?!?");
        }

        public IParametricCurve2d Clone()
        {
            BezierCurve2 c2 = new BezierCurve2();
            c2.mDegree = this.mDegree;
            c2.mNumCtrlPoints = this.mNumCtrlPoints;

            c2.mCtrlPoint = (Vector2d[])this.mCtrlPoint.Clone();
            c2.mDer1CtrlPoint = (Vector2d[])this.mDer1CtrlPoint.Clone();
            c2.mDer2CtrlPoint = (Vector2d[])this.mDer2CtrlPoint.Clone();
            c2.mDer3CtrlPoint = (Vector2d[])this.mDer3CtrlPoint.Clone();
            c2.mChoose = new DenseMatrix(this.mChoose);
            return c2;
        }


        public bool IsTransformable { get { return true; } }
        public void Transform(ITransform2 xform)
        {
            for (int k = 0; k < mCtrlPoint.Length; ++k)
                mCtrlPoint[k] = xform.TransformP(mCtrlPoint[k]);

            // update derivatives
            for (int i = 0; i < mNumCtrlPoints - 1; ++i) 
                mDer1CtrlPoint[i] = mCtrlPoint[i+1] - mCtrlPoint[i];
            for (int i = 0; i < mNumCtrlPoints - 2; ++i) 
                mDer2CtrlPoint[i] = mDer1CtrlPoint[i+1] - mDer1CtrlPoint[i];
            if (mDegree >= 3) {
                for (int i = 0; i < mNumCtrlPoints - 3; ++i)
                    mDer3CtrlPoint[i] = mDer2CtrlPoint[i + 1] - mDer2CtrlPoint[i];
            }
        }


    }
}
