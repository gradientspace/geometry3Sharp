using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
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


        // currently fit is computed in constructor
        public BiArcFit2(Vector2d point1, Vector2d tangent1, Vector2d point2, Vector2d tangent2)
        {
            Point1 = point1; Tangent1 = tangent1;
            Point2 = point2; Tangent2 = tangent2;
            Fit();

            if (arc1.IsSegment) {
                Arc1IsSegment = true;
                Segment1 = new Segment2d(arc1.P0, arc1.P1);
            } else {
                Arc1IsSegment = false;
                Arc1 = get_arc(0);
            }

            if (arc2.IsSegment) {
                Arc2IsSegment = true;
                Segment2 = new Segment2d(arc2.P0, arc2.P1);
            } else {
                Arc2IsSegment = false;
                Arc2 = get_arc(1);
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

            return new Arc2d(a.Center, a.Radius, start_deg, end_deg);
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


        void Fit()
        {
            // get inputs
            Vector2d p1 = Point1;
            Vector2d p2 = Point2;

            Vector2d t1 = Tangent1;
            Vector2d t2 = Tangent2;

            //double d1_min = -500;
            //double d1_max = 500;
            //double d1 = d1_min + (d1_max - d1_min) * ToNumber_Safe(document.getElementById('d1_frac').value) / 100.0;
            //var d1_min = ToNumber_Safe(document.getElementById('d1_min').value);
            //var d1_max = ToNumber_Safe(document.getElementById('d1_max').value);
            //var d1 = d1_min + (d1_max - d1_min) * ToNumber_Safe(document.getElementById('d1_frac').value) / 100.0;

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
                Vector2d joint = p1 + 0.5 * v;

                // d1 = d2 = infinity here...

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

                Vector2d joint = p1 + p2 + d1 * (t1 - t2);
                joint *= 0.5;

                // construct arcs
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




    }
}
