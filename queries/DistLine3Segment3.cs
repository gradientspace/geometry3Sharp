using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
	// ported from WildMagic 5 
	// https://www.geometrictools.com/Downloads/Downloads.html

	public class DistLine3Segment3
	{
		Line3d line;
		public Line3d Line
		{
			get { return line; }
			set { line = value; DistanceSquared = -1.0; }
		}

		Segment3d segment;
		public Segment3d Segment
		{
			get { return segment; }
			set { segment = value; DistanceSquared = -1.0; }
		}

		public double DistanceSquared = -1.0;

		public Vector3d LineClosest;
		public double LineParameter;
		public Vector3d SegmentClosest;
		public double SegmentParameter;


		public DistLine3Segment3( Line3d LineIn, Segment3d SegmentIn)
		{
			this.segment = SegmentIn; this.line = LineIn;
		}

		static public double MinDistance(Line3d line, Segment3d segment)
		{
			return new DistLine3Segment3( line, segment ).Get();
		}
		static public double MinDistanceLineParam( Line3d line, Segment3d segment )
		{
			return new DistLine3Segment3( line, segment ).Compute().LineParameter;
		}


		public DistLine3Segment3 Compute()
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

            Vector3d diff = line.Origin - segment.Center;
            double a01 = -line.Direction.Dot(segment.Direction);
            double b0 = diff.Dot(line.Direction);
            double c = diff.LengthSquared;
            double det = Math.Abs(1 - a01 * a01);
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
                        double invDet = (1) / det;
                        s0 = (a01 * b1 - b0) * invDet;
                        s1 *= invDet;
                        sqrDist = s0 * (s0 + a01 * s1 + (2) * b0) +
                            s1 * (a01 * s0 + s1 + (2) * b1) + c;
                    } else {
                        // The endpoint e1 of the segment and an interior point of
                        // the line are closest.
                        s1 = segment.Extent;
                        s0 = -(a01 * s1 + b0);
                        sqrDist = -s0 * s0 + s1 * (s1 + (2) * b1) + c;
                    }
                } else {
                    // The end point e0 of the segment and an interior point of the
                    // line are closest.
                    s1 = -segment.Extent;
                    s0 = -(a01 * s1 + b0);
                    sqrDist = -s0 * s0 + s1 * (s1 + (2) * b1) + c;
                }
            } else {
                // The line and segment are parallel.  Choose the closest pair so that
                // one point is at segment center.
                s1 = 0;
                s0 = -b0;
                sqrDist = b0 * s0 + c;
            }

            LineClosest = line.Origin + s0 * line.Direction;
            SegmentClosest = segment.Center + s1 * segment.Direction;
            LineParameter = s0;
            SegmentParameter = s1;

            // Account for numerical round-off errors.
            if (sqrDist < 0) {
                sqrDist = 0;
            }

            DistanceSquared = sqrDist;
			return sqrDist;
		}
	}

}
