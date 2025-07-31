using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace g3
{
    public static class RandomSampling
    {

        public static Vector2d PointInBox2(AxisAlignedBox2d box, ref Random random, int max_rejections = 10)
        {
            AxisAlignedBox2d square = new AxisAlignedBox2d(box.Center, box.MaxDim/2.0);
            
            for (int i = 0; i < max_rejections; ++i ) {
                double tx = random.NextDouble();
                double ty = random.NextDouble();
                Vector2d pt = square.SampleT(tx, ty);
                if (box.Contains(pt))
                    return pt;
            }

            // if rejection samples failed, fall back to just lerping inside square, which is
            // still random but biased
			double sx = random.NextDouble();
			double sy = random.NextDouble();
            return box.SampleT(sx, sy);
		}


        public static Vector2d PointOnCircle2(Vector2d center, double Radius, ref Random random)
        {
            double AngleRad = random.NextDouble() * MathUtil.TwoPI;
            return new Vector2d(Radius * Math.Cos(AngleRad), Radius * Math.Sin(AngleRad));
        }

    }
}
