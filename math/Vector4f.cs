using System;
using System.Collections.Generic;
using System.Text;

#if G3_USING_UNITY
using UnityEngine;
#endif

namespace g3
{
    public struct Vector4f : IComparable<Vector4f>, IEquatable<Vector4f>
    {
        public float x;
        public float y;
        public float z;
        public float w;

        public Vector4f(float f) { x = y = z = w = f; }
        public Vector4f(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public Vector4f(float[] v2) { x = v2[0]; y = v2[1]; z = v2[2]; w = v2[3]; }
        public Vector4f(Vector4f copy) { x = copy.x; y = copy.y; z = copy.z; w = copy.w; }

        static public readonly Vector4f Zero = new Vector4f(0.0f, 0.0f, 0.0f, 0.0f);
        static public readonly Vector4f One = new Vector4f(1.0f, 1.0f, 1.0f, 1.0f);

        public float this[int key]
        {
            get { return (key < 2) ? ((key == 0) ? x : y) : ((key == 2) ? z : w); }
            set {
                if (key < 2) { if (key == 0) x = value; else y = value; }
                else { if (key == 2) z = value; else w = value; }
            }
        }

        public float LengthSquared
        {
            get { return x * x + y * y + z * z + w * w; }
        }
        public float Length
        {
            get { return (float)Math.Sqrt(LengthSquared); }
        }

        public float LengthL1
        {
            get { return Math.Abs(x) + Math.Abs(y) + Math.Abs(z) + Math.Abs(w); }
        }


        public float Normalize(float epsilon = MathUtil.Epsilonf)
        {
            float length = Length;
            if (length > epsilon) {
                float invLength = 1.0f / length;
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
        public Vector4f Normalized {
            get {
                float length = Length;
                if (length > MathUtil.Epsilon) {
                    float invLength = 1.0f / length;
                    return new Vector4f(x * invLength, y * invLength, z * invLength, w * invLength);
                } else
                    return Vector4f.Zero;
            }
        }

        public bool IsNormalized {
            get { return Math.Abs((x * x + y * y + z * z + w * w) - 1) < MathUtil.ZeroTolerance; }
        }


        public bool IsFinite
        {
            get { float f = x + y + z + w; return float.IsNaN(f) == false && float.IsInfinity(f) == false; }
        }

        public void Round(int nDecimals) {
            x = (float)Math.Round(x, nDecimals);
            y = (float)Math.Round(y, nDecimals);
            z = (float)Math.Round(z, nDecimals);
            w = (float)Math.Round(w, nDecimals);
        }


        public float Dot(Vector4f v2) {
            return x * v2.x + y * v2.y + z * v2.z + w * v2.w;
        }
        public float Dot(ref Vector4f v2) {
            return x * v2.x + y * v2.y + z * v2.z + w * v2.w;
        }

        public static float Dot(Vector4f v1, Vector4f v2) {
            return v1.Dot(v2);
        }


        public float AngleD(Vector4f v2)
        {
            float fDot = MathUtil.Clamp(Dot(v2), -1, 1);
            return (float)Math.Acos(fDot) * MathUtil.Rad2Degf;
        }
        public static float AngleD(Vector4f v1, Vector4f v2)
        {
            return v1.AngleD(v2);
        }
        public float AngleR(Vector4f v2)
        {
            float fDot = MathUtil.Clamp(Dot(v2), -1, 1);
            return (float)Math.Acos(fDot);
        }
        public static float AngleR(Vector4f v1, Vector4f v2)
        {
            return v1.AngleR(v2);
        }

		public float DistanceSquared(Vector4f v2) {
			float dx = v2.x-x, dy = v2.y-y, dz = v2.z-z, dw = v2.w-w;
			return dx*dx + dy*dy + dz*dz + dw*dw;
		}
		public float DistanceSquared(ref Vector4f v2) {
			float dx = v2.x-x, dy = v2.y-y, dz = v2.z-z, dw = v2.w-w;
			return dx*dx + dy*dy + dz*dz + dw*dw;
		}

        public float Distance(Vector4f v2) {
            float dx = v2.x-x, dy = v2.y-y, dz = v2.z-z, dw = v2.w - w;
			return (float)Math.Sqrt(dx*dx + dy*dy + dz*dz + dw*dw);
		}
        public float Distance(ref Vector4f v2) {
            float dx = v2.x-x, dy = v2.y-y, dz = v2.z-z, dw = v2.w - w;
			return (float)Math.Sqrt(dx*dx + dy*dy + dz*dz + dw*dw);
		}


        public static Vector4f operator -(Vector4f v)
        {
            return new Vector4f(-v.x, -v.y, -v.z, -v.w);
        }

        public static Vector4f operator *(float f, Vector4f v)
        {
            return new Vector4f(f * v.x, f * v.y, f * v.z, f * v.w);
        }
        public static Vector4f operator *(Vector4f v, float f)
        {
            return new Vector4f(f * v.x, f * v.y, f * v.z, f * v.w);
        }
        public static Vector4f operator /(Vector4f v, float f)
        {
            return new Vector4f(v.x / f, v.y / f, v.z / f, v.w / f);
        }
        public static Vector4f operator /(float f, Vector4f v)
        {
            return new Vector4f(f / v.x, f / v.y, f / v.z, f / v.w);
        }

        public static Vector4f operator *(Vector4f a, Vector4f b)
        {
            return new Vector4f(a.x * b.x, a.y * b.y, a.z * b.z, a.w * b.w);
        }
        public static Vector4f operator /(Vector4f a, Vector4f b)
        {
            return new Vector4f(a.x / b.x, a.y / b.y, a.z / b.z, a.w / b.w);
        }


        public static Vector4f operator +(Vector4f v0, Vector4f v1)
        {
            return new Vector4f(v0.x + v1.x, v0.y + v1.y, v0.z + v1.z, v0.w + v1.w);
        }
        public static Vector4f operator +(Vector4f v0, float f)
        {
            return new Vector4f(v0.x + f, v0.y + f, v0.z + f, v0.w + f);
        }

        public static Vector4f operator -(Vector4f v0, Vector4f v1)
        {
            return new Vector4f(v0.x - v1.x, v0.y - v1.y, v0.z - v1.z, v0.w - v1.w);
        }
        public static Vector4f operator -(Vector4f v0, float f)
        {
            return new Vector4f(v0.x - f, v0.y - f, v0.z - f, v0.w - f);
        }



        public static bool operator ==(Vector4f a, Vector4f b)
        {
            return (a.x == b.x && a.y == b.y && a.z == b.z && a.w == b.w);
        }
        public static bool operator !=(Vector4f a, Vector4f b)
        {
            return (a.x != b.x || a.y != b.y || a.z != b.z || a.w != b.w);
        }
        public override bool Equals(object obj)
        {
            return this == (Vector4f)obj;
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
        public int CompareTo(Vector4f other)
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
        public bool Equals(Vector4f other)
        {
            return (x == other.x && y == other.y && z == other.z && w == other.w);
        }


        public bool EpsilonEqual(Vector4f v2, float epsilon) {
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




#if G3_USING_UNITY
        public static implicit operator Vector4f(Vector4 v)
        {
            return new Vector4f(v.x, v.y, v.z, v.w);
        }
        public static implicit operator Vector4(Vector4f v)
        {
            return new Vector4(v.x, v.y, v.z, v.w);
        }
        public static implicit operator Color(Vector4f v)
        {
            return new Color(v.x, v.y, v.z, v.w);
        }
        public static implicit operator Vector4f(Color c)
        {
            return new Vector4f(c.r, c.g, c.b, c.a);
        }
#endif

    }
}
