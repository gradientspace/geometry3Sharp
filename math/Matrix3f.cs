using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    public struct Matrix3f
    {
        public Vector3f Row0;
        public Vector3f Row1;
        public Vector3f Row2;

        public Matrix3f(bool bIdentity) {
            if (bIdentity) {
                Row0 = Vector3f.AxisX; Row1 = Vector3f.AxisY; Row2 = Vector3f.AxisZ;
            } else {
                Row0 = Row1 = Row2 = Vector3f.Zero;
            }
        }

        // assumes input is row-major...
        public Matrix3f(float[,] mat) {
            Row0 = new Vector3f(mat[0, 0], mat[0, 1], mat[0, 2]);
            Row1 = new Vector3f(mat[1, 0], mat[1, 1], mat[1, 2]);
            Row2 = new Vector3f(mat[2, 0], mat[2, 1], mat[2, 2]);
        }
        public Matrix3f(float[] mat) {
            Row0 = new Vector3f(mat[0], mat[1], mat[2]);
            Row1 = new Vector3f(mat[3], mat[4], mat[5]);
            Row2 = new Vector3f(mat[6], mat[7], mat[8]);
        }
        public Matrix3f(double[,] mat) {
            Row0 = new Vector3f(mat[0, 0], mat[0, 1], mat[0, 2]);
            Row1 = new Vector3f(mat[1, 0], mat[1, 1], mat[1, 2]);
            Row2 = new Vector3f(mat[2, 0], mat[2, 1], mat[2, 2]);
        }
        public Matrix3f(double[] mat) {
            Row0 = new Vector3f(mat[0], mat[1], mat[2]);
            Row1 = new Vector3f(mat[3], mat[4], mat[5]);
            Row2 = new Vector3f(mat[6], mat[7], mat[8]);
        }
        public Matrix3f(Func<int,float> matBufferF)
        {
            Row0 = new Vector3f(matBufferF(0), matBufferF(1), matBufferF(2));
            Row1 = new Vector3f(matBufferF(3), matBufferF(4), matBufferF(5));
            Row2 = new Vector3f(matBufferF(6), matBufferF(7), matBufferF(8));
        }
        public Matrix3f(Func<int, int, float> matF)
        {
            Row0 = new Vector3f(matF(0,0), matF(0,1), matF(0,2));
            Row1 = new Vector3f(matF(1,0), matF(1,1), matF(1,2));
            Row2 = new Vector3f(matF(2,0), matF(1,2), matF(2,2));
        }
        public Matrix3f(float m00, float m11, float m22)
        {
            Row0 = new Vector3f(m00, 0, 0);
            Row1 = new Vector3f(0, m11, 0);
            Row2 = new Vector3f(0, 0, m22);
        }
        public Matrix3f(Vector3f v1, Vector3f v2, Vector3f v3, bool bRows)
        {
            if (bRows) {
                Row0 = v1; Row1 = v2; Row2 = v3;
            } else {
                Row0 = new Vector3f(v1.x, v2.x, v3.x);
                Row1 = new Vector3f(v1.y, v2.y, v3.y);
                Row2 = new Vector3f(v1.z, v2.z, v3.z);
            }
        }
		public Matrix3f(float m00, float m01, float m02, float m10, float m11, float m12, float m20, float m21, float m22) {
            Row0 = new Vector3f(m00, m01, m02);
            Row1 = new Vector3f(m10, m11, m12);
            Row2 = new Vector3f(m20, m21, m22);
        }


        public static readonly Matrix3f Identity = new Matrix3f(true);
        public static readonly Matrix3f Zero = new Matrix3f(false);



        public float this[int r, int c] {
            get {
                return (r == 0) ? Row0[c] : ( (r == 1) ? Row1[c] : Row2[c] );
            }
            set {
                if (r == 0)         Row0[c] = value;
                else if (r == 1)    Row1[c] = value;
                else                Row2[c] = value;
            }
        }


        public float this[int i] {
            get {
                return (i > 5) ? Row2[i%3] : ((i > 2) ? Row1[i%3] : Row0[i%3]);
            }
            set {
                if (i > 5)         Row2[i%3] = value;
                else if (i > 2)    Row1[i%3] = value;
                else               Row0[i%3] = value;
            }
        }



        public Vector3f Row(int i) {
            return (i == 0) ? Row0 : (i == 1) ? Row1 : Row2;
        }
        public Vector3f Column(int i) {
            if (i == 0) return new Vector3f(Row0.x, Row1.x, Row2.x);
            else if ( i==1) return new Vector3f(Row0.y, Row1.y, Row2.y);
            else return new Vector3f(Row0.z, Row1.z, Row2.z);
        }


        public float[] ToBuffer() {
            return new float[9] {
                Row0.x, Row0.y, Row0.z,
                Row1.x, Row1.y, Row1.z,
                Row2.x, Row2.y, Row2.z };
        }
        public void ToBuffer(float[] buf) {
            buf[0] = Row0.x; buf[1] = Row0.y; buf[2] = Row0.z;
            buf[3] = Row1.x; buf[4] = Row1.y; buf[5] = Row1.z;
            buf[6] = Row2.x; buf[7] = Row2.y; buf[8] = Row2.z;
        }




        public static Matrix3f operator *(Matrix3f mat, float f) {
            return new Matrix3f(
                mat.Row0.x * f, mat.Row0.y * f, mat.Row0.z * f,
                mat.Row1.x * f, mat.Row1.y * f, mat.Row1.z * f,
                mat.Row2.x * f, mat.Row2.y * f, mat.Row2.z * f);
        }
        public static Matrix3f operator *(float f, Matrix3f mat) {
            return new Matrix3f(
                mat.Row0.x * f, mat.Row0.y * f, mat.Row0.z * f,
                mat.Row1.x * f, mat.Row1.y * f, mat.Row1.z * f,
                mat.Row2.x * f, mat.Row2.y * f, mat.Row2.z * f);
        }


        public static Vector3f operator *(Matrix3f mat, Vector3f v) {
            return new Vector3f(
                mat.Row0.x * v.x + mat.Row0.y * v.y + mat.Row0.z * v.z,
                mat.Row1.x * v.x + mat.Row1.y * v.y + mat.Row1.z * v.z,
                mat.Row2.x * v.x + mat.Row2.y * v.y + mat.Row2.z * v.z);
        }

        public Vector3f Multiply(ref Vector3f v) {
            return new Vector3f(
                Row0.x * v.x + Row0.y * v.y + Row0.z * v.z,
                Row1.x * v.x + Row1.y * v.y + Row1.z * v.z,
                Row2.x * v.x + Row2.y * v.y + Row2.z * v.z);
        }

        public void Multiply(ref Vector3f v, ref Vector3f vOut) {
            vOut.x = Row0.x * v.x + Row0.y * v.y + Row0.z * v.z;
            vOut.y = Row1.x * v.x + Row1.y * v.y + Row1.z * v.z;
            vOut.z = Row2.x * v.x + Row2.y * v.y + Row2.z * v.z;
        }

		public static Matrix3f operator *(Matrix3f mat1, Matrix3f mat2)
		{
            float m00 = mat1.Row0.x * mat2.Row0.x + mat1.Row0.y * mat2.Row1.x + mat1.Row0.z * mat2.Row2.x;
            float m01 = mat1.Row0.x * mat2.Row0.y + mat1.Row0.y * mat2.Row1.y + mat1.Row0.z * mat2.Row2.y;
            float m02 = mat1.Row0.x * mat2.Row0.z + mat1.Row0.y * mat2.Row1.z + mat1.Row0.z * mat2.Row2.z;

            float m10 = mat1.Row1.x * mat2.Row0.x + mat1.Row1.y * mat2.Row1.x + mat1.Row1.z * mat2.Row2.x;
            float m11 = mat1.Row1.x * mat2.Row0.y + mat1.Row1.y * mat2.Row1.y + mat1.Row1.z * mat2.Row2.y;
            float m12 = mat1.Row1.x * mat2.Row0.z + mat1.Row1.y * mat2.Row1.z + mat1.Row1.z * mat2.Row2.z;

            float m20 = mat1.Row2.x * mat2.Row0.x + mat1.Row2.y * mat2.Row1.x + mat1.Row2.z * mat2.Row2.x;
            float m21 = mat1.Row2.x * mat2.Row0.y + mat1.Row2.y * mat2.Row1.y + mat1.Row2.z * mat2.Row2.y;
            float m22 = mat1.Row2.x * mat2.Row0.z + mat1.Row2.y * mat2.Row1.z + mat1.Row2.z * mat2.Row2.z;

			return new Matrix3f(m00, m01, m02, m10, m11, m12, m20, m21, m22);
		}



        public static Matrix3f operator +(Matrix3f mat1, Matrix3f mat2) {
            return new Matrix3f(mat1.Row0 + mat2.Row0, mat1.Row1 + mat2.Row1, mat1.Row2 + mat2.Row2, true);
        }
        public static Matrix3f operator -(Matrix3f mat1, Matrix3f mat2) {
            return new Matrix3f(mat1.Row0 - mat2.Row0, mat1.Row1 - mat2.Row1, mat1.Row2 - mat2.Row2, true);
        }


        public float Determinant {
			get {
				float a11 = Row0.x, a12 = Row0.y, a13 = Row0.z, a21 = Row1.x, a22 = Row1.y, a23 = Row1.z, a31 = Row2.x, a32 = Row2.y, a33 = Row2.z;
                float i00 = a33 * a22 - a32 * a23;
                float i01 = -(a33 * a12 - a32 * a13);
                float i02 = a23 * a12 - a22 * a13;
				return a11 * i00 + a21 * i01 + a31 * i02;
			}
		}


		public Matrix3f Inverse() {
            float a11 = Row0.x, a12 = Row0.y, a13 = Row0.z, a21 = Row1.x, a22 = Row1.y, a23 = Row1.z, a31 = Row2.x, a32 = Row2.y, a33 = Row2.z;
            float i00 = a33 * a22 - a32 * a23;
            float i01 = -(a33 * a12 - a32 * a13);
            float i02 = a23 * a12 - a22 * a13;

            float i10 = -(a33 * a21 - a31 * a23);
            float i11 = a33*a11 - a31*a13;
            float i12 = -(a23*a11 - a21*a13);

            float i20 = a32*a21 - a31*a22;
            float i21 = -(a32*a11 - a31*a12);
            float i22 = a22*a11 - a21*a12;

            float det = a11 * i00 + a21 * i01 + a31 * i02;
			if (Math.Abs(det) < float.Epsilon)
				throw new Exception("Matrix3f.Inverse: matrix is not invertible");
			det = 1.0f / det;
			return new Matrix3f(i00 * det, i01 * det, i02 * det, i10 * det, i11 * det, i12 * det, i20 * det, i21 * det, i22 * det);
		}

        public Matrix3f Transpose()
        {
            return new Matrix3f(
                Row0.x, Row1.x, Row2.x,
                Row0.y, Row1.y, Row2.y,
                Row0.z, Row1.z, Row2.z);
        }

        public Quaternionf ToQuaternion() {
            return new Quaternionf(this);
        }





        public bool EpsilonEqual(Matrix3f m2, float epsilon)
        {
            return Row0.EpsilonEqual(m2.Row0, epsilon) &&
                Row1.EpsilonEqual(m2.Row1, epsilon) &&
                Row2.EpsilonEqual(m2.Row2, epsilon);
        }




        public static Matrix3f AxisAngleD(Vector3f axis, float angleDeg)
        {
            double angle = angleDeg * MathUtil.Deg2Rad;
            float cs = (float)Math.Cos(angle);
            float sn = (float)Math.Sin(angle);
            float oneMinusCos = 1.0f - cs;
            float x2 = axis[0] * axis[0];
            float y2 = axis[1] * axis[1];
            float z2 = axis[2] * axis[2];
            float xym = axis[0] * axis[1] * oneMinusCos;
            float xzm = axis[0] * axis[2] * oneMinusCos;
            float yzm = axis[1] * axis[2] * oneMinusCos;
            float xSin = axis[0] * sn;
            float ySin = axis[1] * sn;
            float zSin = axis[2] * sn;
            return new Matrix3f(
                x2 * oneMinusCos + cs, xym - zSin, xzm + ySin,
                xym + zSin, y2 * oneMinusCos + cs, yzm - xSin,
                xzm - ySin, yzm + xSin, z2 * oneMinusCos + cs);
        }




        public override string ToString() {
            return string.Format("[{0}] [{1}] [{2}]", Row0, Row1, Row2);
        }
        public string ToString(string fmt) {
            return string.Format("[{0}] [{1}] [{2}]", Row0.ToString(fmt), Row1.ToString(fmt), Row2.ToString(fmt));
        }
    }
}
