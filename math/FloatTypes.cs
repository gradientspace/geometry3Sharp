// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Distributed under the Boost Software License, Version 1.0. http://www.boost.org/LICENSE_1_0.txt
using System;

namespace g3
{

    /// <summary>
    /// Matrix2f is a float-storage variant of Matrix2d, intended to be used for serialization and buffer storage (to reduce memory usage)
    /// Matrix2d should be used for any geometric calculations.
    /// </summary>
    public class Matrix2f
    {
        public float m00, m01, m10, m11;

        public static readonly Matrix2f Identity = new Matrix2f(true);
        public static readonly Matrix2f Zero = new Matrix2f(false);
        public static readonly Matrix2f One = new Matrix2f(1, 1, 1, 1);

        public Matrix2f(bool bIdentity)
        {
            if (bIdentity) {
                m00 = m11 = 1;
                m01 = m10 = 0;
            } else
                m00 = m01 = m10 = m11 = 0;
        }
        public Matrix2f(float m00, float m01, float m10, float m11) {
            this.m00 = m00; this.m01 = m01; this.m10 = m10; this.m11 = m11;
        }
        public Matrix2f(float m00, float m11) {
            this.m00 = m00; this.m11 = m11; this.m01 = this.m10 = 0;
        }

        // Create a tensor product U*V^T.
        public Matrix2f(Vector2f u, Vector2f v) {
            m00 = u.x * v.x;
            m01 = u.x * v.y;
            m10 = u.y * v.x;
            m11 = u.y * v.y;
        }
    }


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





    /// <summary>
    /// Quaternionf is a float-storage variant of Quaterniond, intended to be used for serialization and buffer storage (to reduce memory usage)
    /// Quaterniond should be used for any geometric calculations.
    /// </summary>
    public struct Quaternionf
    {
        // note: in Wm5 version, this is a 4-element array stored in order (w,x,y,z).
        public float x, y, z, w;

        public Quaternionf(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public Quaternionf(float[] v2) { x = v2[0]; y = v2[1]; z = v2[2]; w = v2[3]; }
        public Quaternionf(Quaternionf q2) { x = q2.x; y = q2.y; z = q2.z; w = q2.w; }

        static public readonly Quaternionf Zero = new Quaternionf(0.0f, 0.0f, 0.0f, 0.0f);
        static public readonly Quaternionf Identity = new Quaternionf(0.0f, 0.0f, 0.0f, 1.0f);

        public float this[int key] {
            get { if (key == 0) return x; else if (key == 1) return y; else if (key == 2) return z; else return w; }
            set { if (key == 0) x = value; else if (key == 1) y = value; else if (key == 2) z = value; else w = value; }
        }


        // these multiply quaternion by (1,0,0), (0,1,0), (0,0,1), respectively.
        // faster than full multiply, because of all the zeros
        public Vector3f AxisX {
            get {
                float twoY = 2 * y; float twoZ = 2 * z;
                float twoWY = twoY * w; float twoWZ = twoZ * w;
                float twoXY = twoY * x; float twoXZ = twoZ * x;
                float twoYY = twoY * y; float twoZZ = twoZ * z;
                return new Vector3f(1 - (twoYY + twoZZ), twoXY + twoWZ, twoXZ - twoWY);
            }
        }
        public Vector3f AxisY {
            get {
                float twoX = 2 * x; float twoY = 2 * y; float twoZ = 2 * z;
                float twoWX = twoX * w; float twoWZ = twoZ * w; float twoXX = twoX * x;
                float twoXY = twoY * x; float twoYZ = twoZ * y; float twoZZ = twoZ * z;
                return new Vector3f(twoXY - twoWZ, 1 - (twoXX + twoZZ), twoYZ + twoWX);
            }
        }
        public Vector3f AxisZ {
            get {
                float twoX = 2 * x; float twoY = 2 * y; float twoZ = 2 * z;
                float twoWX = twoX * w; float twoWY = twoY * w; float twoXX = twoX * x;
                float twoXZ = twoZ * x; float twoYY = twoY * y; float twoYZ = twoZ * y;
                return new Vector3f(twoXZ + twoWY, twoYZ - twoWX, 1 - (twoXX + twoYY));
            }
        }


        public bool EpsilonEqual(Quaternionf q2, float epsilon) {
            return (float)Math.Abs(x - q2.x) <= epsilon && 
                   (float)Math.Abs(y - q2.y) <= epsilon &&
                   (float)Math.Abs(z - q2.z) <= epsilon &&
                   (float)Math.Abs(w - q2.w) <= epsilon;
        }


        public override string ToString() {
            return string.Format("{0:F8} {1:F8} {2:F8} {3:F8}", x, y, z, w);
        }
        public string ToString(string fmt) {
            return string.Format("{0} {1} {2} {3}", x.ToString(fmt), y.ToString(fmt), z.ToString(fmt), w.ToString(fmt));
        }

    }




    /// <summary>
    /// Frame3f is a float-storage variant of Frame3d, intended to be used for serialization and buffer storage (to reduce memory usage)
    /// Frame3d should be used for any geometric calculations.
    /// </summary>
    public struct Frame3f
    {
        Quaternionf rotation;
        Vector3f origin;

        static readonly public Frame3f Identity = new Frame3f(Vector3f.Zero, Quaternionf.Identity);

        public Frame3f(Frame3d copy)
        {
            this.rotation = (Quaternionf)copy.Rotation;
            this.origin = (Vector3f)copy.Origin;
        }
        public Frame3f(Vector3f origin, Quaternionf orientation)
        {
            rotation = orientation;
            this.origin = origin;
        }
        public Frame3f(Vector3d origin, Quaterniond orientation)
        {
            rotation = (Quaternionf)orientation;
            this.origin = (Vector3f)origin;
        }


        public Quaternionf Rotation
        {
            get { return rotation; }
            set { rotation = value; }
        }
        public Vector3f Origin
        {
            get { return origin; }
            set { origin = value; }
        }

        public readonly Vector3f X {
            get { return rotation.AxisX; }
        }
        public readonly Vector3f Y {
            get { return rotation.AxisY; }
        }
        public readonly Vector3f Z {
            get { return rotation.AxisZ; }
        }

        public readonly bool EpsilonEqual(Frame3f f2, float epsilon) {
            return origin.EpsilonEqual(f2.origin, epsilon) &&
                rotation.EpsilonEqual(f2.rotation, epsilon);
        }

        public override string ToString() {
            return ToString("F4");
        }
        public string ToString(string fmt) {
            return string.Format("[Frame3f: Origin={0}, X={1}, Y={2}, Z={3}]", Origin.ToString(fmt), X.ToString(fmt), Y.ToString(fmt), Z.ToString(fmt));
        }

    }


}
