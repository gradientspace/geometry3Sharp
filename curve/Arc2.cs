using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3 {

	public struct Arc2d : IParametricCurve2d
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



		public bool HasArcLength { get {return true;} }

		public double ArcLength {
			get {
				return (AngleEndDeg-AngleStartDeg) * MathUtil.Deg2Rad * Radius;
			}
		}

		public Vector2d SampleArcLength(double a) {
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

	}
}
