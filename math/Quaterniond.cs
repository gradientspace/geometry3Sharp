using System;

#if G3_USING_UNITY
using UnityEngine;
#endif


namespace g3
{
    // mostly ported from WildMagic5 Wm5Quaternion, from geometrictools.com
    public struct Quaterniond
    {
        // note: in Wm5 version, this is a 4-element array stored in order (w,x,y,z).
        public double x, y, z, w;

        public Quaterniond(double x, double y, double z, double w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public Quaterniond(double[] v2) { x = v2[0]; y = v2[1]; z = v2[2]; w = v2[3]; }
        public Quaterniond(Quaterniond q2) { x = q2.x; y = q2.y; z = q2.z; w = q2.w; }

        public Quaterniond(Vector3d axis, double AngleDeg) {
            x = y = z = 0; w = 1;
            SetAxisAngleD(axis, AngleDeg);
        }
        public Quaterniond(Vector3d vFrom, Vector3d vTo) {
            x = y = z = 0; w = 1;
            SetFromTo(vFrom, vTo);
        }
        public Quaterniond(Quaterniond p, Quaterniond q, double t) {
            x = y = z = 0; w = 1;
            SetToSlerp(p, q, t);
        }
        public Quaterniond(Matrix3d mat) {
            x = y = z = 0; w = 1;
            SetFromRotationMatrix(mat);
        }

        static public readonly Quaterniond Zero = new Quaterniond(0.0, 0.0, 0.0, 0.0);
        static public readonly Quaterniond Identity = new Quaterniond(0.0, 0.0, 0.0, 1.0);

        public double this[int key] {
            get { if (key == 0) return x; else if (key == 1) return y; else if (key == 2) return z; else return w; }
            set { if (key == 0) x = value; else if (key == 1) y = value; else if (key == 2) z = value; else w = value; }

        }


        public double LengthSquared {
            get { return x * x + y * y + z * z + w*w; }
        }
        public double Length {
            get { return (double)Math.Sqrt(x * x + y * y + z * z + w * w); }
        }

        public double Normalize(double epsilon = 0) {
            double length = Length;
            if (length > epsilon) {
                double invLength = 1.0 / length;
                x *= invLength;
                y *= invLength;
                z *= invLength;
                w *= invLength;
            } else {
                length = 0;
                x = y = z = w = 0;
            }
            return length;
        }
        public Quaterniond Normalized {
            get { Quaterniond q = new Quaterniond(this); q.Normalize(); return q; }
        }

        public double Dot(Quaterniond q2) {
            return x * q2.x + y * q2.y + z * q2.z + w * q2.w;
        }


        public static Quaterniond operator -(Quaterniond q2) {
            return new Quaterniond(-q2.x, -q2.y, -q2.z, -q2.w);
        }

        public static Quaterniond operator*(Quaterniond a, Quaterniond b) {
            double w = a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z;
            double x = a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y;
            double y = a.w * b.y + a.y * b.w + a.z * b.x - a.x * b.z;
            double z = a.w * b.z + a.z * b.w + a.x * b.y - a.y * b.x;
            return new Quaterniond(x, y, z, w);
        }
        public static Quaterniond operator *(Quaterniond q1, double d) {
            return new Quaterniond(d * q1.x, d * q1.y, d * q1.z, d * q1.w);
        }
        public static Quaterniond operator *(double d, Quaterniond q1) {
            return new Quaterniond(d * q1.x, d * q1.y, d * q1.z, d * q1.w);
        }

        public static Quaterniond operator -(Quaterniond q1, Quaterniond q2) {
            return new Quaterniond(q1.x - q2.x, q1.y - q2.y, q1.z - q2.z, q1.w - q2.w);
        }
        public static Quaterniond operator +(Quaterniond q1, Quaterniond q2) {
            return new Quaterniond(q1.x + q2.x, q1.y + q2.y, q1.z + q2.z, q1.w + q2.w);
        }

        public static Vector3d operator *(Quaterniond q, Vector3d v) {
            //return q.ToRotationMatrix() * v;
            // inline-expansion of above:
            double twoX = 2 * q.x; double twoY = 2 * q.y; double twoZ = 2 * q.z;
            double twoWX = twoX * q.w; double twoWY = twoY * q.w; double twoWZ = twoZ * q.w;
            double twoXX = twoX * q.x; double twoXY = twoY * q.x; double twoXZ = twoZ * q.x;
            double twoYY = twoY * q.y; double twoYZ = twoZ * q.y; double twoZZ = twoZ * q.z;
            return new Vector3d(
                v.x * (1 - (twoYY + twoZZ)) + v.y * (twoXY - twoWZ) + v.z * (twoXZ + twoWY),
                v.x * (twoXY + twoWZ) + v.y * (1 - (twoXX + twoZZ)) + v.z * (twoYZ - twoWX),
                v.x * (twoXZ - twoWY) + v.y * (twoYZ + twoWX) + v.z * (1 - (twoXX + twoYY))); ;
        }


        // these multiply quaternion by (1,0,0), (0,1,0), (0,0,1), respectively.
        // faster than full multiply, because of all the zeros
        public Vector3d AxisX {
            get {
                double twoY = 2 * y; double twoZ = 2 * z;
                double twoWY = twoY * w; double twoWZ = twoZ * w;
                double twoXY = twoY * x; double twoXZ = twoZ * x;
                double twoYY = twoY * y; double twoZZ = twoZ * z;
                return new Vector3d(1 - (twoYY + twoZZ), twoXY + twoWZ, twoXZ - twoWY);
            }
        }
        public Vector3d AxisY {
            get {
                double twoX = 2 * x; double twoY = 2 * y; double twoZ = 2 * z;
                double twoWX = twoX * w; double twoWZ = twoZ * w; double twoXX = twoX * x;
                double twoXY = twoY * x; double twoYZ = twoZ * y; double twoZZ = twoZ * z;
                return new Vector3d(twoXY - twoWZ, 1 - (twoXX + twoZZ), twoYZ + twoWX);
            }
        }
        public Vector3d AxisZ {
            get {
                double twoX = 2 * x; double twoY = 2 * y; double twoZ = 2 * z;
                double twoWX = twoX * w; double twoWY = twoY * w; double twoXX = twoX * x;
                double twoXZ = twoZ * x; double twoYY = twoY * y; double twoYZ = twoZ * y;
                return new Vector3d(twoXZ + twoWY, twoYZ - twoWX, 1 - (twoXX + twoYY));
            }
        }



        public Quaterniond Inverse() {
            double norm = LengthSquared;
            if (norm > 0) {
                double invNorm = 1.0 / norm;
                return new Quaterniond(
                    -x * invNorm, -y * invNorm, -z * invNorm, w * invNorm);
            } else 
                return Quaterniond.Zero;
        }
        public static Quaterniond Inverse(Quaterniond q) {
            return q.Inverse();
        }


        /// <summary>
        /// Equivalent to transpose of matrix. similar to inverse, but w/o normalization...
        /// </summary>
        public Quaterniond Conjugate() {
            return new Quaterniond(-x, -y, -z, w);
        }

        
        public Matrix3d ToRotationMatrix()
        {
            double twoX = 2 * x; double twoY = 2 * y; double twoZ = 2 * z;
            double twoWX = twoX * w; double twoWY = twoY * w; double twoWZ = twoZ * w;
            double twoXX = twoX * x; double twoXY = twoY * x; double twoXZ = twoZ * x;
            double twoYY = twoY * y; double twoYZ = twoZ * y; double twoZZ = twoZ * z;
            return new Matrix3d(
                1 - (twoYY + twoZZ), twoXY - twoWZ, twoXZ + twoWY,
                twoXY + twoWZ, 1 - (twoXX + twoZZ), twoYZ - twoWX,
                twoXZ - twoWY, twoYZ + twoWX, 1 - (twoXX + twoYY));
        }



        public void SetAxisAngleD(Vector3d axis, double AngleDeg) {
            double angle_rad = MathUtil.Deg2Rad * AngleDeg;
            double halfAngle = 0.5 * angle_rad;
            double sn = Math.Sin(halfAngle);
            w = (double)Math.Cos(halfAngle);
            x = (double)(sn * axis.x);
            y = (double)(sn * axis.y);
            z = (double)(sn * axis.z);
        }
        public static Quaterniond AxisAngleD(Vector3d axis, double angleDeg) {
            return new Quaterniond(axis, angleDeg);
        }
        public static Quaterniond AxisAngleR(Vector3d axis, double angleRad) {
            return new Quaterniond(axis, angleRad * MathUtil.Rad2Degf);
        }

        // this function can take non-normalized vectors vFrom and vTo (normalizes internally)
        public void SetFromTo(Vector3d vFrom, Vector3d vTo) {
            // [TODO] this page seems to have optimized version:
            //    http://lolengine.net/blog/2013/09/18/beautiful-maths-quaternion-from-vectors

            // [RMS] not ideal to explicitly normalize here, but if we don't,
            //   output quaternion is not normalized and this causes problems,
            //   eg like drift if we do repeated SetFromTo()
            Vector3d from = vFrom.Normalized, to = vTo.Normalized;
            Vector3d bisector = (from + to).Normalized;
            w = from.Dot(bisector);
            if (w != 0) {
                Vector3d cross = from.Cross(bisector);
                x = cross.x;
                y = cross.y;
                z = cross.z;
            } else {
                double invLength;
                if (Math.Abs(from.x) >= Math.Abs(from.y)) {
                    // V1.x or V1.z is the largest magnitude component.
                    invLength = (double)(1.0 / Math.Sqrt(from.x * from.x + from.z * from.z));
                    x = -from.z * invLength;
                    y = 0;
                    z = +from.x * invLength;
                } else {
                    // V1.y or V1.z is the largest magnitude component.
                    invLength = (double)(1.0 / Math.Sqrt(from.y * from.y + from.z * from.z));
                    x = 0;
                    y = +from.z * invLength;
                    z = -from.y * invLength;
                }
            }
            Normalize();   // aaahhh just to be safe...
        }
        public static Quaterniond FromTo(Vector3d vFrom, Vector3d vTo) {
            return new Quaterniond(vFrom, vTo);
        }
        public static Quaterniond FromToConstrained(Vector3d vFrom, Vector3d vTo, Vector3d vAround)
        {
            double fAngle = MathUtil.PlaneAngleSignedD(vFrom, vTo, vAround);
            return Quaterniond.AxisAngleD(vAround, fAngle);
        }


        public void SetToSlerp(Quaterniond p, Quaterniond q, double t)
        {
            double cs = p.Dot(q);
            double angle = (double)Math.Acos(cs);
            if (Math.Abs(angle) >= MathUtil.ZeroTolerance) {
                double sn = (double)Math.Sin(angle);
                double invSn = 1 / sn;
                double tAngle = t * angle;
                double coeff0 = (double)Math.Sin(angle - tAngle) * invSn;
                double coeff1 = (double)Math.Sin(tAngle) * invSn;
                x = coeff0 * p.x + coeff1 * q.x;
                y = coeff0 * p.y + coeff1 * q.y;
                z = coeff0 * p.z + coeff1 * q.z;
                w = coeff0 * p.w + coeff1 * q.w;
            } else {
                x = p.x;
                y = p.y;
                z = p.z;
                w = p.w;
            }
        }
        public static Quaterniond Slerp(Quaterniond p, Quaterniond q, double t) {
            return new Quaterniond(p, q, t);
        }


        public void SetFromRotationMatrix(Matrix3d rot) {
            SetFromRotationMatrix(ref rot);
        }
        public void SetFromRotationMatrix(ref Matrix3d rot)
        {
            // Algorithm in Ken Shoemake's article in 1987 SIGGRAPH course notes
            // article "Quaternion Calculus and Fast Animation".
            Index3i next = new Index3i(1, 2, 0);

            double trace = rot[0, 0] + rot[1, 1] + rot[2, 2];
            double root;

            if (trace > 0) {
                // |w| > 1/2, may as well choose w > 1/2
                root = Math.Sqrt(trace + 1.0);  // 2w
                w = (0.5) * root;
                root = (0.5) / root;  // 1/(4w)
                x = (rot[2, 1] - rot[1, 2]) * root;
                y = (rot[0, 2] - rot[2, 0]) * root;
                z = (rot[1, 0] - rot[0, 1]) * root;
            } else {
                // |w| <= 1/2
                int i = 0;
                if (rot[1, 1] > rot[0, 0]) {
                    i = 1;
                }
                if (rot[2, 2] > rot[i, i]) {
                    i = 2;
                }
                int j = next[i];
                int k = next[j];

                root = Math.Sqrt(rot[i, i] - rot[j, j] - rot[k, k] + 1.0);

                Vector3d quat = new Vector3d(x, y, z);
                quat[i] = (0.5) * root;
                root = (0.5) / root;
                w = (rot[k, j] - rot[j, k]) * root;
                quat[j] = (rot[j, i] + rot[i, j]) * root;
                quat[k] = (rot[k, i] + rot[i, k]) * root;
                x = quat.x; y = quat.y; z = quat.z;
            }

            Normalize();   // we prefer normalized quaternions...
        }





        public bool EpsilonEqual(Quaterniond q2, double epsilon) {
            return Math.Abs(x - q2.x) <= epsilon && 
                   Math.Abs(y - q2.y) <= epsilon &&
                   Math.Abs(z - q2.z) <= epsilon &&
                   Math.Abs(w - q2.w) <= epsilon;
        }


        // [TODO] should we be normalizing in these casts??
        public static implicit operator Quaterniond(Quaternionf q) {
            return new Quaterniond(q.x, q.y, q.z, q.w);
        }
        public static explicit operator Quaternionf(Quaterniond q) {
            return new Quaternionf((float)q.x, (float)q.y, (float)q.z, (float)q.w);
        }


        public override string ToString() {
            return string.Format("{0:F8} {1:F8} {2:F8} {3:F8}", x, y, z, w);
        }
        public string ToString(string fmt) {
            return string.Format("{0} {1} {2} {3}", x.ToString(fmt), y.ToString(fmt), z.ToString(fmt), w.ToString(fmt));
        }


#if G3_USING_UNITY
        public static implicit operator Quaterniond(Quaternion q)
        {
            return new Quaterniond(q.x, q.y, q.z, q.w);
        }
        public static explicit operator Quaternion(Quaterniond q)
        {
            return new Quaternion((float)q.x, (float)q.y, (float)q.z, (float)q.w);
        }
#endif

    }
}
