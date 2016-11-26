using System;
using System.Collections.Generic;
using System.Text;

#if G3_USING_UNITY
using UnityEngine;
#endif

namespace g3
{
    public class Colorf
    {
        public float[] v = { 0, 0, 0, 1 };

        public Colorf() { }
        public Colorf(float greylevel, float a = 1) { v[0] = v[1] = v[2] = greylevel; v[3] = 1.0f; }
        public Colorf(float r, float g, float b, float a = 1) { v[0] = r; v[1] = g; v[2] = b; v[3] = a; }
        public Colorf(int r, int g, int b, int a = 255) {
            v[0] = MathUtil.Clamp((float)r, 0.0f, 255.0f) / 255.0f;
            v[1] = MathUtil.Clamp((float)g, 0.0f, 255.0f) / 255.0f;
            v[2] = MathUtil.Clamp((float)b, 0.0f, 255.0f) / 255.0f;
            v[3] = MathUtil.Clamp((float)a, 0.0f, 255.0f) / 255.0f;
        }
        public Colorf(float[] v2) { v[0] = v2[0]; v[1] = v2[1]; v[2] = v2[2]; v[3] = v2[3]; }
        public Colorf(Colorf copy) { v[0] = copy.v[0]; v[1] = copy.v[1]; v[2] = copy.v[2]; v[3] = copy.v[3]; }


        public float r
        {
            get { return v[0]; }
            set { v[0] = value; }
        }
        public float g
        {
            get { return v[1]; }
            set { v[1] = value; }
        }
        public float b
        {
            get { return v[2]; }
            set { v[2] = value; }
        }
        public float this[int key]
        {
            get { return v[key]; }
            set { v[key] = value; }
        }

        public float SqrDistance(Colorf v2)
        {
            float a = (v[0] - v2[0]), b = (v[1] - v2[1]), c = (v[2] - v2[2]), d = (v[3] - v2[3]);
            return a * a + b * b + c * c + d*d;
        }


        public Vector3f ToRGB() {
            return new Vector3f(v[0], v[1], v[2]);
        }
        public byte[] ToBytes() {
            return new byte[4] {
                (byte)MathUtil.Clamp((int)(v[0]*255.0f), 0, 255),
                (byte)MathUtil.Clamp((int)(v[1]*255.0f), 0, 255),
                (byte)MathUtil.Clamp((int)(v[2]*255.0f), 0, 255),
                (byte)MathUtil.Clamp((int)(v[3]*255.0f), 0, 255),
            };
        }

        public void Set(Colorf o)
        {
            v[0] = o[0]; v[1] = o[1]; v[2] = o[2]; v[3] = o[3];
        }
        public void Set(float fR, float fG, float fB, float fA)
        {
            v[0] = fR; v[1] = fG; v[2] = fB; v[3] = fA;
        }
        public Colorf SetAlpha(float a) {
            v[3] = a;
            return this;
        }
        public void Add(Colorf o)
        {
            v[0] += o[0]; v[1] += o[1]; v[2] += o[2]; v[3] += o[3];
        }
        public void Subtract(Colorf o)
        {
            v[0] -= o[0]; v[1] -= o[1]; v[2] -= o[2]; v[3] -= o[3];
        }



        public static Colorf operator -(Colorf v)
        {
            return new Colorf(-v[0], -v[1], -v[2], -v[3]);
        }

        public static Colorf operator *(float f, Colorf v)
        {
            return new Colorf(f * v[0], f * v[1], f * v[2], f * v[3]);
        }
        public static Colorf operator *(Colorf v, float f)
        {
            return new Colorf(f * v[0], f * v[1], f * v[2], f * v[3]);
        }

        public static Colorf operator +(Colorf v0, Colorf v1)
        {
            return new Colorf(v0[0] + v1[0], v0[1] + v1[1], v0[2] + v1[2], v0[3] + v1[3]);
        }
        public static Colorf operator +(Colorf v0, float f)
        {
            return new Colorf(v0[0] + f, v0[1] + f, v0[2] + f, v0[3] + f);
        }

        public static Colorf operator -(Colorf v0, Colorf v1)
        {
            return new Colorf(v0[0] - v1[0], v0[1] - v1[1], v0[2] - v1[2], v0[3]-v1[3]);
        }
        public static Colorf operator -(Colorf v0, float f)
        {
            return new Colorf(v0[0] - f, v0[1] - f, v0[2] - f, v0[3] = f);
        }


        public override string ToString()
        {
            return string.Format("{0:F8} {1:F8} {2:F8} {3:F8}", v[0], v[1], v[2], v[3]);
        }
        public virtual string ToString(string fmt)
        {
            return string.Format("{0} {1} {2} {3}", v[0].ToString(fmt), v[1].ToString(fmt), v[2].ToString(fmt), v[3].ToString(fmt));
        }



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
        static public readonly Colorf Orange = new Colorf(230, 73, 16, 255);
        static public readonly Colorf Gold = new Colorf(235, 115, 63, 255);
        static public readonly Colorf DarkYellow = new Colorf(235, 200, 95, 255);
        static public readonly Colorf BlueMetal = new Colorf(176, 197, 235, 255);



#if G3_USING_UNITY
        public static implicit operator Colorf(UnityEngine.Color c)
        {
            return new Colorf(c.r, c.g, c.b, c.a);
        }
        public static implicit operator Color(Colorf c)
        {
            return new Color(c[0], c[1], c[2], c[3]);
        }
#endif

    }
}
