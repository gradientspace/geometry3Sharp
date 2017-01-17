using System;

namespace g3
{
    // ported from WildMagic5 BSplineBasis
    public class BSplineBasis
    {
        // Defaultructor.  The number of control points is n+1 and the
        // indices i for the control points satisfy 0 <= i <= n.  The degree of
        // the curve is d.  The knot array has n+d+2 elements.  Whether uniform
        // or nonuniform knots, it is required that
        //   knot[i] = 0, 0 <= i <= d
        //   knot[i] = 1, n+1 <= i <= n+d+1
        // BSplineBasis enforces these conditions by not exposing SetKnot for the
        // relevant values of i.
        //
        protected BSplineBasis() {
            // [RMS] removed Create(), so default constructor is useless...
        }

        // Open uniform or periodic uniform.  The knot array is internally
        // generated with equally spaced elements.  It is required that
        //   knot[i] = (i-d)/(n+1-d), d+1 <= i <= n
        // BSplineBasis enforces these conditions by not exposing SetKnot for the
        // relevant values of i.  GetKnot(j) will return knot[i] for i = j+d+1.
        public BSplineBasis(int numCtrlPoints, int degree, bool open)
        {
            // code from c++ Create(int numCtrlPoints, int degree, bool open);
            mUniform = true;

            int i, numKnots = Initialize(numCtrlPoints, degree, open);
            double factor = 1.0 / (mNumCtrlPoints - mDegree);
            if (mOpen) {
                for (i = 0; i <= mDegree; ++i) 
                    mKnot[i] = 0;
                for (/**/; i < mNumCtrlPoints; ++i) 
                    mKnot[i] = (i - mDegree) * factor;
                for (/**/; i < numKnots; ++i) 
                    mKnot[i] = 1;
            } else {
                for (i = 0; i < numKnots; ++i) 
                    mKnot[i] = (i - mDegree) * factor;
            }
        }
        

        // Open nonuniform.  
		// if bIsInteriorKnots, the knots array must have n-d-1 nondecreasing
        //   elements in the interval [0,1].  The values are
        //     knot[i] = interiorKnot[j]
        //   with 0 <= j < n-d-1 and i = j+d+1, so d+1 <= i < n.  
		//
		// if bIsInteriorKnots = false, the knot vector is copied directly, and must have
		//   n+d+1 elements
		//
        // An internal copy of knots[] is made, so to dynamically change knots you 
		// must use the SetKnot(j,*) function.
		public BSplineBasis(int numCtrlPoints, int degree, double[] knots, bool bIsInteriorKnots)
        {
            //code from c++ Create(int numCtrlPoints, int degree, double* interiorKnot);
            mUniform = false;
            int i, numKnots = Initialize(numCtrlPoints, degree, true);

			if ( bIsInteriorKnots ) {
				if ( knots.Length != mNumCtrlPoints-mDegree-1 )
					throw new Exception("BSplineBasis nonuniform constructor: invalid interior knot vector");
				for (i = 0; i <= mDegree; ++i)
					mKnot[i] = 0;
				for (int j = 0; i < mNumCtrlPoints; ++i, ++j)
					mKnot[i] = knots[j];
				for (/**/; i < numKnots; ++i)
					mKnot[i] = 1;
			} else { 
				if ( mKnot.Length != knots.Length )
					throw new Exception("BSplineBasis nonuniform constructor: invalid knot vector");
				Array.Copy(knots, mKnot, knots.Length);
			}

        }

        
        public BSplineBasis Clone()
        {
            BSplineBasis b2 = new BSplineBasis();
            b2.mNumCtrlPoints = this.mNumCtrlPoints;
            b2.mDegree = this.mDegree;
            b2.mKnot = (double[])this.mKnot.Clone();
            b2.mOpen = this.mOpen;
            b2.mUniform = this.mUniform;
            return b2;
        }



        public int GetNumCtrlPoints() {
            return mNumCtrlPoints;
        }
        public int GetDegree() {
            return mDegree;
        }
        public bool IsOpen() {
            return mOpen;
        }
        public bool IsUniform() {
            return mUniform;
        }


		public int KnotCount {
			get { return mNumCtrlPoints+mDegree+1; }
		}
		public int InteriorKnotCount {
			get { return mNumCtrlPoints-mDegree-1; }
		}

		// For a nonuniform spline, the knot[i] are modified by SetInteriorKnot(j,value)
        // for j = i+d+1.  That is, you specify j with 0 <= j <= n-d-1, i = j+d+1,
		// and knot[i] = value.  SetInteriorKnot(j,value) does nothing for indices outside
        // the j-range or for uniform splines.  
        public void SetInteriorKnot(int j, double value)
        {
            if (!mUniform) {
                // Access only allowed to elements d+1 <= i <= n.
                int i = j + mDegree + 1;
                if (mDegree + 1 <= i && i <= mNumCtrlPoints) {
                    mKnot[i] = value;
                } else
                    throw new Exception("BSplineBasis.SetKnot: index out of range: " + j);
            } else
                throw new Exception("BSplineBasis.SetKnot: knots cannot be set for uniform splines");
        }

        public double GetInteriorKnot(int j)
        {
            // Access only allowed to elements d+1 <= i <= n.
            int i = j + mDegree + 1;
            if (mDegree + 1 <= i && i <= mNumCtrlPoints) {
                return mKnot[i];
            }
            //assertion(false, "Knot index out of range.\n");
            throw new Exception("BSplineBasis.GetKnot: index out of range: " + j);
            //return double.MaxValue;
        }

		// [RMS] direct access to all knots. Not sure why this was not allowed in
		//   original code - are there assumptions that some knots are 0/1 ???
		public void SetKnot(int j, double value) {
			mKnot[j] = value;
		}
		public double GetKnot(int j) 
		{
			return mKnot[j];
		}

        // Access basis functions and their derivatives.
        public double GetD0(int i) {
            return mBD0[mDegree,i];
        }
        public double GetD1(int i)  {
            return mBD1[mDegree,i];
        }
        public double GetD2(int i) {
            return mBD2[mDegree,i];
        }
        public double GetD3(int i) {
            return mBD3[mDegree,i];
        }

        // Evaluate basis functions and their derivatives.
        public void Compute(double t, int order, ref int minIndex, ref int maxIndex)
        {
            //assertion(order <= 3, "Only derivatives to third order supported\n");
            if (order > 3)
                throw new Exception("BSplineBasis.Compute: cannot compute order " + order);

            if (order >= 1) {
                if (mBD1 == null) 
                    mBD1 = Allocate();

                if (order >= 2) {
                    if (mBD2 == null) 
                        mBD2 = Allocate();

                    if (order >= 3) {
                        if (mBD3 == null) 
                            mBD3 = Allocate();
                    }
                }
            }

            int i = GetKey(ref t);
            mBD0[0,i] = (double)1;

            if (order >= 1) {
                mBD1[0,i] = (double)0;
                if (order >= 2) {
                    mBD2[0,i] = (double)0;
                    if (order >= 3) {
                        mBD3[0,i] = (double)0;
                    }
                }
            }

            double n0 = t - mKnot[i], n1 = mKnot[i + 1] - t;
            double invD0, invD1;
            int j;
            for (j = 1; j <= mDegree; j++) {
                invD0 = 1.0 / (mKnot[i + j] - mKnot[i]);
                invD1 = 1.0 / (mKnot[i + 1] - mKnot[i - j + 1]);

				// [RMS] convention is 0/0 = 0. invD0/D1 will be Infinity in these
				// cases, so we set explicitly to 0
				if ( mKnot[i+j] == mKnot[i] ) invD0 = 0;
				if ( mKnot[i+1] == mKnot[i - j + 1] ) invD1 = 0;


                mBD0[j,i] = n0 * mBD0[j - 1,i] * invD0;
                mBD0[j,i - j] = n1 * mBD0[j - 1,i - j + 1] * invD1;

                if (order >= 1) {
                    mBD1[j,i] = (n0 * mBD1[j - 1,i] + mBD0[j - 1,i]) * invD0;
                    mBD1[j,i - j] = (n1 * mBD1[j - 1,i - j + 1] - mBD0[j - 1,i - j + 1]) * invD1;

                    if (order >= 2) {
                        mBD2[j,i] = (n0 * mBD2[j - 1,i] + ((double)2) * mBD1[j - 1,i]) * invD0;
                        mBD2[j,i - j] = (n1 * mBD2[j - 1,i - j + 1] -
                            ((double)2) * mBD1[j - 1,i - j + 1]) * invD1;

                        if (order >= 3) {
                            mBD3[j,i] = (n0 * mBD3[j - 1,i] +
                                ((double)3) * mBD2[j - 1,i]) * invD0;
                            mBD3[j,i - j] = (n1 * mBD3[j - 1,i - j + 1] -
                                ((double)3) * mBD2[j - 1,i - j + 1]) * invD1;
                        }
                    }
                }
            }

            for (j = 2; j <= mDegree; ++j) {
                for (int k = i - j + 1; k < i; ++k) {
                    n0 = t - mKnot[k];
                    n1 = mKnot[k + j + 1] - t;
                    invD0 = 1.0 / (mKnot[k + j] - mKnot[k]);
                    invD1 = 1.0 / (mKnot[k + j + 1] - mKnot[k + 1]);

					// [RMS] convention is 0/0 = 0. invD0/D1 will be Infinity in these
					// cases, so we set explicitly to 0
					if ( mKnot[k+j] == mKnot[k] ) invD0 = 0;
					if ( mKnot[k+j+1] == mKnot[k+1] ) invD1 = 0;

					mBD0[j,k] = n0 * mBD0[j - 1,k] * invD0 + n1 * mBD0[j - 1,k + 1] * invD1;

                    if (order >= 1) {
                        mBD1[j,k] = (n0 * mBD1[j - 1,k] + mBD0[j - 1,k]) * invD0 +
                            (n1 * mBD1[j - 1,k + 1] - mBD0[j - 1,k + 1]) * invD1;

                        if (order >= 2) {
                            mBD2[j,k] = (n0 * mBD2[j - 1,k] +
                                ((double)2) * mBD1[j - 1,k]) * invD0 +
                                (n1 * mBD2[j - 1,k + 1] - ((double)2) * mBD1[j - 1,k + 1]) * invD1;

                            if (order >= 3) {
                                mBD3[j,k] = (n0 * mBD3[j - 1,k] +
                                    ((double)3) * mBD2[j - 1,k]) * invD0 +
                                    (n1 * mBD3[j - 1,k + 1] - ((double)3) *
                                    mBD2[j - 1,k + 1]) * invD1;
                            }
                        }
                    }
                }
            }

            minIndex = i - mDegree;
            maxIndex = i;
        }


        protected int Initialize(int numCtrlPoints, int degree, bool open)
        {
            if (numCtrlPoints < 2)
                throw new Exception("BSplineBasis.Initialize: only received " + numCtrlPoints + " control points!");
            if (degree < 1 || degree > numCtrlPoints - 1)
                throw new Exception("BSplineBasis.Initialize: invalid degree " + degree);
            //assertion(numCtrlPoints >= 2, "Invalid input\n");
            //assertion(1 <= degree && degree <= numCtrlPoints - 1, "Invalid input\n");

            mNumCtrlPoints = numCtrlPoints;
            mDegree = degree;
            mOpen = open;

            int numKnots = mNumCtrlPoints + mDegree + 1;
            mKnot = new double[numKnots];

            mBD0 = Allocate();
            mBD1 = null;
            mBD2 = null;
            mBD3 = null;

            return numKnots;
        }

        protected double[,] Allocate()
        {
            int numRows = mDegree + 1;
            int numCols = mNumCtrlPoints + mDegree;
            double[,] data = new double[numRows, numCols];
            for (int i = 0; i < numRows; ++i)
                for (int j = 0; j < numCols; ++j)
                    data[i,j] = 0;
            return data;
        }

        // [RMS] not necessary
        //protected void Deallocate(double[,] data);


        // Determine knot index i for which knot[i] <= rfTime < knot[i+1].
        protected int GetKey(ref double t)
        {
            if (mOpen) {
                // Open splines clamp to [0,1].
                if (t <= 0) {
                    t = 0;
                    return mDegree;
                } else if (t >= 1) {
                    t = 1;
                    return mNumCtrlPoints - 1;
                }
            } else {
                // Periodic splines wrap to [0,1).
                if (t < 0 || t >= 1) {
                    t -= Math.Floor(t);
                }
            }


            int i;

            if (mUniform) {
                i = mDegree + (int)((mNumCtrlPoints - mDegree) * t);
            } else {
                for (i = mDegree + 1; i <= mNumCtrlPoints; ++i) {
                    if (t < mKnot[i]) {
                        break;
                    }
                }
                --i;
            }

            return i;
        }


        //
        // data members
        //

        protected int mNumCtrlPoints;   // n+1
        protected int mDegree;          // d
        protected double[] mKnot;          // knot[n+d+2]
        protected bool mOpen, mUniform;

        // Storage for the basis functions and their derivatives first three
        // derivatives.  The basis array is always allocated by theructor
        // calls.  A derivative basis array is allocated on the first call to a
        // derivative member function.
        protected double[,] mBD0;          // bd0[d+1,n+d+1]
        protected double[,] mBD1;  // bd1[d+1,n+d+1]
        protected double[,] mBD2;  // bd2[d+1,n+d+1]
        protected double[,] mBD3;  // bd3[d+1,n+d+1]
    }
}
