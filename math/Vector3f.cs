using System;
using System.Collections.Generic;
using System.Text;

#if G3_USING_UNITY
using UnityEngine;
#endif

namespace g3
{
    public class Vector3f
    {
        public float[] v = { 0, 0, 0 };

        public Vector3f() { }
        public Vector3f(float f) { v[0] = v[1] = v[2] = f; }
        public Vector3f(float x, float y, float z) { v[0] = x; v[1] = y; v[2] = z; }
        public Vector3f(float[] v2) { v[0] = v2[0]; v[1] = v2[1]; v[2] = v2[2]; }
        public Vector3f(Vector3f copy) { v[0] = copy.v[0]; v[1] = copy.v[1]; v[2] = copy.v[2]; }

        public Vector3f(double f) { v[0] = v[1] = v[2] = (float)f; }
        public Vector3f(double x, double y, double z) { v[0] = (float)x; v[1] = (float)y; v[2] = (float)z; }
        public Vector3f(double[] v2) { v[0] = (float)v2[0]; v[1] = (float)v2[1]; v[2] = (float)v2[2]; }
        public Vector3f(Vector3d copy) { v[0] = (float)copy.v[0]; v[1] = (float)copy.v[1]; v[2] = (float)copy.v[2]; }

        static public readonly Vector3f Zero = new Vector3f(0.0f, 0.0f, 0.0f);
        static public readonly Vector3f AxisX = new Vector3f(1.0f, 0.0f, 0.0f);
        static public readonly Vector3f AxisY = new Vector3f(0.0f, 1.0f, 0.0f);
        static public readonly Vector3f AxisZ = new Vector3f(0.0f, 0.0f, 1.0f);

        public float x
        {
            get { return v[0]; }
            set { v[0] = value; }
        }
        public float y
        {
            get { return v[1]; }
            set { v[1] = value; }
        }
        public float z
        {
            get { return v[2]; }
            set { v[2] = value; }
        }
        public float this[int key]
        {
            get { return v[key]; }
            set { v[key] = value; }
        }

        public float LengthSquared
        {
            get { return v[0] * v[0] + v[1] * v[1] + v[2] * v[2]; }
        }
        public float Length
        {
            get { return (float)Math.Sqrt(LengthSquared); }
        }

        public float Normalize(float epsilon = MathUtil.Epsilonf)
        {
            float length = Length;
            if (length > epsilon) {
                float invLength = 1.0f / length;
                v[0] *= invLength;
                v[1] *= invLength;
                v[2] *= invLength;
            } else {
                length = 0;
                v[0] = v[1] = v[2] = 0;
            }
            return length;
        }
        public Vector3f Normalized {
            get { Vector3f n = new Vector3f(v[0], v[1], v[2]); n.Normalize(); return n; }
        }


        public float Dot(Vector3f v2)
        {
            return v[0] * v2[0] + v[1] * v2[1] + v[2] * v2[2];
        }
        public static float Dot(Vector3f v1, Vector3f v2) {
            return v1.Dot(v2);
        }


        public Vector3f Cross(Vector3f v2)
        {
            return new Vector3f(
                v[1] * v2.v[2] - v[2] * v2.v[1],
                v[2] * v2.v[0] - v[0] * v2.v[2],
                v[0] * v2.v[1] - v[1] * v2.v[0]);
        }
        public static Vector3f Cross(Vector3f v1, Vector3f v2) {
            return v1.Cross(v2);
        }

        public Vector3f UnitCross(Vector3f v2) {
            Vector3f n = new Vector3f(
                v[1] * v2.v[2] - v[2] * v2.v[1],
                v[2] * v2.v[0] - v[0] * v2.v[2],
                v[0] * v2.v[1] - v[1] * v2.v[0]);
            n.Normalize();
            return n;
        }

        public float AngleD(Vector3f v2) {
            float fDot = MathUtil.Clamp(Dot(v2), -1, 1);
            return (float)(Math.Acos(fDot) * MathUtil.Rad2Deg);
        }
        public static float AngleD(Vector3f v1, Vector3f v2) {
            return v1.AngleD(v2);
        }
        public float AngleR(Vector3f v2) {
            float fDot = MathUtil.Clamp(Dot(v2), -1, 1);
            return (float)(Math.Acos(fDot));
        }
        public static float AngleR(Vector3f v1, Vector3f v2) {
            return v1.AngleR(v2);
        }


        public float SqrDistance(Vector3f v2)
        {
            float a = (v[0] - v2[0]), b = (v[1] - v2[1]), c = (v[2] - v2[2]);
            return a * a + b * b + c * c;
        }


        public void Set(Vector3f o)
        {
            v[0] = o[0]; v[1] = o[1]; v[2] = o[2];
        }
        public void Set(float fX, float fY, float fZ)
        {
            v[0] = fX; v[1] = fY; v[2] = fZ;
        }
        public void Add(Vector3f o)
        {
            v[0] += o[0]; v[1] += o[1]; v[2] += o[2];
        }
        public void Subtract(Vector3f o)
        {
            v[0] -= o[0]; v[1] -= o[1]; v[2] -= o[2];
        }



        public static Vector3f operator -(Vector3f v)
        {
            return new Vector3f(-v[0], -v[1], -v[2]);
        }

        public static Vector3f operator *(float f, Vector3f v)
        {
            return new Vector3f(f * v[0], f * v[1], f * v[2]);
        }
        public static Vector3f operator *(Vector3f v, float f)
        {
            return new Vector3f(f * v[0], f * v[1], f * v[2]);
        }

        public static Vector3f operator /(Vector3f v, float f)
        {
            return new Vector3f(v[0]/f, v[1]/f, v[2]/f);
        }

        public static Vector3f operator +(Vector3f v0, Vector3f v1)
        {
            return new Vector3f(v0[0] + v1[0], v0[1] + v1[1], v0[2] + v1[2]);
        }
        public static Vector3f operator +(Vector3f v0, float f)
        {
            return new Vector3f(v0[0] + f, v0[1] + f, v0[2] + f);
        }

        public static Vector3f operator -(Vector3f v0, Vector3f v1)
        {
            return new Vector3f(v0[0] - v1[0], v0[1] - v1[1], v0[2] - v1[2]);
        }
        public static Vector3f operator -(Vector3f v0, float f)
        {
            return new Vector3f(v0[0] - f, v0[1] - f, v0[2] - f);
        }


        public override string ToString() {
            return string.Format("{0:F8} {1:F8} {2:F8}", v[0], v[1], v[2]);
        }
        public virtual string ToString(string fmt) {
            return string.Format("{0} {1} {2}", v[0].ToString(fmt), v[1].ToString(fmt), v[2].ToString(fmt));
        }


#if G3_USING_UNITY
        public static implicit operator Vector3f(UnityEngine.Vector3 v)
        {
            return new Vector3f(v[0], v[1], v[2]);
        }
        public static implicit operator Vector3(Vector3f v)
        {
            return new Vector3(v[0], v[1], v[2]);
        }
        public static implicit operator Color(Vector3f v)
        {
            return new Color(v[0], v[1], v[2], 1.0f);
        }
#endif

    }
}
