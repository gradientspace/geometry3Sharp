using System;

#if G3_USING_UNITY
using UnityEngine;
#endif


namespace g3
{
    public class Quaternionf
    {
        public float[] v = { 0, 0, 0, 1 };

        public Quaternionf() { }
        public Quaternionf(float x, float y, float z, float w) { v[0] = x; v[1] = y; v[2] = z; v[3] = w; }
        public Quaternionf(float[] v2) { v[0] = v2[0]; v[1] = v2[1]; v[2] = v2[2]; v[3] = v2[3]; }
        public Quaternionf(Quaternionf q2) { v[0] = q2.v[0]; v[1] = q2.v[1]; v[2] = q2.v[2]; v[3] = q2.v[3]; }

        public Quaternionf(Vector3f axis, float AngleDeg) {
            SetAxisAngleD(axis, AngleDeg);
        }
        public Quaternionf(Vector3f vFrom, Vector3f vTo) {
            SetFromTo(vFrom, vTo);
        }
        public Quaternionf(Quaternionf p, Quaternionf q, float t) {
            SetToSlerp(p, q, t);
        }

        static public readonly Quaternionf Zero = new Quaternionf(0.0f, 0.0f, 0.0f, 0.0f);
        static public readonly Quaternionf Identity = new Quaternionf(0.0f, 0.0f, 0.0f, 1.0f);


        public float x {
            get { return v[0]; }
            set { v[0] = value; }
        }
        public float y {
            get { return v[1]; }
            set { v[1] = value; }
        }
        public float z {
            get { return v[2]; }
            set { v[2] = value; }
        }
        public float w {
            get { return v[3]; }
            set { v[3] = value; }
        }
        public float this[int key] {
            get { return v[key]; }
            set { v[key] = value; }
        }


        public float LengthSquared {
            get { return v[0] * v[0] + v[1] * v[1] + v[2] * v[2] + v[3]*v[3]; }
        }
        public float Length {
            get { return (float)Math.Sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2] + v[3] * v[3]); }
        }


        public float Dot(Quaternionf q2) {
            return v[0] * q2.v[0] + v[1] * q2.v[1] + v[2] * q2.v[2] + v[3] * q2.v[3];
        }



        public static Quaternionf operator*(Quaternionf a, Quaternionf b) {
            float w = a.v[3] * b.v[3] - a.v[0] * b.v[0] - a.v[1] * b.v[1] - a.v[2] * b.v[2];
            float x = a.v[3] * b.v[0] + a.v[0] * b.v[3] + a.v[1] * b.v[2] - a.v[2] * b.v[1];
            float y = a.v[3] * b.v[1] + a.v[1] * b.v[3] + a.v[2] * b.v[0] - a.v[0] * b.v[2];
            float z = a.v[3] * b.v[2] + a.v[2] * b.v[3] + a.v[0] * b.v[1] - a.v[1] * b.v[0];
            return new Quaternionf(x, y, z, w);
        }


        public static Quaternionf operator -(Quaternionf q1, Quaternionf q2) {
            return new Quaternionf(q1.v[0] - q2.v[0], q1.v[1] - q2.v[1], q1.v[2] - q2.v[2], q1.v[3] - q2.v[3]);
        }

        public static Vector3f operator *(Quaternionf q, Vector3f v) {
            return q.ToRotationMatrix() * v;
        }



        public Quaternionf Inverse() {
            float norm = LengthSquared;
            if (norm > 0) {
                float invNorm = 1.0f / norm;
                return new Quaternionf(
                    -v[0] * invNorm, -v[1] * invNorm, -v[2] * invNorm, -v[3] * invNorm);
            } else 
                return Quaternionf.Zero;
        }
        public static Quaternionf Inverse(Quaternionf q) {
            return q.Inverse();
        }


        
        public Matrix3f ToRotationMatrix()
        {
            float twoX = 2 * v[0]; float twoY = 2 * v[1]; float twoZ = 2 * v[2];
            float twoWX = twoX * v[3]; float twoWY = twoY * v[3]; float twoWZ = twoZ * v[3];
            float twoXX = twoX * v[0]; float twoXY = twoY * v[0]; float twoXZ = twoZ * v[0];
            float twoYY = twoY * v[1]; float twoYZ = twoZ * v[1]; float twoZZ = twoZ * v[2];
            Matrix3f m = new Matrix3f();
            m[0, 0] = 1 - (twoYY + twoZZ); m[0, 1] = twoXY - twoWZ; m[0, 2] = twoXZ + twoWY;
            m[1, 0] = twoXY + twoWZ; m[1, 1] = 1 - (twoXX + twoZZ); m[1, 2] = twoYZ - twoWX;
            m[2, 0] = twoXZ - twoWY; m[2, 1] = twoYZ + twoWX; m[2, 2] = 1 - (twoXX + twoYY);
            return m;
        }



        public void SetAxisAngleD(Vector3f axis, float AngleDeg) {
            double angle_rad = MathUtil.Deg2Rad * AngleDeg;
            double halfAngle = 0.5 * angle_rad;
            double sn = Math.Sin(halfAngle);
            v[3] = (float)Math.Cos(halfAngle);
            v[0] = (float)(sn * axis[0]);
            v[1] = (float)(sn * axis[1]);
            v[2] = (float)(sn * axis[2]);
        }
        public static Quaternionf AxisAngleD(Vector3f axis, float angleDeg) {
            return new Quaternionf(axis, angleDeg);
        }



        public void SetFromTo(Vector3f vFrom, Vector3f vTo) {
            Vector3f bisector = (vFrom + vTo).Normalized;
            v[3] = vFrom.Dot(bisector);
            if (v[3] != 0) {
                Vector3f cross = vFrom.Cross(bisector);
                v[0] = cross[0];
                v[1] = cross[1];
                v[2] = cross[2];
            } else {
                if ( Math.Abs(vFrom[0]) >= Math.Abs(vFrom[1]) ) {
                    float invLength = 1.0f / (float)Math.Sqrt(vFrom[0] * vFrom[0] + vFrom[2] * vFrom[2]);
                    v[0] = -vFrom[2] * invLength;
                    v[1] = 0;
                    v[2] = +vFrom[0] * invLength;
                } else {
                    float invLength = 1.0f / (float)Math.Sqrt(vFrom[1] * vFrom[1] + vFrom[2] * vFrom[2]);
                    v[0] = 0;
                    v[1] = +vFrom[2] * invLength;
                    v[2] = -vFrom[1] * invLength;
                }
            }
        }
        public static Quaternionf FromTo(Vector3f vFrom, Vector3f vTo) {
            return new Quaternionf(vFrom, vTo);
        }



        public void SetToSlerp(Quaternionf p, Quaternionf q, float t)
        {
            float cs = p.Dot(q);
            float angle = (float)Math.Acos(cs);
            if (Math.Abs(angle) >= MathUtil.ZeroTolerance) {
                float sn = (float)Math.Sin(angle);
                float invSn = 1 / sn;
                float tAngle = t * angle;
                float coeff0 = (float)Math.Sin(angle - tAngle) * invSn;
                float coeff1 = (float)Math.Sin(tAngle) * invSn;
                v[0] = coeff0 * p.v[0] + coeff1 * q.v[0];
                v[1] = coeff0 * p.v[1] + coeff1 * q.v[1];
                v[2] = coeff0 * p.v[2] + coeff1 * q.v[2];
                v[3] = coeff0 * p.v[3] + coeff1 * q.v[3];
            } else {
                v[0] = p.v[0];
                v[1] = p.v[1];
                v[2] = p.v[2];
                v[3] = p.v[3];
            }
        }
        public static Quaternionf Slerp(Quaternionf p, Quaternionf q, float t) {
            return new Quaternionf(p, q, t);
        }



        public override string ToString() {
            return string.Format("{0:F8} {1:F8} {2:F8} {3:F8}", v[0], v[1], v[2], v[3]);
        }
        public virtual string ToString(string fmt) {
            return string.Format("{0} {1} {2} {3}", v[0].ToString(fmt), v[1].ToString(fmt), v[2].ToString(fmt), v[3].ToString(fmt));
        }


#if G3_USING_UNITY
        public static implicit operator Quaternionf(Quaternion q)
        {
            return new Quaternionf(q.x, q.y, q.z, q.w);
        }
        public static implicit operator Quaternion(Quaternionf q)
        {
            return new Quaternion(q[0], q[1], q[2], q[3]);
        }
#endif

    }
}
