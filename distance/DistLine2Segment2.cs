using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
	// ported from WildMagic 5 
	// https://www.geometrictools.com/Downloads/Downloads.html

	public class DistLine2Segment2
	{
		Line2d line;
		public Line2d Line
		{
			get { return line; }
			set { line = value; DistanceSquared = -1.0; }
		}

		Segment2d segment;
		public Segment2d Segment
		{
			get { return segment; }
			set { segment = value; DistanceSquared = -1.0; }
		}

		public double DistanceSquared = -1.0;

		public Vector2d LineClosest;
		public double LineParameter;
		public Vector2d SegmentClosest;
		public double SegmentParameter;


		public DistLine2Segment2( Line2d LineIn, Segment2d SegmentIn )
		{
			this.segment = SegmentIn; this.line = LineIn;
		}

		static public double MinDistance(Line2d line, Segment2d segment)
		{
			return new DistLine2Segment2( line, segment ).Get();
		}


		public DistLine2Segment2 Compute()
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

            Vector2d diff = line.Origin - segment.Center;
            double a01 = -line.Direction.Dot(segment.Direction);
            double b0 = diff.Dot(line.Direction);
            double c = diff.LengthSquared;
            double det = Math.Abs((double)1 - a01 * a01);
            double b1, s0, s1, sqrDist, extDet;

            if (det >= MathUtil.ZeroTolerance) {
                // The line and segment are not parallel.
                b1 = -diff.Dot(segment.Direction);
                s1 = a01 * b0 - b1;
                extDet = segment.Extent * det;

                if (s1 >= -extDet) {
                    if (s1 <= extDet) {
                        // Two interior points are closest, one on the line and one
                        // on the segment.
                        double invDet = ((double)1) / det;
                        s0 = (a01 * b1 - b0) * invDet;
                        s1 *= invDet;
                        sqrDist = (double)0;
                    } else {
                        // The endpoint e1 of the segment and an interior point of
                        // the line are closest.
                        s1 = segment.Extent;
                        s0 = -(a01 * s1 + b0);
                        sqrDist = -s0 * s0 + s1 * (s1 + ((double)2) * b1) + c;
                    }
                } else {
                    // The endpoint e0 of the segment and an interior point of the
                    // line are closest.
                    s1 = -segment.Extent;
                    s0 = -(a01 * s1 + b0);
                    sqrDist = -s0 * s0 + s1 * (s1 + ((double)2) * b1) + c;
                }
            } else {
                // The line and segment are parallel.  Choose the closest pair so that
                // one point is at segment origin.
                s1 = (double)0;
                s0 = -b0;
                sqrDist = b0 * s0 + c;
            }

            LineParameter = s0;
            LineClosest = line.Origin + s0 * line.Direction;
            SegmentParameter = s1;
		    SegmentClosest = segment.Center + s1 * segment.Direction;

            // Account for numerical round-off errors
            if (sqrDist < (double)0) 
                sqrDist = (double)0;

            DistanceSquared = sqrDist;
			return sqrDist;
		}
	}

}
