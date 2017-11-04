using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    // Port of Wm5Query2Int64 from WildMagic5 library by David Eberly / geometrictools.com

    /// <summary>
    /// 2D queries for integer coordinates. 
    /// Note that input Vector2d values are directly cast to int64 - you must
    /// scale them to suitable coordinates yourself!
    /// </summary>
    public class Query2Int64 : Query2d
    {

        public Query2Int64(IList<Vector2d> Vertices) : base(Vertices)
        {
        }


        public override int ToLine(ref Vector2d test, int v0, int v1)
        {
            Vector2d vec0 = mVertices[v0];
            Vector2d vec1 = mVertices[v1];

            long x0 = (long)test.x - (long)vec0.x;
            long y0 = (long)test.y - (long)vec0.y;
            long x1 = (long)vec1.x - (long)vec0.x;
            long y1 = (long)vec1.y - (long)vec0.y;

            long det = Det2(x0, y0, x1, y1);
            return (det > 0 ? +1 : (det < 0 ? -1 : 0));
        }


        public override int ToCircumcircle(ref Vector2d test, int v0, int v1, int v2)
        {
            Vector2d vec0 = mVertices[v0];
            Vector2d vec1 = mVertices[v1];
            Vector2d vec2 = mVertices[v2];

            Vector2l iTest = new Vector2l( (long)test.x, (long)test.y);
            Vector2l iV0 = new Vector2l((long)vec0.x, (long)vec0.y);
            Vector2l iV1 = new Vector2l((long)vec1.x, (long)vec1.y);
            Vector2l iV2 = new Vector2l((long)vec2.x, (long)vec2.y);

            long s0x = iV0.x + iTest.x;
            long d0x = iV0.x - iTest.x;
            long s0y = iV0.y + iTest.y;
            long d0y = iV0.y - iTest.y;
            long s1x = iV1.x + iTest.x;
            long d1x = iV1.x - iTest.x;
            long s1y = iV1.y + iTest.y;
            long d1y = iV1.y - iTest.y;
            long s2x = iV2.x + iTest.x;
            long d2x = iV2.x - iTest.x;
            long s2y = iV2.y + iTest.y;
            long d2y = iV2.y - iTest.y;
            long z0 = s0x * d0x + s0y * d0y;
            long z1 = s1x * d1x + s1y * d1y;
            long z2 = s2x * d2x + s2y * d2y;
            long det = Det3(d0x, d0y, z0, d1x, d1y, z1, d2x, d2y, z2);
            return (det < 0 ? 1 : (det > 0 ? -1 : 0));
        }


        long Dot(long x0, long y0, long x1, long y1)
        {
            return x0 * x1 + y0 * y1;
        }


        long Det2(long x0, long y0, long x1, long y1)
        {
            return x0 * y1 - x1 * y0;
        }


        long Det3(long x0, long y0, long z0, long x1, long y1, long z1, long x2, long y2, long z2)
        {
            long c00 = y1 * z2 - y2 * z1;
            long c01 = y2 * z0 - y0 * z2;
            long c02 = y0 * z1 - y1 * z0;
            return x0 * c00 + x1 * c01 + x2 * c02;
        }













    }
}
