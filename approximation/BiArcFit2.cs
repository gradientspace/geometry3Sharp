using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
	//
	// 2D Biarc fitting ported from http://www.ryanjuckett.com/programming/biarc-interpolation/
	//
	//
    public class BiArcFit2
    {
        public Vector2d Point1;
        public Vector2d Point2;
        public Vector2d Tangent1;
        public Vector2d Tangent2;

        // original code used 0.0001 here...
        public double Epsilon = MathUtil.ZeroTolerance;

        public Arc2d Arc1;
        public Arc2d Arc2;

        public bool Arc1IsSegment;
        public bool Arc2IsSegment;

        public Segment2d Segment1;
        public Segment2d Segment2;

		// these are the computed d1 and d2 parameters. By default you will,
		// get d1==d2, unless you specify d1 in the second constructor
		public double FitD1;
		public double FitD2;


        // compute standard biarc fit with d1==d2
        public BiArcFit2(Vector2d point1, Vector2d tangent1, Vector2d point2, Vector2d tangent2)
        {
            Point1 = point1; Tangent1 = tangent1;
            Point2 = point2; Tangent2 = tangent2;
            Fit();
			set_output();
        }

		// advanced biarc fit with specified d1. Note that d1 can technically be any value, but
		// outside of some range you will get nonsense (like 2 full circles, etc). A reasonable
		// strategy is to compute the default fit first (d1==d2), then it seems like d1 can safely be in
		// the range [0,2*first_d1]. This will vary the length of the two arcs, and for some d1 you
		// will almost always get a better fit.
		public BiArcFit2(Vector2d point1, Vector2d tangent1, Vector2d point2, Vector2d tangent2, double d1)
		{
			Point1 = point1; Tangent1 = tangent1;
			Point2 = point2; Tangent2 = tangent2;
			Fit(d1);
			set_output();
		}


		void set_output() {
			if (arc1.IsSegment) {
				Arc1IsSegment = true;
				Segment1 = new Segment2d(arc1.P0, arc1.P1);
			} else {
				Arc1IsSegment = false;
				Arc1 = get_arc(0);
			}

			if (arc2.IsSegment) {
				Arc2IsSegment = true;
				Segment2 = new Segment2d(arc2.P1, arc2.P0);
			} else {
				Arc2IsSegment = false;
				Arc2 = get_arc(1);
			}
		}



        public double Distance(Vector2d point)
        {
            double d0 = (Arc1IsSegment) ?
                Math.Sqrt(Segment1.DistanceSquared(point)) : Arc1.Distance(point);
            double d1 = (Arc2IsSegment) ?
                Math.Sqrt(Segment2.DistanceSquared(point)) : Arc2.Distance(point);
            return Math.Min(d0, d1);
        }
        public Vector2d NearestPoint(Vector2d point)
        {
            Vector2d n1 = (Arc1IsSegment) ?
                Segment1.NearestPoint(point) : Arc1.NearestPoint(point);
            Vector2d n2 = (Arc2IsSegment) ?
                Segment2.NearestPoint(point) : Arc2.NearestPoint(point);
            return (n1.DistanceSquared(point) < n2.DistanceSquared(point)) ? n1 : n2;
        }

        public List<IParametricCurve2d> Curves {
            get {
				IParametricCurve2d c1 = (Arc1IsSegment) ? (IParametricCurve2d)Segment1 : (IParametricCurve2d)Arc1;
				IParametricCurve2d c2 = (Arc2IsSegment) ? (IParametricCurve2d)Segment2 : (IParametricCurve2d)Arc2;
				return new List<IParametricCurve2d>() { c1, c2 };
            }
        }

        public IParametricCurve2d Curve1 {
            get {
                return (Arc1IsSegment) ? (IParametricCurve2d)Segment1 : (IParametricCurve2d)Arc1;
            }
        }
        public IParametricCurve2d Curve2 {
            get {
                return (Arc2IsSegment) ? (IParametricCurve2d)Segment2 : (IParametricCurve2d)Arc2;
            }
        }



        struct Arc
        {
            public Vector2d Center;
            public double Radius;
            public double AngleStartR;
            public double AngleEndR;
            public bool PositiveRotation;

            public bool IsSegment;
            public Vector2d P0, P1;

            public Arc(Vector2d c, double r, double startR, double endR, bool posRotation)
            {
                Center = c;
                Radius = r;
                AngleStartR = startR;
                AngleEndR = endR;
                PositiveRotation = posRotation;
                IsSegment = false;
                P0 = P1 = Vector2d.Zero;
            }

            public Arc(Vector2d p0, Vector2d p1)
            {
                Center = Vector2d.Zero;
                Radius = AngleStartR = AngleEndR = 0;
                PositiveRotation = false;
                IsSegment = true;
                P0 = p0; P1 = p1;
            }
        }


        Arc arc1, arc2;

        void set_arc(int i, Arc a)
        {
            if (i == 0)
                arc1 = a;
            else
                arc2 = a;
        }


        Arc2d get_arc(int i)
        {
            Arc a = (i == 0) ? arc1 : arc2;
            double start_deg = a.AngleStartR * MathUtil.Rad2Deg;
            double end_deg = a.AngleEndR * MathUtil.Rad2Deg;
            if ( a.PositiveRotation == true ) {
                double tmp = start_deg;
                start_deg = end_deg;
                end_deg = tmp;
            }
            Arc2d arc = new Arc2d(a.Center, a.Radius, start_deg, end_deg);

            // [RMS] code above does not preserve CW/CCW of arcs. 
            //  It would be better to fix that. But for now, just check if
            //  we preserved start and end points, and if not reverse curves.
            if ( i == 0 && arc.SampleT(0.0).DistanceSquared(Point1) > MathUtil.ZeroTolerance ) 
                arc.Reverse();
            if (i == 1 && arc.SampleT(1.0).DistanceSquared(Point2) > MathUtil.ZeroTolerance)
                arc.Reverse();

            return arc;
        }




        // [TODO]
        //    - we could get a better fit to the original curve if we use the ability to have separate
        //      d1 and d2 values. There is a point where d1 > 0 and d2 > 0 where we will get a best-fit.
        //
        //      if d1==0, the first arc degenerates. The second arc degenerates when d2 == 0.
        //      If either d1 or d2 go negative, then we shouldn't use that result. 
        //      But it's not clear if we can directly compute the positive-d-range...
        //
        //      It does seem like if we solve the d1=d2 case, then we use [0,2*d1] as the d1 range,
        //      then maybe we are safe. And we can always discard solutions where it is negative...


		// solve biarc fit where the free parameter is automatically set so that
		// d1=d2, which is basically the 'middle' case
        void Fit()
        {
            // get inputs
            Vector2d p1 = Point1;
            Vector2d p2 = Point2;

            Vector2d t1 = Tangent1;
            Vector2d t2 = Tangent2;

            // fit biarc
            Vector2d v = p2 - p1;
            double vMagSqr = v.LengthSquared;

            // set d1 equal to d2
            Vector2d t = t1 + t2;
            double tMagSqr = t.LengthSquared;

            // original code used 0.0001 here...
            bool equalTangents = MathUtil.EpsilonEqual(tMagSqr, 4.0, Epsilon);
            //var equalTangents = IsEqualEps(tMagSqr, 4.0);

            double vDotT1 = v.Dot(t1);
            bool perpT1 = MathUtil.EpsilonEqual(vDotT1, 0.0, Epsilon );
            if (equalTangents && perpT1) {
                // we have two semicircles
                //Vector2d joint = p1 + 0.5 * v;

                // d1 = d2 = infinity here...
				FitD1 = FitD2 = double.PositiveInfinity;

                // draw arcs
                double angle = Math.Atan2(v.y, v.x);
                Vector2d center1 = p1 + 0.25 * v;
                Vector2d center2 = p1 + 0.75 * v;
                double radius = Math.Sqrt(vMagSqr) * 0.25;
                double cross = v.x * t1.y - v.y * t1.x;

                arc1 = new Arc(center1, radius, angle, angle + Math.PI, (cross < 0));
                arc1 = new Arc(center2, radius, angle, angle + Math.PI, (cross > 0));

            } else {
                double vDotT = v.Dot(t);

                // [RMS] this was unused in original code...
                //bool perpT1 = MathUtil.EpsilonEqual(vDotT1, 0, epsilon);

                double d1 = 0;
                if (equalTangents) {
                    d1 = vMagSqr / (4 * vDotT1);
                } else {
                    double denominator = 2.0 - 2.0 * t1.Dot(t2);
                    double discriminant = vDotT*vDotT + denominator * vMagSqr;
                    d1 = (Math.Sqrt(discriminant) - vDotT) / denominator;
                }
				FitD1 = FitD2 = d1;

                Vector2d joint = p1 + p2 + d1 * (t1 - t2);
                joint *= 0.5;

                // construct arcs
                SetArcFromEdge(0, p1, t1, joint, true);
                SetArcFromEdge(1, p2, t2, joint, false);
            }

        }



		// This is a variant of Fit() where the d1 value is specified.
		// Note: has not been tested extensively, particularly the special case
		// where one of the arcs beomes a semi-circle...
		void Fit(double d1) {
			
			Vector2d p1 = Point1;
			Vector2d p2 = Point2;

			Vector2d t1 = Tangent1;
			Vector2d t2 = Tangent2;

			// fit biarc
			Vector2d v = p2 - p1;
			double vMagSqr = v.LengthSquared;

			// set d1 equal to d2
			Vector2d t = t1 + t2;
			double tMagSqr = t.LengthSquared;

			double vDotT1 = v.Dot(t1);

			double vDotT2 =  v.Dot(t2);
			double t1DotT2 = t1.Dot(t2);
			double denominator = (vDotT2 - d1*(t1DotT2 - 1.0));

			if ( MathUtil.EpsilonEqual(denominator, 0.0, MathUtil.ZeroTolerancef) ) {
				// the second arc is a semicircle

				FitD1 = d1;
				FitD2 = double.PositiveInfinity;

				Vector2d joint = p1 + d1*t1;
				joint += (vDotT2 - d1*t1DotT2) * t2;

				// construct arcs
				// [TODO] this might not be right for semi-circle...
				SetArcFromEdge(0, p1, t1, joint, true);
				SetArcFromEdge(1, p2, t2, joint, false);

			} else {
				double d2 = (0.5*vMagSqr - d1*vDotT1) / denominator;
				double invLen = 1.0 / (d1 + d2);

				Vector2d joint = (d1*d2) * (t1 - t2);
				joint += d1*p2;
				joint += d2*p1;
				joint *= invLen;

				FitD1 = d1;
				FitD2 = d2;

				// draw arcs
				SetArcFromEdge(0, p1, t1, joint, true);
				SetArcFromEdge(1, p2, t2, joint, false);
			}


		}






        void SetArcFromEdge(int i, Vector2d p1, Vector2d t1, Vector2d p2, bool fromP1)
        {
            Vector2d chord = p2 - p1;
	        Vector2d n1 = new Vector2d(-t1.y, t1.x);
            double chordDotN1 = chord.Dot(n1);

	        if (MathUtil.EpsilonEqual(chordDotN1,0,Epsilon)) {
		        // straight line case
                set_arc(i, new Arc(p1, p2));

	        } else {
		        double radius = chord.LengthSquared / (2.0*chordDotN1);
                Vector2d center = p1 + radius * n1;

                Vector2d p1Offset = p1 - center;
                Vector2d p2Offset = p2 - center;
		
		        var p1Ang1 = Math.Atan2(p1Offset.y, p1Offset.x);
		        var p2Ang1 = Math.Atan2(p2Offset.y, p2Offset.x);
                if (p1Offset.x * t1.y - p1Offset.y * t1.x > 0)
                    set_arc(i, new Arc(center, Math.Abs(radius), p1Ang1, p2Ang1, !fromP1));
                else
                    set_arc(i, new Arc(center, Math.Abs(radius), p1Ang1, p2Ang1, fromP1));
	        }
        }




        public void DebugPrint()
        {
            System.Console.WriteLine("biarc fit Pt0 {0} Pt1 {1}  Tan0 {2} Tan1 {3}", Point1, Point2, Tangent1, Tangent2);
            System.Console.WriteLine("  First: Start {0} End {1}  {2}",
                (Arc1IsSegment) ? Segment1.P0 : Arc1.SampleT(0),
                (Arc1IsSegment) ? Segment1.P1 : Arc1.SampleT(1),
                (Arc1IsSegment) ? "segment" : "arc");
            System.Console.WriteLine("  Second: Start {0} End {1}  {2}",
                (Arc2IsSegment) ? Segment2.P0 : Arc2.SampleT(0),
                (Arc2IsSegment) ? Segment2.P1 : Arc2.SampleT(1),
                (Arc2IsSegment) ? "segment" : "arc");
        }



    }
}
