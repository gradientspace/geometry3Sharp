using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace g3 
{
    // This class is analogous to GeneralPolygon2d, but for closed loops of curves, instead
    // of polygons. However, we cannot do some of the operations we would otherwise do in
    // GeneralPolygon2d (for example cw/ccw checking, intersctions, etc).
    //
    // So, it is strongly recommended that this be constructed alongside a GeneralPolygon2d,
    // which can be used for checking everything.
	public class PlanarSolid2d 
	{
		IParametricCurve2d outer;
		//bool bOuterIsCW;

		List<IParametricCurve2d> holes = new List<IParametricCurve2d>();


		public PlanarSolid2d() {
		}


		public IParametricCurve2d Outer {
			get { return outer; }
		}
        public void SetOuter(IParametricCurve2d loop, bool bIsClockwise)
        {
            Debug.Assert(loop.IsClosed);
            outer = loop;
            //bOuterIsCW = bIsClockwise;
        }


		public void AddHole(IParametricCurve2d hole) {
			if ( outer == null )
				throw new Exception("PlanarSolid2d.AddHole: outer polygon not set!");

    //        if ( (bOuterIsCW && hole.IsClockwise) || (bOuterIsCW == false && hole.IsClockwise == false) )
				//throw new Exception("PlanarSolid2d.AddHole: new hole has same orientation as outer polygon!");

			holes.Add(hole);
		}


		bool HasHoles {
			get { return Holes.Count > 0; }
		}

		public ReadOnlyCollection<IParametricCurve2d> Holes {
			get { return holes.AsReadOnly(); }
		}


        public bool HasArcLength
        {
            get {
                bool bHas = outer.HasArcLength;
                foreach (var h in Holes)
                    bHas = bHas && h.HasArcLength;
                return bHas;
            }
        }


        public double Perimeter
        {
            get {
                if (!HasArcLength)
                    throw new Exception("PlanarSolid2d.Perimeter: some curves do not have arc length");
                double dPerim = outer.ArcLength;
                foreach (var h in Holes)
                    dPerim += h.ArcLength;
                return dPerim;
            }
        }


        /// <summary>
        /// Resample parametric solid into polygonal solid
        /// </summary>
        public GeneralPolygon2d Convert(double fSpacingLength, double fSpacingT, double fDeviationTolerance)
        {
            GeneralPolygon2d poly = new GeneralPolygon2d();
            poly.Outer = new Polygon2d(
                CurveSampler2.AutoSample(this.outer, fSpacingLength, fSpacingT));
            poly.Outer.Simplify(0, fDeviationTolerance);
            foreach (var hole in this.Holes) {
                Polygon2d holePoly = new Polygon2d(
                    CurveSampler2.AutoSample(hole, fSpacingLength, fSpacingT));
                holePoly.Simplify(0, fDeviationTolerance);
                poly.AddHole(holePoly, false);
            }
            return poly;
        }
	}
}
