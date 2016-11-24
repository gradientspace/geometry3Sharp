using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    public class CurveUtils
    {


        public static int FindNearestIndex(ICurve c, Vector3d v)
        {
            int iNearest = -1;
            double dNear = Double.MaxValue;
            int N = c.VertexCount;
            for ( int i = 0; i < N; ++i ) {
                double dSqr = (c.GetVertex(i) - v).LengthSquared;
                if (dSqr < dNear) {
                    dNear = dSqr;
                    iNearest = i;
                }
            }
            return iNearest;
        }



        public static bool FindClosestRayIntersection(ICurve c, double segRadius, Ray3d ray, out double rayT)
        {
            if (c.Closed)
                throw new InvalidOperationException("CurveUtils.FindClosestRayIntersection doesn't support closed curves yet");

            DistRay3Segment3 dist = new DistRay3Segment3(ray, new Segment3d(Vector3d.Zero, Vector3d.Zero) );

            rayT = double.MaxValue;
            int nNearSegment = -1;
            double fNearSegT = 0.0;

            int N = c.VertexCount;
            for (int i = 0; i < N-1; ++i) {
                dist.Segment.SetEndpoints(c.GetVertex(i), c.GetVertex(i + 1));
                dist.Reset();

                // raycast to line bounding-sphere first (is this going ot be faster??)
                double fSphereHitT;
                bool bHitBoundSphere = RayIntersection.SphereSigned(ray.Origin, ray.Direction,
                    dist.Segment.Center, dist.Segment.Extent + segRadius, out fSphereHitT);
                if (bHitBoundSphere == false)
                    continue;

                // find ray/seg min-distance and use ray T
                double dSqr = dist.GetSquared();
                if ( dSqr < segRadius*segRadius) {
                    if (dist.RayParameter < rayT) {
                        rayT = dist.RayParameter;
                        fNearSegT = dist.SegmentParameter;
                        nNearSegment = i;
                    }
                }
            }
            return (nNearSegment >= 0);
        }

    }
}
