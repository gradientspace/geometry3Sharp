using System;
using System.Collections.Generic;
using System.Diagnostics;

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
			public IParametricCurve2d source;
			public int ID = 0;

            Colorf color = Colorf.Black;
            bool has_set_color = false;
            public Colorf Color {
                get { return color; }
                set { color = value; has_set_color = true; }
            }
            public bool HasSetColor {
                get { return has_set_color; }
            }

            protected void copy_to(Element new_element)
            {
                new_element.ID = this.ID;
                new_element.color = this.color;
                new_element.has_set_color = this.has_set_color;
                if (source != null)
                    new_element.source = this.source.Clone();
            }

			public abstract IEnumerable<Segment2d> SegmentItr();
			public abstract AxisAlignedBox2d Bounds();
            public abstract Element Clone();
		}

		public class SmoothCurveElement : Element 
		{
			public PolyLine2d polyLine;

			public override IEnumerable<Segment2d> SegmentItr() {
				return polyLine.SegmentItr();
			}
			public override AxisAlignedBox2d Bounds() {
				return polyLine.GetBounds();
			}

            public override Element Clone() {
                SmoothCurveElement curve = new SmoothCurveElement();
                this.copy_to(curve);
                curve.polyLine = (this.polyLine == this.source) ? curve.source as PolyLine2d : new PolyLine2d(this.polyLine);
                return curve;
            }
		}

		public class SmoothLoopElement : Element 
		{
			public Polygon2d polygon;

			public override IEnumerable<Segment2d> SegmentItr() {
				return polygon.SegmentItr();
			}
			public override AxisAlignedBox2d Bounds() {
				return polygon.GetBounds();
			}

            public override Element Clone()
            {
                SmoothLoopElement loop = new SmoothLoopElement();
                this.copy_to(loop);
                loop.polygon = (this.polygon == this.source) ? loop.source as Polygon2d : new Polygon2d(this.polygon);
                return loop;
            }
        }




        List<Element> vElements;


		public PlanarComplex() {
			vElements = new List<Element>();
		}


        public int ElementCount
        {
            get { return vElements.Count; }
        }

		public Element Add(IParametricCurve2d curve) {
			if ( curve.IsClosed ) {
				SmoothLoopElement e = new SmoothLoopElement();
				e.ID = id_generator++;
				e.source = curve;
				UpdateSampling(e);
				vElements.Add(e);
                return e;
			} else {
				SmoothCurveElement e = new SmoothCurveElement();
				e.ID = id_generator++;
				e.source = curve;
				UpdateSampling(e);
				vElements.Add(e);
                return e;
			}
		}


        public Element Add(Polygon2d poly)
        {
            SmoothLoopElement e = new SmoothLoopElement();
            e.ID = id_generator++;
            e.source = new Polygon2DCurve() { Polygon = poly };
            e.polygon = new Polygon2d(poly);
            vElements.Add(e);
            return e;
        }


        public Element Add(PolyLine2d pline)
        {
            SmoothCurveElement e = new SmoothCurveElement();
            e.ID = id_generator++;
            e.source = new PolyLine2DCurve() { Polyline = pline };
            e.polyLine = new PolyLine2d(pline);
            vElements.Add(e);
            return e;
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


		public IEnumerable<Element> ElementsItr() {
			foreach ( Element e in vElements ) {
                yield return e;
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

        public bool HasOpenCurves()
        {
            foreach (Element e in vElements) {
                if (e is SmoothCurveElement)
                    return true;
            }
            return false;
        }
            


        /// <summary>
        /// iterate through "leaf" curves, ie all the IParametricCurve2D's 
        /// embedded in loops that do not contain any child curves
        /// </summary>
        public IEnumerable<IParametricCurve2d> LoopLeafComponentsItr()
        {
            foreach ( Element e in vElements ) {
                if ( e is SmoothLoopElement ) {
                    IParametricCurve2d source = e.source;
                    if (source is IMultiCurve2d) {
                        foreach (var c in CurveUtils2.LeafCurvesIteration(source) )
                            yield return c;
                    } else
                        yield return source;
                }
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




		public void SplitAllLoops() {
			List<Element> vRemove = new List<Element>();
			List<IParametricCurve2d> vAdd = new List<IParametricCurve2d>();

			foreach ( SmoothLoopElement loop in LoopsItr() ) {
				if ( loop.source is IMultiCurve2d ) {
					vRemove.Add(loop);
					find_sub_elements(loop.source as IMultiCurve2d, vAdd);
				}
			}

			foreach ( var e in vRemove )
				Remove(e);
			foreach ( var c in vAdd )
				Add(c);
		}
		private void find_sub_elements(IMultiCurve2d multicurve, List<IParametricCurve2d> vAdd) {
			foreach ( IParametricCurve2d curve in multicurve.Curves ) {
				if ( curve is IMultiCurve2d ) {
					find_sub_elements(curve as IMultiCurve2d, vAdd);
				} else {
					vAdd.Add(curve);
				}
			}
		}




		public bool JoinElements(ComplexEndpoint2d a, ComplexEndpoint2d b, double loop_tolerance = MathUtil.ZeroTolerance) {
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
				if ( dDelta < loop_tolerance ) {

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




        public void ConvertToLoop(SmoothCurveElement curve, double tolerance = MathUtil.ZeroTolerance)
        {
			double dDelta = ( curve.polyLine.Start - curve.polyLine.End ).Length;
			if ( dDelta < tolerance ) {

				// handle degenerate element case
				if ( curve.polyLine.VertexCount == 2 ) {
					vElements.Remove(curve);
					return;
				}

                // should always be one of these since we constructed it in append()
                if ( curve.source is ParametricCurveSequence2 ) {
                    (curve.source as ParametricCurveSequence2).IsClosed = true;
                } else {
                    throw new Exception("PlanarComplex.ConvertToLoop: we have closed a loop but it is not a parametric seq??");
                }

				SmoothLoopElement loop = new SmoothLoopElement() {
					ID = id_generator++, source = curve.source
				};
				vElements.Remove(curve);
				vElements.Add(loop);
				UpdateSampling(loop);
			}
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



        public class GeneralSolid
        {
            public Element Outer;
            public List<Element> Holes = new List<Element>();
        }

        public class SolidRegionInfo
        {
            public List<GeneralPolygon2d> Polygons;
            public List<PlanarSolid2d> Solids;

            // map from polygon solids back to element(s) they came from
            public List<GeneralSolid> PolygonsSources;

            public AxisAlignedBox2d Bounds {
                get {
                    AxisAlignedBox2d bounds = AxisAlignedBox2d.Empty;
                    foreach (GeneralPolygon2d p in Polygons)
                        bounds.Contain(p.Bounds);
                    return bounds;
                }
            }


            public double Area {
                get {
                    double area = 0;
                    foreach (GeneralPolygon2d p in Polygons)
                        area += p.Area;
                    return area;
                }
            }


            public double HolesArea {
                get {
                    double area = 0;
                    foreach (GeneralPolygon2d p in Polygons) {
                        foreach ( Polygon2d h in p.Holes )
                            area += Math.Abs(h.SignedArea);
                    }
                    return area;
                }
            }
        }



		public struct FindSolidsOptions
		{
			public double SimplifyDeviationTolerance;
			public bool WantCurveSolids;
			public bool TrustOrientations;
			public bool AllowOverlappingHoles;

			public static readonly FindSolidsOptions Default = new FindSolidsOptions() {
				SimplifyDeviationTolerance = 0.1,
				WantCurveSolids = true,
				TrustOrientations = false,
				AllowOverlappingHoles = false
			};

            public static readonly FindSolidsOptions SortPolygons = new FindSolidsOptions() {
                SimplifyDeviationTolerance = 0.0,
                WantCurveSolids = false,
                TrustOrientations = true,
                AllowOverlappingHoles = false
            };
        }


		public SolidRegionInfo FindSolidRegions(double fSimplifyDeviationTol = 0.1, bool bWantCurveSolids = true)
		{
			FindSolidsOptions opt = FindSolidsOptions.Default;
			opt.SimplifyDeviationTolerance = fSimplifyDeviationTol;
			opt.WantCurveSolids = bWantCurveSolids;
			return FindSolidRegions(opt);
		}

        // Finds set of "solid" regions - eg boundary loops with interior holes.
        // Result has outer loops being clockwise, and holes counter-clockwise
		public SolidRegionInfo FindSolidRegions(FindSolidsOptions options) 
		{
			List<SmoothLoopElement> validLoops = new List<SmoothLoopElement>(LoopsItr());
			int N = validLoops.Count;

			// precompute bounding boxes
			int maxid = 0;
			foreach ( var v in validLoops )
				maxid = Math.Max(maxid, v.ID+1);
			AxisAlignedBox2d[] bounds = new AxisAlignedBox2d[maxid];
			foreach ( var v in validLoops )
				bounds[v.ID] = v.Bounds();

			// copy polygons, simplify if desired
			double fClusterTol = 0.0;		// don't do simple clustering, can lose corners
			double fDeviationTol = options.SimplifyDeviationTolerance;
			Polygon2d[] polygons = new Polygon2d[maxid];
			foreach ( var v in validLoops ) {
				Polygon2d p = new Polygon2d(v.polygon);
				if ( fClusterTol > 0 || fDeviationTol > 0 )
					p.Simplify(fClusterTol, fDeviationTol);
				polygons[v.ID] = p;
			}

			// sort by bbox containment to speed up testing (does it??)
			validLoops.Sort((x, y) => {
				return bounds[x.ID].Contains( bounds[y.ID] ) ? -1 : 1; 
			});

            // containment sets
			bool[] bIsContained = new bool[N];
			Dictionary<int, List<int>> ContainSets = new Dictionary<int, List<int>>();
            Dictionary<int, List<int>> ContainedParents = new Dictionary<int, List<int>>();

			bool bUseOrient = options.TrustOrientations;
			bool bWantCurveSolids = options.WantCurveSolids;
			bool bCheckHoles = ! options.AllowOverlappingHoles;

            // construct containment sets
			for ( int i = 0; i < N; ++i ) {
				SmoothLoopElement loopi = validLoops[i];
				Polygon2d polyi = polygons[loopi.ID];

				for ( int j = 0; j < N; ++j ) {
					if ( i == j )
						continue;
					SmoothLoopElement loopj = validLoops[j];
					Polygon2d polyj = polygons[loopj.ID];

					// if we are preserving orientations, holes cannot contain holes and
					// outers cannot contain outers!
					if (bUseOrient && loopj.polygon.IsClockwise == loopi.polygon.IsClockwise)
						continue;

					// cannot be contained if bounds are not contained
					if ( bounds[loopi.ID].Contains( bounds[loopj.ID] ) == false )
						continue;

					// any other early-outs??

					if ( polyi.Contains( polyj ) ) {
						if ( ContainSets.ContainsKey(i) == false )
							ContainSets.Add(i, new List<int>() );
						ContainSets[i].Add(j);
						bIsContained[j] = true;

                        if (ContainedParents.ContainsKey(j) == false)
                            ContainedParents.Add(j, new List<int>());
                        ContainedParents[j].Add(i);
					}

				}
			}

			List<GeneralPolygon2d> polysolids = new List<GeneralPolygon2d>();
            List<GeneralSolid> polySolidsInfo = new List<GeneralSolid>();

            List<PlanarSolid2d> solids = new List<PlanarSolid2d>();
			HashSet<SmoothLoopElement> used = new HashSet<SmoothLoopElement>();

            Dictionary<SmoothLoopElement, int> LoopToOuterIndex = new Dictionary<SmoothLoopElement, int>();

            List<int> ParentsToProcess = new List<int>();


            // The following is a lot of code but it is very similar, just not clear how
            // to refactor out the common functionality
            //   1) we find all the top-level uncontained polys and add them to the final polys list
            //   2a) for any poly contained in those parent-polys, that is not also contained in anything else,
            //       add as hole to that poly
            //   2b) remove all those used parents & holes from consideration
            //   2c) now find all the "new" top-level polys
            //   3) repeat 2a-c until done all polys
            //   4) any remaining polys must be interior solids w/ no holes
            //          **or** weird leftovers like intersecting polys...

            // add all top-level uncontained polys
            for (int i = 0; i < N; ++i) {
                SmoothLoopElement loopi = validLoops[i];
                if (bIsContained[i])
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

                int idx = polysolids.Count;
                LoopToOuterIndex[loopi] = idx;
                used.Add(loopi);

                if (ContainSets.ContainsKey(i))
                    ParentsToProcess.Add(i);

                polysolids.Add(g);
                polySolidsInfo.Add(new GeneralSolid() { Outer = loopi });
                if ( bWantCurveSolids )
                    solids.Add(s);
            }


            // keep iterating until we processed all parent loops
            while (ParentsToProcess.Count > 0 ) {

                List<int> ContainersToRemove = new List<int>();

                // now for all top-level polys that contain children, add those children
                // as long as they do not have multiple contain-parents
                foreach ( int i in ParentsToProcess ) {
                    SmoothLoopElement parentloop = validLoops[i];
                    int outer_idx = LoopToOuterIndex[parentloop];

                    List<int> children = ContainSets[i];
                    foreach ( int childj in children ) {
                        SmoothLoopElement childLoop = validLoops[childj];
                        Debug.Assert(used.Contains(childLoop) == false);

                        // skip multiply-contained children
                        List<int> parents = ContainedParents[childj];
                        if (parents.Count > 1)
                            continue;

					    Polygon2d hole_poly = polygons[childLoop.ID];
                        IParametricCurve2d hole_loop = (bWantCurveSolids) ? childLoop.source.Clone() : null;
                        if (hole_poly.IsClockwise) {
                            hole_poly.Reverse();
                            if ( bWantCurveSolids )
                                hole_loop.Reverse();
                        }

                        try {
                            polysolids[outer_idx].AddHole(hole_poly, bCheckHoles);
                            polySolidsInfo[outer_idx].Holes.Add(childLoop);
                            if ( hole_loop != null )
                                solids[outer_idx].AddHole(hole_loop);
                        } catch {
                            // don't add this hole - must intersect or something?
                            // We should have caught this earlier!
                        }

					    used.Add(childLoop);
                        if (ContainSets.ContainsKey(childj))
                            ContainersToRemove.Add(childj);
                    }
                    ContainersToRemove.Add(i);
                }

                // remove all containers that are no longer valid
                foreach (int ci in ContainersToRemove) {
                    ContainSets.Remove(ci);

                    // have to remove from each ContainedParents list
                    List<int> keys = new List<int>(ContainedParents.Keys);
                    foreach  (int j in keys) {
                        if (ContainedParents[j].Contains(ci))
                            ContainedParents[j].Remove(ci);
                    }
                }

                ParentsToProcess.Clear();

                // ok now find next-level uncontained parents...
                for (int i = 0; i < N; ++i) {
                    SmoothLoopElement loopi = validLoops[i];
                    if (used.Contains(loopi))
                        continue;
                    if (ContainSets.ContainsKey(i) == false)
                        continue;
                    List<int> parents = ContainedParents[i];
                    if (parents.Count > 0)
                        continue;

                    Polygon2d outer_poly = polygons[loopi.ID];
                    IParametricCurve2d outer_loop = (bWantCurveSolids) ? loopi.source.Clone() : null;
                    if (outer_poly.IsClockwise == false) {
                        outer_poly.Reverse();
                        if (bWantCurveSolids)
                            outer_loop.Reverse();
                    }

                    GeneralPolygon2d g = new GeneralPolygon2d();
                    g.Outer = outer_poly;
                    PlanarSolid2d s = new PlanarSolid2d();
                    if (bWantCurveSolids)
                        s.SetOuter(outer_loop, true);

                    int idx = polysolids.Count;
                    LoopToOuterIndex[loopi] = idx;
                    used.Add(loopi);

                    if (ContainSets.ContainsKey(i))
                        ParentsToProcess.Add(i);

                    polysolids.Add(g);
                    polySolidsInfo.Add(new GeneralSolid() { Outer = loopi });
                    if (bWantCurveSolids)
                        solids.Add(s);
                }
            }


            // any remaining loops must be top-level
            for (int i = 0; i < N; ++i) {
                SmoothLoopElement loopi = validLoops[i];
                if (used.Contains(loopi))
                    continue;

                Polygon2d outer_poly = polygons[loopi.ID];
                IParametricCurve2d outer_loop = (bWantCurveSolids) ? loopi.source.Clone() : null;
                if (outer_poly.IsClockwise == false) {
                    outer_poly.Reverse();
                    if (bWantCurveSolids)
                        outer_loop.Reverse();
                }

                GeneralPolygon2d g = new GeneralPolygon2d();
                g.Outer = outer_poly;
                PlanarSolid2d s = new PlanarSolid2d();
                if (bWantCurveSolids)
                    s.SetOuter(outer_loop, true);

                polysolids.Add(g);
                polySolidsInfo.Add(new GeneralSolid() { Outer = loopi });
                if (bWantCurveSolids)
                    solids.Add(s);
            }



            return new SolidRegionInfo() {
                Polygons = polysolids,
                PolygonsSources = polySolidsInfo,
                Solids = (bWantCurveSolids) ? solids : null
            };
		}





		public class ClosedLoopsInfo
		{
			public List<Polygon2d> Polygons;
			public List<IParametricCurve2d> Loops;


			public AxisAlignedBox2d Bounds {
				get {
					AxisAlignedBox2d bounds = AxisAlignedBox2d.Empty;
					foreach (Polygon2d p in Polygons)
						bounds.Contain(p.GetBounds());
					return bounds;
				}
			}
		}
		// returns set of closed loops (not necessarily solids)
		public ClosedLoopsInfo FindClosedLoops(double fSimplifyDeviationTol = 0.1)
		{
			List<SmoothLoopElement> loopElems = new List<SmoothLoopElement>(LoopsItr());
			int N = loopElems.Count;

			int maxid = 0;
			foreach (var v in loopElems)
				maxid = Math.Max(maxid, v.ID + 1);

			// copy polygons, simplify if desired
			double fClusterTol = 0.0;       // don't do simple clustering, can lose corners
			double fDeviationTol = fSimplifyDeviationTol;
			Polygon2d[] polygons = new Polygon2d[maxid];
			IParametricCurve2d[] curves = new IParametricCurve2d[maxid];
			foreach (var v in loopElems) {
				Polygon2d p = new Polygon2d(v.polygon);
				if (fClusterTol > 0 || fDeviationTol > 0)
					p.Simplify(fClusterTol, fDeviationTol);
				polygons[v.ID] = p;
				curves[v.ID] = v.source;
			}

			ClosedLoopsInfo ci = new ClosedLoopsInfo() {
				Polygons = new List<Polygon2d>(),
				Loops = new List<IParametricCurve2d>()
			};

			for (int i = 0; i < polygons.Length; ++i ) {
				if ( polygons[i] != null && polygons[i].VertexCount > 0 ) {
					ci.Polygons.Add(polygons[i]);
					ci.Loops.Add(curves[i]);
				}
			}

			return ci;
		}







		public class OpenCurvesInfo
		{
			public List<PolyLine2d> Polylines;
			public List<IParametricCurve2d> Curves;


			public AxisAlignedBox2d Bounds {
				get {
					AxisAlignedBox2d bounds = AxisAlignedBox2d.Empty;
					foreach (PolyLine2d p in Polylines)
						bounds.Contain(p.GetBounds());
					return bounds;
				}
			}
		}
		// returns set of open curves (ie non-solids)
		public OpenCurvesInfo FindOpenCurves(double fSimplifyDeviationTol = 0.1)
		{
			List<SmoothCurveElement> curveElems = new List<SmoothCurveElement>(CurvesItr());
			int N = curveElems.Count;

			int maxid = 0;
			foreach (var v in curveElems)
				maxid = Math.Max(maxid, v.ID + 1);

			// copy polygons, simplify if desired
			double fClusterTol = 0.0;       // don't do simple clustering, can lose corners
			double fDeviationTol = fSimplifyDeviationTol;
			PolyLine2d[] polylines = new PolyLine2d[maxid];
			IParametricCurve2d[] curves = new IParametricCurve2d[maxid];
			foreach (var v in curveElems) {
				PolyLine2d p = new PolyLine2d(v.polyLine);
				if (fClusterTol > 0 || fDeviationTol > 0)
					p.Simplify(fClusterTol, fDeviationTol);
				polylines[v.ID] = p;
				curves[v.ID] = v.source;
			}

			OpenCurvesInfo ci = new OpenCurvesInfo() {
				Polylines = new List<PolyLine2d>(),
				Curves = new List<IParametricCurve2d>()
			};

			for (int i = 0; i < polylines.Length; ++i ) {
				if ( polylines[i] != null && polylines[i].VertexCount > 0 ) {
					ci.Polylines.Add(polylines[i]);
					ci.Curves.Add(curves[i]);
				}
			}

			return ci;
		}






        public PlanarComplex Clone()
        {
            PlanarComplex clone = new PlanarComplex();
            clone.DistanceAccuracy = this.DistanceAccuracy;
            clone.AngleAccuracyDeg = this.AngleAccuracyDeg;
            clone.SpacingT = this.SpacingT;
            clone.MinimizeSampling = this.MinimizeSampling;
            clone.id_generator = this.id_generator;

            clone.vElements = new List<Element>(vElements.Count);
            foreach ( var element in vElements )
                clone.vElements.Add(element.Clone());

            return clone;
        }




        public void Append(PlanarComplex append)
        {
            foreach ( var element in append.vElements ) {
                element.ID = this.id_generator++;
                vElements.Add(element);
            }

            // clear elements in other so we don't make any mistakes...
            append.vElements.Clear();
        }



        public void Transform(ITransform2 xform, bool bApplyToSources, bool bRecomputePolygons = false)
        {
            foreach ( var element in vElements ) {
                if ( element is SmoothLoopElement ) {
                    var loop = element as SmoothLoopElement;
                    if (bApplyToSources && loop.source != loop.polygon)
                        loop.source.Transform(xform);

                    if ( bRecomputePolygons )
                        UpdateSampling(loop);
                    else
                        loop.polygon.Transform(xform);

                } else if (element is SmoothCurveElement) {
                    var curve = element as SmoothCurveElement;
                    if (bApplyToSources && curve.source != curve.polyLine)
                        curve.source.Transform(xform);

                    if (bRecomputePolygons)
                        UpdateSampling(curve);
                    else
                        curve.polyLine.Transform(xform);
                }
            }
        }









		public void PrintStats(string label = "") {
			System.Console.WriteLine("PlanarComplex Stats {0}", label);
			List<SmoothLoopElement> Loops = new List<SmoothLoopElement>(LoopsItr());
			List<SmoothCurveElement> Curves = new List<SmoothCurveElement>(CurvesItr());

            AxisAlignedBox2d bounds = Bounds();
            System.Console.WriteLine("  Bounding Box  w: {0} h: {1}  range {2} ", bounds.Width, bounds.Height, bounds);

			List<ComplexEndpoint2d> vEndpoints = new List<ComplexEndpoint2d>(EndpointsItr());
            System.Console.WriteLine("  Closed Loops {0}  Open Curves {1}   Open Endpoints {2}",
                Loops.Count, Curves.Count, vEndpoints.Count);

            int nSegments = CountType( typeof(Segment2d) );
            int nArcs = CountType(typeof(Arc2d));
            int nCircles = CountType(typeof(Circle2d));
            int nNURBS = CountType(typeof(NURBSCurve2));
            int nEllipses = CountType(typeof(Ellipse2d));
            int nEllipseArcs = CountType(typeof(EllipseArc2d));
            int nSeqs = CountType(typeof(ParametricCurveSequence2));
            System.Console.WriteLine("  [Type Counts]   // {0} multi-curves", nSeqs);
            System.Console.WriteLine("    segments {0,4}  arcs     {1,4}  circles      {2,4}", nSegments, nArcs, nCircles);
            System.Console.WriteLine("    nurbs    {0,4}  ellipses {1,4}  ellipse-arcs {2,4}", nNURBS, nEllipses, nEllipseArcs);
		}
        public int CountType(Type t)
        {
            int count = 0;
			foreach (Element loop in vElements) {
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
