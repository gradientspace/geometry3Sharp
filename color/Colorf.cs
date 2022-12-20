using System;
using System.Collections.Generic;
using System.Text;

#if G3_USING_UNITY
using UnityEngine;
#endif

namespace g3
{
    public struct Colorf : IComparable<Colorf>, IEquatable<Colorf>
    {
        public float r;
        public float g;
        public float b;
        public float a;

        public Colorf(float greylevel, float a = 1) { r = g = b = greylevel; this.a = a; }
        public Colorf(float r, float g, float b, float a = 1) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public Colorf(int r, int g, int b, int a = 255) {
            this.r = MathUtil.Clamp((float)r, 0.0f, 255.0f) / 255.0f;
            this.g = MathUtil.Clamp((float)g, 0.0f, 255.0f) / 255.0f;
            this.b = MathUtil.Clamp((float)b, 0.0f, 255.0f) / 255.0f;
            this.a = MathUtil.Clamp((float)a, 0.0f, 255.0f) / 255.0f;
        }
        public Colorf(float[] v2) { r = v2[0]; g = v2[1]; b = v2[2]; a = v2[3]; }
        public Colorf(Colorf copy) { r = copy.r; g = copy.g; b = copy.b; a = copy.a; }
        public Colorf(Colorf copy, float newAlpha) { r = copy.r; g = copy.g; b = copy.b; a = newAlpha; }


        public Colorf Clone(float fAlphaMultiply = 1.0f) {
            return new Colorf(r, g, b, a * fAlphaMultiply);
        }


        public float this[int key]
        {
            get { if (key == 0) return r; else if (key == 1) return g; else if (key == 2) return b; else return a; }
            set { if (key == 0) r = value; else if (key == 1) g = value; else if (key == 2) b = value; else a = value; }
        }

        public float SqrDistance(Colorf v2)
        {
            float a = (r - v2.r), b = (g - v2.g), c = (b - v2.b), d = (a - v2.a);
            return a * a + b * b + c * c + d*d;
        }


        public Vector3f ToRGB() {
            return new Vector3f(r, g, b);
        }
        public Colorb ToBytes() {
            return new Colorb(r, g, b, a);
        }

        public void Set(Colorf o)
        {
            r = o.r; g = o.g; b = o.b; a = o.a;
        }
        public void Set(float fR, float fG, float fB, float fA)
        {
            r = fR; g = fG; b = fB; a = fA;
        }
        public Colorf SetAlpha(float a) {
            this.a = a;
            return this;
        }
        public void Add(Colorf o)
        {
            r += o.r; g += o.g; b += o.b; a += o.a;
        }
        public void Subtract(Colorf o)
        {
            r -= o.r; g -= o.g; b -= o.b; a -= o.a;
        }
        public Colorf WithAlpha(float newAlpha)
        {
            return new Colorf(r, g, b, newAlpha);
        }


        public static Colorf operator -(Colorf v)
        {
            return new Colorf(-v.r, -v.g, -v.b, -v.a);
        }

        public static Colorf operator *(float f, Colorf v)
        {
            return new Colorf(f * v.r, f * v.g, f * v.b, f * v.a);
        }
        public static Colorf operator *(Colorf v, float f)
        {
            return new Colorf(f * v.r, f * v.g, f * v.b, f * v.a);
        }

        public static Colorf operator +(Colorf v0, Colorf v1)
        {
            return new Colorf(v0.r + v1.r, v0.g + v1.g, v0.b + v1.b, v0.a + v1.a);
        }
        public static Colorf operator +(Colorf v0, float f)
        {
            return new Colorf(v0.r + f, v0.g + f, v0.b + f, v0.a + f);
        }

        public static Colorf operator -(Colorf v0, Colorf v1)
        {
            return new Colorf(v0.r - v1.r, v0.g - v1.g, v0.b - v1.b, v0.a-v1.a);
        }
        public static Colorf operator -(Colorf v0, float f)
        {
            return new Colorf(v0.r - f, v0.g - f, v0.b - f, v0.a = f);
        }


        public static bool operator ==(Colorf a, Colorf b)
        {
            return (a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a);
        }
        public static bool operator !=(Colorf a, Colorf b)
        {
            return (a.r != b.r || a.g != b.g || a.b != b.b || a.a != b.a);
        }
        public override bool Equals(object obj)
        {
            return this == (Colorf)obj;
        }
        public override int GetHashCode()
        {
            return (r+g+b+a).GetHashCode();
        }
        public int CompareTo(Colorf other)
        {
            if (r != other.r)
                return r < other.r ? -1 : 1;
            else if (g != other.g)
                return g < other.g ? -1 : 1;
            else if (b != other.b)
                return b < other.b ? -1 : 1;
            else if (a != other.a)
                return a < other.a ? -1 : 1;
            return 0;
        }
        public bool Equals(Colorf other)
        {
            return (r == other.r && g == other.g && b == other.b && a == other.a);
        }


        public static Colorf Lerp(Colorf a, Colorf b, float t) {
            float s = 1 - t;
            return new Colorf(s * a.r + t * b.r, s * a.g + t * b.g, s * a.b + t * b.b, s * a.a + t * b.a);
        }



        public override string ToString()
        {
            return string.Format("{0:F8} {1:F8} {2:F8} {3:F8}", r, g, b, a);
        }
        public string ToString(string fmt)
        {
            return string.Format("{0} {1} {2} {3}", r.ToString(fmt), g.ToString(fmt), b.ToString(fmt), a.ToString(fmt));
        }


        static public readonly Colorf TransparentWhite = new Colorf(255, 255, 255, 0);
        static public readonly Colorf TransparentBlack = new Colorf(0, 0, 0, 0);


        static public readonly Colorf White = new Colorf(255, 255, 255, 255);
        static public readonly Colorf Black = new Colorf(0, 0, 0, 255);
        static public readonly Colorf Blue = new Colorf(0, 0, 255, 255);
        static public readonly Colorf Green = new Colorf(0, 255, 0, 255);
        static public readonly Colorf Red = new Colorf(255, 0, 0, 255);
        static public readonly Colorf Yellow = new Colorf(255, 255, 0, 255);
        static public readonly Colorf Cyan = new Colorf(0, 255, 255, 255);
        static public readonly Colorf Magenta = new Colorf(255, 0, 255, 255);

        static public readonly Colorf VideoWhite = new Colorf(235, 235, 235, 255);
        static public readonly Colorf VideoBlack = new Colorf(16, 16, 16, 255);
        static public readonly Colorf VideoBlue = new Colorf(16, 16, 235, 255);
        static public readonly Colorf VideoGreen = new Colorf(16, 235, 16, 255);
        static public readonly Colorf VideoRed = new Colorf(235, 16, 16, 255);
        static public readonly Colorf VideoYellow = new Colorf(235, 235, 16, 255);
        static public readonly Colorf VideoCyan = new Colorf(16, 235, 235, 255);
        static public readonly Colorf VideoMagenta = new Colorf(235, 16, 235, 255);


        static public readonly Colorf Purple = new Colorf(161, 16, 193, 255);
        static public readonly Colorf DarkRed = new Colorf(128, 16, 16, 255);
        static public readonly Colorf FireBrick = new Colorf(178, 34, 34, 255);
        static public readonly Colorf HotPink = new Colorf(255, 105, 180, 255);
        static public readonly Colorf LightPink = new Colorf(255, 182, 193, 255);

        static public readonly Colorf DarkBlue = new Colorf(16, 16, 139, 255);
        static public readonly Colorf BlueMetal = new Colorf(176, 197, 235, 255);       // I made this one up...
        static public readonly Colorf Navy = new Colorf(16, 16, 128, 255);
        static public readonly Colorf CornflowerBlue = new Colorf(100, 149, 237, 255);
        static public readonly Colorf LightSteelBlue = new Colorf(176, 196, 222, 255);
        static public readonly Colorf DarkSlateBlue = new Colorf(72, 61, 139, 255);

        static public readonly Colorf Teal = new Colorf(16, 128, 128, 255);
        static public readonly Colorf ForestGreen = new Colorf(16, 139, 16, 255);
        static public readonly Colorf LightGreen = new Colorf(144, 238, 144, 255);

        static public readonly Colorf Orange = new Colorf(230, 73, 16, 255);
        static public readonly Colorf Gold = new Colorf(235, 115, 63, 255);
        static public readonly Colorf DarkYellow = new Colorf(235, 200, 95, 255);

        static public readonly Colorf SiennaBrown = new Colorf(160, 82,  45, 255);
        static public readonly Colorf SaddleBrown = new Colorf(139,  69,  19, 255);
        static public readonly Colorf Goldenrod = new Colorf(218, 165,  32, 255);
        static public readonly Colorf Wheat = new Colorf(245, 222, 179, 255);



        static public readonly Colorf LightGrey = new Colorf(211, 211, 211, 255);
        static public readonly Colorf Silver = new Colorf(192, 192, 192, 255);
        static public readonly Colorf LightSlateGrey = new Colorf(119, 136, 153, 255);
        static public readonly Colorf Grey = new Colorf(128, 128, 128, 255);
        static public readonly Colorf DarkGrey = new Colorf(169, 169, 169, 255);
        static public readonly Colorf SlateGrey = new Colorf(112, 128, 144, 255);
        static public readonly Colorf DimGrey = new Colorf(105, 105, 105, 255);
        static public readonly Colorf DarkSlateGrey = new Colorf(47,  79,  79, 255);



        // default colors
        static readonly public Colorf StandardBeige = new Colorf(0.75f, 0.75f, 0.5f);
        static readonly public Colorf SelectionGold = new Colorf(1.0f, 0.6f, 0.05f);
        static readonly public Colorf PivotYellow = new Colorf(1.0f, 1.0f, 0.05f);



        // allow conversion to/from Vector3f
        public static implicit operator Vector3f(Colorf c)
        {
            return new Vector3f(c.r, c.g, c.b);
        }
        public static implicit operator Colorf(Vector3f c)
        {
            return new Colorf(c.x, c.y, c.z, 1);
        }



#if G3_USING_UNITY
        public static implicit operator Colorf(UnityEngine.Color c)
        {
            return new Colorf(c.r, c.g, c.b, c.a);
        }
        public static implicit operator Color(Colorf c)
        {
            return new Color(c.r, c.g, c.b, c.a);
        }
        public static implicit operator Color32(Colorf c)
        {
            Colorb cb = c.ToBytes();
            return new Color32(cb.r, cb.g, cb.b, cb.a);
        }
#endif

    }
}
