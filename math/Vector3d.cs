using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.CompilerServices; // Added for Inlining
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

#if G3_USING_UNITY
using UnityEngine;
#endif

namespace g3 {
    [JsonConverter(typeof(Vector3dJsonConverter))]
    public struct Vector3d : IComparable<Vector3d>, IEquatable<Vector3d> {
        public double x;
        public double y;
        public double z;

        public Vector3d(double f) { x = y = z = f; }
        public Vector3d(double x, double y, double z) { this.x = x; this.y = y; this.z = z; }
        public Vector3d(double[] v2) { x = v2[0]; y = v2[1]; z = v2[2]; }
        public Vector3d(Vector3d copy) { x = copy.x; y = copy.y; z = copy.z; }
        public Vector3d(Vector3f copy) { x = copy.x; y = copy.y; z = copy.z; }

        static public readonly Vector3d Zero = new Vector3d(0.0, 0.0, 0.0);
        static public readonly Vector3d One = new Vector3d(1.0, 1.0, 1.0);
        static public readonly Vector3d OneNormalized = new Vector3d(1.0, 1.0, 1.0).Normalized;
        static public readonly Vector3d Invalid = new Vector3d(double.MaxValue, double.MaxValue, double.MaxValue);
        static public readonly Vector3d AxisX = new Vector3d(1.0, 0.0, 0.0);
        static public readonly Vector3d AxisY = new Vector3d(0.0, 1.0, 0.0);
        static public readonly Vector3d AxisZ = new Vector3d(0.0, 0.0, 1.0);
        static public readonly Vector3d UnitX = new Vector3d(1.0, 0.0, 0.0);
        static public readonly Vector3d UnitY = new Vector3d(0.0, 1.0, 0.0);
        static public readonly Vector3d UnitZ = new Vector3d(0.0, 0.0, 1.0);
        static public readonly Vector3d MaxValue = new Vector3d(double.MaxValue, double.MaxValue, double.MaxValue);
        static public readonly Vector3d MinValue = new Vector3d(double.MinValue, double.MinValue, double.MinValue);

        public double this[int key] {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return (key == 0) ? x : (key == 1) ? y : z; }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set { if (key == 0) x = value; else if (key == 1) y = value; else z = value; }
        }

        public Vector2d xy {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return new Vector2d(x, y); }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set { x = value.x; y = value.y; }
        }
        public Vector2d xz {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return new Vector2d(x, z); }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set { x = value.x; z = value.y; }
        }
        public Vector2d yz {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return new Vector2d(y, z); }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set { y = value.x; z = value.y; }
        }

        public readonly double LengthSquared {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return x * x + y * y + z * z; }
        }
        public readonly double Length {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return Math.Sqrt(x * x + y * y + z * z); }
        }

        public readonly double LengthL1 {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return Math.Abs(x) + Math.Abs(y) + Math.Abs(z); }
        }

        public readonly double Max {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return Math.Max(x, Math.Max(y, z)); }
        }
        public readonly double Min {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return Math.Min(x, Math.Min(y, z)); }
        }
        public readonly double MaxAbs {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return Math.Max(Math.Abs(x), Math.Max(Math.Abs(y), Math.Abs(z))); }
        }
        public readonly double MinAbs {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return Math.Min(Math.Abs(x), Math.Min(Math.Abs(y), Math.Abs(z))); }
        }

        public readonly Vector3d Abs {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return new Vector3d(Math.Abs(x), Math.Abs(y), Math.Abs(z)); }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double Normalize(double epsilon = MathUtil.Epsilon) {
            double length = Length;
            if (length > epsilon) {
                double invLength = 1.0 / length;
                x *= invLength;
                y *= invLength;
                z *= invLength;
            }
            else {
                length = 0;
                x = y = z = 0;
            }
            return length;
        }

        public readonly Vector3d Normalized {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get {
                double length = Length;
                if (length > MathUtil.Epsilon) {
                    double invLength = 1.0 / length;
                    return new Vector3d(x * invLength, y * invLength, z * invLength);
                }
                else
                    return Vector3d.Zero;
            }
        }

        public readonly bool IsNormalized {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return Math.Abs((x * x + y * y + z * z) - 1) < MathUtil.ZeroTolerance; }
        }

        public readonly bool IsFinite {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { double f = x + y + z; return double.IsNaN(f) == false && double.IsInfinity(f) == false; }
        }

        public void Round(int nDecimals) {
            x = Math.Round(x, nDecimals);
            y = Math.Round(y, nDecimals);
            z = Math.Round(z, nDecimals);
        }
        public readonly Vector3d RoundFrac(int nDecimals) {
            return new Vector3d(Math.Round(x, nDecimals), Math.Round(y, nDecimals), Math.Round(z, nDecimals));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly double Dot(Vector3d v2) {
            return x * v2.x + y * v2.y + z * v2.z;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly double Dot(ref Vector3d v2) {
            return x * v2.x + y * v2.y + z * v2.z;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Dot(Vector3d v1, Vector3d v2) {
            return v1.Dot(ref v2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly Vector3d Cross(Vector3d v2) {
            return new Vector3d(
                y * v2.z - z * v2.y,
                z * v2.x - x * v2.z,
                x * v2.y - y * v2.x);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly Vector3d Cross(ref Vector3d v2) {
            return new Vector3d(
                y * v2.z - z * v2.y,
                z * v2.x - x * v2.z,
                x * v2.y - y * v2.x);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3d Cross(Vector3d v1, Vector3d v2) {
            return v1.Cross(ref v2);
        }

        public readonly Vector3d UnitCross(ref Vector3d v2) {
            Vector3d n = new Vector3d(
                y * v2.z - z * v2.y,
                z * v2.x - x * v2.z,
                x * v2.y - y * v2.x);
            n.Normalize();
            return n;
        }
        public readonly Vector3d UnitCross(Vector3d v2) {
            return UnitCross(ref v2);
        }


        public readonly double AngleD(Vector3d v2) {
            double fDot = MathUtil.Clamp(Dot(v2), -1, 1);
            return Math.Acos(fDot) * MathUtil.Rad2Deg;
        }
        public static double AngleD(Vector3d v1, Vector3d v2) {
            return v1.AngleD(v2);
        }
        public readonly double AngleR(Vector3d v2) {
            double fDot = MathUtil.Clamp(Dot(v2), -1, 1);
            return Math.Acos(fDot);
        }
        public static double AngleR(Vector3d v1, Vector3d v2) {
            return v1.AngleR(v2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly double DistanceSquared(Vector3d v2) {
            double dx = v2.x - x, dy = v2.y - y, dz = v2.z - z;
            return dx * dx + dy * dy + dz * dz;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly double DistanceSquared(ref Vector3d v2) {
            double dx = v2.x - x, dy = v2.y - y, dz = v2.z - z;
            return dx * dx + dy * dy + dz * dz;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly double Distance(Vector3d v2) {
            double dx = v2.x - x, dy = v2.y - y, dz = v2.z - z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly double Distance(ref Vector3d v2) {
            double dx = v2.x - x, dy = v2.y - y, dz = v2.z - z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public void Set(Vector3d o) {
            x = o.x; y = o.y; z = o.z;
        }
        public void Set(double fX, double fY, double fZ) {
            x = fX; y = fY; z = fZ;
        }
        public void Add(Vector3d o) {
            x += o.x; y += o.y; z += o.z;
        }
        public void Subtract(Vector3d o) {
            x -= o.x; y -= o.y; z -= o.z;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3d operator -(Vector3d v) {
            return new Vector3d(-v.x, -v.y, -v.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3d operator *(double f, Vector3d v) {
            return new Vector3d(f * v.x, f * v.y, f * v.z);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3d operator *(Vector3d v, double f) {
            return new Vector3d(f * v.x, f * v.y, f * v.z);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3d operator /(Vector3d v, double f) {
            double invF = 1.0 / f;
            return new Vector3d(v.x * invF, v.y * invF, v.z * invF);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3d operator /(double f, Vector3d v) {
            return new Vector3d(f / v.x, f / v.y, f / v.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3d operator *(Vector3d a, Vector3d b) {
            return new Vector3d(a.x * b.x, a.y * b.y, a.z * b.z);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3d operator /(Vector3d a, Vector3d b) {
            return new Vector3d(a.x / b.x, a.y / b.y, a.z / b.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3d operator +(Vector3d v0, Vector3d v1) {
            return new Vector3d(v0.x + v1.x, v0.y + v1.y, v0.z + v1.z);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3d operator +(Vector3d v0, double f) {
            return new Vector3d(v0.x + f, v0.y + f, v0.z + f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3d operator -(Vector3d v0, Vector3d v1) {
            return new Vector3d(v0.x - v1.x, v0.y - v1.y, v0.z - v1.z);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3d operator -(Vector3d v0, double f) {
            return new Vector3d(v0.x - f, v0.y - f, v0.z - f);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Vector3d a, Vector3d b) {
            return (a.x == b.x && a.y == b.y && a.z == b.z);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Vector3d a, Vector3d b) {
            return (a.x != b.x || a.y != b.y || a.z != b.z);
        }
        public override bool Equals(object obj) {
            return (obj is Vector3d) ? (this == (Vector3d)obj) : false;
        }
        public override int GetHashCode() {
            unchecked {
                int hash = (int)2166136261;
                hash = (hash * 16777619) ^ x.GetHashCode();
                hash = (hash * 16777619) ^ y.GetHashCode();
                hash = (hash * 16777619) ^ z.GetHashCode();
                return hash;
            }
        }
        public int CompareTo(Vector3d other) {
            if (x != other.x)
                return x < other.x ? -1 : 1;
            else if (y != other.y)
                return y < other.y ? -1 : 1;
            else if (z != other.z)
                return z < other.z ? -1 : 1;
            return 0;
        }
        public bool Equals(Vector3d other) {
            return (x == other.x && y == other.y && z == other.z);
        }


        public readonly bool EpsilonEqual(Vector3d v2, double epsilon) {
            return Math.Abs(x - v2.x) <= epsilon &&
                   Math.Abs(y - v2.y) <= epsilon &&
                   Math.Abs(z - v2.z) <= epsilon;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3d Lerp(Vector3d a, Vector3d b, double t) {
            double s = 1 - t;
            return new Vector3d(s * a.x + t * b.x, s * a.y + t * b.y, s * a.z + t * b.z);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3d Lerp(ref Vector3d a, ref Vector3d b, double t) {
            double s = 1 - t;
            return new Vector3d(s * a.x + t * b.x, s * a.y + t * b.y, s * a.z + t * b.z);
        }


        public override string ToString() {
            return string.Format("{0:F8} {1:F8} {2:F8}", x, y, z);
        }
        public string ToString(string fmt) {
            return string.Format("{0} {1} {2}", x.ToString(fmt), y.ToString(fmt), z.ToString(fmt));
        }


        public static implicit operator Vector3d(Vector3f v) {
            return new Vector3d(v.x, v.y, v.z);
        }
        public static explicit operator Vector3f(Vector3d v) {
            return new Vector3f((float)v.x, (float)v.y, (float)v.z);
        }

#if G3_USING_UNITY
        public static implicit operator Vector3d(UnityEngine.Vector3 v)
        {
            return new Vector3d(v.x, v.y, v.z);
        }
        public static explicit operator Vector3(Vector3d v)
        {
            return new Vector3((float)v.x, (float)v.y, (float)v.z);
        }
#endif


        // complicated functions go down here...

        public static double Orthonormalize(ref Vector3d u, ref Vector3d v, ref Vector3d w) {
            double minLength = u.Normalize();

            double dot0 = u.Dot(v);
            v -= dot0 * u;
            double l = v.Normalize();
            if (l < minLength)
                minLength = l;

            double dot1 = v.Dot(w);
            dot0 = u.Dot(w);
            w -= dot0 * u + dot1 * v;
            l = w.Normalize();
            if (l < minLength)
                minLength = l;

            return minLength;
        }


        public static void GenerateComplementBasis(ref Vector3d u, ref Vector3d v, Vector3d w) {
            double invLength;

            if (Math.Abs(w.x) >= Math.Abs(w.y)) {
                invLength = MathUtil.InvSqrt(w.x * w.x + w.z * w.z);
                u.x = -w.z * invLength;
                u.y = 0;
                u.z = +w.x * invLength;
                v.x = w.y * u.z;
                v.y = w.z * u.x - w.x * u.z;
                v.z = -w.y * u.x;
            }
            else {
                invLength = MathUtil.InvSqrt(w.y * w.y + w.z * w.z);
                u.x = 0;
                u.y = +w.z * invLength;
                u.z = -w.y * invLength;
                v.x = w.y * u.z - w.z * u.y;
                v.y = -w.x * u.z;
                v.z = w.x * u.y;
            }
        }

        public static double ComputeOrthogonalComplement(int numInputs, Vector3d v0, ref Vector3d v1, ref Vector3d v2) {
            if (numInputs == 1) {
                if (Math.Abs(v0[0]) > Math.Abs(v0[1])) {
                    v1 = new Vector3d(-v0[2], 0.0, +v0[0]);
                }
                else {
                    v1 = new Vector3d(0.0, +v0[2], -v0[1]);
                }
                numInputs = 2;
            }

            if (numInputs == 2) {
                v2 = Vector3d.Cross(v0, v1);
                return Vector3d.Orthonormalize(ref v0, ref v1, ref v2);
            }

            return 0;
        }

        public static void MakePerpVectors(Vector3d n, out Vector3d b1, out Vector3d b2) {
            if (n.z < 0.0) {
                double a = 1.0 / (1.0 - n.z);
                double b = n.x * n.y * a;
                b1.x = 1.0f - n.x * n.x * a;
                b1.y = -b;
                b1.z = n.x;
                b2.x = b;
                b2.y = n.y * n.y * a - 1.0f;
                b2.z = -n.y;
            }
            else {
                double a = 1.0 / (1.0 + n.z);
                double b = -n.x * n.y * a;
                b1.x = 1.0 - n.x * n.x * a;
                b1.y = b;
                b1.z = -n.x;
                b2.x = b;
                b2.y = 1.0 - n.y * n.y * a;
                b2.z = -n.y;
            }
        }
        public static void MakePerpVectors(ref Vector3d n, out Vector3d b1, out Vector3d b2) {
            MakePerpVectors(n, out b1, out b2);
        }

        public static bool TryParse(string s, out Vector3d result) {
            result = default;
            if (MathUtil.TryParseRealVector(s, 3, out Vector4d result4)) {
                result = new Vector3d(result4.x, result4.y, result4.z);
                return true;
            }
            return false;
        }

    }

    // Keep the JsonConverter as is
    public class Vector3dJsonConverter : JsonConverter<g3.Vector3d> {
#nullable enable
        public override g3.Vector3d Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
            JsonNode? node = JsonNode.Parse(ref reader);
            if (node == null)
                return Vector3d.Zero;

            string? value = node["Vector3d"]?.GetValue<string>() ?? null;
            if (value == null)
                return Vector3d.Zero;

            string[] values = value.Split(' ', StringSplitOptions.TrimEntries);
            if (values.Length != 3)
                return Vector3d.Zero;

            double x = 0, y = 0, z = 0;
            double.TryParse(values[0], out x);
            double.TryParse(values[1], out y);
            double.TryParse(values[2], out z);
            return new Vector3d(x, y, z);
        }

        public override void Write(Utf8JsonWriter writer, g3.Vector3d value, JsonSerializerOptions options) {
            writer.WriteStartObject();
            writer.WriteString("Vector3d", $"{value.x} {value.y} {value.z}");
            writer.WriteEndObject();
        }
#nullable disable
    }

}