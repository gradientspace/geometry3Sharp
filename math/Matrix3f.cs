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


        public static readonly Matrix3f Identity = new Matrix3f(true);
        public static readonly Matrix3f Zero = new Matrix3f(false);


        public float this[int r, int c] {
            get { return m[r*3+c]; }
            set { m[r * 3 + c] = value; }
        }


        public static Vector3f operator *(Matrix3f mat, Vector3f v) {
            return new Vector3f(
                mat.m[0] * v[0] + mat.m[1] * v[1] + mat.m[2] * v[2],
                mat.m[3] * v[0] + mat.m[4] * v[1] + mat.m[5] * v[2],
                mat.m[6] * v[0] + mat.m[7] * v[1] + mat.m[8] * v[2]);
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
