using System;
using System.Collections.Generic;
using System.Text;

#if G3_USING_UNITY
using UnityEngine;
#endif

namespace g3
{
    public struct Vector4d : IComparable<Vector4d>, IEquatable<Vector4d>
    {
        public double x;
        public double y;
        public double z;
        public double w;

        public Vector4d(double f) { x = y = z = w = f; }
        public Vector4d(double x, double y, double z, double w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public Vector4d(double[] v2) { x = v2[0]; y = v2[1]; z = v2[2]; w = v2[3]; }
        public Vector4d(Vector4d copy) { x = copy.x; y = copy.y; z = copy.z; w = copy.w; }

        static public readonly Vector4d Zero = new Vector4d(0.0f, 0.0f, 0.0f, 0.0f);
        static public readonly Vector4d One = new Vector4d(1.0f, 1.0f, 1.0f, 1.0f);

        public double this[int key]
        {
            get { return (key < 2) ? ((key == 0) ? x : y) : ((key == 2) ? z : w); }
            set {
                if (key < 2) { if (key == 0) x = value; else y = value; }
                else { if (key == 2) z = value; else w = value; }
            }
        }

        public double LengthSquared
        {
            get { return x * x + y * y + z * z + w * w; }
        }
        public double Length
        {
            get { return Math.Sqrt(LengthSquared); }
        }

        public double LengthL1
        {
            get { return Math.Abs(x) + Math.Abs(y) + Math.Abs(z) + Math.Abs(w); }
        }


        public double Normalize(double epsilon = MathUtil.Epsilon)
        {
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
        public Vector4d Normalized {
            get {
                double length = Length;
                if (length > MathUtil.Epsilon) {
                    double invLength = 1.0 / length;
                    return new Vector4d(x * invLength, y * invLength, z * invLength, w * invLength);
                } else
                    return Vector4d.Zero;
            }
        }

        public bool IsNormalized {
            get { return Math.Abs((x * x + y * y + z * z + w * w) - 1) < MathUtil.ZeroTolerance; }
        }


        public bool IsFinite
        {
            get { double f = x + y + z + w; return double.IsNaN(f) == false && double.IsInfinity(f) == false; }
        }

        public void Round(int nDecimals) {
            x = Math.Round(x, nDecimals);
            y = Math.Round(y, nDecimals);
            z = Math.Round(z, nDecimals);
            w = Math.Round(w, nDecimals);
        }


        public double Dot(Vector4d v2) {
            return x * v2.x + y * v2.y + z * v2.z + w * v2.w;
        }
        public double Dot(ref Vector4d v2) {
            return x * v2.x + y * v2.y + z * v2.z + w * v2.w;
        }

        public static double Dot(Vector4d v1, Vector4d v2) {
            return v1.Dot(v2);
        }


        public double AngleD(Vector4d v2)
        {
            double fDot = MathUtil.Clamp(Dot(v2), -1, 1);
            return Math.Acos(fDot) * MathUtil.Rad2Deg;
        }
        public static double AngleD(Vector4d v1, Vector4d v2)
        {
            return v1.AngleD(v2);
        }
        public double AngleR(Vector4d v2)
        {
            double fDot = MathUtil.Clamp(Dot(v2), -1, 1);
            return Math.Acos(fDot);
        }
        public static double AngleR(Vector4d v1, Vector4d v2)
        {
            return v1.AngleR(v2);
        }

		public double DistanceSquared(Vector4d v2) {
			double dx = v2.x-x, dy = v2.y-y, dz = v2.z-z, dw = v2.w-w;
			return dx*dx + dy*dy + dz*dz + dw*dw;
		}
		public double DistanceSquared(ref Vector4d v2) {
			double dx = v2.x-x, dy = v2.y-y, dz = v2.z-z, dw = v2.w-w;
			return dx*dx + dy*dy + dz*dz + dw*dw;
		}

        public double Distance(Vector4d v2) {
            double dx = v2.x-x, dy = v2.y-y, dz = v2.z-z, dw = v2.w - w;
			return Math.Sqrt(dx*dx + dy*dy + dz*dz + dw*dw);
		}
        public double Distance(ref Vector4d v2) {
            double dx = v2.x-x, dy = v2.y-y, dz = v2.z-z, dw = v2.w - w;
			return Math.Sqrt(dx*dx + dy*dy + dz*dz + dw*dw);
		}


        public static Vector4d operator -(Vector4d v)
        {
            return new Vector4d(-v.x, -v.y, -v.z, -v.w);
        }

        public static Vector4d operator *(double f, Vector4d v)
        {
            return new Vector4d(f * v.x, f * v.y, f * v.z, f * v.w);
        }
        public static Vector4d operator *(Vector4d v, double f)
        {
            return new Vector4d(f * v.x, f * v.y, f * v.z, f * v.w);
        }
        public static Vector4d operator /(Vector4d v, double f)
        {
            return new Vector4d(v.x / f, v.y / f, v.z / f, v.w / f);
        }
        public static Vector4d operator /(double f, Vector4d v)
        {
            return new Vector4d(f / v.x, f / v.y, f / v.z, f / v.w);
        }

        public static Vector4d operator *(Vector4d a, Vector4d b)
        {
            return new Vector4d(a.x * b.x, a.y * b.y, a.z * b.z, a.w * b.w);
        }
        public static Vector4d operator /(Vector4d a, Vector4d b)
        {
            return new Vector4d(a.x / b.x, a.y / b.y, a.z / b.z, a.w / b.w);
        }


        public static Vector4d operator +(Vector4d v0, Vector4d v1)
        {
            return new Vector4d(v0.x + v1.x, v0.y + v1.y, v0.z + v1.z, v0.w + v1.w);
        }
        public static Vector4d operator +(Vector4d v0, double f)
        {
            return new Vector4d(v0.x + f, v0.y + f, v0.z + f, v0.w + f);
        }

        public static Vector4d operator -(Vector4d v0, Vector4d v1)
        {
            return new Vector4d(v0.x - v1.x, v0.y - v1.y, v0.z - v1.z, v0.w - v1.w);
        }
        public static Vector4d operator -(Vector4d v0, double f)
        {
            return new Vector4d(v0.x - f, v0.y - f, v0.z - f, v0.w - f);
        }



        public static bool operator ==(Vector4d a, Vector4d b)
        {
            return (a.x == b.x && a.y == b.y && a.z == b.z && a.w == b.w);
        }
        public static bool operator !=(Vector4d a, Vector4d b)
        {
            return (a.x != b.x || a.y != b.y || a.z != b.z || a.w != b.w);
        }
        public override bool Equals(object obj)
        {
            return this == (Vector4d)obj;
        }
        public override int GetHashCode()
        {
            unchecked // Overflow is fine, just wrap
            {
                int hash = (int) 2166136261;
                // Suitable nullity checks etc, of course :)
                hash = (hash * 16777619) ^ x.GetHashCode();
                hash = (hash * 16777619) ^ y.GetHashCode();
                hash = (hash * 16777619) ^ z.GetHashCode();
                hash = (hash * 16777619) ^ w.GetHashCode();
                return hash;
            }
        }
        public int CompareTo(Vector4d other)
        {
            if (x != other.x)
                return x < other.x ? -1 : 1;
            else if (y != other.y)
                return y < other.y ? -1 : 1;
            else if (z != other.z)
                return z < other.z ? -1 : 1;
            else if (w != other.w)
                return w < other.w ? -1 : 1;
            return 0;
        }
        public bool Equals(Vector4d other)
        {
            return (x == other.x && y == other.y && z == other.z && w == other.w);
        }


        public bool EpsilonEqual(Vector4d v2, double epsilon) {
            return Math.Abs(x - v2.x) <= epsilon && 
                   Math.Abs(y - v2.y) <= epsilon &&
                   Math.Abs(z - v2.z) <= epsilon &&
                   Math.Abs(w - v2.w) <= epsilon;
        }



        public override string ToString() {
            return string.Format("{0:F8} {1:F8} {2:F8} {3:F8}", x, y, z, w);
        }
        public string ToString(string fmt) {
            return string.Format("{0} {1} {2} {3}", x.ToString(fmt), y.ToString(fmt), z.ToString(fmt), w.ToString(fmt));
        }


    }
}
