using System;
using System.Collections.Generic;

namespace g3 
{
	public class ParametricCurveSequence2 : IParametricCurve2d
	{

		List<IParametricCurve2d> curves;
		bool closed;

		public ParametricCurveSequence2() {
			curves = new List<IParametricCurve2d>();
		}

		public int Count {
			get { return curves.Count; }
		}

		public IReadOnlyList<IParametricCurve2d> Curves {
			get { return curves.AsReadOnly(); }
		}

		public bool IsClosed { 
			get { return closed; }
			set { closed = value; }
		}


		public void Append(IParametricCurve2d c) {
			// sanity checking??
			curves.Add(c);
		}


		public double ParamLength {
			get { 
				double sum = 0;
				foreach ( var c in Curves )
					sum += c.ParamLength;
				return sum;
			}
		}

		public Vector2d SampleT(double t) {
			double sum = 0;
			for ( int i = 0; i < Curves.Count; ++i ) {
				double l = curves[i].ParamLength;
				if (t <= sum+l) {
					double ct = (t - sum);
					return curves[i].SampleT(ct);
				}
				sum += l;
			}
			throw new ArgumentException("ParametricCurveSequence2.SampleT: argument out of range");
		}

		public bool HasArcLength { get { 
				foreach ( var c in Curves )
					if ( c.HasArcLength == false )
						return false;
				return true;
			} 
		}

		public double ArcLength {
			get {
				double sum = 0;
				foreach ( var c in Curves )
					sum += c.ArcLength;
				return sum;
			}
		}

		public Vector2d SampleArcLength(double a) {
			double sum = 0;
			for ( int i = 0; i < Curves.Count; ++i ) {
				double l = curves[i].ArcLength;
				if (a <= sum+l) {
					double ca = (a - sum);
					return curves[i].SampleArcLength(ca);
				}
				sum += l;
			}
			throw new ArgumentException("ParametricCurveSequence2.SampleArcLength: argument out of range");
		}



	}
}
