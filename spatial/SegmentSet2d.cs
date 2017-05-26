using System;
using System.Collections.Generic;

namespace g3
{
	// [TODO] some kind of spatial sorting!!
	public class SegmentSet2d
	{
		List<Segment2d> Segments;

		public SegmentSet2d()
		{
			Segments = new List<Segment2d>();
		}

		public SegmentSet2d(GeneralPolygon2d poly) {
			Segments = new List<Segment2d>(poly.Outer.SegmentItr());
			foreach (var hole in poly.Holes)
				Segments.AddRange(hole.SegmentItr());
		}

		public SegmentSet2d(List<GeneralPolygon2d> polys)
		{
			Segments = new List<Segment2d>();
			foreach (var poly in polys) {
				Segments.AddRange(poly.Outer.SegmentItr());
				foreach (var hole in poly.Holes)
					Segments.AddRange(hole.SegmentItr());
			}
		}


		/// <summary>
		/// Find any segment in set that intersects input segment. 
		/// Returns intersection test, and index of segment
		/// </summary>
		public IntrSegment2Segment2 FindAnyIntersection(Segment2d seg, out int iSegment)
		{
			int N = Segments.Count;
			for (iSegment = 0; iSegment < N; ++iSegment) {
				IntrSegment2Segment2 intr = new IntrSegment2Segment2(seg, Segments[iSegment]);
				if (intr.Find())
					return intr;
			}
			return null;
		}


		public void FindAllIntersections(Segment2d seg, List<double> segmentTs, List<int> indices = null, List<IntrSegment2Segment2> tests = null, bool bOnlySimple = true)
		{
			int N = Segments.Count;
			for (int i = 0; i < N; ++i) {

				// want to make sure we do not miss any hits, even if it means
				// we get duplicates...
				IntrSegment2Segment2 intr = new IntrSegment2Segment2(seg, Segments[i]) {
					IntervalThreshold = MathUtil.ZeroTolerance
				};

				if (intr.Find()) {
					if (bOnlySimple && intr.IsSimpleIntersection == false)
						continue;

					if (tests != null)
						tests.Add(intr);
					if (indices != null)
						indices.Add(i);
					if ( segmentTs != null ) {
						segmentTs.Add(intr.Parameter0);
						if (!intr.IsSimpleIntersection)
							segmentTs.Add(intr.Parameter1);
					}

				}
			}
		}


	}
}
