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
    }
}
