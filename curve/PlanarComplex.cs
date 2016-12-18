using System;
using System.Collections.Generic;

namespace g3 
{

	public struct ComplexSegment2d
	{
		public Segment2d seg;
		public bool isClosed;
		public PlanarComplex.Element source;
	}
	public struct ComplexEndpoint2d
	{
		public Vector2d v;
		public bool isStart;
		public PlanarComplex.SmoothCurveElement source;
	}


	public class PlanarComplex 
	{
		// these determine pointwise sampling rates

		public double DistanceAccuracy = 0.1;
		public double AngleAccuracyDeg = 5.0;



		public abstract class Element {
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
				e.source = curve;
				e.polygon = new Polygon2d(
					CurveSampler2.AutoSample(curve, DistanceAccuracy) );
				vElements.Add(e);
			} else {
				SmoothCurveElement e = new SmoothCurveElement();
				e.source = curve;
				e.polyLine = new PolyLine2d( 
                    CurveSampler2.AutoSample(curve, DistanceAccuracy) );
				vElements.Add(e);
			}
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
					s.source = e;
					yield return s;
				}
			}
		}



		// iterate through endpoints of open curves
		public IEnumerable<ComplexEndpoint2d> EndpointsItr() {
			foreach ( Element e in vElements ) {
				if ( e is SmoothCurveElement ) {
					SmoothCurveElement s = e as SmoothCurveElement;
					yield return new ComplexEndpoint2d() {
						v = s.polyLine.Start, isStart = true, source = s
					};
					yield return new ComplexEndpoint2d() {
						v = s.polyLine.End, isStart = false, source = s
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

	}
}
