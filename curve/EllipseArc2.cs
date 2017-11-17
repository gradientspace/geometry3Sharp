using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3 {

	// [RMS] partial ellipse. 
	//   Note: Seems like there is something weird about start/end angles for elliptic arcs. 
	//   If I just evaluate the same way I would evaluate circular arc (eg lerp between angles),
	//   then the arc is too long (at least compared to how it "should" be in a .dxf file).
	//
	//   Currently the SampleT function corrects for this, based on formula from dxf.net.
	//   However possibly this is dxf-specific?
	//
	//   Possibly this is just inherent in the "angle" being used as parameter of the ellipse.
	//   Seems right at 0/90/180/270, but eg at t=45 degrees, the line to the point on the 
	//   ellipse will *not* be 45 deg from the x axis. So what do these angles mean, exactly??
	//
	// 	 This post http://adndevblog.typepad.com/autocad/2013/01/an-explanation-of-elliptical-arcs-dxf-group-code-41-for-lisp-and-ads-objectarx.html
	//   explains how to convert from a point on ellipse to "parametric angle", based
	//   on two concentric circles w/ radii of major/minor axes. Possibly that is what
	//   the formula in SampleT is doing?
	//
	public class EllipseArc2d : IParametricCurve2d
	{
		public Vector2d Center;
		public Vector2d Axis0, Axis1;
		public Vector2d Extent;
		public double AngleStartDeg;
		public double AngleEndDeg;
		public bool IsReversed;		// use ccw orientation instead of cw

		public EllipseArc2d(Vector2d center, double rotationAngleDeg, double extent0, double extent1,
		                 double startDeg, double endDeg) {
			Center = center;
			Matrix2d m = new Matrix2d(rotationAngleDeg * MathUtil.Deg2Rad);
			Axis0 = m * Vector2d.AxisX;
			Axis1 = m * Vector2d.AxisY;
			Extent = new Vector2d(extent0, extent1);
			IsReversed = false;
			AngleStartDeg = startDeg;
			AngleEndDeg = endDeg;
			if ( AngleEndDeg < AngleStartDeg )
				AngleEndDeg += 360;
		}

		public EllipseArc2d(Vector2d center, Vector2d axis0, Vector2d axis1, Vector2d extent,
		                 double startDeg, double endDeg) {
			Center = center;
            Axis0 = axis0;
            Axis1 = axis1;
            Extent = extent;
			IsReversed = false;
			AngleStartDeg = startDeg;
			AngleEndDeg = endDeg;
			if ( AngleEndDeg < AngleStartDeg )
				AngleEndDeg += 360;
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
			double cost = Math.Cos(theta);
			double sint = Math.Sin(theta);

			// [RMS] adapted this formula from dxf.net. 
			double a = this.Extent.x, b = this.Extent.y;
			double a1 = a * sint;
			double b1 = b * cost;
			double radius = (a*b) / Math.Sqrt(b1*b1 + a1*a1);
			Vector2d v = new Vector2d(radius*cost, radius*sint);
			return Center + v.x*Axis0 + v.y*Axis1;

			// standard formula that produces incorrect ellipses (??)
			//double c = Math.Cos(theta), s = Math.Sin(theta);
			//return Center + (Extent.x * c * Axis0) + (Extent.y * s * Axis1);
		}


		// t in range[0,1] spans ellipse
		public Vector2d TangentT(double t) {
			double theta = (IsReversed) ?
				(1-t)*AngleEndDeg + (t)*AngleStartDeg : 
				(1-t)*AngleStartDeg + (t)*AngleEndDeg;
			theta = theta * MathUtil.Deg2Rad;	
			double cost = Math.Cos(theta);
			double sint = Math.Sin(theta);

			// [RMS] adapted this formula from dxf.net. 
			double a = this.Extent.x, b = this.Extent.y;
			double a1 = a * sint;
			double b1 = b * cost;

            double k = a1 * a1 + b1 * b1;
            double d = Math.Sqrt(k);
            //double k1 = (-a * b * sint) * d;
            double ddt = 0.5 * (1 / d) * ((2 * a * a * sint * cost) - (2 * b * b * cost * sint));
            //double k2 = ddt * (a * b * cost);

            double dx = ( (-a * b * sint) * d  -  ddt * (a * b * cost) ) / k;
            double dy = ( ( a * b * cost) * d  -  ddt * (a * b * sint) ) / k;

            Vector2d tangent = dx * Axis0 + dy * Axis1;
            if (IsReversed)
                tangent = -tangent;
            tangent.Normalize();
            return tangent;
		}



		// [TODO] could use RombergIntegral like BaseCurve2, but need
		// first-derivative function

		public bool HasArcLength { get {return false;} }

		public double ArcLength {
			get { throw new NotImplementedException("Ellipse2.ArcLength"); }
		}

		public Vector2d SampleArcLength(double a) {
			throw new NotImplementedException("Ellipse2.SampleArcLength");
		}


		public void Reverse() {
			IsReversed = ! IsReversed;
		}

        public IParametricCurve2d Clone() {
            return new EllipseArc2d(this.Center, this.Axis0, this.Axis1, this.Extent, this.AngleStartDeg, this.AngleEndDeg)
                { IsReversed = this.IsReversed };
        }


        public bool IsTransformable { get { return true; } }
        public void Transform(ITransform2 xform)
        {
            Center = xform.TransformP(Center);
            Axis0 = xform.TransformN(Axis0);
            Axis1 = xform.TransformN(Axis1);
            Extent.x = xform.TransformScalar(Extent.x);
            Extent.y = xform.TransformScalar(Extent.y);
        }

    }
}
