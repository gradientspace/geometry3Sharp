using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    public class ColorHSV
    {
        public float h;
        public float s;
        public float v;
        public float a;

        public ColorHSV(float h, float s, float v, float a = 1) { this.h = h; this.s = s; this.v = v; this.a = a; }
        public ColorHSV(Colorf rgb) {
            ConvertFromRGB(rgb);
        }



        public Colorf RGBA {
            get {
                return ConvertToRGB();
            }
            set { ConvertFromRGB(value); }
        }



        public Colorf ConvertToRGB()
        {
            float h = this.h;
            float s = this.s;
            float v = this.v;

            if (h > 360)
                h -= 360;
            if (h < 0)
                h += 360;
            h = MathUtil.Clamp(h, 0.0f, 360.0f);
            s = MathUtil.Clamp(s, 0.0f, 1.0f);
            v = MathUtil.Clamp(v, 0.0f, 1.0f);
            float c = v * s;
            float x = c * (1 - Math.Abs( ((h / 60.0f) % 2) - 1) );
            float m = v - c;
            float rp, gp, bp;
            int a = (int)(h / 60.0f);

            switch (a) {
                case 0:
                    rp = c;
                    gp = x;
                    bp = 0;
                    break;

                case 1:
                    rp = x;
                    gp = c;
                    bp = 0;
                    break;

                case 2:
                    rp = 0;
                    gp = c;
                    bp = x;
                    break;

                case 3:
                    rp = 0;
                    gp = x;
                    bp = c;
                    break;

                case 4:
                    rp = x;
                    gp = 0;
                    bp = c;
                    break;

                default: // case 5:
                    rp = c;
                    gp = 0;
                    bp = x;
                    break;
            }

            return new Colorf(
                MathUtil.Clamp(rp + m,0,1), 
                MathUtil.Clamp(gp + m,0,1), 
                MathUtil.Clamp(bp + m,0,1), this.a);
        }


        public void ConvertFromRGB(Colorf rgb)
        {
            this.a = rgb.a;
            float rp = rgb.r, gp = rgb.g, bp = rgb.b;

            float cmax = rp;
            int cmaxwhich = 0; /* faster comparison afterwards */
            if (gp > cmax) { cmax = gp; cmaxwhich = 1; }
            if (bp > cmax) { cmax = bp; cmaxwhich = 2; }
            float cmin = rp;
            //int cminwhich = 0;
            if (gp < cmin) { cmin = gp; /*cminwhich = 1;*/ }
            if (bp < cmin) { cmin = bp; /*cminwhich = 2;*/ }

            float delta = cmax - cmin;

            /* HUE */
            if (delta == 0) {
                this.h = 0;
            } else {
                switch (cmaxwhich) {
                    case 0: /* cmax == rp */
                        h = 60.0f * ( ((gp - bp) / delta) % 6.0f );
                        break;

                    case 1: /* cmax == gp */
                        h = 60.0f * (((bp - rp) / delta) + 2);
                        break;

                    case 2: /* cmax == bp */
                        h = 60.0f * (((rp - gp) / delta) + 4);
                        break;
                }
                if (h < 0)
                    h += 360.0f;
            }

            /* LIGHTNESS/VALUE */
            //l = (cmax + cmin) / 2;
            v = cmax;

            /* SATURATION */
            /*if (delta == 0) {
              *r_s = 0;
            } else {
              *r_s = delta / (1 - fabs (1 - (2 * (l - 1))));
            }*/
            if (cmax == 0) {
                s = 0;
            } else {
                s = delta / cmax;
            }
        }

    }
}
