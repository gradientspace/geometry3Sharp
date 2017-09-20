using System;
using System.Collections;
using System.Collections.Generic;


namespace g3
{


    /// <summary>
    /// Base-class for DCurve3 spatial deformations. 
    /// Subclasses must implement abstract Apply() method.
    /// </summary>
    public abstract class SculptCurveDeformation
    {
        protected DCurve3 _curve;
        public DCurve3 Curve
        {
            get { return _curve; }
            set { if (_curve != value) { _curve = value; } }
        }

        // weight function applied over falloff region. Defaults to Wyvill falloff.
        protected Func<double, double, double> _weightfunc;
        public Func<double, double, double> WeightFunc
        {
            get { return _weightfunc; }
            set { if (_weightfunc != value) { _weightfunc = value; } }
        }


        protected double radius = 0.1f;
        public double Radius
        {
            get { return radius; }
            set { radius = value; }
        }


        public SculptCurveDeformation()
        {
            WeightFunc = (d, r) => { return MathUtil.WyvillFalloff01(MathUtil.Clamp(d / r, 0.0, 1.0)); };
        }


        protected Frame3f vPreviousPos;


        public virtual void BeginDeformation(Frame3f vStartPos)
        {
            vPreviousPos = vStartPos;
        }


        public struct DeformInfo
        {
            public bool bNoChange;
            public double maxEdgeLenSqr;
            public double minEdgeLenSqr;
        }
        public virtual DeformInfo UpdateDeformation(Frame3f vNextPos)
        {
            DeformInfo result = Apply(vNextPos);
            vPreviousPos = vNextPos;
            return result;
        }


        public abstract DeformInfo Apply(Frame3f vNextPos);



    }



    public class StandardSculptCurveDeformation : SculptCurveDeformation
    {
        // Deformation function. 
        // Arguments are curve index and weight "t" value in range [0,1]
        // Return new position for Cuve[i]
        // If null, no deformation
        public Func<int, double, Vector3d> DeformF = null;


        // standard deformation supports default smooth pass
        public double SmoothAlpha = 0.1f;
        public int SmoothIterations = 1;


        public DVector<Vector3d> NewV;
        public BitArray ModifiedV;



        public StandardSculptCurveDeformation()
        {
            NewV = new DVector<Vector3d>();
            NewV.resize(256);
            ModifiedV = new BitArray(256);
        }


        public override DeformInfo Apply(Frame3f vNextPos)
        {
            Interval1d edgeRangeSqr = Interval1d.Empty;

            int N = Curve.VertexCount;
            if (N > NewV.size) 
                NewV.resize(N);
            if (N > ModifiedV.Length)
                ModifiedV = new BitArray(2 * N);

            // clear modified
            ModifiedV.SetAll(false);

            bool bSmooth = (SmoothAlpha > 0 && SmoothIterations > 0);
            double r2 = Radius * Radius;

            // deform pass
            if (DeformF != null) {
                for (int i = 0; i < N; ++i) {

                    Vector3d v = Curve[i];
                    double d2 = (v - vPreviousPos.Origin).LengthSquared;
                    if (d2 < r2) {
                        double t = WeightFunc(Math.Sqrt(d2), Radius);

                        Vector3d vNew = DeformF(i, t);

                        if (bSmooth == false) {
                            if (i > 0)
                                edgeRangeSqr.Contain(vNew.DistanceSquared(Curve[i - 1]));
                            if (i < N - 1)
                                edgeRangeSqr.Contain(vNew.DistanceSquared(Curve[i + 1]));
                        }

                        NewV[i] = vNew;
                        ModifiedV[i] = true;
                    }
                }
            } else {
                // anything?
            }

            // smooth pass
            if ( bSmooth ) {
                for ( int j = 0; j < SmoothIterations; ++j ) {

                    int iStart = (Curve.Closed) ? 0 : 1;
                    int iEnd = (Curve.Closed) ? N : N - 1;
                    for (int i = iStart; i < iEnd; ++i) {
                        Vector3d v = (ModifiedV[i]) ? NewV[i] : Curve[i];
                        double d2 = (v - vPreviousPos.Origin).LengthSquared;
                        if (ModifiedV[i] ||  d2 < r2) {         // always smooth any modified verts
                            double a = SmoothAlpha * WeightFunc(Math.Sqrt(d2), Radius);

                            int iPrev = (i == 0) ? N - 1 : i - 1;
                            int iNext = (i + 1) % N;
                            Vector3d vPrev = (ModifiedV[iPrev]) ? NewV[iPrev] : Curve[iPrev];
                            Vector3d vNext = (ModifiedV[iNext]) ? NewV[iNext] : Curve[iNext];
                            Vector3d c = (vPrev + vNext) * 0.5f;
                            NewV[i] = (1 - a) * v + (a) * c;
                            ModifiedV[i] = true;

                            if (i > 0)
                                edgeRangeSqr.Contain(NewV[i].DistanceSquared(Curve[i - 1]));
                            if (i < N - 1)
                                edgeRangeSqr.Contain(NewV[i].DistanceSquared(Curve[i + 1]));
                        }
                    }
                }

            }

            // bake
            for ( int i = 0; i < N; ++i ) {
                if (ModifiedV[i])
                    Curve[i] = NewV[i];
            }

            return new DeformInfo() { bNoChange = false, minEdgeLenSqr = edgeRangeSqr.a, maxEdgeLenSqr = edgeRangeSqr.b };
        }
    }



    // just apply smoothing pass from standard op
    public class SculptCurveSmooth : StandardSculptCurveDeformation
    {
        public SculptCurveSmooth()
        {
            DeformF = null;
            SmoothAlpha = 0.1f;
            SmoothIterations = 3;
        }
    }




    public class SculptCurveMove : StandardSculptCurveDeformation
    {

        public SculptCurveMove()
        {
            SmoothAlpha = 0.0f;
            SmoothIterations = 0;
        }


        // returns max edge length of moved vertices, after deformation
        public override DeformInfo Apply(Frame3f vNextPos)
        {
            // if we did not move brush far enough, don't do anything
            Vector3d vDelta = (vNextPos.Origin - vPreviousPos.Origin);
            if (vDelta.Length < 0.0001f)
                return new DeformInfo() { bNoChange = true, maxEdgeLenSqr = 0, minEdgeLenSqr = double.MaxValue };

            // otherwise apply base deformation
            DeformF = (idx, t) => {
                Vector3d v = vPreviousPos.ToFrameP(Curve[idx]);
                Vector3d vNew = vNextPos.FromFrameP(v);
                return Vector3d.Lerp(Curve[idx], vNew, t);
            };
            return base.Apply(vNextPos);

        }

    }










}
