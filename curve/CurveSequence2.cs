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
				throw new NotImplementedException();
			}
		}

		public Vector2d SampleT(double t) {
			throw new NotImplementedException();
		}

		public bool HasArcLength { get { throw new NotImplementedException(); } }

		public double ArcLength {
			get {
				throw new NotImplementedException();
			}
		}

		public Vector2d SampleArcLength(double a) {
			throw new NotImplementedException();
		}



	}
}
