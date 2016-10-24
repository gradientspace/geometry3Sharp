using System;
using System.Collections.Generic;
using System.Text;

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

        static public Vector3f Zero
        {
            get { return new Vector3f(0.0f, 0.0f, 0.0f); }
        }
        static public Vector3f AxisX
        {
            get { return new Vector3f(1.0f, 0.0f, 0.0f); }
        }
        static public Vector3f AxisY
        {
            get { return new Vector3f(0.0f, 1.0f, 0.0f); }
        }
        static public Vector3f AxisZ
        {
            get { return new Vector3f(0.0f, 0.0f, 1.0f); }
        }

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

        public float Normalize()
        {
            float f = Length;
            v[0] /= f; v[1] /= f; v[2] /= f;
            return f;
        }
        public Vector3f Normalized
        {
            get { float f = Length; return new Vector3f(v[0] / f, v[1] / f, v[2] / f); }
        }


        public float Dot(Vector3f v2)
        {
            return v[0] * v2[0] + v[1] * v2[1] + v[2] * v2[2];
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





        public static Vector3f operator *(float f, Vector3f v)
        {
            return new Vector3f(f * v[0], f * v[1], f * v[2]);
        }
        public static Vector3f operator *(Vector3f v, float f)
        {
            return new Vector3f(f * v[0], f * v[1], f * v[2]);
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

    }
}
