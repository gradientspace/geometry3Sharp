using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Text;

namespace g3
{
    public struct Vector2d
    {
        public double x;
        public double y;

        public Vector2d(double f) { x = y = f; }
        public Vector2d(double x, double y) { this.x = x; this.y = y; }
        public Vector2d(double[] v2) { x = v2[0]; y = v2[1]; }
        public Vector2d(float f) { x = y = f; }
        public Vector2d(float x, float y) { this.x = x; this.y = y; }
        public Vector2d(float[] v2) { x = v2[0]; y = v2[1]; }
        public Vector2d(Vector2d copy) { x = copy.x; y = copy.y; }
        public Vector2d(Vector2f copy) { x = copy.x; y = copy.y; }


        static public readonly Vector2d Zero = new Vector2d(0.0f, 0.0f);
        static public readonly Vector2d One = new Vector2d(1.0f, 1.0f);
        static public readonly Vector2d AxisX = new Vector2d(1.0f, 0.0f);
        static public readonly Vector2d AxisY = new Vector2d(0.0f, 1.0f);


        public double this[int key]
        {
            get { return (key == 0) ? x : y; }
            set { if (key == 0) x = value; else y = value; }
        }


        public double LengthSquared
        {
            get { return x * x + y * y; }
        }
        public double Length
        {
            get { return (double)Math.Sqrt(LengthSquared); }
        }

        public double Normalize(double epsilon = MathUtil.Epsilon)
        {
            double length = Length;
            if (length > epsilon) {
                double invLength = 1.0 / length;
                x *= invLength;
                y *= invLength;
            } else {
                length = 0;
                x = y = 0;
            }
            return length;
        }
        public Vector2d Normalized
        {
            get {
                double length = Length;
                if (length > MathUtil.Epsilon) {
                    double invLength = 1 / length;
                    return new Vector2d(x * invLength, y * invLength);
                } else
                    return Vector2d.Zero;
            }
        }


        public double Dot(Vector2d v2)
        {
            return x * v2.x + y * v2.y;
        }


        public double Cross(Vector2d v2) {
            return y * v2.y - y * v2.x;
        }



        public double SquaredDist(Vector2d o) {
            return ((x - o.x) * (x - o.x) + (y - o.y) * (y - o.y));
        }
        public double Dist(Vector2d o) {
            return (double)Math.Sqrt((x - o.x) * (x - o.x) + (y - o.y) * (y - o.y));
        }


        public void Set(Vector2d o) {
            x = o.x; y = o.y;
        }
        public void Set(double fX, double fY) {
            x = fX; y = fY;
        }
        public void Add(Vector2d o) {
            x += o.x; y += o.y;
        }
        public void Subtract(Vector2d o) {
            x -= o.x; y -= o.y;
        }



		public static Vector2d operator -(Vector2d v) {
			return new Vector2d(-v.x, -v.y);
		}

        public static Vector2d operator+( Vector2d a, Vector2d o ) {
            return new Vector2d(a.x + o.x, a.y + o.y); 
        }
        public static Vector2d operator +(Vector2d a, double f) {
            return new Vector2d(a.x + f, a.y + f);
        }

        public static Vector2d operator-(Vector2d a, Vector2d o) {
            return new Vector2d(a.x - o.x, a.y - o.y);
        }
        public static Vector2d operator -(Vector2d a, double f) {
            return new Vector2d(a.x - f, a.y - f);
        }

        public static Vector2d operator *(Vector2d a, double f) {
            return new Vector2d(a.x * f, a.y * f);
        }
        public static Vector2d operator *(double f, Vector2d a) {
            return new Vector2d(a.x * f, a.y * f);
        }


        public override string ToString() {
            return string.Format("{0:F8} {1:F8}", x, y);
        }


        public static implicit operator Vector2d(Vector2f v)
        {
            return new Vector2d(v.x, v.y);
        }
        public static explicit operator Vector2f(Vector2d v)
        {
            return new Vector2f((float)v.x, (float)v.y);
        }

    }
}
