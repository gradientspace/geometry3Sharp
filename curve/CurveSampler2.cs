using System;
using System.Collections.Generic;

namespace g3 {
	
	public static class CurveSampler2 
	{
		public static VectorArray2d AutoSample(IParametricCurve2d curve, double fSpacingLength, double fSpacingT)
		{
			if ( curve is ParametricCurveSequence2 )
				return AutoSample(curve as ParametricCurveSequence2, fSpacingLength, fSpacingT);

			if ( curve.HasArcLength )
				return SampleArcLen(curve, fSpacingLength);
			else
				return SampleT(curve, fSpacingT);
		}



		public static VectorArray2d SampleT(IParametricCurve2d curve, double fSpacing)
		{
			double fLenT = 1.0f;		// assumption for now is that all curves span [0,1] t-range

			int nSteps = Math.Max( (int)(fLenT / fSpacing)+1, 2 );

			VectorArray2d vec = new VectorArray2d(nSteps);

			for ( int i = 0; i < nSteps; ++i ) {
				double t = (double)i / (double)(nSteps-1);
				vec[i] = curve.SampleT(t * fLenT);
			}

			return vec;
		}


		public static VectorArray2d SampleArcLen(IParametricCurve2d curve, double fSpacing) 
		{
			if ( curve.HasArcLength == false )
				throw new InvalidOperationException("CurveSampler2.SampleArcLen: curve does not support arc length sampling!");

			double fLen = curve.ArcLength;
			int nSteps = Math.Max( (int)(fLen / fSpacing)+1, 2 );

			VectorArray2d vec = new VectorArray2d(nSteps);

			for ( int i = 0; i < nSteps; ++i ) {
				double t = (double)i / (double)(nSteps-1);
				vec[i] = curve.SampleArcLength(t * fLen);
			}

			return vec;
		}


		// [TODO]
		//   - faster vectorarray accumulation
		public static VectorArray2d AutoSample(ParametricCurveSequence2 curves, double fSpacingLength, double fSpacingT)
		{
			int N = curves.Count;
			bool bClosed = curves.IsClosed;

			VectorArray2d[] vecs = new VectorArray2d[N];
			int i = 0;
			int nTotal = 0;
			foreach ( IParametricCurve2d c in curves.Curves ) {
				vecs[i] = AutoSample(c, fSpacingLength, fSpacingT);
				nTotal += vecs[i].Count;
				i++;
			}

			int nDuplicates = (bClosed) ? N : N-1;		// handle closed here...
			nTotal -= nDuplicates;

			VectorArray2d final = new VectorArray2d(nTotal);

			// TODO this could be faster!
			int k = 0;
			for ( int vi = 0; vi < N; ++vi ) {
				VectorArray2d vv = vecs[vi];

				// skip final vertex unless we are on last curve (because it is
				// the same as first vertex of next curve)
				int nStop = (bClosed || vi < N-1) ? vv.Count-1 : vv.Count;
				for ( int j = 0; j < nStop; ++j )
					final[k++] = vv[j];
			}

			return final;
		}
	}
}
