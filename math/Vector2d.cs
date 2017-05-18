using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Text;

namespace g3
{
    public struct Vector2d : IComparable<Vector2d>, IEquatable<Vector2d>
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
		static public readonly Vector2d MaxValue = new Vector2d(double.MaxValue,double.MaxValue);
		static public readonly Vector2d MinValue = new Vector2d(double.MinValue,double.MinValue);


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

		public bool IsNormalized {
			get { return Math.Abs( (x * x + y * y) - 1) < MathUtil.ZeroTolerance; }
		}

        public bool IsFinite
        {
            get { double f = x + y; return double.IsNaN(f) == false && double.IsInfinity(f) == false; }
        }

        public void Round(int nDecimals) {
            x = Math.Round(x, nDecimals);
            y = Math.Round(y, nDecimals);
        }


        public double Dot(Vector2d v2)
        {
            return x * v2.x + y * v2.y;
        }


        public double Cross(Vector2d v2) {
            return y * v2.y - y * v2.x;
        }


		public Vector2d Perp {
			get { return new Vector2d(y, -x); }
		}
		public Vector2d UnitPerp {
			get { return new Vector2d(y, -x).Normalized; }
		}
		public double DotPerp(Vector2d v2) {
			return x*v2.y - y*v2.x;
		}


        public double AngleD(Vector2d v2) {
            double fDot = MathUtil.Clamp(Dot(v2), -1, 1);
            return Math.Acos(fDot) * MathUtil.Rad2Deg;
        }
        public static double AngleD(Vector2d v1, Vector2d v2) {
            return v1.AngleD(v2);
        }
        public double AngleR(Vector2d v2) {
            double fDot = MathUtil.Clamp(Dot(v2), -1, 1);
            return Math.Acos(fDot);
        }
        public static double AngleR(Vector2d v1, Vector2d v2) {
            return v1.AngleR(v2);
        }



		public double DistanceSquared(Vector2d v2) {
			double dx = v2.x-x, dy = v2.y-y;
			return dx*dx + dy*dy;
		}
        public double Distance(Vector2d v2) {
            double dx = v2.x-x, dy = v2.y-y;
			return Math.Sqrt(dx*dx + dy*dy);
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
        public static Vector2d operator /(Vector2d v, double f)
        {
            return new Vector2d(v.x / f, v.y / f);
        }
        public static Vector2d operator /(double f, Vector2d v)
        {
            return new Vector2d(f / v.x, f / v.y);
        }


		public static Vector2d operator *(Vector2d a, Vector2d b)
		{
			return new Vector2d(a.x * b.x, a.y * b.y);
		}
		public static Vector2d operator /(Vector2d a, Vector2d b)
		{
			return new Vector2d(a.x / b.x, a.y / b.y);
		}


        public static bool operator ==(Vector2d a, Vector2d b)
        {
            return (a.x == b.x && a.y == b.y);
        }
        public static bool operator !=(Vector2d a, Vector2d b)
        {
            return (a.x != b.x || a.y != b.y);
        }
        public override bool Equals(object obj)
        {
            return this == (Vector2d)obj;
        }
        public override int GetHashCode()
        {
            unchecked // Overflow is fine, just wrap
            {
                int hash = (int) 2166136261;
                // Suitable nullity checks etc, of course :)
                hash = (hash * 16777619) ^ x.GetHashCode();
                hash = (hash * 16777619) ^ y.GetHashCode();
                return hash;
            }
        }
        public int CompareTo(Vector2d other)
        {
            if (x != other.x)
                return x < other.x ? -1 : 1;
            else if (y != other.y)
                return y < other.y ? -1 : 1;
            return 0;
        }
        public bool Equals(Vector2d other)
        {
            return (x == other.x && y == other.y);
        }


        public bool EpsilonEqual(Vector2d v2, double epsilon) {
            return Math.Abs(x - v2.x) < epsilon && 
                   Math.Abs(y - v2.y) < epsilon;
        }
        public bool PrecisionEqual(Vector2d v2, int nDigits)
        {
            return Math.Round(x, nDigits) == Math.Round(v2.x, nDigits) &&
                   Math.Round(y, nDigits) == Math.Round(v2.y, nDigits);
        }


        public static Vector2d Lerp(Vector2d a, Vector2d b, double t)
        {
            double s = 1 - t;
            return new Vector2d(s * a.x + t * b.x, s * a.y + t * b.y);
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
