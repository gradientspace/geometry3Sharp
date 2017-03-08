using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{

    public class InPlaceIterativeCurveSmooth
    {
        DCurve3 _curve;
        public DCurve3 Curve {
            get { return _curve; }
            set { if (_curve != value) { _curve = value; } }
        }

        int _startRange;
        public int Start {
            get { return _startRange; }
            set { _startRange = value; }
        }

        int _endRange;
        public int End {
            get { return _endRange; }
            set { _endRange = value; }
        }

        float _alpha;
        public float Alpha {
            get { return _alpha; }
            set { _alpha = MathUtil.Clamp(value, 0.0f, 1.0f); }
        }

        public InPlaceIterativeCurveSmooth()
        {
            Start = End = -1;
            Alpha = 0.25f;
        }
        public InPlaceIterativeCurveSmooth(DCurve3 curve, float alpha = 0.25f)
        {
            Curve = curve;
            Start = 0;
            End = Curve.VertexCount;
            Alpha = alpha;
        }


        public void UpdateDeformation(int nIterations = 1)
        {
            if (Curve.Closed)
                UpdateDeformation_Closed(nIterations);
            else
                UpdateDeformation_Open(nIterations);
        }


        public void UpdateDeformation_Closed(int nIterations = 1)
        {
            if (Start < 0 || Start > Curve.VertexCount || End > Curve.VertexCount)
                throw new ArgumentOutOfRangeException("InPlaceIterativeCurveSmooth.UpdateDeformation: range is invalid");

            int N = Curve.VertexCount;
            for (int iter = 0; iter < nIterations; ++iter) {
                for (int ii = Start; ii < End; ++ii) {
                    int i = (ii % N);
                    int iPrev = (ii == 0) ? N - 1 : ii - 1;
                    int iNext = (ii + 1) % N;
                    Vector3d prev = Curve[iPrev], next = Curve[iNext];
                    Vector3d c = (prev + next) * 0.5f;
                    Curve[i] = (1 - Alpha) * Curve[i] + (Alpha) * c;
                }
            }
        }


        public void UpdateDeformation_Open(int nIterations = 1)
        {
            if (Start < 0 || Start > Curve.VertexCount || End > Curve.VertexCount)
                throw new ArgumentOutOfRangeException("InPlaceIterativeCurveSmooth.UpdateDeformation: range is invalid");

            for (int iter = 0; iter < nIterations; ++iter) {
                for (int i = Start; i <= End; ++i) {
                    if (i == 0 || i >= Curve.VertexCount - 1)
                        continue;

                    Vector3d prev = Curve[i - 1], next = Curve[i + 1];
                    Vector3d c = (prev + next) * 0.5f;
                    Curve[i] = (1 - Alpha) * Curve[i] + (Alpha) * c;
                }
            }
        }

    }









    public class ArcLengthSoftTranslation
    {
        DCurve3 _curve;
        public DCurve3 Curve {
            get { return _curve; }
            set { if (_curve != value) { _curve = value; invalidate_roi(); } }
        }

        // handle is position of deformation handle
        //  (but currently we are finding nearest vertex to this anyway...)
        Vector3d _handle;
        public Vector3d Handle {
            get { return _handle; }
            set { if (_handle != value) { _handle = value; invalidate_roi(); } }
        }

        // arclength in either direction along curve, from handle, over which deformation falls off
        double _arcradius;
        public double ArcRadius {
            get { return _arcradius; }
            set { if (_arcradius != value) { _arcradius = value; invalidate_roi(); } }
        }

        // weight function applied over falloff region. currently linear!
        Func<double, double, double> _weightfunc;
        public Func<double, double, double> WeightFunc
        {
            get { return _weightfunc; }
            set { if (_weightfunc != value) { _weightfunc = value; invalidate_roi(); } }
        }
            
        public int[] roi_index;
        public double[] roi_weights;
        public Vector3d[] start_positions;
        bool roi_valid;
        int curve_timestamp;

        public ArcLengthSoftTranslation()
        {
            Handle = Vector3d.Zero;
            ArcRadius = 1.0f;
            WeightFunc = (d, r) => { return MathUtil.WyvillFalloff01(MathUtil.Clamp(d / r, 0.0, 1.0)); }; 
            roi_valid = false;
        }



        Vector3d start_handle;

        public void BeginDeformation()
        {
            UpdateROI(-1);      // will be ignored if you called this yourself first
            start_handle = Handle;

            if (start_positions == null || start_positions.Length != roi_index.Length)
                start_positions = new Vector3d[roi_index.Length];
            for (int i = 0; i < roi_index.Length; ++i)
                start_positions[i] = Curve.GetVertex(roi_index[i]);
        }

        public void UpdateDeformation(Vector3d newHandlePos)
        {
            Vector3d dv = newHandlePos - start_handle;
            for ( int i = 0; i < roi_index.Length; ++i ) {
                Vector3d vNew = start_positions[i] + roi_weights[i] * dv;
                Curve.SetVertex(roi_index[i], vNew);
            }
        }

        public void EndDeformation()
        {
            // do nothing
        }




        void invalidate_roi() {
            roi_valid = false;
        }

        bool check_roi_valid() {
            if (roi_valid == false)
                return false;
            if (Curve.Timestamp != curve_timestamp)
                return false;
            return true;
        }

        public void UpdateROI(int nNearVertexHint = -1)
        {
            if (check_roi_valid())
                return;

            int iStart = nNearVertexHint;
            if ( nNearVertexHint < 0 ) {
                iStart = CurveUtils.FindNearestIndex(Curve, Handle);
            }
            int N = Curve.VertexCount;

            // walk forward and backward to figure out how many verts we have in ROI
            int nTotal = 1;

            double cumSumFW = 0;
            int nForward = -1;
            for ( int i = iStart+1; i < N && cumSumFW < ArcRadius; ++i ) {
                double d = (Curve.GetVertex(i) - Curve.GetVertex(i - 1)).Length;
                cumSumFW += d;
                if (cumSumFW < ArcRadius) {
                    nTotal++;
                    nForward = i;
                }
            }
            double cumSumBW = 0;
            int nBack = -1;
            for (int i = iStart - 1; i >= 0 && cumSumBW < ArcRadius; --i) {
                double d = (Curve.GetVertex(i) - Curve.GetVertex(i+1)).Length;
                cumSumBW += d;
                if (cumSumBW < ArcRadius) {
                    nTotal++;
                    nBack = i;
                }
            }

            if (roi_index == null || roi_index.Length != nTotal ) {
                roi_index = new int[nTotal];
                roi_weights = new double[nTotal];
            }
            int roiI = 0;

            roi_index[roiI] = iStart;
            roi_weights[roiI++] = WeightFunc(0, ArcRadius);

            // now fill roi arrays
            if (nForward >= 0) {
                cumSumFW = 0;
                for (int i = iStart + 1; i <= nForward; ++i) {
                    cumSumFW += (Curve.GetVertex(i) - Curve.GetVertex(i - 1)).Length;
                    roi_index[roiI] = i;
                    roi_weights[roiI++] = WeightFunc(cumSumFW, ArcRadius);
                }
            }
            if (nBack >= 0) {
                cumSumBW = 0;
                for (int i = iStart - 1; i >= nBack; --i) {
                    cumSumBW += (Curve.GetVertex(i) - Curve.GetVertex(i + 1)).Length;
                    roi_index[roiI] = i;
                    roi_weights[roiI++] = WeightFunc(cumSumBW, ArcRadius);
                }
            }

            roi_valid = true;
            curve_timestamp = Curve.Timestamp;
        }

    }
}
