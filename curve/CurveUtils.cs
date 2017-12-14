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





        /// <summary>
        /// smooth set of vertices in-place (will not produce a symmetric result, but does not require extra buffer)
        /// </summary>
        public static void InPlaceSmooth(IList<Vector3d> vertices, double alpha, int nIterations, bool bClosed)
        {
            InPlaceSmooth(vertices, 0, vertices.Count, alpha, nIterations, bClosed);
        }
        /// <summary>
        /// smooth set of vertices in-place (will not produce a symmetric result, but does not require extra buffer)
        /// </summary>
        public static void InPlaceSmooth(IList<Vector3d> vertices, int iStart, int iEnd, double alpha, int nIterations, bool bClosed)
        {
            int N = vertices.Count;
            if ( bClosed ) {
                for (int iter = 0; iter < nIterations; ++iter) {
                    for (int ii = iStart; ii < iEnd; ++ii) {
                        int i = (ii % N);
                        int iPrev = (ii == 0) ? N - 1 : ii - 1;
                        int iNext = (ii + 1) % N;
                        Vector3d prev = vertices[iPrev], next = vertices[iNext];
                        Vector3d c = (prev + next) * 0.5f;
                        vertices[i] = (1 - alpha) * vertices[i] + (alpha) * c;
                    }
                }
            } else {
                for (int iter = 0; iter < nIterations; ++iter) {
                    for (int i = iStart; i <= iEnd; ++i) {
                        if (i == 0 || i >= N - 1)
                            continue;
                        Vector3d prev = vertices[i - 1], next = vertices[i + 1];
                        Vector3d c = (prev + next) * 0.5f;
                        vertices[i] = (1 - alpha) * vertices[i] + (alpha) * c;
                    }
                }
            }
        }



    }





    /// <summary>
    /// Simple sampled-curve wrapper type
    /// </summary>
    public class IWrappedCurve3d : ISampledCurve3d
    {
        public IList<Vector3d> VertexList;
        public bool Closed { get; set; }

        public int VertexCount { get { return (VertexList == null) ? 0 : VertexList.Count; } }
        public Vector3d GetVertex(int i) { return VertexList[i]; }
        public IEnumerable<Vector3d> Vertices { get { return VertexList; } }
    }

}
