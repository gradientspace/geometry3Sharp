using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace g3 {

	public class Arc2d : IParametricCurve2d
	{
		public Vector2d Center;
		public double Radius;
		public double AngleStartDeg;
		public double AngleEndDeg;
		public bool IsReversed;		// use ccw orientation instead of cw
		

		public Arc2d(Vector2d center, double radius, double startDeg, double endDeg)
		{
			IsReversed = false;
			Center = center;
			Radius = radius;
			AngleStartDeg = startDeg;
			AngleEndDeg = endDeg;
			if ( AngleEndDeg < AngleStartDeg )
				AngleEndDeg += 360;

			// [TODO] handle full arcs, which should be circles?
		}


        /// <summary>
        /// Create Arc around center, **clockwise** from start to end points.
        /// Points must both be the same distance from center (ie on circle)
        /// </summary>
        public Arc2d(Vector2d vCenter, Vector2d vStart, Vector2d vEnd)
        {
            IsReversed = false;
            SetFromCenterAndPoints(vCenter, vStart, vEnd);
        }


        /// <summary>
        /// Initialize Arc around center, **clockwise** from start to end points.
        /// Points must both be the same distance from center (ie on circle)
        /// </summary>
        public void SetFromCenterAndPoints(Vector2d vCenter, Vector2d vStart, Vector2d vEnd)
        {
            Vector2d ds = vStart - vCenter;
            Vector2d de = vEnd - vCenter;
            Debug.Assert(Math.Abs(ds.LengthSquared - de.LengthSquared) < MathUtil.ZeroTolerancef);
            AngleStartDeg = Math.Atan2(ds.y, ds.x) * MathUtil.Rad2Deg;
            AngleEndDeg = Math.Atan2(de.y, de.x) * MathUtil.Rad2Deg;
            if (AngleEndDeg < AngleStartDeg)
                AngleEndDeg += 360;
            Center = vCenter;
            Radius = ds.Length;
        }



		public Vector2d P0 {
			get { return SampleT(0.0); }
		}
		public Vector2d P1 {
			get { return SampleT(1.0); }
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
			get { return false; }
		}


		public double ParamLength {
			get { return 1.0f; }
		}


		// t in range[0,1] spans arc
		public Vector2d SampleT(double t) {
			double theta = (IsReversed) ?
				(1-t)*AngleEndDeg + (t)*AngleStartDeg : 
				(1-t)*AngleStartDeg + (t)*AngleEndDeg;
			theta = theta * MathUtil.Deg2Rad;
			double c = Math.Cos(theta), s = Math.Sin(theta);
			return new Vector2d(Center.x + Radius*c, Center.y + Radius*s);
		}


        public Vector2d TangentT(double t)
        {
			double theta = (IsReversed) ?
				(1-t)*AngleEndDeg + (t)*AngleStartDeg : 
				(1-t)*AngleStartDeg + (t)*AngleEndDeg;
			theta = theta * MathUtil.Deg2Rad;
            Vector2d tangent = new Vector2d(-Math.Sin(theta), Math.Cos(theta));
            if (IsReversed)
                tangent = -tangent;
            tangent.Normalize();
            return tangent;
        }


		public bool HasArcLength { get {return true;} }

		public double ArcLength {
			get {
				return (AngleEndDeg-AngleStartDeg) * MathUtil.Deg2Rad * Radius;
			}
		}

		public Vector2d SampleArcLength(double a) {
            if (ArcLength < MathUtil.Epsilon)
                return (a < 0.5) ? SampleT(0) : SampleT(1);
			double t = a / ArcLength;
			double theta = (IsReversed) ?
				(1-t)*AngleEndDeg + (t)*AngleStartDeg : 
				(1-t)*AngleStartDeg + (t)*AngleEndDeg;
			theta = theta * MathUtil.Deg2Rad;
			double c = Math.Cos(theta), s = Math.Sin(theta);
			return new Vector2d(Center.x + Radius*c, Center.y + Radius*s);
		}

		public void Reverse() {
			IsReversed = ! IsReversed;
		}

        public IParametricCurve2d Clone() {
            return new Arc2d(this.Center, this.Radius, this.AngleStartDeg, this.AngleEndDeg) 
                { IsReversed = this.IsReversed };
        }


        public bool IsTransformable { get { return true; } }
        public void Transform(ITransform2 xform)
        {
            Vector2d vCenter = xform.TransformP(Center);
            Vector2d vStart = xform.TransformP((IsReversed) ? P1 : P0);
            Vector2d vEnd = xform.TransformP((IsReversed) ? P0 : P1);

            SetFromCenterAndPoints(vCenter, vStart, vEnd);
        }



        public AxisAlignedBox2d Bounds {
            get {
                // extrema of arc are P0, P1, and any axis-crossings that lie in arc span.
                // We can compute bounds of axis-crossings in normalized space and then scale/translate.
                int k = (int)(AngleStartDeg / 90.0);
                if (k * 90 < AngleStartDeg) 
                    k++;
                int stop_k = (int)(AngleEndDeg / 90);       
                if (stop_k * 90 > AngleEndDeg)
                    stop_k--;
                // [TODO] we should only ever need to check at most 4 here, right? then we have gone a circle...
                AxisAlignedBox2d bounds = AxisAlignedBox2d.Empty;
                while (k <= stop_k) {
                    int i = k++ % 4;
                    bounds.Contain(bounds_dirs[i]);
                }
                bounds.Scale(Radius); bounds.Translate(Center);
                bounds.Contain(P0); bounds.Contain(P1);
                return bounds;
            }
        }
        private static readonly Vector2d[] bounds_dirs = new Vector2d[4] {
            Vector2d.AxisX, Vector2d.AxisY, -Vector2d.AxisX, -Vector2d.AxisY };




        public double Distance(Vector2d point)
        {
            Vector2d PmC = point - Center;
            double lengthPmC = PmC.Length;
            if (lengthPmC > MathUtil.Epsilon) {
                Vector2d dv = PmC / lengthPmC;
				double theta = Math.Atan2(dv.y, dv.x) * MathUtil.Rad2Deg;
				if ( ! (theta >= AngleStartDeg && theta <= AngleEndDeg) ) {
					double ctheta = MathUtil.ClampAngleDeg(theta, AngleStartDeg, AngleEndDeg);
                    double radians = ctheta * MathUtil.Deg2Rad;
					double c = Math.Cos(radians), s = Math.Sin(radians);
                    Vector2d pos = new Vector2d(Center.x + Radius * c, Center.y + Radius * s);
					return pos.Distance(point);
                } else {
					return Math.Abs(lengthPmC - Radius);
                }
            } else {
                return Radius;
            }
        }


        public Vector2d NearestPoint(Vector2d point)
        {
            Vector2d PmC = point - Center;
            double lengthPmC = PmC.Length;
            if (lengthPmC > MathUtil.Epsilon) {
                Vector2d dv = PmC / lengthPmC;
                double theta = Math.Atan2(dv.y, dv.x);
                theta *= MathUtil.Rad2Deg;
                theta = MathUtil.ClampAngleDeg(theta, AngleStartDeg, AngleEndDeg);
                theta = MathUtil.Deg2Rad * theta;
                double c = Math.Cos(theta), s = Math.Sin(theta);
                return new Vector2d(Center.x + Radius * c, Center.y + Radius * s);
            } else 
                return SampleT(0.5);        // all points equidistant
        }


	}
}
