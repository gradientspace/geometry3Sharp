using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    public class CurveUtils
    {

        public static Vector3d GetTangent(List<Vector3d> vertices, int i)
        {
            if (i == 0)
                return (vertices[1] - vertices[0]).Normalized;
            else if (i == vertices.Count - 1)
                return (vertices[vertices.Count - 1] - vertices[vertices.Count - 2]).Normalized;
            else
                return (vertices[i + 1] - vertices[i - 1]).Normalized;
        }


        public static double ArcLength(List<Vector3d> vertices) {
            double sum = 0;
            for (int i = 1; i < vertices.Count; ++i)
                sum += (vertices[i] - vertices[i - 1]).Length;
            return sum;
        }
        public static double ArcLength(Vector3d[] vertices) {
            double sum = 0;
            for (int i = 1; i < vertices.Length ; ++i)
                sum += (vertices[i] - vertices[i - 1]).Length;
            return sum;
        }
        public static double ArcLength(IEnumerable<Vector3d> vertices) {
            double sum = 0;
            Vector3d prev = Vector3f.Zero;
            int i = 0;
            foreach (Vector3d v in vertices) {
                if (i++ > 0)
                    sum += (v - prev).Length;
            }
            return sum;
        }



        public static int FindNearestIndex(ISampledCurve3d c, Vector3d v)
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



        public static bool FindClosestRayIntersection(ISampledCurve3d c, double segRadius, Ray3d ray, out double rayT)
        {
            rayT = double.MaxValue;
            int nNearSegment = -1;
            //double fNearSegT = 0.0;

            int N = c.VertexCount;
            int iStop = (c.Closed) ? N : N - 1;
            for (int i = 0; i < iStop; ++i) {
                DistRay3Segment3 dist = new DistRay3Segment3(ray,
                    new Segment3d(c.GetVertex(i), c.GetVertex( (i + 1) % N )));

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
                        //fNearSegT = dist.SegmentParameter;
                        nNearSegment = i;
                    }
                }
            }
            return (nNearSegment >= 0);
        }

    }
}
