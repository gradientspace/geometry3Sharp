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


		//! return this color with a different alpha value
		public readonly Colorb NewAlpha(byte newAlpha) {
			return new Colorb(r, g, b, newAlpha);
		}

		public static explicit operator Colorf(Colorb c)
		{
			return new Colorf(c.r, c.g, c.b, c.a);
		}


		public static Colorb Lerp(Colorb a, Colorb b, double t)
		{
			return Colorf.Lerp((Colorf)a, (Colorf)b, (float)t).ToBytes();
		}



		static public readonly Colorb TransparentWhite = new Colorb(255, 255, 255, 0);
		static public readonly Colorb TransparentBlack = new Colorb(0, 0, 0, 0);


		static public readonly Colorb White = new Colorb(255, 255, 255, 255);
		static public readonly Colorb Black = new Colorb(0, 0, 0, 255);
		static public readonly Colorb Blue = new Colorb(0, 0, 255, 255);
		static public readonly Colorb Green = new Colorb(0, 255, 0, 255);
		static public readonly Colorb Red = new Colorb(255, 0, 0, 255);
		static public readonly Colorb Yellow = new Colorb(255, 255, 0, 255);
		static public readonly Colorb Cyan = new Colorb(0, 255, 255, 255);
		static public readonly Colorb Magenta = new Colorb(255, 0, 255, 255);

		static public readonly Colorb VideoWhite = new Colorb(235, 235, 235, 255);
		static public readonly Colorb VideoBlack = new Colorb(16, 16, 16, 255);
		static public readonly Colorb VideoBlue = new Colorb(16, 16, 235, 255);
		static public readonly Colorb VideoGreen = new Colorb(16, 235, 16, 255);
		static public readonly Colorb VideoRed = new Colorb(235, 16, 16, 255);
		static public readonly Colorb VideoYellow = new Colorb(235, 235, 16, 255);
		static public readonly Colorb VideoCyan = new Colorb(16, 235, 235, 255);
		static public readonly Colorb VideoMagenta = new Colorb(235, 16, 235, 255);


		static public readonly Colorb Purple = new Colorb(161, 16, 193, 255);
		static public readonly Colorb DarkRed = new Colorb(128, 16, 16, 255);
		static public readonly Colorb FireBrick = new Colorb(178, 34, 34, 255);
		static public readonly Colorb HotPink = new Colorb(255, 105, 180, 255);
		static public readonly Colorb LightPink = new Colorb(255, 182, 193, 255);

		static public readonly Colorb DarkBlue = new Colorb(16, 16, 139, 255);
		static public readonly Colorb BlueMetal = new Colorb(176, 197, 235, 255);       // I made this one up...
		static public readonly Colorb Navy = new Colorb(16, 16, 128, 255);
		static public readonly Colorb CornflowerBlue = new Colorb(100, 149, 237, 255);
		static public readonly Colorb LightSteelBlue = new Colorb(176, 196, 222, 255);
		static public readonly Colorb DarkSlateBlue = new Colorb(72, 61, 139, 255);

		static public readonly Colorb Teal = new Colorb(16, 128, 128, 255);
		static public readonly Colorb ForestGreen = new Colorb(16, 139, 16, 255);
		static public readonly Colorb LightGreen = new Colorb(144, 238, 144, 255);

		static public readonly Colorb Orange = new Colorb(230, 73, 16, 255);
		static public readonly Colorb Gold = new Colorb(235, 115, 63, 255);
		static public readonly Colorb DarkYellow = new Colorb(235, 200, 95, 255);

		static public readonly Colorb SiennaBrown = new Colorb(160, 82, 45, 255);
		static public readonly Colorb SaddleBrown = new Colorb(139, 69, 19, 255);
		static public readonly Colorb Goldenrod = new Colorb(218, 165, 32, 255);
		static public readonly Colorb Wheat = new Colorb(245, 222, 179, 255);



		static public readonly Colorb LightGrey = new Colorb(211, 211, 211, 255);
		static public readonly Colorb Silver = new Colorb(192, 192, 192, 255);
		static public readonly Colorb LightSlateGrey = new Colorb(119, 136, 153, 255);
		static public readonly Colorb Grey = new Colorb(128, 128, 128, 255);
		static public readonly Colorb DarkGrey = new Colorb(169, 169, 169, 255);
		static public readonly Colorb SlateGrey = new Colorb(112, 128, 144, 255);
		static public readonly Colorb DimGrey = new Colorb(105, 105, 105, 255);
		static public readonly Colorb DarkSlateGrey = new Colorb(47, 79, 79, 255);



		// default colors
		static readonly public Colorf StandardBeige = new Colorf(0.75f, 0.75f, 0.5f);
		static readonly public Colorf SelectionGold = new Colorf(1.0f, 0.6f, 0.05f);
		static readonly public Colorf PivotYellow = new Colorf(1.0f, 1.0f, 0.05f);




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
