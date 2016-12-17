using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Text;

#if G3_USING_UNITY
using UnityEngine;
#endif

namespace g3
{
    public struct Vector2f
    {
        public float x;
        public float y;

        public Vector2f(float f) { x = y = f; }
        public Vector2f(float x, float y) { this.x = x; this.y = y; }
        public Vector2f(float[] v2) { x = v2[0]; y = v2[1]; }
        public Vector2f(double f) { x = y = (float)f; }
        public Vector2f(double x, double y) { this.x = (float)x; this.y = (float)y; }
        public Vector2f(double[] v2) { x = (float)v2[0]; y = (float)v2[1]; }
        public Vector2f(Vector2f copy) { x = copy[0]; y = copy[1]; }
        public Vector2f(Vector2d copy) { x = (float)copy[0]; y = (float)copy[1]; }


        static public readonly Vector2f Zero = new Vector2f(0.0f, 0.0f);
        static public readonly Vector2f One = new Vector2f(1.0f, 1.0f);
        static public readonly Vector2f AxisX = new Vector2f(1.0f, 0.0f);
        static public readonly Vector2f AxisY = new Vector2f(0.0f, 1.0f);

        public float this[int key]
        {
            get { return (key == 0) ? x : y; }
            set { if (key == 0) x = value; else y = value; }
        }


        public float LengthSquared
        {
            get { return x * x + y * y; }
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
                x *= invLength;
                y *= invLength;
            } else {
                length = 0;
                x = y = 0;
            }
            return length;
        }
        public Vector2f Normalized
        {
            get {
                float length = Length;
                if (length > MathUtil.Epsilonf) {
                    float invLength = 1 / length;
                    return new Vector2f(x * invLength, y * invLength);
                } else
                    return Vector2f.Zero;
            }
        }


        public float Dot(Vector2f v2)
        {
            return x * v2.x + y * v2.y;
        }


        public float Cross(Vector2f v2) {
            return y * v2.y - y * v2.x;
        }



        public float SquaredDist(Vector2f o) {
            return ((x - o.x) * (x - o.x) + (y - o.y) * (y - o.y));
        }
        public float Dist(Vector2f o) {
            return (float)Math.Sqrt((x - o.x) * (x - o.x) + (y - o.y) * (y - o.y));
        }


        public void Set(Vector2f o) {
            x = o.x; y = o.y;
        }
        public void Set(float fX, float fY) {
            x = fX; y = fY;
        }
        public void Add(Vector2f o) {
            x += o.x; y += o.y;
        }
        public void Subtract(Vector2f o) {
            x -= o.x; y -= o.y;
        }


		public static Vector2f operator -(Vector2f v) {
			return new Vector2f(-v.x, -v.y);
		}

        public static Vector2f operator+( Vector2f a, Vector2f o ) {
            return new Vector2f(a.x + o.x, a.y + o.y); 
        }
        public static Vector2f operator +(Vector2f a, float f) {
            return new Vector2f(a.x + f, a.y + f);
        }

        public static Vector2f operator-(Vector2f a, Vector2f o) {
            return new Vector2f(a.x - o.x, a.y - o.y);
        }
        public static Vector2f operator -(Vector2f a, float f) {
            return new Vector2f(a.x - f, a.y - f);
        }

        public static Vector2f operator *(Vector2f a, float f) {
            return new Vector2f(a.x * f, a.y * f);
        }
        public static Vector2f operator *(float f, Vector2f a) {
            return new Vector2f(a.x * f, a.y * f);
        }


        public override string ToString() {
            return string.Format("{0:F8} {1:F8}", x, y);
        }




#if G3_USING_UNITY
        public static implicit operator Vector2f(UnityEngine.Vector2 v)
        {
            return new Vector2f(v.x, v.y);
        }
        public static implicit operator Vector2(Vector2f v)
        {
            return new Vector2(v.x, v.y);
        }
#endif

    }
}
