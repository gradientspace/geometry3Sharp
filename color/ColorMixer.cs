using System;
using System.Collections.Generic;


namespace g3
{
    public static class ColorMixer
    {

        public static Colorf Lighten(Colorf baseColor, float fValueMult = 1.25f)
        {
            ColorHSV baseHSV = new ColorHSV(baseColor);
            baseHSV.v = MathUtil.Clamp(baseHSV.v * fValueMult, 0.0f, 1.0f);
            return baseHSV.ConvertToRGB();
        }

        public static Colorf Darken(Colorf baseColor, float fValueMult = 0.75f)
        {
            ColorHSV baseHSV = new ColorHSV(baseColor);
            baseHSV.v *= fValueMult;
            return baseHSV.ConvertToRGB();
        }


        public static Colorf CopyHue(Colorf BaseColor, Colorf TakeHue, float fBlendAlpha)
        {
            ColorHSV baseHSV = new ColorHSV(BaseColor);
            ColorHSV takeHSV = new ColorHSV(TakeHue);
            baseHSV.h = takeHSV.h;
            baseHSV.s = MathUtil.Lerp(baseHSV.s, takeHSV.s, fBlendAlpha);
            baseHSV.v = MathUtil.Lerp(baseHSV.v, takeHSV.v, fBlendAlpha);
            return baseHSV.ConvertToRGB();
        }

    }
}
