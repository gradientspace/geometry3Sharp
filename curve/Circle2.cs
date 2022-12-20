using System;

namespace g3
{
    public class Circle2d : IParametricCurve2d
    {
		public Vector2d Center;
		public double Radius;
		public bool IsReversed;		// use ccw orientation instead of cw

        public Circle2d(double radius) {
            IsReversed = false;
            Center = Vector2d.Zero;
            Radius = radius;
        }

		public Circle2d(Vector2d center, double radius)
		{
			IsReversed = false;
			Center = center;
			Radius = radius;
		}


        public double Curvature
        {
            get { return 1.0 / Radius; }
        }
        public double SignedCurvature
        {
            get { return (IsReversed) ? (-1.0 / Radius) : (1.0 / Radius); }
        }


		public bool IsClosed {
			get { return true; }
		}

		public void Reverse() {
			IsReversed = ! IsReversed;
		}

        public IParametricCurve2d Clone() {
            return new Circle2d(this.Center, this.Radius) 
                { IsReversed = this.IsReversed };
        }

        public bool IsTransformable { get { return true; } }
        public void Transform(ITransform2 xform)
        {
            Center = xform.TransformP(Center);
            Radius = xform.TransformScalar(Radius);
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
            if (IsReversed)
                tangent = -tangent;
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
            double d = Center.DistanceSquared(p);
            return d <= Radius * Radius;
        }


        public double Circumference {
			get { return MathUtil.TwoPI * Radius; }
            set { Radius = value / MathUtil.TwoPI; }
		}
        public double Diameter {
			get { return 2 * Radius; }
            set { Radius = value / 2; }
		}
        public double Area {
            get { return Math.PI * Radius * Radius; }
            set { Radius = Math.Sqrt(value / Math.PI); }
        }


		public AxisAlignedBox2d Bounds {
			get { return new AxisAlignedBox2d(Center, Radius, Radius); }
		}

        public double SignedDistance(Vector2d pt)
        {
            double d = Center.Distance(pt);
            return d - Radius;
        }
        public double Distance(Vector2d pt)
        {
            double d = Center.Distance(pt);
            return Math.Abs(d - Radius);
        }



        public static double RadiusArea(double r) {
            return Math.PI * r * r;
        }
        public static double RadiusCircumference(double r) {
            return MathUtil.TwoPI * r;
        }

        /// <summary>
        /// Radius of n-sided regular polygon that contains circle of radius r
        /// </summary>
        public static double BoundingPolygonRadius(double r, int n) {
            double theta = (MathUtil.TwoPI / (double)n) / 2.0;
            return r / Math.Cos(theta);
        }
    }
}
