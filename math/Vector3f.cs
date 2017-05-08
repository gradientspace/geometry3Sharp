using System;
using System.Collections.Generic;
using System.Text;

#if G3_USING_UNITY
using UnityEngine;
#endif

namespace g3
{
    public struct Vector3f : IComparable<Vector3f>, IEquatable<Vector3f>
    {
        public float x;
        public float y;
        public float z;

        public Vector3f(float f) {  x = y = z = f; }
        public Vector3f(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public Vector3f(float[] v2) { x = v2[0]; y = v2[1]; z = v2[2]; }
        public Vector3f(Vector3f copy) {  x = copy.x; y = copy.y; z = copy.z; }

        public Vector3f(double f) {  x = y = z = (float)f; }
        public Vector3f(double x, double y, double z) { this.x = (float)x; this.y = (float)y; this.z = (float)z; }
        public Vector3f(double[] v2) {  x = (float)v2[0]; y = (float)v2[1]; z = (float)v2[2]; }
        public Vector3f(Vector3d copy) {  x = (float)copy.x; y = (float)copy.y; z = (float)copy.z; }

        static public readonly Vector3f Zero = new Vector3f(0.0f, 0.0f, 0.0f);
        static public readonly Vector3f One = new Vector3f(1.0f, 1.0f, 1.0f);
        static public readonly Vector3f Invalid = new Vector3f(float.MaxValue, float.MaxValue, float.MaxValue);
        static public readonly Vector3f AxisX = new Vector3f(1.0f, 0.0f, 0.0f);
        static public readonly Vector3f AxisY = new Vector3f(0.0f, 1.0f, 0.0f);
        static public readonly Vector3f AxisZ = new Vector3f(0.0f, 0.0f, 1.0f);
		static public readonly Vector3f MaxValue = new Vector3f(float.MaxValue,float.MaxValue,float.MaxValue);
		static public readonly Vector3f MinValue = new Vector3f(float.MinValue,float.MinValue,float.MinValue);

        public float this[int key]
        {
            get { return (key == 0) ? x : (key == 1) ? y : z; }
            set { if (key == 0) x = value; else if (key == 1) y = value; else z = value; }
        }

        public Vector2f xy {
            get { return new Vector2f(x, y); }
            set { x = xy.x; y = xy.y; }
        }
        public Vector2f xz {
            get { return new Vector2f(x, z); }
            set { x = xy.x; z = xy.y; }
        }
        public Vector2f yz {
            get { return new Vector2f(y, z); }
            set { y = xy.x; z = xy.y; }
        }

        public float LengthSquared
        {
            get { return x * x + y * y + z * z; }
        }
        public float Length
        {
            get { return (float)Math.Sqrt(LengthSquared); }
        }

        public float LengthL1
        {
            get { return Math.Abs(x) + Math.Abs(y) + Math.Abs(z); }
        }

        public float Normalize(float epsilon = MathUtil.Epsilonf)
        {
            float length = Length;
            if (length > epsilon) {
                float invLength = 1.0f / length;
                x *= invLength;
                y *= invLength;
                z *= invLength;
            } else {
                length = 0;
                x = y = z = 0;
            }
            return length;
        }
        public Vector3f Normalized {
            get {
                float length = Length;
                if (length > MathUtil.Epsilonf) {
                    float invLength = 1 / length;
                    return new Vector3f(x * invLength, y * invLength, z * invLength);
                } else
                    return Vector3f.Zero;
            }
        }

		public bool IsNormalized {
			get { return Math.Abs( (x * x + y * y + z * z) - 1) < MathUtil.ZeroTolerancef; }
		}

        public bool IsFinite
        {
            get { float f = x + y + z; return float.IsNaN(f) == false && float.IsInfinity(f) == false; }
        }


        public void Round(int nDecimals) {
            x = (float)Math.Round(x, nDecimals);
            y = (float)Math.Round(y, nDecimals);
            z = (float)Math.Round(z, nDecimals);
        }


        public float Dot(Vector3f v2)
        {
            return x * v2[0] + y * v2[1] + z * v2[2];
        }
        public static float Dot(Vector3f v1, Vector3f v2) {
            return v1.Dot(v2);
        }


        public Vector3f Cross(Vector3f v2)
        {
            return new Vector3f(
                y * v2.z - z * v2.y,
                z * v2.x - x * v2.z,
                x * v2.y - y * v2.x);
        }
        public static Vector3f Cross(Vector3f v1, Vector3f v2) {
            return v1.Cross(v2);
        }

        public Vector3f UnitCross(Vector3f v2) {
            Vector3f n = new Vector3f(
                y * v2.z - z * v2.y,
                z * v2.x - x * v2.z,
                x * v2.y - y * v2.x);
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


        public float DistanceSquared(Vector3f v2) {
			float dx = v2.x-x, dy = v2.y-y, dz = v2.z-z;
			return dx*dx + dy*dy + dz*dz;
        }
        public float Distance(Vector3f v2) {
            float dx = v2.x-x, dy = v2.y-y, dz = v2.z-z;
			return (float)Math.Sqrt(dx*dx + dy*dy + dz*dz);
		}



        public void Set(Vector3f o)
        {
            x = o[0]; y = o[1]; z = o[2];
        }
        public void Set(float fX, float fY, float fZ)
        {
            x = fX; y = fY; z = fZ;
        }
        public void Add(Vector3f o)
        {
            x += o[0]; y += o[1]; z += o[2];
        }
        public void Subtract(Vector3f o)
        {
            x -= o[0]; y -= o[1]; z -= o[2];
        }



        public static Vector3f operator -(Vector3f v)
        {
            return new Vector3f(-v.x, -v.y, -v.z);
        }

        public static Vector3f operator *(float f, Vector3f v)
        {
            return new Vector3f(f * v.x, f * v.y, f * v.z);
        }
        public static Vector3f operator *(Vector3f v, float f)
        {
            return new Vector3f(f * v.x, f * v.y, f * v.z);
        }
        public static Vector3f operator /(Vector3f v, float f)
        {
            return new Vector3f(v.x /f, v.y /f, v.z /f);
        }
        public static Vector3f operator /(float f, Vector3f v)
        {
            return new Vector3f(f / v.x, f / v.y, f / v.z);
        }

        public static Vector3f operator *(Vector3f a, Vector3f b)
        {
            return new Vector3f(a.x * b.x, a.y * b.y, a.z * b.z);
        }
        public static Vector3f operator /(Vector3f a, Vector3f b)
        {
            return new Vector3f(a.x / b.x, a.y / b.y, a.z / b.z);
        }


        public static Vector3f operator +(Vector3f v0, Vector3f v1)
        {
            return new Vector3f(v0.x + v1.x, v0.y + v1.y, v0.z + v1.z);
        }
        public static Vector3f operator +(Vector3f v0, float f)
        {
            return new Vector3f(v0.x + f, v0.y + f, v0.z + f);
        }

        public static Vector3f operator -(Vector3f v0, Vector3f v1)
        {
            return new Vector3f(v0.x - v1.x, v0.y - v1.y, v0.z - v1.z);
        }
        public static Vector3f operator -(Vector3f v0, float f)
        {
            return new Vector3f(v0.x - f, v0.y - f, v0.z - f);
        }


        public static bool operator ==(Vector3f a, Vector3f b)
        {
            return (a.x == b.x && a.y == b.y && a.z == b.z);
        }
        public static bool operator !=(Vector3f a, Vector3f b)
        {
            return (a.x != b.x || a.y != b.y || a.z != b.z);
        }
        public override bool Equals(object obj)
        {
            return this == (Vector3f)obj;
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
                return hash;
            }
        }
        public int CompareTo(Vector3f other)
        {
            if (x != other.x)
                return x < other.x ? -1 : 1;
            else if (y != other.y)
                return y < other.y ? -1 : 1;
            else if (z != other.z)
                return z < other.z ? -1 : 1;
            return 0;
        }
        public bool Equals(Vector3f other)
        {
            return (x == other.x && y == other.y && z == other.z);
        }


        public bool EpsilonEqual(Vector3f v2, float epsilon) {
            return (float)Math.Abs(x - v2.x) < epsilon && 
                   (float)Math.Abs(y - v2.y) < epsilon &&
                   (float)Math.Abs(z - v2.z) < epsilon;
        }
        public bool PrecisionEqual(Vector3f v2, int nDigits)
        {
            return Math.Round(x, nDigits) == Math.Round(v2.x, nDigits) &&
                   Math.Round(y, nDigits) == Math.Round(v2.y, nDigits) &&
                   Math.Round(z, nDigits) == Math.Round(v2.z, nDigits);
        }


        public static Vector3f Lerp(Vector3f a, Vector3f b, float t)
        {
            float s = 1 - t;
            return new Vector3f(s * a.x + t * b.x, s * a.y + t * b.y, s * a.z + t * b.z);
        }



        public override string ToString() {
            return string.Format("{0:F8} {1:F8} {2:F8}", x, y, z);
        }
        public string ToString(string fmt) {
            return string.Format("{0} {1} {2}", x.ToString(fmt), y.ToString(fmt), z.ToString(fmt));
        }





#if G3_USING_UNITY
        public static implicit operator Vector3f(UnityEngine.Vector3 v)
        {
            return new Vector3f(v.x, v.y, v.z);
        }
        public static implicit operator Vector3(Vector3f v)
        {
            return new Vector3(v.x, v.y, v.z);
        }
        public static implicit operator Color(Vector3f v)
        {
            return new Color(v.x, v.y, v.z, 1.0f);
        }
        public static implicit operator Vector3f(Color c)
        {
            return new Vector3f(c.r, c.g, c.b);
        }
#endif

    }
}
