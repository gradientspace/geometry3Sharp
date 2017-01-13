using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
	// ported from WildMagic 5 
	// https://www.geometrictools.com/Downloads/Downloads.html

	public class DistSegment2Segment2
	{
		Segment2d segment0;
		public Segment2d Segment1
		{
			get { return segment0; }
			set { segment0 = value; DistanceSquared = -1.0; }
		}

		Segment2d segment1;
		public Segment2d Segment2
		{
			get { return segment1; }
			set { segment1 = value; DistanceSquared = -1.0; }
		}

		public double DistanceSquared = -1.0;

		public Vector2d Segment1Closest;
        public Vector2d Segment2Closest;
		public double Segment1Parameter;
		public double Segment2Parameter;


		public DistSegment2Segment2( Segment2d Segment1, Segment2d Segment2)
		{
			this.segment1 = Segment2; this.segment0 = Segment1;
		}

		static public double MinDistance(Segment2d Segment1, Segment2d Segment2)
		{
			return new DistSegment2Segment2( Segment1, Segment2 ).Get();
		}


		public DistSegment2Segment2 Compute()
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

            Vector2d diff = segment0.Center - segment1.Center;
            double a01 = -segment0.Direction.Dot(segment1.Direction);
            double b0 = diff.Dot(segment0.Direction);
            double b1 = -diff.Dot(segment1.Direction);
            double c = diff.LengthSquared;
            double det = Math.Abs((double)1 - a01 * a01);
            double s0, s1, sqrDist, extDet0, extDet1, tmpS0, tmpS1;

            if (det >= MathUtil.ZeroTolerance) {
                // Segments are not parallel.
                s0 = a01 * b1 - b0;
                s1 = a01 * b0 - b1;
                extDet0 = segment0.Extent * det;
                extDet1 = segment1.Extent * det;

                if (s0 >= -extDet0) {
                    if (s0 <= extDet0) {
                        if (s1 >= -extDet1) {
                            if (s1 <= extDet1)  // region 0 (interior)
                            {
                                // Minimum at interior points of segments.
                                double invDet = ((double)1) / det;
                                s0 *= invDet;
                                s1 *= invDet;
                                sqrDist = (double)0;
                            } else  // region 3 (side)
                              {
                                s1 = segment1.Extent;
                                tmpS0 = -(a01 * s1 + b0);
                                if (tmpS0 < -segment0.Extent) {
                                    s0 = -segment0.Extent;
                                    sqrDist = s0 * (s0 - ((double)2) * tmpS0) +
                                        s1 * (s1 + ((double)2) * b1) + c;
                                } else if (tmpS0 <= segment0.Extent) {
                                    s0 = tmpS0;
                                    sqrDist = -s0 * s0 + s1 * (s1 + ((double)2) * b1) + c;
                                } else {
                                    s0 = segment0.Extent;
                                    sqrDist = s0 * (s0 - ((double)2) * tmpS0) +
                                        s1 * (s1 + ((double)2) * b1) + c;
                                }
                            }
                        } else  // region 7 (side)
                          {
                            s1 = -segment1.Extent;
                            tmpS0 = -(a01 * s1 + b0);
                            if (tmpS0 < -segment0.Extent) {
                                s0 = -segment0.Extent;
                                sqrDist = s0 * (s0 - ((double)2) * tmpS0) +
                                    s1 * (s1 + ((double)2) * b1) + c;
                            } else if (tmpS0 <= segment0.Extent) {
                                s0 = tmpS0;
                                sqrDist = -s0 * s0 + s1 * (s1 + ((double)2) * b1) + c;
                            } else {
                                s0 = segment0.Extent;
                                sqrDist = s0 * (s0 - ((double)2) * tmpS0) +
                                    s1 * (s1 + ((double)2) * b1) + c;
                            }
                        }
                    } else {
                        if (s1 >= -extDet1) {
                            if (s1 <= extDet1)  // region 1 (side)
                            {
                                s0 = segment0.Extent;
                                tmpS1 = -(a01 * s0 + b1);
                                if (tmpS1 < -segment1.Extent) {
                                    s1 = -segment1.Extent;
                                    sqrDist = s1 * (s1 - ((double)2) * tmpS1) +
                                        s0 * (s0 + ((double)2) * b0) + c;
                                } else if (tmpS1 <= segment1.Extent) {
                                    s1 = tmpS1;
                                    sqrDist = -s1 * s1 + s0 * (s0 + ((double)2) * b0) + c;
                                } else {
                                    s1 = segment1.Extent;
                                    sqrDist = s1 * (s1 - ((double)2) * tmpS1) +
                                        s0 * (s0 + ((double)2) * b0) + c;
                                }
                            } else  // region 2 (corner)
                              {
                                s1 = segment1.Extent;
                                tmpS0 = -(a01 * s1 + b0);
                                if (tmpS0 < -segment0.Extent) {
                                    s0 = -segment0.Extent;
                                    sqrDist = s0 * (s0 - ((double)2) * tmpS0) +
                                        s1 * (s1 + ((double)2) * b1) + c;
                                } else if (tmpS0 <= segment0.Extent) {
                                    s0 = tmpS0;
                                    sqrDist = -s0 * s0 + s1 * (s1 + ((double)2) * b1) + c;
                                } else {
                                    s0 = segment0.Extent;
                                    tmpS1 = -(a01 * s0 + b1);
                                    if (tmpS1 < -segment1.Extent) {
                                        s1 = -segment1.Extent;
                                        sqrDist = s1 * (s1 - ((double)2) * tmpS1) +
                                            s0 * (s0 + ((double)2) * b0) + c;
                                    } else if (tmpS1 <= segment1.Extent) {
                                        s1 = tmpS1;
                                        sqrDist = -s1 * s1 + s0 * (s0 + ((double)2) * b0) + c;
                                    } else {
                                        s1 = segment1.Extent;
                                        sqrDist = s1 * (s1 - ((double)2) * tmpS1) +
                                            s0 * (s0 + ((double)2) * b0) + c;
                                    }
                                }
                            }
                        } else  // region 8 (corner)
                          {
                            s1 = -segment1.Extent;
                            tmpS0 = -(a01 * s1 + b0);
                            if (tmpS0 < -segment0.Extent) {
                                s0 = -segment0.Extent;
                                sqrDist = s0 * (s0 - ((double)2) * tmpS0) +
                                    s1 * (s1 + ((double)2) * b1) + c;
                            } else if (tmpS0 <= segment0.Extent) {
                                s0 = tmpS0;
                                sqrDist = -s0 * s0 + s1 * (s1 + ((double)2) * b1) + c;
                            } else {
                                s0 = segment0.Extent;
                                tmpS1 = -(a01 * s0 + b1);
                                if (tmpS1 > segment1.Extent) {
                                    s1 = segment1.Extent;
                                    sqrDist = s1 * (s1 - ((double)2) * tmpS1) +
                                        s0 * (s0 + ((double)2) * b0) + c;
                                } else if (tmpS1 >= -segment1.Extent) {
                                    s1 = tmpS1;
                                    sqrDist = -s1 * s1 + s0 * (s0 + ((double)2) * b0) + c;
                                } else {
                                    s1 = -segment1.Extent;
                                    sqrDist = s1 * (s1 - ((double)2) * tmpS1) +
                                        s0 * (s0 + ((double)2) * b0) + c;
                                }
                            }
                        }
                    }
                } else {
                    if (s1 >= -extDet1) {
                        if (s1 <= extDet1)  // region 5 (side)
                        {
                            s0 = -segment0.Extent;
                            tmpS1 = -(a01 * s0 + b1);
                            if (tmpS1 < -segment1.Extent) {
                                s1 = -segment1.Extent;
                                sqrDist = s1 * (s1 - ((double)2) * tmpS1) +
                                    s0 * (s0 + ((double)2) * b0) + c;
                            } else if (tmpS1 <= segment1.Extent) {
                                s1 = tmpS1;
                                sqrDist = -s1 * s1 + s0 * (s0 + ((double)2) * b0) + c;
                            } else {
                                s1 = segment1.Extent;
                                sqrDist = s1 * (s1 - ((double)2) * tmpS1) +
                                    s0 * (s0 + ((double)2) * b0) + c;
                            }
                        } else  // region 4 (corner)
                          {
                            s1 = segment1.Extent;
                            tmpS0 = -(a01 * s1 + b0);
                            if (tmpS0 > segment0.Extent) {
                                s0 = segment0.Extent;
                                sqrDist = s0 * (s0 - ((double)2) * tmpS0) +
                                    s1 * (s1 + ((double)2) * b1) + c;
                            } else if (tmpS0 >= -segment0.Extent) {
                                s0 = tmpS0;
                                sqrDist = -s0 * s0 + s1 * (s1 + ((double)2) * b1) + c;
                            } else {
                                s0 = -segment0.Extent;
                                tmpS1 = -(a01 * s0 + b1);
                                if (tmpS1 < -segment1.Extent) {
                                    s1 = -segment1.Extent;
                                    sqrDist = s1 * (s1 - ((double)2) * tmpS1) +
                                        s0 * (s0 + ((double)2) * b0) + c;
                                } else if (tmpS1 <= segment1.Extent) {
                                    s1 = tmpS1;
                                    sqrDist = -s1 * s1 + s0 * (s0 + ((double)2) * b0) + c;
                                } else {
                                    s1 = segment1.Extent;
                                    sqrDist = s1 * (s1 - ((double)2) * tmpS1) +
                                        s0 * (s0 + ((double)2) * b0) + c;
                                }
                            }
                        }
                    } else   // region 6 (corner)
                      {
                        s1 = -segment1.Extent;
                        tmpS0 = -(a01 * s1 + b0);
                        if (tmpS0 > segment0.Extent) {
                            s0 = segment0.Extent;
                            sqrDist = s0 * (s0 - ((double)2) * tmpS0) +
                                s1 * (s1 + ((double)2) * b1) + c;
                        } else if (tmpS0 >= -segment0.Extent) {
                            s0 = tmpS0;
                            sqrDist = -s0 * s0 + s1 * (s1 + ((double)2) * b1) + c;
                        } else {
                            s0 = -segment0.Extent;
                            tmpS1 = -(a01 * s0 + b1);
                            if (tmpS1 < -segment1.Extent) {
                                s1 = -segment1.Extent;
                                sqrDist = s1 * (s1 - ((double)2) * tmpS1) +
                                    s0 * (s0 + ((double)2) * b0) + c;
                            } else if (tmpS1 <= segment1.Extent) {
                                s1 = tmpS1;
                                sqrDist = -s1 * s1 + s0 * (s0 + ((double)2) * b0) + c;
                            } else {
                                s1 = segment1.Extent;
                                sqrDist = s1 * (s1 - ((double)2) * tmpS1) +
                                    s0 * (s0 + ((double)2) * b0) + c;
                            }
                        }
                    }
                }
            } else {
                // The segments are parallel.  The average b0 term is designed to
                // ensure symmetry of the function.  That is, dist(seg0,seg1) and
                // dist(seg1,seg0) should produce the same number.
                double e0pe1 = segment0.Extent + segment1.Extent;
                double sign = (a01 > (double)0 ? (double) - 1 : (double)1);
                double b0Avr = ((double)0.5) * (b0 - sign * b1);
                double lambda = -b0Avr;
                if (lambda < -e0pe1) {
                    lambda = -e0pe1;
                } else if (lambda > e0pe1) {
                    lambda = e0pe1;
                }

                s1 = -sign * lambda * segment1.Extent / e0pe1;
                s0 = lambda + sign * s1;
                sqrDist = lambda * (lambda + ((double)2) * b0Avr) + c;
            }

            // Account for numerical round-off errors.
            if (sqrDist < (double)0) 
                sqrDist = (double)0;

            Segment1Parameter = s0;
            Segment1Closest = segment0.Center + s0 * segment0.Direction;
            Segment2Parameter = s1;
            Segment2Closest = segment1.Center + s1*segment1.Direction;

            DistanceSquared = sqrDist;
			return sqrDist;
		}
	}

}
