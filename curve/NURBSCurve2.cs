using System;
using System.Collections.Generic;
using System.Linq;


namespace g3
{
    // ported from WildMagic5 NURBSCurve2
    public class NURBSCurve2 : BaseCurve2, IParametricCurve2d
    {
        // Construction and destruction. Internal copies of the
        // input arrays are made, so to dynamically change control points,
        // control weights, or knots, you must use the 'SetControlPoint',
        // 'GetControlPoint', 'SetControlWeight', 'GetControlWeight', and 'Knot'
        // member functions.

        // The homogeneous input points are (x,y,w) where the (x,y) values are
        // stored in the ctrlPoint array and the w values are stored in the
        // ctrlWeight array.  The output points from curve evaluations are of
        // the form (x',y') = (x/w,y/w).

        // Uniform spline.  The number of control points is n+1 >= 2.  The degree
        // of the spline is d and must satisfy 1 <= d <= n.  The knots are
        // implicitly calculated in [0,1].  If open is 'true', the spline is
        // open and the knots are
        //   t[i] = 0,               0 <= i <= d
        //          (i-d)/(n+1-d),   d+1 <= i <= n
        //          1,               n+1 <= i <= n+d+1
        // If open is 'false', the spline is periodic and the knots are
        //   t[i] = (i-d)/(n+1-d),   0 <= i <= n+d+1
        // If loop is 'true', extra control points are added to generate a closed
        // curve.  For an open spline, the control point array is reallocated and
        // one extra control point is added, set to the first control point
        // C[n+1] = C[0].  For a periodic spline, the control point array is
        // reallocated and the first d points are replicated.  In either case the
        // knot array is calculated accordingly.
		//
		// [RMS] "open" and "loop" are super-confusing here. Perhaps NURBSCurve2 should
		//   be refactored into several subclasses w/ different constructors, so that
		//   the naming makes sense?

        public NURBSCurve2(int numCtrlPoints, Vector2d[] ctrlPoint, double[] ctrlWeight, int degree, bool loop, bool open)
            : base(0,1)
        {
            if (numCtrlPoints < 2)
                throw new Exception("NURBSCurve2(): only received " + numCtrlPoints + " control points!");
            if (degree < 1 || degree > numCtrlPoints - 1)
                throw new Exception("NURBSCurve2(): invalid degree " + degree);

            mLoop = loop;
            mNumCtrlPoints = numCtrlPoints;
            mReplicate = (loop ? (open ? 1 : degree) : 0);
            CreateControl(ctrlPoint, ctrlWeight);
            mBasis = new BSplineBasis(mNumCtrlPoints + mReplicate, degree, open);
        }

        // Open, nonuniform spline, that takes external knot vector. 
		//
		// if bIsInteriorKnot, the knot array must have n-d-1 elements, the standard start/end 
		//   sequences of #degree 0/1 knots will be automatically added by internal BSplineBasis.
		//
		// if !bIsInteriorKnot, the knot array must have n+d+1 elements and is used directly.
		//
		// eg for 7 control points degree-3 curve the full knot vector would be [0 0 0 0 a b c 1 1 1 1],
		//   and the interior knot vector would be [a b c]. 
		//
		// The knot elements must be nondecreasing.  Each element must be in [0,1]. Note that
		//   knot vectors can be arbitrary normalized by dividing by the largest knot, if 
		//   you have a knot vector with values > 1
		// 
		// loop=true duplicates the first control point to force loop closure, however this
		//   was broken in the WildMagic code because it didn't add a knot. I am not
		//   quite sure what to do here - a new non-1 knot value needs to be inserted for
		//   the previous last control point, somehow. Or perhaps the knot vector needs
		//   to be extended, ie the final degree-duplicate knots need value > 1?
		//
		// Currently to create a closed NURBS curve, the caller must handle this duplication
		//   themselves. 
        public NURBSCurve2(int numCtrlPoints, Vector2d[] ctrlPoint, double[] ctrlWeight, int degree, bool loop, 
		                   double[] knot, bool bIsInteriorKnot = true )
            : base(0,1)
        {
            if (numCtrlPoints < 2)
                throw new Exception("NURBSCurve2(): only received " + numCtrlPoints + " control points!");
            if (degree < 1 || degree > numCtrlPoints - 1)
                throw new Exception("NURBSCurve2(): invalid degree " + degree);

			// [RMS] loop mode doesn't work yet
			if ( loop == true )
				throw new Exception("NURBSCUrve2(): loop mode is broken?");

            mLoop = loop;
            mNumCtrlPoints = numCtrlPoints;
            mReplicate = (loop ? 1 : 0);
            CreateControl(ctrlPoint, ctrlWeight);
			mBasis = new BSplineBasis(mNumCtrlPoints + mReplicate, degree, knot, bIsInteriorKnot);
        }


        // used in Clone()
        protected NURBSCurve2() : base(0,1)
        {
        }


        //virtual ~NURBSCurve2();

        public int GetNumCtrlPoints() {
            return mNumCtrlPoints;
        }
        public int GetDegree() {
            return mBasis.GetDegree();
        }

		// [RMS] this is only applicable to Uniform curves, confusing to have in API
		//   for class that also supports non-uniform curves. And "non-open" curve
		//   can still be closed depending on CVs!
        //public bool IsOpen() {
        //    return mBasis.IsOpen();
        //}

        public bool IsUniform() {
            return mBasis.IsUniform();
        }

		// [RMS] loop mode is broken for non-uniform curves. And "non-open" curve
		//   can still be closed depending on CVs!
        //public bool IsLoop() {
        //    return mLoop;
        //}

        // Control points and weights may be changed at any time.  The input index
        // should be valid (0 <= i <= n).  If it is invalid, the return value of
        // GetControlPoint is a vector whose components are all double.MaxValue, and the
        // return value of GetControlWeight is double.MaxValue. 
        public void SetControlPoint(int i, Vector2d ctrl) {
            if (0 <= i && i < mNumCtrlPoints) {
                // Set the control point.
                mCtrlPoint[i] = ctrl;
                // Set the replicated control point.
                if (i < mReplicate) 
                    mCtrlPoint[mNumCtrlPoints + i] = ctrl;
            }
        }
        public Vector2d GetControlPoint(int i) {
            if (0 <= i && i < mNumCtrlPoints)
                return mCtrlPoint[i];
            return new Vector2d(double.MaxValue, double.MaxValue);
        }
        public void SetControlWeight(int i, double weight) {
            if (0 <= i && i < mNumCtrlPoints) {
                // Set the control weight.
                mCtrlWeight[i] = weight;
                // Set the replicated control weight.
                if (i < mReplicate) 
                    mCtrlWeight[mNumCtrlPoints + i] = weight;
            }
        }
        public double GetControlWeight(int i) {
            if (0 <= i && i < mNumCtrlPoints) 
                return mCtrlWeight[i];
            return double.MaxValue;
        }

        // The knot values can be changed only if the basis function is nonuniform
        // and the input index is valid (0 <= i <= n-d-1).  If these conditions
        // are not satisfied, GetKnot returns double.MaxValue.
        public void SetKnot(int i, double value) {
            mBasis.SetInteriorKnot(i, value);
        }
        public double GetKnot(int i) {
                return mBasis.GetInteriorKnot(i);
        }

        // The spline is defined for 0 <= t <= 1.  If a t-value is outside [0,1],
        // an open spline clamps t to [0,1].  That is, if t > 1, t is set to 1;
        // if t < 0, t is set to 0.  A periodic spline wraps to to [0,1].  That
        // is, if t is outside [0,1], then t is set to t-floor(t).
        public override Vector2d GetPosition(double t)
        {
            int i, imin = 0, imax = 0;
            mBasis.Compute(t, 0, ref imin, ref imax);

			// [RMS] clamp imax to valid range in mCtrlWeight/Point. 
			// Have only seen this happen in one file w/curve coming from DXF.
			// Possibly actually a bug in how we construct curve? Not sure though.
			if (imax >= mCtrlWeight.Length)
				imax = mCtrlWeight.Length - 1;

            // Compute position.
            double tmp;
            Vector2d X = Vector2d.Zero;
            double w = (double)0;
            for (i = imin; i <= imax; ++i) {
                tmp = mBasis.GetD0(i) * mCtrlWeight[i];
                X += tmp * mCtrlPoint[i];
                w += tmp;
            }
            double invW = 1.0 / w;
            return invW * X;
        }

        public override Vector2d GetFirstDerivative(double t)
        {
            int i, imin = 0, imax = 0;
            mBasis.Compute(t, 0, ref imin, ref imax);
            mBasis.Compute(t, 1, ref imin, ref imax);

			// [RMS] clamp imax to valid range in mCtrlWeight/Point. See comment in GetPosition()
			if (imax >= mCtrlWeight.Length)
				imax = mCtrlWeight.Length-1;

            // Compute position.
            double tmp;
            Vector2d X = Vector2d.Zero;
            double w = (double)0;
            for (i = imin; i <= imax; ++i) {
                tmp = mBasis.GetD0(i) * mCtrlWeight[i];
                X += tmp * mCtrlPoint[i];
                w += tmp;
            }
            double invW = 1.0 / w;
            Vector2d P = invW * X;

            // Compute first derivative.
            Vector2d XDer1 = Vector2d.Zero;
            double wDer1 = (double)0;
            for (i = imin; i <= imax; ++i) {
                tmp = mBasis.GetD1(i) * mCtrlWeight[i];
                XDer1 += tmp * mCtrlPoint[i];
                wDer1 += tmp;
            }
            return invW * (XDer1 - wDer1 * P);
        }

        public override Vector2d GetSecondDerivative(double t)
        {
            CurveDerivatives cd = new CurveDerivatives();
            cd.init(false, false, true, false);
            Get(t, ref cd);
            return cd.d2;
        }

        public override Vector2d GetThirdDerivative(double t)
        {
            CurveDerivatives cd = new CurveDerivatives();
            cd.init(false, false, false, true);
            Get(t, ref cd);
            return cd.d3;
        }

        // This function sequentially computes position and then higher
        // derivatives. It will stop at the highest derivative you request.
        // More efficient than calling single-value functions above, which
        // would repeat lots of calculations
        public struct CurveDerivatives
        {
            public Vector2d p, d1, d2, d3;
            public bool bPosition, bDer1, bDer2, bDer3;
            public void init() { bPosition = bDer1 = bDer2 = bDer3 = false; }
            public void init(bool pos, bool der1, bool der2, bool der3) {
                bPosition = pos; bDer1 = der1; bDer2 = der2; bDer3 = der3;
            }
        }
        public void Get(double t, ref CurveDerivatives result)
        {
            int i, imin = 0, imax = 0;
            if (result.bDer3) {
                mBasis.Compute(t, 0, ref imin, ref imax);
                mBasis.Compute(t, 1, ref imin, ref imax);
                mBasis.Compute(t, 2, ref imin, ref imax);
                mBasis.Compute(t, 3, ref imin, ref imax);
            } else if (result.bDer2) {
                mBasis.Compute(t, 0, ref imin, ref imax);
                mBasis.Compute(t, 1, ref imin, ref imax);
                mBasis.Compute(t, 2, ref imin, ref imax);
            } else if (result.bDer1) {
                mBasis.Compute(t, 0, ref imin, ref imax);
                mBasis.Compute(t, 1, ref imin, ref imax);
            } else  // pos
                mBasis.Compute(t, 0, ref imin, ref imax);

			// [RMS] clamp imax to valid range in mCtrlWeight/Point. See comment in GetPosition()
			if (imax >= mCtrlWeight.Length)
				imax = mCtrlWeight.Length - 1;

            double tmp;

            // Compute position.
            Vector2d X = Vector2d.Zero;
            double w = (double)0;
            for (i = imin; i <= imax; ++i) {
                tmp = mBasis.GetD0(i) * mCtrlWeight[i];
                X += tmp * mCtrlPoint[i];
                w += tmp;
            }
            double invW = 1.0 / w;
            Vector2d P = invW * X;
            result.p = P;
            result.bPosition = true;

            if (result.bDer1 == false && result.bDer2 == false && result.bDer3 == false)
                return;

            // Compute first derivative.
            Vector2d XDer1 = Vector2d.Zero;
            double wDer1 = (double)0;
            for (i = imin; i <= imax; ++i) {
                tmp = mBasis.GetD1(i) * mCtrlWeight[i];
                XDer1 += tmp * mCtrlPoint[i];
                wDer1 += tmp;
            }
            Vector2d PDer1 = invW * (XDer1 - wDer1 * P);
            result.d1 = PDer1;
            result.bDer1 = true;

            if (result.bDer2 == false && result.bDer3 == false)
                return;

            // Compute second derivative.
            Vector2d XDer2 = Vector2d.Zero;
            double wDer2 = (double)0;
            for (i = imin; i <= imax; ++i) {
                tmp = mBasis.GetD2(i) * mCtrlWeight[i];
                XDer2 += tmp * mCtrlPoint[i];
                wDer2 += tmp;
            }
            Vector2d PDer2 = invW * (XDer2 - ((double)2) * wDer1 * PDer1 - wDer2 * P);
            result.d2 = PDer2;
            result.bDer2 = true;

            if (result.bDer3 == false)
                return;

            // Compute third derivative.
            Vector2d XDer3 = Vector2d.Zero;
            double wDer3 = (double)0;
            for (i = imin; i <= imax; i++) {
                tmp = mBasis.GetD3(i) * mCtrlWeight[i];
                XDer3 += tmp * mCtrlPoint[i];
                wDer3 += tmp;
            }
            result.d3 = invW * (XDer3 - ((double)3) * wDer1 * PDer2 -
                ((double)3) * wDer2 * PDer1 - wDer3 * P);
        }

        // Access the basis function to compute it without control points.  This
        // is useful for least squares fitting of curves.
        public BSplineBasis GetBasis() {
            return mBasis;
        }

        // Replicate the necessary number of control points when the Create
        // function has loop equal to true, in which case the spline curve must
        // be a closed curve.
        protected void CreateControl(Vector2d[] ctrlPoint, double[] ctrlWeight)
        {
            int newNumCtrlPoints = mNumCtrlPoints + mReplicate;

            mCtrlPoint = new Vector2d[newNumCtrlPoints];
            Array.Copy(ctrlPoint, mCtrlPoint, mNumCtrlPoints);
            //memcpy(mCtrlPoint, ctrlPoint, mNumCtrlPoints * sizeof(Vector2d));

            mCtrlWeight = new double[newNumCtrlPoints];
            Array.Copy(ctrlWeight, mCtrlWeight, mNumCtrlPoints);
            //memcpy(mCtrlWeight, ctrlWeight, mNumCtrlPoints * sizeof(double));

            for (int i = 0; i < mReplicate; ++i) {
                mCtrlPoint[mNumCtrlPoints + i] = ctrlPoint[i];
                mCtrlWeight[mNumCtrlPoints + i] = ctrlWeight[i];
            }
        }

        protected int mNumCtrlPoints;
        protected Vector2d[] mCtrlPoint;  // ctrl[n+1]
        protected double[] mCtrlWeight;           // weight[n+1]
        protected bool mLoop;
        protected BSplineBasis mBasis;
        protected int mReplicate;  // the number of replicated control points

        protected bool is_closed = false;       // added by RMS, used in g3




        /*
         * IParametricCurve2d implementation
         */

		// [RMS] original NURBSCurve2 WildMagic5 code does not explicitly support "closed" NURBS curves.
		//   However you can create a closed NURBS curve yourself by setting appropriate control points.
		//   So, this value is independent of IsOpen/IsLoop above
		public bool IsClosed {
			get { return is_closed; }
			set { is_closed = value; }
        }

		// can call SampleT in range [0,ParamLength]
		public double ParamLength {
            get { return mTMax - mTMin; }
        }
		public Vector2d SampleT(double t) {
            return GetPosition(t);
        }

        public Vector2d TangentT(double t) {
            return GetFirstDerivative(t).Normalized;
        }

		public bool HasArcLength {
            get { return true; }
        }
		public double ArcLength {
            get { return GetTotalLength(); }
        }
		public Vector2d SampleArcLength(double a)
        {
            double t = GetTime(a);
            return GetPosition(t);
        }

		public void Reverse() {
            throw new NotSupportedException("NURBSCurve2.Reverse: how to reverse?!?");
        }

        public IParametricCurve2d Clone() {
            NURBSCurve2 c2 = new NURBSCurve2();
            c2.mNumCtrlPoints = this.mNumCtrlPoints;
            c2.mCtrlPoint = (Vector2d[])this.mCtrlPoint.Clone();
            c2.mCtrlWeight = (double[])this.mCtrlWeight.Clone();
            c2.mLoop = this.mLoop;
            c2.mBasis = this.mBasis.Clone();
            c2.mReplicate = this.mReplicate;
            c2.is_closed = this.is_closed;
            return c2;
        }


        public bool IsTransformable { get { return true; } }
        public void Transform(ITransform2 xform)
        {
            for (int k = 0; k < mCtrlPoint.Length; ++k)
                mCtrlPoint[k] = xform.TransformP(mCtrlPoint[k]);
        }


        // returned list is set of unique knot values in range [0,1], ie
        // with no duplicates at repeated knots
        public List<double> GetParamIntervals() {
			List<double> l = new List<double>();
			l.Add(0);
			for ( int i = 0; i < mBasis.KnotCount; ++i ) {
				double k = mBasis.GetKnot(i);
				if ( k != l.Last() ) 
					l.Add(k);
			}
			if ( l.Last() != 1.0 )
				l.Add(1.0);
			return l;
		}


		// similar to GetParamIntervals, but leaves out knots of
		// multiplicity 1, where curve would be continuous. Idea is to
		// get "smooth" intervals, for sampling/etc, because some real-world
		// curves have crazy #'s of knots/CVs.
		// [TODO] knot multiplicity does not mean non-smoothness. EG Bezier represnted
		// as b-spline has count=3 at each CV but can still be C^2. Really should be
		// checking incoming/outgoing tangents at repeated CVs...
		public List<double> GetContinuousParamIntervals() {
			List<double> l = new List<double>();
			//l.Add(0);
			double cur_knot = -1;
			int cur_knot_count = 0;
			for ( int i = 0; i < mBasis.KnotCount; ++i ) {
				double k = mBasis.GetKnot(i);
				if ( k == cur_knot ) {
					cur_knot_count++;
				} else {
					if ( cur_knot_count > 1 )
						l.Add(cur_knot);
					cur_knot = k;
					cur_knot_count = 1;
				}

			}
			if ( l.Last() != 1.0 )
				l.Add(1.0);
			return l;
		}
    }
}
