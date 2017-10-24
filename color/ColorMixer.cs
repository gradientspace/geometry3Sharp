using System;
using System.Collections.Generic;


namespace g3
{
    public class ColorMixer
    {



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
