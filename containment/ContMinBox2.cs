using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    // ported from WildMagic5 ContMinBox2. 
    // Compute a minimum-area oriented box containing the specified points.  The
    // algorithm uses the rotating calipers method.  If the input points represent
    // a counterclockwise-ordered polygon, set 'isConvexPolygon' to 'true';
    // otherwise, set 'isConvexPolygon' to 'false'.


    /// <summary>
    /// Fit minimal bounding-box to a set of 2D points. Result is in MinBox.
    /// </summary>
    public class ContMinBox2
    {
        Box2d mMinBox;

        // Flags for the rotating calipers algorithm.
        protected enum RCFlags { F_NONE, F_LEFT, F_RIGHT, F_BOTTOM, F_TOP };


        public Box2d MinBox {
            get { return mMinBox; }
        }

        public ContMinBox2(IList<Vector2d> points, double epsilon, QueryNumberType queryType, bool isConvexPolygon)
        {
            // Get the convex hull of the points.
            IList<Vector2d> hullPoints;
            int numPoints;
            if (isConvexPolygon) {
                hullPoints = points;
                numPoints = hullPoints.Count;
            } else {
                ConvexHull2 hull = new ConvexHull2(points, epsilon, queryType);
                int hullDim = hull.Dimension;
                int hullNumSimplices = hull.NumSimplices;
                int[] hullIndices = hull.HullIndices;

                if (hullDim == 0) {
                    mMinBox.Center = points[0];
                    mMinBox.AxisX = Vector2d.AxisX;
                    mMinBox.AxisY = Vector2d.AxisY;
                    mMinBox.Extent[0] = (double)0;
                    mMinBox.Extent[1] = (double)0;
                    return;
                }

                if (hullDim == 1) {
                    throw new NotImplementedException("ContMinBox2: Have not implemented 1d case");
                    //ConvexHull1 hull1 = hull.GetConvexHull1();
                    //hullIndices = hull1->GetIndices();

                    //mMinBox.Center = ((double)0.5) * (points[hullIndices[0]] +
                    //    points[hullIndices[1]]);
                    //Vector2d diff =
                    //    points[hullIndices[1]] - points[hullIndices[0]];
                    //mMinBox.Extent[0] = ((double)0.5) * diff.Normalize();
                    //mMinBox.Extent[1] = (double)0.0;
                    //mMinBox.Axis[0] = diff;
                    //mMinBox.Axis[1] = -mMinBox.Axis[0].Perp();
                    //return;
                }

                numPoints = hullNumSimplices;
                Vector2d[] pointsArray = new Vector2d[numPoints];
                for (int i = 0; i < numPoints; ++i) {
                    pointsArray[i] = points[hullIndices[i]];
                }
                hullPoints = pointsArray;
            }

            // The input points are V[0] through V[N-1] and are assumed to be the
            // vertices of a convex polygon that are counterclockwise ordered.  The
            // input points must not contain three consecutive collinear points.

            // Unit-length edge directions of convex polygon.  These could be
            // precomputed and passed to this routine if the application requires it.
            int numPointsM1 = numPoints - 1;
            Vector2d[] edges = new Vector2d[numPoints];
            bool[] visited = new bool[numPoints];
            for (int i = 0; i < numPointsM1; ++i) {
                edges[i] = hullPoints[i + 1] - hullPoints[i];
                edges[i].Normalize();
                visited[i] = false;
            }
            edges[numPointsM1] = hullPoints[0] - hullPoints[numPointsM1];
            edges[numPointsM1].Normalize();
            visited[numPointsM1] = false;

            // Find the smallest axis-aligned box containing the points.  Keep track
            // of the extremum indices, L (left), R (right), B (bottom), and T (top)
            // so that the following constraints are met:
            //   V[L].x <= V[i].x for all i and V[(L+1)%N].x > V[L].x
            //   V[R].x >= V[i].x for all i and V[(R+1)%N].x < V[R].x
            //   V[B].y <= V[i].y for all i and V[(B+1)%N].y > V[B].y
            //   V[T].y >= V[i].y for all i and V[(T+1)%N].y < V[T].y
            double xmin = hullPoints[0].x, xmax = xmin;
            double ymin = hullPoints[0].y, ymax = ymin;
            int LIndex = 0, RIndex = 0, BIndex = 0, TIndex = 0;
            for (int i = 1; i < numPoints; ++i) {
                if (hullPoints[i].x <= xmin) {
                    xmin = hullPoints[i].x;
                    LIndex = i;
                }
                if (hullPoints[i].x >= xmax) {
                    xmax = hullPoints[i].x;
                    RIndex = i;
                }

                if (hullPoints[i].y <= ymin) {
                    ymin = hullPoints[i].y;
                    BIndex = i;
                }
                if (hullPoints[i].y >= ymax) {
                    ymax = hullPoints[i].y;
                    TIndex = i;
                }
            }

            // Apply wrap-around tests to ensure the constraints mentioned above are
            // satisfied.
            if (LIndex == numPointsM1) {
                if (hullPoints[0].x <= xmin) {
                    xmin = hullPoints[0].x;
                    LIndex = 0;
                }
            }

            if (RIndex == numPointsM1) {
                if (hullPoints[0].x >= xmax) {
                    xmax = hullPoints[0].x;
                    RIndex = 0;
                }
            }

            if (BIndex == numPointsM1) {
                if (hullPoints[0].y <= ymin) {
                    ymin = hullPoints[0].y;
                    BIndex = 0;
                }
            }

            if (TIndex == numPointsM1) {
                if (hullPoints[0].y >= ymax) {
                    ymax = hullPoints[0].y;
                    TIndex = 0;
                }
            }

            // The dimensions of the axis-aligned box.  The extents store width and
            // height for now.
            mMinBox.Center.x = ((double)0.5) * (xmin + xmax);
            mMinBox.Center.y = ((double)0.5) * (ymin + ymax);
            mMinBox.AxisX = Vector2d.AxisX;
            mMinBox.AxisY = Vector2d.AxisY;
            mMinBox.Extent[0] = ((double)0.5) * (xmax - xmin);
            mMinBox.Extent[1] = ((double)0.5) * (ymax - ymin);
            double minAreaDiv4 = mMinBox.Extent[0] * mMinBox.Extent[1];

            // The rotating calipers algorithm.
            Vector2d U = Vector2d.AxisX;
            Vector2d V = Vector2d.AxisY;

            bool done = false;
            while (!done) {
                // Determine the edge that forms the smallest angle with the current
                // box edges.
                RCFlags flag = RCFlags.F_NONE;
                double maxDot = (double)0;

                double dot = U.Dot(edges[BIndex]);
                if (dot > maxDot) {
                    maxDot = dot;
                    flag = RCFlags.F_BOTTOM;
                }

                dot = V.Dot(edges[RIndex]);
                if (dot > maxDot) {
                    maxDot = dot;
                    flag = RCFlags.F_RIGHT;
                }

                dot = -U.Dot(edges[TIndex]);
                if (dot > maxDot) {
                    maxDot = dot;
                    flag = RCFlags.F_TOP;
                }

                dot = -V.Dot(edges[LIndex]);
                if (dot > maxDot) {
                    maxDot = dot;
                    flag = RCFlags.F_LEFT;
                }

                switch (flag) {
                    case RCFlags.F_BOTTOM:
                        if (visited[BIndex]) {
                            done = true;
                        } else {
                            // Compute box axes with E[B] as an edge.
                            U = edges[BIndex];
                            V = -U.Perp;
                            UpdateBox(hullPoints[LIndex], hullPoints[RIndex],
                                hullPoints[BIndex], hullPoints[TIndex], ref U, ref V,
                                ref minAreaDiv4);

                            // Mark edge visited and rotate the calipers.
                            visited[BIndex] = true;
                            if (++BIndex == numPoints) {
                                BIndex = 0;
                            }
                        }
                        break;
                    case RCFlags.F_RIGHT:
                        if (visited[RIndex]) {
                            done = true;
                        } else {
                            // Compute box axes with E[R] as an edge.
                            V = edges[RIndex];
                            U = V.Perp;
                            UpdateBox(hullPoints[LIndex], hullPoints[RIndex],
                                hullPoints[BIndex], hullPoints[TIndex], ref U, ref V,
                                ref minAreaDiv4);

                            // Mark edge visited and rotate the calipers.
                            visited[RIndex] = true;
                            if (++RIndex == numPoints) {
                                RIndex = 0;
                            }
                        }
                        break;
                    case RCFlags.F_TOP:
                        if (visited[TIndex]) {
                            done = true;
                        } else {
                            // Compute box axes with E[T] as an edge.
                            U = -edges[TIndex];
                            V = -U.Perp;
                            UpdateBox(hullPoints[LIndex], hullPoints[RIndex],
                                hullPoints[BIndex], hullPoints[TIndex], ref U, ref V,
                                ref minAreaDiv4);

                            // Mark edge visited and rotate the calipers.
                            visited[TIndex] = true;
                            if (++TIndex == numPoints) {
                                TIndex = 0;
                            }
                        }
                        break;
                    case RCFlags.F_LEFT:
                        if (visited[LIndex]) {
                            done = true;
                        } else {
                            // Compute box axes with E[L] as an edge.
                            V = -edges[LIndex];
                            U = V.Perp;
                            UpdateBox(hullPoints[LIndex], hullPoints[RIndex],
                                hullPoints[BIndex], hullPoints[TIndex], ref U, ref V,
                                ref minAreaDiv4);

                            // Mark edge visited and rotate the calipers.
                            visited[LIndex] = true;
                            if (++LIndex == numPoints) {
                                LIndex = 0;
                            }
                        }
                        break;
                    case RCFlags.F_NONE:
                        // The polygon is a rectangle.
                        done = true;
                        break;
                }
            }

        }




    protected void UpdateBox(Vector2d LPoint, Vector2d RPoint, 
                             Vector2d BPoint, Vector2d TPoint, 
                             ref Vector2d U, ref Vector2d V, ref double minAreaDiv4)
    {
        Vector2d RLDiff = RPoint - LPoint;
        Vector2d TBDiff = TPoint - BPoint;
        double extent0 = ((double)0.5) * (U.Dot(RLDiff));
        double extent1 = ((double)0.5) * (V.Dot(TBDiff));
        double areaDiv4 = extent0 * extent1;
        if (areaDiv4 < minAreaDiv4) {
            minAreaDiv4 = areaDiv4;
            mMinBox.AxisX = U;
            mMinBox.AxisY = V;
            mMinBox.Extent[0] = extent0;
            mMinBox.Extent[1] = extent1;
            Vector2d LBDiff = LPoint - BPoint;
            mMinBox.Center = LPoint + U * extent0 + V * (extent1 - V.Dot(LBDiff));
        }
    }

}
}
