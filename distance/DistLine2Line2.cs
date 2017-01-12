using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
	// ported from WildMagic 5 
	// https://www.geometrictools.com/Downloads/Downloads.html

	public class DistLine2Line2
	{
		Line2d line1;
		public Line2d Line
		{
			get { return line1; }
			set { line1 = value; DistanceSquared = -1.0; }
		}

		Line2d line2;
		public Line2d Line2
		{
			get { return line2; }
			set { line2 = value; DistanceSquared = -1.0; }
		}

		public double DistanceSquared = -1.0;

		public Vector2d Line1Closest;
        public Vector2d Line2Closest;
		public double Line1Parameter;
		public double Line2Parameter;


		public DistLine2Line2( Line2d Line1, Line2d Line2)
		{
			this.line2 = Line2; this.line1 = Line1;
		}

		static public double MinDistance(Line2d line1, Line2d line2)
		{
			return new DistLine2Line2( line1, line2 ).Get();
		}


		public DistLine2Line2 Compute()
		{
			GetSquared();
			return this;
		}

		public double Get()
		{
			return Math.Sqrt( GetSquared() );
		}


		public double GetSquared()
		{
			if (DistanceSquared >= 0)
				return DistanceSquared;

            Vector2d diff = line1.Origin - line2.Origin;
            double a01 = -line1.Direction.Dot(line2.Direction);
            double b0 = diff.Dot(line1.Direction);
            double c = diff.LengthSquared;
            double det = Math.Abs(1.0 - a01*a01);
            double b1, s0, s1, sqrDist;

            if (det >= MathUtil.ZeroTolerance)
            {
                // Lines are not parallel.
                b1 = -diff.Dot(line2.Direction);
                double invDet = ((double)1)/det;
                s0 = (a01*b1 - b0)*invDet;
                s1 = (a01*b0 - b1)*invDet;
                sqrDist = (double)0;
            }
            else
            {
                // Lines are parallel, select any closest pair of points.
                s0 = -b0;
                s1 = (double)0;
                sqrDist = b0*s0 + c;

                // Account for numerical round-off errors.
                if (sqrDist < (double)0)
                    sqrDist = (double)0;
            }

            Line1Parameter = s0;
            Line1Closest = line1.Origin + s0*line1.Direction;
            Line2Parameter = s1;
            Line2Closest = line2.Origin + s1*line2.Direction;

            DistanceSquared = sqrDist;
			return sqrDist;
		}
	}

}
