using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    /// <summary>
    /// Distance between ray and segment
    /// ported from WildMagic5
    /// </summary>
    public class DistRay3Segment3
    {
        Ray3d ray;
        public Ray3d Ray
        {
            get { return ray; }
            set { ray = value; DistanceSquared = -1.0; }
        }

        Segment3d segment;
        public Segment3d Segment
        {
            get { return segment; }
            set { segment = value; DistanceSquared = -1.0; }
        }

        public double DistanceSquared = -1.0;
    
        public Vector3d RayClosest;
        public double RayParameter;
        public Vector3d SegmentClosest;
        public double SegmentParameter;


        public DistRay3Segment3(Ray3d rayIn, Segment3d segmentIn)
        {
            this.ray = rayIn; this.segment = segmentIn;
        }


        static public double MinDistance(Ray3d r, Segment3d s) {
            double rayt, segt;
            double dsqr = SquaredDistance(ref r, ref s, out rayt, out segt);
            return Math.Sqrt(dsqr);
        }
        static public double MinDistanceSegmentParam(Ray3d r, Segment3d s) {
            double rayt, segt;
            /*double dsqr = */SquaredDistance(ref r, ref s, out rayt, out segt);
            return segt;
        }


        public DistRay3Segment3 Compute() {
            GetSquared();
            return this;
        }

        public double Get() {
            return Math.Sqrt(GetSquared());
        }

        public double GetSquared()
        {
            if (DistanceSquared >= 0)
                return DistanceSquared;

            Vector3d diff = ray.Origin - segment.Center;
            double a01 = -ray.Direction.Dot(segment.Direction);
            double b0 = diff.Dot(ray.Direction);
            double b1 = -diff.Dot(segment.Direction);
            double c = diff.LengthSquared;
            double det = Math.Abs(1 - a01 * a01);
            double s0, s1, sqrDist, extDet;

            if (det >= MathUtil.ZeroTolerance) {
                // The Ray and Segment are not parallel.
                s0 = a01 * b1 - b0;
                s1 = a01 * b0 - b1;
                extDet = segment.Extent * det;

                if (s0 >= 0) {
                    if (s1 >= -extDet) {
                        if (s1 <= extDet)  // region 0
                        {
                            // Minimum at interior points of Ray and Segment.
                            double invDet = (1) / det;
                            s0 *= invDet;
                            s1 *= invDet;
                            sqrDist = s0 * (s0 + a01 * s1 + (2) * b0) +
                                s1 * (a01 * s0 + s1 + (2) * b1) + c;
                        } else  // region 1
                          {
                            s1 = segment.Extent;
                            s0 = -(a01 * s1 + b0);
                            if (s0 > 0) {
                                sqrDist = -s0 * s0 + s1 * (s1 + (2) * b1) + c;
                            } else {
                                s0 = 0;
                                sqrDist = s1 * (s1 + (2) * b1) + c;
                            }
                        }
                    } else  // region 5
                      {
                        s1 = -segment.Extent;
                        s0 = -(a01 * s1 + b0);
                        if (s0 > 0) {
                            sqrDist = -s0 * s0 + s1 * (s1 + (2) * b1) + c;
                        } else {
                            s0 = 0;
                            sqrDist = s1 * (s1 + (2) * b1) + c;
                        }
                    }
                } else {
                    if (s1 <= -extDet)  // region 4
                    {
                        s0 = -(-a01 * segment.Extent + b0);
                        if (s0 > 0) {
                            s1 = -segment.Extent;
                            sqrDist = -s0 * s0 + s1 * (s1 + (2) * b1) + c;
                        } else {
                            s0 = 0;
                            s1 = -b1;
                            if (s1 < -segment.Extent) {
                                s1 = -segment.Extent;
                            } else if (s1 > segment.Extent) {
                                s1 = segment.Extent;
                            }
                            sqrDist = s1 * (s1 + (2) * b1) + c;
                        }
                    } else if (s1 <= extDet)  // region 3
                      {
                        s0 = 0;
                        s1 = -b1;
                        if (s1 < -segment.Extent) {
                            s1 = -segment.Extent;
                        } else if (s1 > segment.Extent) {
                            s1 = segment.Extent;
                        }
                        sqrDist = s1 * (s1 + (2) * b1) + c;
                    } else  // region 2
                      {
                        s0 = -(a01 * segment.Extent + b0);
                        if (s0 > 0) {
                            s1 = segment.Extent;
                            sqrDist = -s0 * s0 + s1 * (s1 + (2) * b1) + c;
                        } else {
                            s0 = 0;
                            s1 = -b1;
                            if (s1 < -segment.Extent) {
                                s1 = -segment.Extent;
                            } else if (s1 > segment.Extent) {
                                s1 = segment.Extent;
                            }
                            sqrDist = s1 * (s1 + (2) * b1) + c;
                        }
                    }
                }
            } else {
                // Ray and Segment are parallel.
                if (a01 > 0) {
                    // Opposite direction vectors.
                    s1 = -segment.Extent;
                } else {
                    // Same direction vectors.
                    s1 = segment.Extent;
                }

                s0 = -(a01 * s1 + b0);
                if (s0 > 0) {
                    sqrDist = -s0 * s0 + s1 * (s1 + (2) * b1) + c;
                } else {
                    s0 = 0;
                    sqrDist = s1 * (s1 + (2) * b1) + c;
                }
            }

            RayClosest = ray.Origin + s0 * ray.Direction;
            SegmentClosest = segment.Center + s1 * segment.Direction;
            RayParameter = s0;
            SegmentParameter = s1;

            // Account for numerical round-off errors.
            if (sqrDist < 0) {
                sqrDist = 0;
            }
            DistanceSquared = sqrDist;
            return DistanceSquared;
        }






        /// <summary>
        /// compute w/o allocating temporaries/etc
        /// </summary>
        public static double SquaredDistance(ref Ray3d ray, ref Segment3d segment, 
            out double rayT, out double segT)
        {
            Vector3d diff = ray.Origin - segment.Center;
            double a01 = -ray.Direction.Dot(segment.Direction);
            double b0 = diff.Dot(ray.Direction);
            double b1 = -diff.Dot(segment.Direction);
            double c = diff.LengthSquared;
            double det = Math.Abs(1 - a01 * a01);
            double s0, s1, sqrDist, extDet;

            if (det >= MathUtil.ZeroTolerance) {
                // The Ray and Segment are not parallel.
                s0 = a01 * b1 - b0;
                s1 = a01 * b0 - b1;
                extDet = segment.Extent * det;

                if (s0 >= 0) {
                    if (s1 >= -extDet) {
                        if (s1 <= extDet)  // region 0
                        {
                            // Minimum at interior points of Ray and Segment.
                            double invDet = (1) / det;
                            s0 *= invDet;
                            s1 *= invDet;
                            sqrDist = s0 * (s0 + a01 * s1 + (2) * b0) +
                                s1 * (a01 * s0 + s1 + (2) * b1) + c;
                        } else  // region 1
                          {
                            s1 = segment.Extent;
                            s0 = -(a01 * s1 + b0);
                            if (s0 > 0) {
                                sqrDist = -s0 * s0 + s1 * (s1 + (2) * b1) + c;
                            } else {
                                s0 = 0;
                                sqrDist = s1 * (s1 + (2) * b1) + c;
                            }
                        }
                    } else  // region 5
                      {
                        s1 = -segment.Extent;
                        s0 = -(a01 * s1 + b0);
                        if (s0 > 0) {
                            sqrDist = -s0 * s0 + s1 * (s1 + (2) * b1) + c;
                        } else {
                            s0 = 0;
                            sqrDist = s1 * (s1 + (2) * b1) + c;
                        }
                    }
                } else {
                    if (s1 <= -extDet)  // region 4
                    {
                        s0 = -(-a01 * segment.Extent + b0);
                        if (s0 > 0) {
                            s1 = -segment.Extent;
                            sqrDist = -s0 * s0 + s1 * (s1 + (2) * b1) + c;
                        } else {
                            s0 = 0;
                            s1 = -b1;
                            if (s1 < -segment.Extent) {
                                s1 = -segment.Extent;
                            } else if (s1 > segment.Extent) {
                                s1 = segment.Extent;
                            }
                            sqrDist = s1 * (s1 + (2) * b1) + c;
                        }
                    } else if (s1 <= extDet)  // region 3
                      {
                        s0 = 0;
                        s1 = -b1;
                        if (s1 < -segment.Extent) {
                            s1 = -segment.Extent;
                        } else if (s1 > segment.Extent) {
                            s1 = segment.Extent;
                        }
                        sqrDist = s1 * (s1 + (2) * b1) + c;
                    } else  // region 2
                      {
                        s0 = -(a01 * segment.Extent + b0);
                        if (s0 > 0) {
                            s1 = segment.Extent;
                            sqrDist = -s0 * s0 + s1 * (s1 + (2) * b1) + c;
                        } else {
                            s0 = 0;
                            s1 = -b1;
                            if (s1 < -segment.Extent) {
                                s1 = -segment.Extent;
                            } else if (s1 > segment.Extent) {
                                s1 = segment.Extent;
                            }
                            sqrDist = s1 * (s1 + (2) * b1) + c;
                        }
                    }
                }
            } else {
                // Ray and Segment are parallel.
                if (a01 > 0) {
                    // Opposite direction vectors.
                    s1 = -segment.Extent;
                } else {
                    // Same direction vectors.
                    s1 = segment.Extent;
                }

                s0 = -(a01 * s1 + b0);
                if (s0 > 0) {
                    sqrDist = -s0 * s0 + s1 * (s1 + (2) * b1) + c;
                } else {
                    s0 = 0;
                    sqrDist = s1 * (s1 + (2) * b1) + c;
                }
            }

            rayT = s0;
            segT = s1;

            // Account for numerical round-off errors.
            if (sqrDist < 0) 
                sqrDist = 0;
            return sqrDist;
        }



    }
}
