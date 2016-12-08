using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    public struct Matrix3f
    {
        private float[] m;

        public Matrix3f(bool bIdentity) {
            if (bIdentity)
                m = new float[9] { 1, 0, 0, 0, 1, 0, 0, 0, 1 };
            else
                m = new float[9] { 0, 0, 0, 0, 0, 0, 0, 0, 0 };
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
