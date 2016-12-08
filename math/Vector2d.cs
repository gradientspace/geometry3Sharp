using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Text;

namespace g3
{
    public struct Vector2d
    {
        private double[] v;

        public Vector2d(double f) { v = new double[2];  v[0] = v[1] = f; }
        public Vector2d(double x, double y) { v = new double[2]; v[0] = x; v[1] = y; }
        public Vector2d(double[] v2) { v = new double[2]; v[0] = v2[0]; v[1] = v2[1]; }
        public Vector2d(float f) { v = new double[2]; v[0] = v[1] = f; }
        public Vector2d(float x, float y) { v = new double[2]; v[0] = x; v[1] = y; }
        public Vector2d(float[] v2) { v = new double[2]; v[0] = v2[0]; v[1] = v2[1]; }
        public Vector2d(Vector2d copy) { v = new double[2]; v[0] = copy[0]; v[1] = copy[1]; }
        public Vector2d(Vector2f copy) { v = new double[2]; v[0] = copy[0]; v[1] = copy[1]; }


        static public readonly Vector2d Zero = new Vector2d(0.0f, 0.0f);
        static public readonly Vector2d One = new Vector2d(1.0f, 1.0f);
        static public readonly Vector2d AxisX = new Vector2d(1.0f, 0.0f);
        static public readonly Vector2d AxisY = new Vector2d(0.0f, 1.0f);



        public double x
        {
            get { return v[0]; }
            set { v[0] = value; }
        }
        public double y
        {
            get { return v[1]; }
            set { v[1] = value; }
        }
        public double this[int key]
        {
            get { return v[key]; }
            set { v[key] = value; }
        }


        public double LengthSquared
        {
            get { return v[0] * v[0] + v[1] * v[1]; }
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
                v[0] *= invLength;
                v[1] *= invLength;
            } else {
                length = 0;
                v[0] = v[1] = 0;
            }
            return length;
        }
        public Vector2d Normalized
        {
            get { Vector2d n = new Vector2d(v[0], v[1]); n.Normalize(); return n; }
        }


        public double Dot(Vector2d v2)
        {
            return v[0] * v2[0] + v[1] * v2[1];
        }


        public double Cross(Vector2d v2) {
            return y * v2[1] - y * v2[0];
        }



        public double SquaredDist(Vector2d o) {
            return ((v[0] - o[0]) * (v[0] - o[0]) + (v[1] - o[1]) * (v[1] - o[1]));
        }
        public double Dist(Vector2d o) {
            return (double)Math.Sqrt((v[0] - o[0]) * (v[0] - o[0]) + (v[1] - o[1]) * (v[1] - o[1]));
        }


        public void Set(Vector2d o) {
            v[0] = o[0]; v[1] = o[1];
        }
        public void Set(double fX, double fY) {
            v[0] = fX; v[1] = fY;
        }
        public void Add(Vector2d o) {
            v[0] += o[0]; v[1] += o[1];
        }
        public void Subtract(Vector2d o) {
            v[0] -= o[0]; v[1] -= o[1];
        }


        public static Vector2d operator+( Vector2d a, Vector2d o ) {
            return new Vector2d(a[0] + o[0], a[1] + o[1]); 
        }
        public static Vector2d operator +(Vector2d a, double f) {
            return new Vector2d(a[0] + f, a[1] + f);
        }

        public static Vector2d operator-(Vector2d a, Vector2d o) {
            return new Vector2d(a[0] - o[0], a[1] - o[1]);
        }
        public static Vector2d operator -(Vector2d a, double f) {
            return new Vector2d(a[0] - f, a[1] - f);
        }

        public static Vector2d operator *(Vector2d a, double f) {
            return new Vector2d(a[0] * f, a[1] * f);
        }
        public static Vector2d operator *(double f, Vector2d a) {
            return new Vector2d(a[0] * f, a[1] * f);
        }


        public override string ToString() {
            return string.Format("{0:F8} {1:F8}", v[0], v[1]);
        }


        public static implicit operator Vector2d(Vector2f v)
        {
            return new Vector2d(v[0], v[1]);
        }
        public static explicit operator Vector2f(Vector2d v)
        {
            return new Vector2f((float)v[0], (float)v[1]);
        }

    }
}
