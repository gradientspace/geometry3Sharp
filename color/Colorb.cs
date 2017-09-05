using System;

#if G3_USING_UNITY
using UnityEngine;
#endif

namespace g3
{
    public struct Colorb
    {
        public byte r;
        public byte g;
        public byte b;
        public byte a;

        public Colorb(byte greylevel, byte a = 1) { r = g = b = greylevel; this.a = a; }
        public Colorb(byte r, byte g, byte b, byte a = 1) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public Colorb(float r, float g, float b, float a = 1.0f) {
            this.r = (byte)MathUtil.Clamp((int)(r * 255.0f), 0, 255);
            this.g = (byte)MathUtil.Clamp((int)(g * 255.0f), 0, 255);
            this.b = (byte)MathUtil.Clamp((int)(b * 255.0f), 0, 255);
            this.a = (byte)MathUtil.Clamp((int)(a * 255.0f), 0, 255);
        }
        public Colorb(byte[] v2) { r = v2[0]; g = v2[1]; b = v2[2]; a = v2[3]; }
        public Colorb(Colorb copy) { r = copy.r; g = copy.g; b = copy.b; a = copy.a; }
        public Colorb(Colorb copy, byte newAlpha) { r = copy.r; g = copy.g; b = copy.b; a = newAlpha; }


        public byte this[int key]
        {
            get { if (key == 0) return r; else if (key == 1) return g; else if (key == 2) return b; else return a; }
            set { if (key == 0) r = value; else if (key == 1) g = value; else if (key == 2) b = value; else a = value; }
        }


#if G3_USING_UNITY
        public static implicit operator Colorb(UnityEngine.Color32 c)
        {
            return new Colorb(c.r, c.g, c.b, c.a);
        }
        public static implicit operator Color32(Colorb c)
        {
            return new Color32(c.r, c.g, c.b, c.a);
        }
#endif

    }
}
