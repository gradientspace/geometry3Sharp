using System;
using System.Collections.Generic;


namespace g3
{
    public class ColorMixer
    {



        public static Colorf CopyHue(Colorf BaseColor, Colorf TakeHue, float Alpha)
        {
            ColorHSV baseHSV = new ColorHSV(BaseColor);
            ColorHSV takeHSV = new ColorHSV(TakeHue);
            baseHSV.h = takeHSV.h;
            baseHSV.s = MathUtil.Lerp(baseHSV.s, takeHSV.s, Alpha);
            baseHSV.v = MathUtil.Lerp(baseHSV.v, takeHSV.v, Alpha);
            return baseHSV.ConvertToRGB();
        }

    }
}
