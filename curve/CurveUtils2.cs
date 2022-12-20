using System;
using System.Collections.Generic;

namespace g3 
{
	public static class CurveUtils2 
	{


        public static IParametricCurve2d Convert(Polygon2d poly)
        {
            ParametricCurveSequence2 seq = new ParametricCurveSequence2();
            int N = poly.VertexCount;
            for (int i = 0; i < N; ++i)
                seq.Append(new Segment2d(poly[i], poly[(i + 1)%N]));
            seq.IsClosed = true;
            return seq;
        }


		// 2D curve utils?
		public static double SampledDistance(IParametricCurve2d c, Vector2d point, int N = 100)
		{
			double tMax = c.ParamLength;
			double min_dist = double.MaxValue;
			for ( int i = 0; i <= N; ++i ) {
				double fT = (double)i / (double)N;
				fT *= tMax;
				Vector2d p = c.SampleT(fT);
				double d = p.DistanceSquared(point);
				if ( d < min_dist )
					min_dist = d;
			}
			return Math.Sqrt(min_dist);
		}


        /// <summary>
        /// if the children of C are a tree, iterate through all the leaves
        /// </summary>
        public static IEnumerable<IParametricCurve2d> LeafCurvesIteration(IParametricCurve2d c)
        {
            if (c is IMultiCurve2d) {
                var multiC = c as IMultiCurve2d;
                foreach ( IParametricCurve2d c2 in multiC.Curves ) {
                    foreach (var c3 in LeafCurvesIteration(c2))
                        yield return c3;
                }
            } else
                yield return c;
        }


        public static List<IParametricCurve2d> Flatten(List<IParametricCurve2d> curves)
        {
            List<IParametricCurve2d> l = new List<IParametricCurve2d>();
            foreach ( IParametricCurve2d sourceC in curves) {
                foreach (var c in LeafCurvesIteration(sourceC))
                    l.Add(c);
            }
            return l;
        }
        public static List<IParametricCurve2d> Flatten(IParametricCurve2d curve) {
            return new List<IParametricCurve2d>(LeafCurvesIteration(curve));
        }


        // returns largest scalar coordinate value, useful for converting to integer coords
        public static Vector2d GetMaxOriginDistances(IEnumerable<Vector2d> vertices)
		{
			Vector2d max = Vector2d.Zero;
			foreach (Vector2d v in vertices) {
				double x = Math.Abs(v.x);
				if (x > max.x) max.x = x;
				double y = Math.Abs(v.y);
				if (y > max.y) max.y = y;
			}
			return max;
		}


		public static int FindNearestVertex(Vector2d pt, IEnumerable<Vector2d> vertices) {
			int i = 0;
			int iNearest = -1;
			double nearestSqr = double.MaxValue;
			foreach ( Vector2d v in vertices ) {
				double d = v.DistanceSquared(pt);
				if ( d < nearestSqr ) {
					nearestSqr = d;
					iNearest = i;
				}
				i++;
			}
			return iNearest;
		}


        public static Vector2d CentroidVtx(IEnumerable<Vector2d> vertices)
        {
            Vector2d c = Vector2d.Zero;
            int count = 0;
            foreach (Vector2d v in vertices) {
                c += v;
                count++;
            }
            if (count > 1)
                c /= (double)count;
            return c;
        }




        public static void LaplacianSmooth(IList<Vector2d> vertices, double alpha, int iterations, bool is_loop, bool in_place = false)
        {
            int N = vertices.Count;
            Vector2d[] temp = null;
            if (in_place == false)
                temp = new Vector2d[N];
            IList<Vector2d> set = (in_place) ? vertices : temp;

            double beta = 1.0 - alpha;
            for (int ii = 0; ii < iterations; ++ii) {
                if (is_loop) {
                    for (int i = 0; i < N; ++i) {
                        Vector2d c = (vertices[(i + N - 1) % N] + vertices[(i + 1) % N]) * 0.5;
                        set[i] = beta * vertices[i] + alpha * c;
                    }
                } else {
                    set[0] = vertices[0]; set[N - 1] = vertices[N - 1];
                    for (int i = 1; i < N-1; ++i) {
                        Vector2d c = (vertices[i-1] + vertices[i+1]) * 0.5;
                        set[i] = beta * vertices[i] + alpha * c;
                    }
                }

                if (in_place == false) {
                    for (int i = 0; i < N; ++i)
                        vertices[i] = set[i];
                }
            }
        }





        /// <summary>
        /// Constrained laplacian smoothing of input polygon, alpha X iterations.
        /// vertices are only allowed to move at most max_dist from constraint
        /// if bAllowShrink == false, vertices are kept outside input polygon
        /// if bAllowGrow == false, vertices are kept inside input polygon
        /// 
        /// max_dist is measured from vertex[i] to original_vertex[i], unless
        /// you set bPerVertexDistances = false, then distance to original polygon
        /// is used (which is much more expensive)
        /// 
        /// [TODO] this is pretty hacky...could be better in lots of ways...
        /// 
        /// </summary>
        public static void LaplacianSmoothConstrained(Polygon2d poly, double alpha, int iterations, 
            double max_dist, bool bAllowShrink, bool bAllowGrow, bool bPerVertexDistances = true)
        {
            Polygon2d origPoly = new Polygon2d(poly);

            int N = poly.VertexCount;
            Vector2d[] newV = new Vector2d[poly.VertexCount];

            double max_dist_sqr = max_dist * max_dist;

            double beta = 1.0 - alpha;
            for (int ii = 0; ii < iterations; ++ii) {
                for (int i = 0; i < N; ++i ) {
                    Vector2d curpos = poly[i];
                    Vector2d smoothpos = (poly[(i + N - 1) % N] + poly[(i + 1) % N]) * 0.5;

                    bool do_smooth = true;
                    if (bAllowShrink == false || bAllowGrow == false) {
                        bool is_inside = origPoly.Contains(smoothpos);
                        if (is_inside == true)
                            do_smooth = bAllowShrink;
                        else
                            do_smooth = bAllowGrow;
                    }

                    // [RMS] this is old code...I think not correct?
                    //bool contained = true;
                    //if (bAllowShrink == false || bAllowGrow == false)
                    //    contained = origPoly.Contains(smoothpos);
                    //bool do_smooth = true;
                    //if (bAllowShrink && contained == false)
                    //    do_smooth = false;
                    //if (bAllowGrow && contained == true)
                    //    do_smooth = false;

                    if ( do_smooth ) { 
                        Vector2d newpos = beta * curpos + alpha * smoothpos;
                        if (bPerVertexDistances) {
                            while (origPoly[i].DistanceSquared(newpos) > max_dist_sqr) 
                                newpos = (newpos + curpos) * 0.5;
                        } else {
                            while ( origPoly.DistanceSquared(newpos) > max_dist_sqr ) 
                                newpos = (newpos + curpos) * 0.5;
                        }
                        newV[i] = newpos;
                    } else {
                        newV[i] = curpos;
                    }
                }

                for (int i = 0; i < N; ++i)
                    poly[i] = newV[i];
            }
        }




        public static void LaplacianSmoothConstrained(GeneralPolygon2d solid, double alpha, int iterations, double max_dist, bool bAllowShrink, bool bAllowGrow)
        {
            LaplacianSmoothConstrained(solid.Outer, alpha, iterations, max_dist, bAllowShrink, bAllowGrow);
            foreach (Polygon2d hole in solid.Holes) {
                CurveUtils2.LaplacianSmoothConstrained(hole, alpha, iterations, max_dist, bAllowShrink, bAllowGrow);
            }
        }



		/// <summary>
		/// return list of objects for which keepF(obj) returns true
		/// </summary>
		public static List<T> Filter<T>(List<T> objects, Func<T, bool> keepF)
		{
			List<T> result = new List<T>(objects.Count);
			foreach (var obj in objects) {
				if (keepF(obj))
					result.Add(obj);
			}
			return result;
		}


		/// <summary>
		/// Split the input list into two new lists, based on predicate (set1 == true)
		/// </summary>
		public static void Split<T>(List<T> objects, out List<T> set1, out List<T> set2, Func<T, bool> splitF)
		{
			set1 = new List<T>();
			set2 = new List<T>();
			foreach (var obj in objects) {
				if (splitF(obj))
					set1.Add(obj);
				else
					set2.Add(obj);
			}
		}



        public static Polygon2d SplitToTargetLength(Polygon2d poly, double length)
        {
            Polygon2d result = new Polygon2d();
            result.AppendVertex(poly[0]);
            for (int j = 0; j < poly.VertexCount; ++j) {
                int next = (j + 1) % poly.VertexCount;
                double len = poly[j].Distance(poly[next]);
                if (len < length) {
                    result.AppendVertex(poly[next]);
                    continue;
                }

                int steps = (int)Math.Ceiling(len / length);
                for (int k = 1; k < steps; ++k) {
                    double t = (double)(k) / (double)steps;
                    Vector2d v = (1.0 - t) * poly[j] + (t) * poly[next];
                    result.AppendVertex(v);
                }

                if (j < poly.VertexCount - 1) {
                    Util.gDevAssert(poly[j].Distance(result.Vertices[result.VertexCount - 1]) > 0.0001);
                    result.AppendVertex(poly[next]);
                }
            }

            return result;
        }




        /// <summary>
        /// Remove polygons and polygon-holes smaller than minArea
        /// </summary>
        public static List<GeneralPolygon2d> FilterDegenerate(List<GeneralPolygon2d> polygons, double minArea)
        {
            List<GeneralPolygon2d> result = new List<GeneralPolygon2d>(polygons.Count);
            List<Polygon2d> filteredHoles = new List<Polygon2d>();
            foreach (var poly in polygons) {
                if (poly.Outer.Area < minArea)
                    continue;
                if (poly.Holes.Count == 0) {
                    result.Add(poly);
                    continue;
                }
                filteredHoles.Clear();
                for ( int i = 0; i < poly.Holes.Count; ++i ) {
                    Polygon2d hole = poly.Holes[i];
                    if (hole.Area > minArea)
                        filteredHoles.Add(hole);
                }
                if ( filteredHoles.Count != poly.Holes.Count ) {
                    poly.ClearHoles();
                    foreach (var h in filteredHoles)
                        poly.AddHole(h, false, false);
                }
                result.Add(poly);
            }
            return result;
        }



    }
}
