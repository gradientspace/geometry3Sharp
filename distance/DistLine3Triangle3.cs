using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    // ported from WildMagic 5 
    // https://www.geometrictools.com/Downloads/Downloads.html

    public class DistLine3Triangle3
    {
        Line3d line;
        public Line3d Line
        {
            get { return line; }
            set { line = value; DistanceSquared = -1.0; }
        }

        Triangle3d triangle;
        public Triangle3d Triangle
        {
            get { return triangle; }
            set { triangle = value; DistanceSquared = -1.0; }
        }

        public double DistanceSquared = -1.0;

        public Vector3d LineClosest;
        public double LineParam;
        public Vector3d TriangleClosest;
        public Vector3d TriangleBaryCoords;


        public DistLine3Triangle3(Line3d LineIn, Triangle3d TriangleIn)
        {
            this.triangle = TriangleIn; this.line = LineIn;
        }

        public DistLine3Triangle3 Compute()
        {
            GetSquared();
            return this;
        }

        public double Get()
        {
            return Math.Sqrt(GetSquared());
        }


        public double GetSquared()
        {
            if (DistanceSquared >= 0)
                return DistanceSquared;

            // Test if line intersects triangle.  If so, the squared distance is zero.
            Vector3d edge0 = triangle.V1 - triangle.V0;
            Vector3d edge1 = triangle.V2 - triangle.V0;
            Vector3d normal = edge0.UnitCross(edge1);
            double NdD = normal.Dot(line.Direction);
            if (Math.Abs(NdD) > MathUtil.ZeroTolerance) {
                // The line and triangle are not parallel, so the line intersects
                // the plane of the triangle.
                Vector3d diff = line.Origin - triangle.V0;
                Vector3d U = Vector3d.Zero, V = Vector3d.Zero;
                Vector3d.GenerateComplementBasis(ref U, ref V, line.Direction);
                double UdE0 = U.Dot(edge0);
                double UdE1 = U.Dot(edge1);
                double UdDiff = U.Dot(diff);
                double VdE0 = V.Dot(edge0);
                double VdE1 = V.Dot(edge1);
                double VdDiff = V.Dot(diff);
                double invDet = (1) / (UdE0 * VdE1 - UdE1 * VdE0);

                // Barycentric coordinates for the point of intersection.
                double b1 = (VdE1 * UdDiff - UdE1 * VdDiff) * invDet;
                double b2 = (UdE0 * VdDiff - VdE0 * UdDiff) * invDet;
                double b0 = 1 - b1 - b2;

                if (b0 >= 0 && b1 >= 0 && b2 >= 0) {
                    // Line parameter for the point of intersection.
                    double DdE0 = line.Direction.Dot(edge0);
                    double DdE1 = line.Direction.Dot(edge1);
                    double DdDiff = line.Direction.Dot(diff);
                    LineParam = b1 * DdE0 + b2 * DdE1 - DdDiff;

                    // Barycentric coordinates for the point of intersection.
                    TriangleBaryCoords = new Vector3d(b0, b1, b2);

                    // The intersection point is inside or on the triangle.
                    LineClosest = line.Origin + LineParam * line.Direction;
                    TriangleClosest = triangle.V0 + b1 * edge0 + b2 * edge1;
                    DistanceSquared = 0;
                    return 0;
                }
            }

            // Either (1) the line is not parallel to the triangle and the point of
            // intersection of the line and the plane of the triangle is outside the
            // triangle or (2) the line and triangle are parallel.  Regardless, the
            // closest point on the triangle is on an edge of the triangle.  Compare
            // the line to all three edges of the triangle.
            double sqrDist = double.MaxValue;
            for (int i0 = 2, i1 = 0; i1 < 3; i0 = i1++) {
                Segment3d segment = new Segment3d(triangle[i0], triangle[i1]);
                DistLine3Segment3 queryLS = new DistLine3Segment3(line, segment);
                double sqrDistTmp = queryLS.GetSquared();
                if (sqrDistTmp < sqrDist) {
                    LineClosest = queryLS.LineClosest;
                    TriangleClosest = queryLS.SegmentClosest;
                    sqrDist = sqrDistTmp;
                    LineParam = queryLS.LineParameter;
                    double ratio = queryLS.SegmentParameter / segment.Extent;
                    TriangleBaryCoords = Vector3d.Zero;
                    TriangleBaryCoords[i0] = (0.5) * (1 - ratio);
                    TriangleBaryCoords[i1] = 1 - TriangleBaryCoords[i0];
                    TriangleBaryCoords[3 - i0 - i1] = 0;
                }
            }

            DistanceSquared = sqrDist;
            return DistanceSquared;
        }
    }
}
