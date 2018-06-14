using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    public struct Matrix3d
    {
        public Vector3d Row0;
        public Vector3d Row1;
        public Vector3d Row2;

        public Matrix3d(bool bIdentity) {
            if (bIdentity) {
                Row0 = Vector3d.AxisX; Row1 = Vector3d.AxisY; Row2 = Vector3d.AxisZ;
            } else {
                Row0 = Row1 = Row2 = Vector3d.Zero;
            }
        }

        // assumes input is row-major...
        public Matrix3d(float[,] mat) {
            Row0 = new Vector3d(mat[0, 0], mat[0, 1], mat[0, 2]);
            Row1 = new Vector3d(mat[1, 0], mat[1, 1], mat[1, 2]);
            Row2 = new Vector3d(mat[2, 0], mat[2, 1], mat[2, 2]);
        }
        public Matrix3d(float[] mat) {
            Row0 = new Vector3d(mat[0], mat[1], mat[2]);
            Row1 = new Vector3d(mat[3], mat[4], mat[5]);
            Row2 = new Vector3d(mat[6], mat[7], mat[8]);
        }
        public Matrix3d(double[,] mat) {
            Row0 = new Vector3d(mat[0, 0], mat[0, 1], mat[0, 2]);
            Row1 = new Vector3d(mat[1, 0], mat[1, 1], mat[1, 2]);
            Row2 = new Vector3d(mat[2, 0], mat[2, 1], mat[2, 2]);
        }
        public Matrix3d(double[] mat) {
            Row0 = new Vector3d(mat[0], mat[1], mat[2]);
            Row1 = new Vector3d(mat[3], mat[4], mat[5]);
            Row2 = new Vector3d(mat[6], mat[7], mat[8]);
        }
        public Matrix3d(Func<int,double> matBufferF)
        {
            Row0 = new Vector3d(matBufferF(0), matBufferF(1), matBufferF(2));
            Row1 = new Vector3d(matBufferF(3), matBufferF(4), matBufferF(5));
            Row2 = new Vector3d(matBufferF(6), matBufferF(7), matBufferF(8));
        }
        public Matrix3d(Func<int, int, double> matF)
        {
            Row0 = new Vector3d(matF(0,0), matF(0,1), matF(0,2));
            Row1 = new Vector3d(matF(1,0), matF(1,1), matF(1,2));
            Row2 = new Vector3d(matF(2,0), matF(1,2), matF(2,2));
        }
        public Matrix3d(double m00, double m11, double m22)
        {
            Row0 = new Vector3d(m00, 0, 0);
            Row1 = new Vector3d(0, m11, 0);
            Row2 = new Vector3d(0, 0, m22);
        }
        public Matrix3d(Vector3d v1, Vector3d v2, Vector3d v3, bool bRows)
        {
            if (bRows) {
                Row0 = v1; Row1 = v2; Row2 = v3;
            } else {
                Row0 = new Vector3d(v1.x, v2.x, v3.x);
                Row1 = new Vector3d(v1.y, v2.y, v3.y);
                Row2 = new Vector3d(v1.z, v2.z, v3.z);
            }
        }
        public Matrix3d(ref Vector3d v1, ref Vector3d v2, ref Vector3d v3, bool bRows)
        {
            if (bRows) {
                Row0 = v1; Row1 = v2; Row2 = v3;
            } else {
                Row0 = new Vector3d(v1.x, v2.x, v3.x);
                Row1 = new Vector3d(v1.y, v2.y, v3.y);
                Row2 = new Vector3d(v1.z, v2.z, v3.z);
            }
        }
        public Matrix3d(double m00, double m01, double m02, double m10, double m11, double m12, double m20, double m21, double m22) {
            Row0 = new Vector3d(m00, m01, m02);
            Row1 = new Vector3d(m10, m11, m12);
            Row2 = new Vector3d(m20, m21, m22);
        }


        /// <summary>
        /// Construct outer-product of u*transpose(v) of u and v
        /// result is that Mij = u_i * v_j
        /// </summary>
        public Matrix3d(ref Vector3d u, ref Vector3d v)
        {
            Row0 = new Vector3d(u.x*v.x, u.x*v.y, u.x*v.z);
            Row1 = new Vector3d(u.y*v.x, u.y*v.y, u.y*v.z);
            Row2 = new Vector3d(u.z*v.x, u.z*v.y, u.z*v.z);
        }


        public static readonly Matrix3d Identity = new Matrix3d(true);
        public static readonly Matrix3d Zero = new Matrix3d(false);



        public double this[int r, int c] {
            get {
                return (r == 0) ? Row0[c] : ( (r == 1) ? Row1[c] : Row2[c] );
            }
            set {
                if (r == 0)         Row0[c] = value;
                else if (r == 1)    Row1[c] = value;
                else                Row2[c] = value;
            }
        }


        public double this[int i] {
            get {
                return (i > 5) ? Row2[i%3] : ((i > 2) ? Row1[i%3] : Row0[i%3]);
            }
            set {
                if (i > 5)         Row2[i%3] = value;
                else if (i > 2)    Row1[i%3] = value;
                else               Row0[i%3] = value;
            }
        }



        public Vector3d Row(int i) {
            return (i == 0) ? Row0 : (i == 1) ? Row1 : Row2;
        }
        public Vector3d Column(int i) {
            if (i == 0) return new Vector3d(Row0.x, Row1.x, Row2.x);
            else if ( i==1) return new Vector3d(Row0.y, Row1.y, Row2.y);
            else return new Vector3d(Row0.z, Row1.z, Row2.z);
        }


        public double[] ToBuffer() {
            return new double[9] {
                Row0.x, Row0.y, Row0.z,
                Row1.x, Row1.y, Row1.z,
                Row2.x, Row2.y, Row2.z };
        }
        public void ToBuffer(double[] buf) {
            buf[0] = Row0.x; buf[1] = Row0.y; buf[2] = Row0.z;
            buf[3] = Row1.x; buf[4] = Row1.y; buf[5] = Row1.z;
            buf[6] = Row2.x; buf[7] = Row2.y; buf[8] = Row2.z;
        }




        public static Matrix3d operator *(Matrix3d mat, double f) {
            return new Matrix3d(
                mat.Row0.x * f, mat.Row0.y * f, mat.Row0.z * f,
                mat.Row1.x * f, mat.Row1.y * f, mat.Row1.z * f,
                mat.Row2.x * f, mat.Row2.y * f, mat.Row2.z * f);
        }
        public static Matrix3d operator *(double f, Matrix3d mat) {
            return new Matrix3d(
                mat.Row0.x * f, mat.Row0.y * f, mat.Row0.z * f,
                mat.Row1.x * f, mat.Row1.y * f, mat.Row1.z * f,
                mat.Row2.x * f, mat.Row2.y * f, mat.Row2.z * f);
        }


        public static Vector3d operator *(Matrix3d mat, Vector3d v) {
            return new Vector3d(
                mat.Row0.x * v.x + mat.Row0.y * v.y + mat.Row0.z * v.z,
                mat.Row1.x * v.x + mat.Row1.y * v.y + mat.Row1.z * v.z,
                mat.Row2.x * v.x + mat.Row2.y * v.y + mat.Row2.z * v.z);
        }

        public Vector3d Multiply(ref Vector3d v) {
            return new Vector3d(
                Row0.x * v.x + Row0.y * v.y + Row0.z * v.z,
                Row1.x * v.x + Row1.y * v.y + Row1.z * v.z,
                Row2.x * v.x + Row2.y * v.y + Row2.z * v.z);
        }

        public void Multiply(ref Vector3d v, ref Vector3d vOut) {
            vOut.x = Row0.x * v.x + Row0.y * v.y + Row0.z * v.z;
            vOut.y = Row1.x * v.x + Row1.y * v.y + Row1.z * v.z;
            vOut.z = Row2.x * v.x + Row2.y * v.y + Row2.z * v.z;
        }

		public static Matrix3d operator *(Matrix3d mat1, Matrix3d mat2)
		{
            double m00 = mat1.Row0.x * mat2.Row0.x + mat1.Row0.y * mat2.Row1.x + mat1.Row0.z * mat2.Row2.x;
            double m01 = mat1.Row0.x * mat2.Row0.y + mat1.Row0.y * mat2.Row1.y + mat1.Row0.z * mat2.Row2.y;
            double m02 = mat1.Row0.x * mat2.Row0.z + mat1.Row0.y * mat2.Row1.z + mat1.Row0.z * mat2.Row2.z;

            double m10 = mat1.Row1.x * mat2.Row0.x + mat1.Row1.y * mat2.Row1.x + mat1.Row1.z * mat2.Row2.x;
            double m11 = mat1.Row1.x * mat2.Row0.y + mat1.Row1.y * mat2.Row1.y + mat1.Row1.z * mat2.Row2.y;
            double m12 = mat1.Row1.x * mat2.Row0.z + mat1.Row1.y * mat2.Row1.z + mat1.Row1.z * mat2.Row2.z;

            double m20 = mat1.Row2.x * mat2.Row0.x + mat1.Row2.y * mat2.Row1.x + mat1.Row2.z * mat2.Row2.x;
            double m21 = mat1.Row2.x * mat2.Row0.y + mat1.Row2.y * mat2.Row1.y + mat1.Row2.z * mat2.Row2.y;
            double m22 = mat1.Row2.x * mat2.Row0.z + mat1.Row2.y * mat2.Row1.z + mat1.Row2.z * mat2.Row2.z;

			return new Matrix3d(m00, m01, m02, m10, m11, m12, m20, m21, m22);
		}



        public static Matrix3d operator +(Matrix3d mat1, Matrix3d mat2) {
            return new Matrix3d(mat1.Row0 + mat2.Row0, mat1.Row1 + mat2.Row1, mat1.Row2 + mat2.Row2, true);
        }
        public static Matrix3d operator -(Matrix3d mat1, Matrix3d mat2) {
            return new Matrix3d(mat1.Row0 - mat2.Row0, mat1.Row1 - mat2.Row1, mat1.Row2 - mat2.Row2, true);
        }



        public double InnerProduct(ref Matrix3d m2)
        {
            return Row0.Dot(ref m2.Row0) + Row1.Dot(ref m2.Row1) + Row2.Dot(ref m2.Row2);
        }



        public double Determinant {
			get {
				double a11 = Row0.x, a12 = Row0.y, a13 = Row0.z, a21 = Row1.x, a22 = Row1.y, a23 = Row1.z, a31 = Row2.x, a32 = Row2.y, a33 = Row2.z;
                double i00 = a33 * a22 - a32 * a23;
                double i01 = -(a33 * a12 - a32 * a13);
                double i02 = a23 * a12 - a22 * a13;
				return a11 * i00 + a21 * i01 + a31 * i02;
			}
		}


		public Matrix3d Inverse() {
            double a11 = Row0.x, a12 = Row0.y, a13 = Row0.z, a21 = Row1.x, a22 = Row1.y, a23 = Row1.z, a31 = Row2.x, a32 = Row2.y, a33 = Row2.z;
            double i00 = a33 * a22 - a32 * a23;
            double i01 = -(a33 * a12 - a32 * a13);
            double i02 = a23 * a12 - a22 * a13;

            double i10 = -(a33 * a21 - a31 * a23);
            double i11 = a33*a11 - a31*a13;
            double i12 = -(a23*a11 - a21*a13);

            double i20 = a32*a21 - a31*a22;
            double i21 = -(a32*a11 - a31*a12);
            double i22 = a22*a11 - a21*a12;

            double det = a11 * i00 + a21 * i01 + a31 * i02;
			if (Math.Abs(det) < double.Epsilon)
				throw new Exception("Matrix3d.Inverse: matrix is not invertible");
			det = 1.0 / det;
			return new Matrix3d(i00 * det, i01 * det, i02 * det, i10 * det, i11 * det, i12 * det, i20 * det, i21 * det, i22 * det);
		}

        public Matrix3d Transpose()
        {
            return new Matrix3d(
                Row0.x, Row1.x, Row2.x,
                Row0.y, Row1.y, Row2.y,
                Row0.z, Row1.z, Row2.z);
        }

        public Quaterniond ToQuaternion() {
            return new Quaterniond(this);
        }


        public bool EpsilonEqual(Matrix3d m2, double epsilon)
        {
            return Row0.EpsilonEqual(m2.Row0, epsilon) &&
                Row1.EpsilonEqual(m2.Row1, epsilon) &&
                Row2.EpsilonEqual(m2.Row2, epsilon);
        }




        public static Matrix3d AxisAngleD(Vector3d axis, double angleDeg)
        {
            double angle = angleDeg * MathUtil.Deg2Rad;
            double cs = Math.Cos(angle);
            double sn = Math.Sin(angle);
            double oneMinusCos = 1.0 - cs;
            double x2 = axis[0] * axis[0];
            double y2 = axis[1] * axis[1];
            double z2 = axis[2] * axis[2];
            double xym = axis[0] * axis[1] * oneMinusCos;
            double xzm = axis[0] * axis[2] * oneMinusCos;
            double yzm = axis[1] * axis[2] * oneMinusCos;
            double xSin = axis[0] * sn;
            double ySin = axis[1] * sn;
            double zSin = axis[2] * sn;
            return new Matrix3d(
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
