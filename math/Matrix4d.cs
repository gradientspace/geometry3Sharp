using System;
using Unity.Mathematics;
using UnityEngine;

namespace VirgisGeometry
{
    public struct Matrix4d
    {
        public Vector4d Row0;
        public Vector4d Row1;
        public Vector4d Row2;
        public Vector4d Row3;

        public Matrix4d(bool bIdentity) {
            if (bIdentity) {
                Row0 = new Vector4d(1,0,0,0);
                Row1 = new Vector4d(0,1,0,0);
                Row2 = new Vector4d(0,0,1,0);
                Row3 = new Vector4d(0,0,0,1);
            } else {
                Row0 = Row1 = Row2 = Row3 = Vector4d.Zero;
            }
        }

        // assumes input is row-major...
        public Matrix4d(float[,] mat) {
            Row0 = new Vector4d(mat[0, 0], mat[0, 1], mat[0, 2], mat[0, 3]);
            Row1 = new Vector4d(mat[1, 0], mat[1, 1], mat[1, 2], mat[1, 3]);
            Row2 = new Vector4d(mat[2, 0], mat[2, 1], mat[2, 2], mat[2, 3]);
            Row3 = new Vector4d(mat[3, 0], mat[3, 1], mat[3, 2], mat[3, 3]);
        }
        public Matrix4d(float[] mat) {
            Row0 = new Vector4d(mat[0], mat[1], mat[2], mat[3]);
            Row1 = new Vector4d(mat[4], mat[5], mat[6], mat[7]);
            Row2 = new Vector4d(mat[8], mat[9], mat[10], mat[11]);
            Row3 = new Vector4d(mat[12], mat[13], mat[14], mat[15]);
        }
        public Matrix4d(double[,] mat) {
            Row0 = new Vector4d(mat[0, 0], mat[0, 1], mat[0, 2], mat[0, 3]);
            Row1 = new Vector4d(mat[1, 0], mat[1, 1], mat[1, 2], mat[1, 3]);
            Row2 = new Vector4d(mat[2, 0], mat[2, 1], mat[2, 2], mat[2, 3]);
            Row3 = new Vector4d(mat[3, 0], mat[3, 1], mat[3, 2], mat[3, 3]);
        }
        public Matrix4d(double[] mat) {
            Row0 = new Vector4d(mat[0], mat[1], mat[2], mat[3]);
            Row1 = new Vector4d(mat[4], mat[5], mat[6], mat[7]);
            Row2 = new Vector4d(mat[8], mat[9], mat[10], mat[11]);
            Row3 = new Vector4d(mat[12], mat[13], mat[14], mat[15]);
        }
        public Matrix4d(Func<int,double> matBufferF)
        {
            Row0 = new Vector4d(matBufferF(0), matBufferF(1), matBufferF(2), matBufferF(3));
            Row1 = new Vector4d(matBufferF(4), matBufferF(5), matBufferF(6), matBufferF(7));
            Row2 = new Vector4d(matBufferF(8), matBufferF(9), matBufferF(10), matBufferF(11));
            Row3 = new Vector4d(matBufferF(12), matBufferF(13), matBufferF(4), matBufferF(15));
        }
        public Matrix4d(Func<int, int, double> matF)
        {
            Row0 = new Vector4d(matF(0,0), matF(0,1), matF(0,2), matF(0,3));
            Row1 = new Vector4d(matF(1,0), matF(1,1), matF(1,2), matF(1,3));
            Row2 = new Vector4d(matF(2,0), matF(2,1), matF(2,2), matF(2,3));
            Row3 = new Vector4d(matF(3,0), matF(3,1), matF(3,2), matF(3,3));
        }
        public Matrix4d(double m00, double m11, double m22, double m33)
        {
            Row0 = new Vector4d(m00, 0, 0, 0);
            Row1 = new Vector4d(0, m11, 0, 0);
            Row2 = new Vector4d(0, 0, m22, 0);
            Row3 = new Vector4d(0, 0, 0, m33);
        }
        public Matrix4d(Vector4d v1, Vector4d v2, Vector4d v3, Vector4d v4, bool bRows)
        {
            if (bRows) {
                Row0 = v1; Row1 = v2; Row2 = v3; Row3 = v4;
            } else {
                Row0 = new Vector4d(v1.x, v2.x, v3.x, v4.x);
                Row1 = new Vector4d(v1.y, v2.y, v3.y, v4.y);
                Row2 = new Vector4d(v1.z, v2.z, v3.z, v4.z);
                Row3 = new Vector4d(v1.w, v2.w, v3.w, v4.w);
            }
        }
        public Matrix4d(ref Vector4d v1, ref Vector4d v2, ref Vector4d v3, ref Vector4d v4, bool bRows)
        {
            if (bRows) {
                Row0 = v1; Row1 = v2; Row2 = v3; Row3 = v4;
            } else {
                Row0 = new Vector4d(v1.x, v2.x, v3.x, v4.x);
                Row1 = new Vector4d(v1.y, v2.y, v3.y, v4.y);
                Row2 = new Vector4d(v1.z, v2.z, v3.z, v4.z);
                Row3 = new Vector4d(v1.w, v2.w, v3.w, v4.w);
            }
        }
        public Matrix4d(double m00, double m01, double m02, double m03, double m10, double m11, double m12, double m13, double m20, double m21, double m22, double m23,double m30, double m31, double m32, double m33) {
            Row0 = new Vector4d(m00, m01, m02, m03);
            Row1 = new Vector4d(m10, m11, m12, m13);
            Row2 = new Vector4d(m20, m21, m22, m23);
            Row3 = new Vector4d(m30, m31, m32, m33);
        }

        ///// <summary>
        ///// Construct outer-product of u*transpose(v) of u and v
        ///// result is that Mij = u_i * v_j
        ///// </summary>
        //public Matrix3d(ref Vector3d u, ref Vector3d v)
        //{
        //    Row0 = new Vector3d(u.x*v.x, u.x*v.y, u.x*v.z);
        //    Row1 = new Vector3d(u.y*v.x, u.y*v.y, u.y*v.z);
        //    Row2 = new Vector3d(u.z*v.x, u.z*v.y, u.z*v.z);
        //}

        public static readonly Matrix4d Identity = new Matrix4d(true);
        public static readonly Matrix4d Zero = new Matrix4d(false);

        public double this[int r, int c] {
            get {
                return (r == 0) ? Row0[c] : ( (r == 1) ? Row1[c] : ( r == 2) ? Row2[c] : Row3[c] );
            }
            set {
                if (r == 0)         Row0[c] = value;
                else if (r == 1)    Row1[c] = value;
                else if (r == 2)    Row2[c] = value;
                else                Row3[c] = value;
            }
        }

        //public double this[int i] {
        //    get {
        //        return (i > 5) ? Row2[i%3] : ((i > 2) ? Row1[i%3] : Row0[i%3]);
        //    }
        //    set {
        //        if (i > 5)         Row2[i%3] = value;
        //        else if (i > 2)    Row1[i%3] = value;
        //        else               Row0[i%3] = value;
        //    }
        //}

        public Vector4d Row(int i) {
            return (i == 0) ? Row0 : (i == 1) ? Row1 : (i == 2 ) ? Row2 : Row3;
        }
        public Vector4d Column(int i) {
            if (i == 0) return new Vector4d(Row0.x, Row1.x, Row2.x, Row3.x);
            else if ( i==1) return new Vector4d(Row0.y, Row1.y, Row2.y, Row3.y);
            else if ( i==2 ) return new Vector4d(Row0.z, Row1.z, Row2.z, Row3.z);
            return new Vector4d(Row0.w, Row1.w, Row2.w, Row3.w);
        }

        public double[] ToBuffer() {
            return new double[16] {
                Row0.x, Row0.y, Row0.z, Row0.w,
                Row1.x, Row1.y, Row1.z, Row1.w,
                Row2.x, Row2.y, Row2.z, Row2.w,
                Row3.x, Row3.y, Row3.z, Row3.w};
        }
        public void ToBuffer(double[] buf) {
            buf[0] = Row0.x; buf[1] = Row0.y; buf[2] = Row0.z;
            buf[3] = Row1.x; buf[4] = Row1.y; buf[5] = Row1.z;
            buf[6] = Row2.x; buf[7] = Row2.y; buf[8] = Row2.z;
        }

        // This is a transformation
        // Use homogenous coordinates
        public static Vector3d operator *(Matrix4d mat, Vector3d v)
        {
            return (Vector3d)(mat * new Vector4d(v));
        }

        public static Matrix4d operator *(Matrix4d mat, double f) {
            return new Matrix4d(
                mat.Row0.x * f, mat.Row0.y * f, mat.Row0.z * f, mat.Row0.w * f,
                mat.Row1.x * f, mat.Row1.y * f, mat.Row1.z * f, mat.Row1.w * f,
                mat.Row2.x * f, mat.Row2.y * f, mat.Row2.z * f, mat.Row2.w * f,
                mat.Row3.x * f, mat.Row3.y * f, mat.Row3.z * f, mat.Row3.w * f);
        }
        public static Matrix4d operator *(double f, Matrix4d mat) {
            return mat * f;
        }


        public static Vector4d operator *(Matrix4d mat, Vector4d v) {
            return mat.Multiply(ref v);
        }

        public Vector4d Multiply(ref Vector4d v) {
            return new Vector4d(
                Row0.x * v.x + Row0.y * v.y + Row0.z * v.z + Row0.w * v.w,
                Row1.x * v.x + Row1.y * v.y + Row1.z * v.z + Row1.w * v.w,
                Row2.x * v.x + Row2.y * v.y + Row2.z * v.z + Row2.w * v.w,
                Row3.x * v.x + Row3.y * v.y + Row3.z * v.z + Row3.w * v.w);
        }

        public void Multiply(ref Vector4d v, ref Vector4d vOut) {
            vOut.x = Row0.x * v.x + Row0.y * v.y + Row0.z * v.z;
            vOut.y = Row1.x * v.x + Row1.y * v.y + Row1.z * v.z;
            vOut.z = Row2.x * v.x + Row2.y * v.y + Row2.z * v.z;
        }

		public static Matrix4d operator *(Matrix4d mat1, Matrix4d mat2)
		{
            double m00 = mat1.Row0.Dot(mat2.Column(0));
            double m01 = mat1.Row0.Dot(mat2.Column(1));
            double m02 = mat1.Row0.Dot(mat2.Column(2));
            double m03 = mat1.Row0.Dot(mat2.Column(3));

            double m10 = mat1.Row1.Dot(mat2.Column(0));
            double m11 = mat1.Row1.Dot(mat2.Column(1));
            double m12 = mat1.Row1.Dot(mat2.Column(2));
            double m13 = mat1.Row1.Dot(mat2.Column(3));

            double m20 = mat1.Row2.Dot(mat2.Column(0));
            double m21 = mat1.Row2.Dot(mat2.Column(1));
            double m22 = mat1.Row2.Dot(mat2.Column(2));
            double m23 = mat1.Row2.Dot(mat2.Column(3));

            double m30 = mat1.Row3.Dot(mat2.Column(0));
            double m31 = mat1.Row3.Dot(mat2.Column(1));
            double m32 = mat1.Row3.Dot(mat2.Column(2));
            double m33 = mat1.Row3.Dot(mat2.Column(3));

            return new Matrix4d(m00, m01, m02, m03, m10, m11, m12, m13, m20, m21, m22, m23, m30, m31, m32, m33);
		}



        public static Matrix4d operator +(Matrix4d mat1, Matrix4d mat2) {
            return new Matrix4d(mat1.Row0 + mat2.Row0, mat1.Row1 + mat2.Row1, mat1.Row2 + mat2.Row2, mat1.Row3 + mat2.Row3, true);
        }
        public static Matrix4d operator -(Matrix4d mat1, Matrix4d mat2) {
            return new Matrix4d(mat1.Row0 - mat2.Row0, mat1.Row1 - mat2.Row1, mat1.Row2 - mat2.Row2, mat1.Row3 - mat2.Row3 , true);
        }

        public static bool operator ==(Matrix4d a, Matrix4d b)
        {
            return a.Row0 == b.Row0 && a.Row1 == b.Row1 && a.Row2 == b.Row2 && a.Row3 == b.Row3;
        }

        public static bool operator !=(Matrix4d a, Matrix4d b)
        {
            return a.Row0 != b.Row0 || a.Row1 != b.Row1 || a.Row2 != b.Row2 || a.Row3 != b.Row3;
        }

        public override bool Equals(object obj)
        {
            return this == (Matrix4d)obj;
        }



        public double InnerProduct(ref Matrix4d m2)
        {
            return Row0.Dot(ref m2.Row0) + Row1.Dot(ref m2.Row1) + Row2.Dot(ref m2.Row2) + Row3.Dot(ref m2.Row3);
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


		public Matrix4d Inverse() {
            double4x4 matrix = this;
            return math.inverse(matrix);
		}

        public Matrix4d Transpose()
        {
            return new Matrix4d(
                Row0.x, Row1.x, Row2.x, Row3.x,
                Row0.y, Row1.y, Row2.y, Row3.y,
                Row0.z, Row1.z, Row2.z, Row3.z,
                Row0.w, Row1.w, Row2.w, Row3.w);
        }


        public bool EpsilonEqual(Matrix4d m2, double epsilon)
        {
            return Row0.EpsilonEqual(m2.Row0, epsilon) &&
                Row1.EpsilonEqual(m2.Row1, epsilon) &&
                Row2.EpsilonEqual(m2.Row2, epsilon) &&
                Row3.EpsilonEqual(m2.Row3, epsilon);
        }

        public override string ToString() {
            return string.Format("[{0}] [{1}] [{2}]", Row0, Row1, Row2);
        }

        public string ToString(string fmt) {
            return string.Format("[{0}] [{1}] [{2}]", Row0.ToString(fmt), Row1.ToString(fmt), Row2.ToString(fmt));
        }

        public static implicit operator Matrix4d(double4x4 m)
        {
            return new Matrix4d(m.c0, m.c1, m.c2, m.c3, false);
        }
        public static implicit operator double4x4(Matrix4d m)
        {
            return new double4x4(m.Column(0), m.Column(1), m.Column(2), m.Column(3));
        }
        public static implicit operator Matrix4d(Matrix4x4 mat)
        {
            return new Matrix4d(mat.GetRow(0), mat.GetRow(1), mat.GetRow(2), mat.GetRow(3), true);
        }
        public static explicit operator Matrix4x4(Matrix4d mat)
        {
            return new Matrix4x4((Vector4)mat.Column(0), (Vector4)mat.Column(1), (Vector4)mat.Column(2), (Vector4)mat.Column(3));
        }
    }
}
