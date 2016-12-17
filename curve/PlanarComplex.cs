using System;
using System.Collections.Generic;

namespace g3 
{
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

			} else {
				SmoothCurveElement e = new SmoothCurveElement();
				e.source = curve;
				e.polyLine = new PolyLine2d( 
                    CurveSampler2.AutoSample(curve, DistanceAccuracy) );
				vElements.Add(e);
			}
		}



		public IEnumerable<Segment2d> AllSegmentsItr() {
			foreach ( Element e in vElements ) {
				foreach ( Segment2d seg in e.SegmentItr() )
					yield return seg;
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
