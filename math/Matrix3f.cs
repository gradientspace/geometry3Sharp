using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    // [RMS] this class is dangerous because of internal array...really should
    //   replace with 9 internal members, except that is really shit!
    //   (could do 6, for symmetric matrix?)
    public struct Matrix3f
    {
        private float[] m;

        public Matrix3f(bool bIdentity) {
            if (bIdentity)
                m = new float[9] { 1, 0, 0, 0, 1, 0, 0, 0, 1 };
            else
                m = new float[9] { 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        }

        // assumes input is row-major...
        public Matrix3f(float[,] mat) {
            m = new float[9] { mat[0, 0], mat[0, 1], mat[0, 2], mat[1, 0], mat[1, 1], mat[1, 2], mat[2, 0], mat[2, 1], mat[2, 2] };
        }
        public Matrix3f(float[] mat) {
            m = new float[9];
            Array.Copy(mat, m, 9);
        }
        public Matrix3f(double[,] mat) {
            m = new float[9] { (float)mat[0, 0], (float)mat[0, 1], (float)mat[0, 2], (float)mat[1, 0], (float)mat[1, 1], (float)mat[1, 2], (float)mat[2, 0], (float)mat[2, 1], (float)mat[2, 2] };
        }
        public Matrix3f(double[] mat) {
            m = new float[9];
            for (int i = 0; i < 9; ++i)
                m[i] = (float)mat[i];
        }
        public Matrix3f(Vector3f v1, Vector3f v2, Vector3f v3, bool bRows)
        {
            if (bRows) {
                m = new float[9] { v1.x, v1.y, v1.z, v2.x, v2.y, v2.z, v3.x, v3.y, v3.z };
            } else {
                m = new float[9] { v1.x, v2.x, v3.x, v1.y, v2.y, v3.y, v1.z, v2.z, v3.z };
            }
        }
		public Matrix3f(float m00, float m01, float m02, float m10, float m11, float m12, float m20, float m21, float m22) {
			m = new float[9] { m00, m01, m02, m10, m11, m12, m20, m21, m22 };
		}


        public static readonly Matrix3f Identity = new Matrix3f(true);
        public static readonly Matrix3f Zero = new Matrix3f(false);


        public float this[int r, int c] {
            get { return m[r*3+c]; }
            set { m[r * 3 + c] = value; }
        }
        public float this[int i] {
            get { return m[i]; }
            set { m[i] = value; }
        }


        public static Vector3f operator *(Matrix3f mat, Vector3f v) {
            return new Vector3f(
                mat.m[0] * v[0] + mat.m[1] * v[1] + mat.m[2] * v[2],
                mat.m[3] * v[0] + mat.m[4] * v[1] + mat.m[5] * v[2],
                mat.m[6] * v[0] + mat.m[7] * v[1] + mat.m[8] * v[2]);
        }
		public static Matrix3f operator *(Matrix3f mat1, Matrix3f mat2)
		{
			float m00 = mat1.m[0] * mat2.m[0] + mat1.m[1] * mat2.m[3] + mat1.m[2] * mat2.m[6];
			float m01 = mat1.m[0] * mat2.m[1] + mat1.m[1] * mat2.m[4] + mat1.m[2] * mat2.m[7];
			float m02 = mat1.m[0] * mat2.m[2] + mat1.m[1] * mat2.m[5] + mat1.m[2] * mat2.m[8];

			float m10 = mat1.m[3] * mat2.m[0] + mat1.m[4] * mat2.m[3] + mat1.m[5] * mat2.m[6];
			float m11 = mat1.m[3] * mat2.m[1] + mat1.m[4] * mat2.m[4] + mat1.m[5] * mat2.m[7];
			float m12 = mat1.m[3] * mat2.m[2] + mat1.m[4] * mat2.m[5] + mat1.m[5] * mat2.m[8];

			float m20 = mat1.m[6] * mat2.m[0] + mat1.m[7] * mat2.m[3] + mat1.m[8] * mat2.m[6];
			float m21 = mat1.m[6] * mat2.m[1] + mat1.m[7] * mat2.m[4] + mat1.m[8] * mat2.m[7];
			float m22 = mat1.m[6] * mat2.m[2] + mat1.m[7] * mat2.m[5] + mat1.m[8] * mat2.m[8];

			return new Matrix3f(m00, m01, m02, m10, m11, m12, m20, m21, m22);
		}



		public float Determinant {
			get {
				float a11 = m[0], a12 = m[1], a13 = m[2], a21 = m[3], a22 = m[4], a23 = m[5], a31 = m[6], a32 = m[7], a33 = m[8];
				float i00 = a33 * a22 - a32 * a23;
				float i01 = -(a33 * a12 - a32 * a13);
				float i02 = a23 * a12 - a22 * a13;
				return a11 * i00 + a21 * i01 + a31 * i02;
			}
		}


		public Matrix3f Inverse() {
			float a11 = m[0], a12 = m[1], a13 = m[2], a21 = m[3], a22 = m[4], a23 = m[5], a31 = m[6], a32 = m[7], a33 = m[8];
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



        public Quaternionf ToQuaternion()
        {
            // from here: http://www.euclideanspace.com/maths/geometry/rotations/conversions/matrixToQuaternion/

            float m00 = this[0, 0], m01 = this[0, 1], m02 = this[0, 2];
            float m10 = this[1, 0], m11 = this[1, 1], m12 = this[1, 2];
            float m20 = this[2, 0], m21 = this[2, 1], m22 = this[2, 2];

            float tr = m00 + m11 + m22;
            float qw, qx, qy, qz;

            if (tr > 0) {
                float S = (float)Math.Sqrt(tr + 1.0) * 2; // S=4*qw 
                qw = 0.25f * S;
                qx = (m21 - m12) / S;
                qy = (m02 - m20) / S;
                qz = (m10 - m01) / S;
            } else if ((m00 > m11) & (m00 > m22)) {
                float S = (float)Math.Sqrt(1.0 + m00 - m11 - m22) * 2; // S=4*qx 
                qw = (m21 - m12) / S;
                qx = 0.25f * S;
                qy = (m01 + m10) / S;
                qz = (m02 + m20) / S;
            } else if (m11 > m22) {
                float S = (float)Math.Sqrt(1.0 + m11 - m00 - m22) * 2; // S=4*qy
                qw = (m02 - m20) / S;
                qx = (m01 + m10) / S;
                qy = 0.25f * S;
                qz = (m12 + m21) / S;
            } else {
                float S = (float)Math.Sqrt(1.0 + m22 - m00 - m11) * 2; // S=4*qz
                qw = (m10 - m01) / S;
                qx = (m02 + m20) / S;
                qy = (m12 + m21) / S;
                qz = 0.25f * S;
            }

            return new Quaternionf(qx, qy, qz, qw).Normalized;
        }




        public override string ToString() {
            return string.Format("[{0:F8} {1:F8} {2:F8}] [{3:F8} {4:F8} {5:F8}] [{6:F8} {7:F8} {8:F8}]",
                m[0], m[1], m[2], m[3], m[4], m[5], m[6], m[7], m[8]);
        }
        public string ToString(string fmt) {
            return string.Format("[{0} {1} {2}] [{3} {4} {5}] [{6} {7} {8}]",
                m[0].ToString(fmt), m[1].ToString(fmt), m[2].ToString(fmt), 
                m[3].ToString(fmt), m[4].ToString(fmt), m[5].ToString(fmt), 
                m[6].ToString(fmt), m[7].ToString(fmt), m[8].ToString(fmt));
        }
    }
}
