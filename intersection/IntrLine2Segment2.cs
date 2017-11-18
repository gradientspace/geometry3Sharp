using System;
using System.Diagnostics;

namespace g3
{
    // ported from WildMagic5
    //
    //
    // double IntervalThreshold
    // 		The intersection testing uses the center-extent form for line segments.
    // 		If you start with endpoints (Vector2<Real>) and create Segment2<Real>
    // 		objects, the conversion to center-extent form can contain small
    // 		numerical round-off errors.  Testing for the intersection of two
    // 		segments that share an endpoint might lead to a failure due to the
    // 		round-off errors.  To allow for this, you may specify a small positive
    // 		threshold that slightly enlarges the intervals for the segments.  The
    // 		default value is zero.
    //
    // double DotThreshold
    // 		The computation for determining whether the linear components are
    // 		parallel might contain small floating-point round-off errors.  The
    // 		default threshold is MathUtil.ZeroTolerance.  If you set the value,
    // 		pass in a nonnegative number.
    //
    //
    // The intersection set.  Let q = Quantity.  The cases are
    //
    //   q = 0: The line/segment do not intersect.  Type is Empty
    //
    //   q = 1: The line/segment intersect in a single point. Type is Point
    //          Intersection point is Point0.
    //          
    //   q = int.MaxValue:  The line/segment are collinear.  Type is Segment.


    public class IntrLine2Segment2
    {
        Line2d line;
        public Line2d Line {
            get { return line; }
            set { line = value; Result = IntersectionResult.NotComputed; }
        }

        Segment2d segment;
        public Segment2d Segment {
            get { return segment; }
            set { segment = value; Result = IntersectionResult.NotComputed; }
        }

        double intervalThresh = 0;
        public double IntervalThreshold {
            get { return intervalThresh; }
            set { intervalThresh = Math.Max(value, 0); Result = IntersectionResult.NotComputed; }
        }

        double dotThresh = MathUtil.ZeroTolerance;
        public double DotThreshold {
            get { return dotThresh; }
            set { dotThresh = Math.Max(value, 0); Result = IntersectionResult.NotComputed; }
        }

        public int Quantity = 0;
        public IntersectionResult Result = IntersectionResult.NotComputed;
        public IntersectionType Type = IntersectionType.Empty;

        public bool IsSimpleIntersection {
            get { return Result == IntersectionResult.Intersects && Type == IntersectionType.Point; }
        }

        /// <summary> Point on line, only set if Quantity = 1    </summary>
        public Vector2d Point;

        /// <summary>Parameter along line, only set if Quanityt = 1    </summary>
        public double Parameter;

        public IntrLine2Segment2(Line2d line, Segment2d seg)
        {
            this.line = line; segment = seg;
        }

        public IntrLine2Segment2 Compute()
        {
            Find();
            return this;
        }


        public bool Find()
        {
            if (Result != IntersectionResult.NotComputed)
                return (Result == IntersectionResult.Intersects);

            // [RMS] if either segment direction is not a normalized vector, 
            //   results are garbage, so fail query
            if (line.Direction.IsNormalized == false || segment.Direction.IsNormalized == false) {
                Type = IntersectionType.Empty;
                Result = IntersectionResult.InvalidQuery;
                return false;
            }

            Vector2d s = Vector2d.Zero;
            Type = IntrLine2Line2.Classify(line.Origin, line.Direction,
                                           segment.Center, segment.Direction,
                                           dotThresh, ref s);

            if (Type == IntersectionType.Point) {

                // Test whether the line-line intersection is on the segment.
                if (Math.Abs(s[1]) <= segment.Extent + intervalThresh) {
                    Quantity = 1;
                    Point = line.Origin + s[0] * line.Direction;
                    Parameter = s[0];
                } else {
                    Quantity = 0;
                    Type = IntersectionType.Empty;
                }

            } else if (Type == IntersectionType.Line) {
                Type = IntersectionType.Segment;
                Quantity = int.MaxValue;
            } else {
                Quantity = 0;
            }

            Result = (Type != IntersectionType.Empty) ?
                IntersectionResult.Intersects : IntersectionResult.NoIntersection;

            return (Result == IntersectionResult.Intersects);
        }
    }
}
