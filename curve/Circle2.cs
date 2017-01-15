using System;

namespace g3
{
    public class Circle2d : IParametricCurve2d
    {
		public Vector2d Center;
		public double Radius;
		public bool IsReversed;		// use ccw orientation instead of cw

		public Circle2d(Vector2d center, double radius)
		{
			IsReversed = false;
			Center = center;
			Radius = radius;
		}


		public bool IsClosed {
			get { return true; }
		}

		public void Reverse() {
			IsReversed = ! IsReversed;
		}


		// angle in range [0,360] (but works for any value, obviously)
        public Vector2d SampleDeg(double degrees)
        {
            double theta = degrees * MathUtil.Deg2Rad;
            double c = Math.Cos(theta), s = Math.Sin(theta);
			return new Vector2d(Center.x + Radius*c, Center.y + Radius*s);
        }

		// angle in range [0,2pi] (but works for any value, obviously)
        public Vector2d SampleRad(double radians)
        {
            double c = Math.Cos(radians), s = Math.Sin(radians);
			return new Vector2d(Center.x + Radius*c, Center.y + Radius*s);
        }


		public double ParamLength {
			get { return 1.0f; }
		}

		// t in range[0,1] spans circle [0,2pi]
		public Vector2d SampleT(double t) {
			double theta = (IsReversed) ? -t*MathUtil.TwoPI : t*MathUtil.TwoPI;
			double c = Math.Cos(theta), s = Math.Sin(theta);
			return new Vector2d(Center.x + Radius*c, Center.y + Radius*s);
		}

        public Vector2d TangentT(double t)
        {
			double theta = (IsReversed) ? -t*MathUtil.TwoPI : t*MathUtil.TwoPI;
            Vector2d tangent = new Vector2d(-Math.Sin(theta), Math.Cos(theta));
            tangent.Normalize();
            return tangent;
        }


		public bool HasArcLength { get {return true;} }

		public double ArcLength {
			get {
				return MathUtil.TwoPI * Radius;
			}
		}

		public Vector2d SampleArcLength(double a) {
			double t = a / ArcLength;
			double theta = (IsReversed) ? -t*MathUtil.TwoPI : t*MathUtil.TwoPI;
			double c = Math.Cos(theta), s = Math.Sin(theta);
			return new Vector2d(Center.x + Radius*c, Center.y + Radius*s);
		}


        public bool Contains (Vector2d p ) {
            double d = Center.SquaredDist(p);
            return d <= Radius * Radius;
        }


        public double Circumference {
			get { return MathUtil.TwoPI * Radius; }
		}
        public double Diameter {
			get { return 2 * Radius; }
		}
        public double Area {
            get { return Math.PI * Radius * Radius; }
        }


        public double SignedDistance(Vector2d pt)
        {
            double d = Center.Dist(pt);
            return d - Radius;
        }
        public double Distance(Vector2d pt)
        {
            double d = Center.Dist(pt);
            return Math.Abs(d - Radius);
        }

    }
}
