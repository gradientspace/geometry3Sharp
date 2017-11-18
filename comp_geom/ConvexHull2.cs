using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace g3
{
    // Port of Wm5ConvexHull2 from WildMagic5 library by David Eberly / geometrictools.com

    // The input to the constructor is the array of vertices whose convex hull
    // is required.  If you want ConvexHull2 to delete the vertices during
    // destruction, set bOwner to 'true'.  Otherwise, you own the vertices and
    // must delete them yourself.
    //
    // You have a choice of speed versus accuracy.  The fastest choice is
    // Query::QT_INT64, but it gives up a lot of precision, scaling the points
    // to [0,2^{20}]^3.  The choice Query::QT_INTEGER gives up less precision,
    // scaling the points to [0,2^{24}]^3.  The choice Query::QT_RATIONAL uses
    // exact arithmetic, but is the slowest choice.  The choice Query::QT_REAL
    // uses floating-point arithmetic, but is not robust in all cases.


    /// <summary>
    /// Construct convex hull of a set of 2D points, with various accuracy levels.
    /// 
    /// HullIndices provides ordered indices of vertices of input points that form hull.
    /// </summary>
    public class ConvexHull2
    {
        //QueryNumberType mQueryType = QueryNumberType.QT_DOUBLE;
        IList<Vector2d> mVertices;
        int mNumVertices;

        int mDimension;
        int mNumSimplices;

        double mEpsilon;

        Vector2d[] mSVertices;
        int[] mIndices;

        Query2 mQuery;
        Vector2d mLineOrigin;
        Vector2d mLineDirection;


        /*
         * Outputs
         */

        public int Dimension {
            get { return mDimension; }
        }

        /// <summary>
        /// Number of convex polygon edges
        /// </summary>
        public int NumSimplices {
            get { return mNumSimplices; }
        }


        /// <summary>
        ///   array of indices into V that represent the convex polygon edges (NumSimplices total elements)
        /// The i-th edge has vertices
        ///   vertex[0] = V[I[i]]
        ///   vertex[1] = V[I[(i+1) % SQ]]
        /// </summary>
        public int[] HullIndices {
            get { return mIndices; }
        }


        /// <summary>
        /// Compute convex hull of input points. 
        /// epsilon is only used for check if points lie on a line (1d hull), not for rest of compute.
        /// </summary>
        public ConvexHull2(IList<Vector2d> vertices, double epsilon, QueryNumberType queryType)
        {
            //mQueryType = queryType;
            mVertices = vertices;
            mNumVertices = vertices.Count;
            mDimension = 0;
            mNumSimplices = 0;
            mIndices = null;
            mSVertices = null;

            mEpsilon = epsilon;

            mQuery = null;

            mLineOrigin = Vector2d.Zero;
            mLineDirection = Vector2d.Zero;

            Vector2d.Information info;
            Vector2d.GetInformation(mVertices, mEpsilon, out info);
            if (info.mDimension == 0) {
                mDimension = 0;
                mIndices = null;
                return;
            }

            if (info.mDimension == 1) {
                // The set is (nearly) collinear.  The caller is responsible for
                // creating a ConvexHull1 object.
                mDimension = 1;
                mLineOrigin = info.mOrigin;
                mLineDirection = info.mDirection0;
                return;
            }

            mDimension = 2;

            int i0 = info.mExtreme[0];
            int i1 = info.mExtreme[1];
            int i2 = info.mExtreme[2];

            mSVertices = new Vector2d[mNumVertices];

            if (queryType != QueryNumberType.QT_RATIONAL && queryType != QueryNumberType.QT_FILTERED) {

                // Transform the vertices to the square [0,1]^2.
                Vector2d minValue = new Vector2d(info.mMin[0], info.mMin[1]);
                double scale = ((double)1) / info.mMaxRange;
                for (int i = 0; i < mNumVertices; ++i) {
                    mSVertices[i] = (mVertices[i] - minValue) * scale;
                }

                double expand;
                if (queryType == QueryNumberType.QT_INT64) {
                    // Scale the vertices to the square [0,2^{20}]^2 to allow use of
                    // 64-bit integers.
                    expand = (double)(1 << 20);
                    mQuery = new Query2Int64(mSVertices);

                } else if (queryType == QueryNumberType.QT_INTEGER) {
                    throw new NotImplementedException("ConvexHull2: Query type QT_INTEGER not currently supported");
                    // Scale the vertices to the square [0,2^{24}]^2 to allow use of
                    // Integer.
                    //expand = (double)(1 << 24);
                    //mQuery = new Query2Integer(mNumVertices, mSVertices);
                } else {  // queryType == Query::QT_double
                    // No scaling for floating point.
                    expand = (double)1;
                    mQuery = new Query2d(mSVertices);
                }

                for (int i = 0; i < mNumVertices; ++i) 
                    mSVertices[i] *= expand;

            } else {
                throw new NotImplementedException("ConvexHull2: Query type QT_RATIONAL/QT_FILTERED not currently supported");

                // No transformation needed for exact rational arithmetic or filtered
                // predicates.
                //for (int i = 0; i < mSVertices.Length; ++i)
                //    mSVertices[i] = mVertices[i];

                //if (queryType == Query::QT_RATIONAL) {
                //    mQuery = new Query2Rational(mNumVertices, mSVertices);
                //} else { // queryType == Query::QT_FILTERED
                //    mQuery = new Query2Filtered(mNumVertices, mSVertices,
                //        mEpsilon);
                //}
            }


            Edge edge0 = null;
            Edge edge1 = null;
            Edge edge2 = null;

            if (info.mExtremeCCW) {
                edge0 = new Edge(i0, i1);
                edge1 = new Edge(i1, i2);
                edge2 = new Edge(i2, i0);
            } else {
                edge0 = new Edge(i0, i2);
                edge1 = new Edge(i2, i1);
                edge2 = new Edge(i1, i0);
            }

            edge0.Insert(edge2, edge1);
            edge1.Insert(edge0, edge2);
            edge2.Insert(edge1, edge0);

            Edge hull = edge0;

            // ideally we insert points in random order. but instead of
            // generating a permutation, just insert them using modulo-indexing, 
            // which is in the ballpark...
            int ii = 0;
            do {
                if (!Update(ref hull, ii))
                    return;
                ii = (ii + 31337) % mNumVertices;
            } while (ii != 0);

            // original code, vastly slower in pathological cases
            //for (int i = 0; i < mNumVertices; ++i) {
            //    if ( ! Update(ref hull, i) )
            //        return;
            //}

            hull.GetIndices(ref mNumSimplices, ref mIndices);
        }



        /// <summary>
        /// If the resulting Dimension == 1, then you can use this to get some info...
        /// </summary>
        public void Get1DHullInfo(out Vector2d origin, out Vector2d direction)
        {
            origin = mLineOrigin;
            direction = mLineDirection;
        }


        /// <summary>
        /// Extract convex hull polygon from input points
        /// </summary>
        public Polygon2d GetHullPolygon()
        {
            if (mIndices == null)
                return null;

            Polygon2d poly = new Polygon2d();
            for (int i = 0; i < mIndices.Length; ++i)
                poly.AppendVertex(mVertices[mIndices[i]]);

            return poly;
        }



        //ConvexHull1<double>* GetConvexHull1()
        //{
        //    assertion(mDimension == 1, "The dimension must be 1\n");
        //    if (mDimension != 1) {
        //        return 0;
        //    }

        //    double* projection = new1<double>(mNumVertices);
        //    for (int i = 0; i < mNumVertices; ++i) {
        //        Vector2d diff = mVertices[i] - mLineOrigin;
        //        projection[i] = mLineDirection.Dot(diff);
        //    }

        //    return new ConvexHull1<double>(mNumVertices, projection, mEpsilon, true,
        //        mQueryType);
        //}

        bool Update(ref Edge hull, int i)
        {
            // Locate an edge visible to the input point (if possible).
            Edge visible = null;
            Edge current = hull;
            do {
                if (current.GetSign(i, mQuery) > 0) {
                    visible = current;
                    break;
                }

                current = current.E1;
            }
            while (current != hull);

            if (visible == null) {
                // The point is inside the current hull; nothing to do.
                return true;
            }

            // Remove the visible edges.
            Edge adj0 = visible.E0;
            Debug.Assert(adj0 != null); // "Expecting nonnull adjacent\n");
            if (adj0 == null) {
                return false;
            }

            Edge adj1 = visible.E1;
            Debug.Assert(adj1 != null); // "Expecting nonnull adjacent\n");
            if (adj1 == null) {
                return false;
            }

            visible.DeleteSelf();

            while (adj0.GetSign(i, mQuery) > 0) {
                hull = adj0;
                adj0 = adj0.E0;
                Debug.Assert(adj0 != null); // "Expecting nonnull adjacent\n");
                if (adj0 == null) {
                    return false;
                }

                adj0.E1.DeleteSelf();
            }

            while (adj1.GetSign(i, mQuery) > 0) {
                hull = adj1;
                adj1 = adj1.E1;
                Debug.Assert(adj1 != null); // "Expecting nonnull adjacent\n");
                if (adj1 == null) {
                    return false;
                }

                adj1.E0.DeleteSelf();
            }

            // Insert the new edges formed by the input point and the end points of
            // the polyline of invisible edges.
            Edge edge0 = new Edge(adj0.V[1], i);
            Edge edge1 = new Edge(i, adj1.V[0]);
            edge0.Insert(adj0, edge1);
            edge1.Insert(edge0, adj1);
            hull = edge0;

            return true;
        }




        /// <summary>
        /// Internal class that represents edge of hull, and neighbours
        /// </summary>
        protected class Edge {
            public Vector2i V;
            public Edge E0;
            public Edge E1;
            public int Sign;
            public int Time;

            public Edge(int v0, int v1) {
                Sign = 0;
                Time = -1;
                V[0] = v0;
                V[1] = v1;
                E0 = null;
                E1 = null;
            }

            public int GetSign(int i, Query2 query) {
                if (i != Time) {
                    Time = i;
                    Sign = query.ToLine(i, V[0], V[1]);
                }
                return Sign;
            }

            public void Insert(Edge adj0, Edge adj1) {
                adj0.E1 = this;
                adj1.E0 = this;
                E0 = adj0;
                E1 = adj1;
            }

            public void DeleteSelf() {
                if (E0 != null) 
                    E0.E1 = null;
                if (E1 != null) 
                    E1.E0 = null;
            }

            public void GetIndices(ref int numIndices, ref int[] indices) {
                // Count the number of edge vertices and allocate the index array.
                numIndices = 0;
                Edge current = this;
                do {
                    ++numIndices;
                    current = current.E1;
                } while (current != this);

                indices = new int[numIndices];

                // Fill the index array.
                numIndices = 0;
                current = this;
                do {
                    indices[numIndices] = current.V[0];
                    ++numIndices;
                    current = current.E1;
                } while (current != this);
            }


        };
    }
}
