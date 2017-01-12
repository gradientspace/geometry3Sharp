using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    // ported from WildMagic 5 
    // https://www.geometrictools.com/Downloads/Downloads.html

    public class DistSegment3Triangle3
    {
        Segment3d segment;
        public Segment3d Segment
        {
            get { return segment; }
            set { segment = value; DistanceSquared = -1.0; }
        }

        Triangle3d triangle;
        public Triangle3d Triangle
        {
            get { return triangle; }
            set { triangle = value; DistanceSquared = -1.0; }
        }

        public double DistanceSquared = -1.0;

        public Vector3d SegmentClosest;
        public double SegmentParam;
        public Vector3d TriangleClosest;
        public Vector3d TriangleBaryCoords;

        public DistSegment3Triangle3(Segment3d SegmentIn, Triangle3d TriangleIn)
        {
            this.triangle = TriangleIn; this.segment = SegmentIn;
        }


        public DistSegment3Triangle3 Compute()
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
            Line3d line = new Line3d(segment.Center, segment.Direction);
            DistLine3Triangle3 queryLT = new DistLine3Triangle3(line, triangle);
            double sqrDist = queryLT.GetSquared();
            SegmentParam = queryLT.LineParam;

            if (SegmentParam >= -segment.Extent) {
                if (SegmentParam <= segment.Extent) {
                    SegmentClosest = queryLT.LineClosest;
                    TriangleClosest = queryLT.TriangleClosest;
                    TriangleBaryCoords = queryLT.TriangleBaryCoords;
                } else {
                    SegmentClosest = segment.P1;
                    DistPoint3Triangle3 queryPT = new DistPoint3Triangle3(SegmentClosest, triangle);
                    sqrDist = queryPT.GetSquared();
                    TriangleClosest = queryPT.TriangleClosest;
                    SegmentParam = segment.Extent;
                    TriangleBaryCoords = queryPT.TriangleBaryCoords;
                }
            } else {
                SegmentClosest = segment.P0;
                DistPoint3Triangle3 queryPT = new DistPoint3Triangle3(SegmentClosest, triangle);
                sqrDist = queryPT.GetSquared();
                TriangleClosest = queryPT.TriangleClosest;
                SegmentParam = -segment.Extent;
                TriangleBaryCoords = queryPT.TriangleBaryCoords;
            }

            DistanceSquared = sqrDist;
            return DistanceSquared;
        }
    }
}
