using System;
using System.Collections.Generic;

namespace g3 
{

	public struct ComplexSegment2d
	{
		public Segment2d seg;
		public bool isClosed;
		public PlanarComplex.Element element;
	}
	public struct ComplexEndpoint2d
	{
		public Vector2d v;
		public bool isStart;
		public PlanarComplex.SmoothCurveElement element;
	}


	public class PlanarComplex 
	{
		// these determine pointwise sampling rates
		public double DistanceAccuracy = 0.1;
		public double AngleAccuracyDeg = 5.0;
		public double SpacingT = 0.01;		// for curves where we don't know arc length
		public bool MinimizeSampling = false;	// if true, we don't subsample straight lines

		int id_generator = 1;

		public abstract class Element {
			public int ID = 0;

			public abstract IEnumerable<Segment2d> SegmentItr();
			public abstract AxisAlignedBox2d Bounds();
		}

		public class SmoothCurveElement : Element 
		{
			public IParametricCurve2d source;
			public PolyLine2d polyLine;

			public override IEnumerable<Segment2d> SegmentItr() {
				return polyLine.SegmentItr();
			}
			public override AxisAlignedBox2d Bounds() {
				return polyLine.GetBounds();
			}
		}

		public class SmoothLoopElement : Element 
		{
			public IParametricCurve2d source;
			public Polygon2d polygon;

			public override IEnumerable<Segment2d> SegmentItr() {
				return polygon.SegmentItr();
			}
			public override AxisAlignedBox2d Bounds() {
				return polygon.GetBounds();
			}
		}




		List<Element> vElements;


		public PlanarComplex() {
			vElements = new List<Element>();
		}


		public void Add(IParametricCurve2d curve) {
			if ( curve.IsClosed ) {
				SmoothLoopElement e = new SmoothLoopElement();
				e.ID = id_generator++;
				e.source = curve;
				UpdateSampling(e);
				vElements.Add(e);
			} else {
				SmoothCurveElement e = new SmoothCurveElement();
				e.ID = id_generator++;
				e.source = curve;
				UpdateSampling(e);
				vElements.Add(e);
			}
		}


        public void Remove(Element e)
        {
            vElements.Remove(e);
        }


		void UpdateSampling(SmoothCurveElement c) {
			if ( MinimizeSampling && c.source is Segment2d ) {
				c.polyLine = new PolyLine2d();
				c.polyLine.AppendVertex( ((Segment2d)c.source).P0 );
				c.polyLine.AppendVertex( ((Segment2d)c.source).P1 );
			} else {
				c.polyLine = new PolyLine2d( 
              	  CurveSampler2.AutoSample(c.source, DistanceAccuracy, SpacingT) );
			}
		}
		void UpdateSampling(SmoothLoopElement l) {
			l.polygon = new Polygon2d(
				CurveSampler2.AutoSample(l.source, DistanceAccuracy, SpacingT) );
		}


		public void Reverse(SmoothCurveElement c) {
			c.source.Reverse();
			UpdateSampling(c);
		}



		public IEnumerable<ComplexSegment2d> AllSegmentsItr() {
			foreach ( Element e in vElements ) {
				ComplexSegment2d s = new ComplexSegment2d();
				if ( e is SmoothLoopElement )
					s.isClosed = true;
				else if (e is SmoothCurveElement )
					s.isClosed = false;

				foreach ( Segment2d seg in e.SegmentItr() ) {
					s.seg = seg;
					s.element = e;
					yield return s;
				}
			}
		}


		public IEnumerable<SmoothLoopElement> LoopsItr() {
			foreach ( Element e in vElements ) {
				if ( e is SmoothLoopElement )
					yield return e as SmoothLoopElement;
			}
		}
		public IEnumerable<SmoothCurveElement> CurvesItr() {
			foreach ( Element e in vElements ) {
				if ( e is SmoothCurveElement )
					yield return e as SmoothCurveElement;
			}
		}

		// iterate through endpoints of open curves
		public IEnumerable<ComplexEndpoint2d> EndpointsItr() {
			foreach ( Element e in vElements ) {
				if ( e is SmoothCurveElement ) {
					SmoothCurveElement s = e as SmoothCurveElement;
					yield return new ComplexEndpoint2d() {
						v = s.polyLine.Start, isStart = true, element = s
					};
					yield return new ComplexEndpoint2d() {
						v = s.polyLine.End, isStart = false, element = s
					};
				}
			}
		}



		public AxisAlignedBox2d Bounds() {
			AxisAlignedBox2d box = AxisAlignedBox2d.Empty;
			foreach ( Element e in vElements ) {
				box.Contain(e.Bounds());
			}			
			return box;
		}





		public bool JoinElements(ComplexEndpoint2d a, ComplexEndpoint2d b) {
			if (a.element == b.element)
				throw new Exception("PlanarComplex.ChainElements: same curve!!");

			SmoothCurveElement c1 = a.element;
			SmoothCurveElement c2 = b.element;

			SmoothCurveElement joined = null;
			if ( a.isStart == false && b.isStart == true ) {
				vElements.Remove(c2);
				append(c1,c2);
				joined = c1;
			} else if ( a.isStart == true && b.isStart == false ) {
				vElements.Remove(c1);
				append(c2,c1);
				joined = c2;
			} else if (a.isStart == false) {		// end-to-end join
				c2.source.Reverse();
				vElements.Remove(c2);
				append(c1,c2);
				joined = c1;
			} else if (a.isStart == true) {		// start-to-start join
				c1.source.Reverse();
				vElements.Remove(c2);
				append(c1, c2);
				joined = c1;
			}

			if ( joined != null ) {
				// check if we have closed a loop
				double dDelta = ( joined.polyLine.Start - joined.polyLine.End ).Length;
				if ( dDelta < MathUtil.ZeroTolerance ) {

                    // should always be one of these since we constructed it in append()
                    if ( joined.source is ParametricCurveSequence2 ) {
                        (joined.source as ParametricCurveSequence2).IsClosed = true;
                    } else {
                        throw new Exception("PlanarComplex.JoinElements: we have closed a loop but it is not a parametric seq??");
                    }

					SmoothLoopElement loop = new SmoothLoopElement() {
						ID = id_generator++, source = joined.source
					};
					vElements.Remove(joined);
					vElements.Add(loop);
					UpdateSampling(loop);
				}
				return true;
			}

			return false;
		}




		void append(SmoothCurveElement cTo, SmoothCurveElement cAppend) {
			ParametricCurveSequence2 use = null;
			if ( cTo.source is ParametricCurveSequence2 ) {
				use = cTo.source as ParametricCurveSequence2;
			} else {
				use = new ParametricCurveSequence2();
				use.Append( cTo.source );
			}

			if ( cAppend.source is ParametricCurveSequence2 ) {
				var cseq = cAppend.source as ParametricCurveSequence2;
				foreach ( var c in cseq.Curves )
					use.Append(c);
			} else {
				use.Append( cAppend.source );
			}

			cTo.source = use;
			UpdateSampling(cTo);
		}



        public struct SolidRegionInfo
        {
            public List<GeneralPolygon2d> Polygons;
            public List<PlanarSolid2d> Solids;
        }

        // Finds set of "solid" regions - eg boundary loops with interior holes.
        // Result has outer loops being clockwise, and holes counter-clockwise
		public SolidRegionInfo FindSolidRegions(double fSimplifyDeviationTol = 0.1, bool bWantCurveSolids = true) 
		{
			List<SmoothLoopElement> valid = new List<SmoothLoopElement>(LoopsItr());
			int N = valid.Count;

			// precompute bounding boxes
			int maxid = 0;
			foreach ( var v in valid )
				maxid = Math.Max(maxid, v.ID+1);
			AxisAlignedBox2d[] bounds = new AxisAlignedBox2d[maxid];
			foreach ( var v in valid )
				bounds[v.ID] = v.Bounds();

			// copy polygons, simplify if desired
			double fClusterTol = 0.0;		// don't do simple clustering, can lose corners
			double fDeviationTol = fSimplifyDeviationTol;
			Polygon2d[] polygons = new Polygon2d[maxid];
			foreach ( var v in valid ) {
				Polygon2d p = new Polygon2d(v.polygon);
				if ( fClusterTol > 0 || fDeviationTol > 0 )
					p.Simplify(fClusterTol, fDeviationTol);
				polygons[v.ID] = p;
			}

			// sort by bbox containment to speed up testing (does it??)
			valid.Sort((x, y) => {
				return bounds[x.ID].Contains( bounds[y.ID] ) ? -1 : 1; 
			});

            // containment sets
			bool[] bIsContained = new bool[N];
			Dictionary<int, List<int>> ContainSets = new Dictionary<int, List<int>>();

            // construct containment sets
			for ( int i = 0; i < N; ++i ) {
				SmoothLoopElement loopi = valid[i];
				Polygon2d polyi = polygons[loopi.ID];

				for ( int j = 0; j < N; ++j ) {
					if ( i == j )
						continue;
					SmoothLoopElement loopj = valid[j];
					Polygon2d polyj = polygons[loopj.ID];

					// cannot be contained if bounds are not contained
					if ( bounds[loopi.ID].Contains( bounds[loopj.ID] ) == false )
						continue;

					// any other early-outs??

					if ( polyi.Contains( polyj ) ) {
						if ( ContainSets.ContainsKey(i) == false )
							ContainSets.Add(i, new List<int>() );
						ContainSets[i].Add(j);
						bIsContained[j] = true;
					}
	
				}
			}

			List<GeneralPolygon2d> polysolids = new List<GeneralPolygon2d>();
            List<PlanarSolid2d> solids = new List<PlanarSolid2d>();
			List<SmoothLoopElement> used = new List<SmoothLoopElement>();

            // extract solids from containment relationships
            foreach ( var i in ContainSets.Keys ) {
                SmoothLoopElement outer_element = valid[i];
                used.Add(outer_element);
                if ( bIsContained[i] )
					throw new Exception("PlanarComplex.FindSolidRegions: multiply-nested regions not supported!");

				Polygon2d outer_poly = polygons[outer_element.ID];
                IParametricCurve2d outer_loop = (bWantCurveSolids) ? outer_element.source.Clone() : null;
                if (outer_poly.IsClockwise == false) {
                    outer_poly.Reverse();
                    if ( bWantCurveSolids )
                        outer_loop.Reverse();
                }

				GeneralPolygon2d g = new GeneralPolygon2d();
				g.Outer = outer_poly;
                PlanarSolid2d s = new PlanarSolid2d();
                if (bWantCurveSolids) 
                    s.SetOuter(outer_loop, true);

				foreach ( int hi in ContainSets[i] ) {
					SmoothLoopElement he = valid[hi];
					used.Add(he);
					Polygon2d hole_poly = polygons[he.ID];
                    IParametricCurve2d hole_loop = (bWantCurveSolids) ? he.source.Clone() : null;
                    if (hole_poly.IsClockwise) {
                        hole_poly.Reverse();
                        if ( bWantCurveSolids )
                            hole_loop.Reverse();
                    }

                    try {
                        g.AddHole(hole_poly);
                        if ( hole_loop != null )
                            s.AddHole(hole_loop);
                    } catch {
                        // don't add this hole - must intersect or something
                        // We should have caught this earlier!
                    }
				}

				polysolids.Add(g);
                if ( bWantCurveSolids )
                    solids.Add(s);
			}

            for (int i = 0; i < N; ++i) {
                SmoothLoopElement loopi = valid[i];
                if (used.Contains(loopi))
                    continue;

				Polygon2d outer_poly = polygons[loopi.ID];
                IParametricCurve2d outer_loop = (bWantCurveSolids) ? loopi.source.Clone() : null;
                if (outer_poly.IsClockwise == false) {
                    outer_poly.Reverse();
                    if ( bWantCurveSolids )
                        outer_loop.Reverse();
                }

                GeneralPolygon2d g = new GeneralPolygon2d();
                g.Outer = outer_poly;
                PlanarSolid2d s = new PlanarSolid2d();
                if ( bWantCurveSolids )
                    s.SetOuter(outer_loop, true);

                polysolids.Add(g);
                if ( bWantCurveSolids )
                    solids.Add(s);
            }

            return new SolidRegionInfo() {
                Polygons = polysolids,
                Solids = (bWantCurveSolids) ? solids : null
            };
		}





		public void PrintStats(string label = "") {
			System.Console.WriteLine("PlanarComplex Stats {0}", label);
			List<SmoothLoopElement> Loops = new List<SmoothLoopElement>(LoopsItr());
			List<SmoothCurveElement> Curves = new List<SmoothCurveElement>(CurvesItr());

            AxisAlignedBox2d bounds = Bounds();
            System.Console.WriteLine("  Bounding Box: " + bounds);

			System.Console.WriteLine("  Closed Loops: " + Loops.Count.ToString());
			System.Console.WriteLine("  Open Curves: " + Curves.Count.ToString());

			List<ComplexEndpoint2d> vEndpoints = new List<ComplexEndpoint2d>(EndpointsItr());
			System.Console.WriteLine("  Open Endpoints: " + vEndpoints.Count.ToString());

            int nSegments = CountType( typeof(Segment2d) );
            int nArcs = CountType(typeof(Arc2d));
            int nCircles = CountType(typeof(Circle2d));
            int nNURBS = CountType(typeof(NURBSCurve2));
            int nEllipses = CountType(typeof(Ellipse2d));
            int nEllipseArcs = CountType(typeof(EllipseArc2d));
            int nSeqs = CountType(typeof(ParametricCurveSequence2));
            System.Console.WriteLine("  Type Counts: ");
            System.Console.WriteLine("    segments {0,4}  arcs     {1,4}  circles      {2,4}", nSegments, nArcs, nCircles);
            System.Console.WriteLine("    nurbs    {0,4}  ellipses {1,4}  ellipse-arcs {2,4}", nNURBS, nEllipses, nEllipseArcs);
            System.Console.WriteLine("    multi    {0,4}  ", nSeqs);
		}
        public int CountType(Type t)
        {
            int count = 0;
            foreach (SmoothLoopElement loop in LoopsItr()) {
                if (loop.source.GetType() == t)
                    count++;
                if (loop.source is IMultiCurve2d)
                    count += CountType(loop.source as IMultiCurve2d, t);
            }
            return count;
        }
        public int CountType(IMultiCurve2d curve, Type t)
        {
            int count = 0;
            foreach ( IParametricCurve2d c in curve.Curves ) {
                if (c.GetType() == t)
                    count++;
                if (c is IMultiCurve2d)
                    count += CountType(c as IMultiCurve2d, t);
            }
            return count;
        }

	}
}
