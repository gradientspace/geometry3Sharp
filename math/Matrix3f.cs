using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    /// <summary>
    /// Matrix3f is a float-storage variant of Matrix3d, intended to be used for serialization and buffer storage (to reduce memory usage)
    /// Matrix3d should be used for any geometric calculations.
    /// </summary>
    public struct Matrix3f
    {
        public Vector3f Row0;
        public Vector3f Row1;
        public Vector3f Row2;

        public Matrix3f(bool bIdentity) 
        {
            if (bIdentity) {
                Row0 = Vector3f.AxisX; Row1 = Vector3f.AxisY; Row2 = Vector3f.AxisZ;
            } else {
                Row0 = Row1 = Row2 = Vector3f.Zero;
            }
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

        public bool EpsilonEqual(Matrix3f m2, float epsilon)
        {
            return Row0.EpsilonEqual(m2.Row0, epsilon) &&
                Row1.EpsilonEqual(m2.Row1, epsilon) &&
                Row2.EpsilonEqual(m2.Row2, epsilon);
        }

        public override string ToString() {
            return string.Format("[{0}] [{1}] [{2}]", Row0, Row1, Row2);
        }
        public string ToString(string fmt) {
            return string.Format("[{0}] [{1}] [{2}]", Row0.ToString(fmt), Row1.ToString(fmt), Row2.ToString(fmt));
        }
    }
}
