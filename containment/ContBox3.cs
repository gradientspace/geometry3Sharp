using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{

    // ported from GTEngine GteContOrientedBox3.h
    // (2017) url: https://www.geometrictools.com/GTEngine/Include/Mathematics/GteContOrientedBox3.h
    public class ContOrientedBox3
    {
        public Box3d Box;
        public bool ResultValid = false;

        public ContOrientedBox3(IEnumerable<Vector3d> points)
        {
            // Fit the points with a Gaussian distribution.
            GaussPointsFit3 fitter = new GaussPointsFit3(points);
            if (fitter.ResultValid == false)
                return;
            this.Box = fitter.Box;
            this.Box.Contain(points);
        }

        public ContOrientedBox3(IEnumerable<Vector3d> points, IEnumerable<double> pointWeights)
        {
            // Fit the points with a Gaussian distribution.
            GaussPointsFit3 fitter = new GaussPointsFit3(points, pointWeights);
            if (fitter.ResultValid == false)
                return;
            this.Box = fitter.Box;
            this.Box.Contain(points);
        }
    }
}
