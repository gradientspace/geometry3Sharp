using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    /// <summary>
    /// Construct spherical histogram of normals of mesh. 
    /// Binning is done using a Spherical Fibonacci point set.
    /// </summary>
    public class NormalHistogram
    {
        public int Bins = 1024;
        public SphericalFibonacciPointSet Points;
        public double[] Counts;

        public HashSet<int> UsedBins;

        public NormalHistogram(int bins, bool bTrackUsed = false)
        {
            Bins = bins;
            Points = new SphericalFibonacciPointSet(bins);
            Counts = new double[bins];
            if (bTrackUsed)
                UsedBins = new HashSet<int>();
        }

        /// <summary>
        /// legacy API
        /// </summary>
        public NormalHistogram(DMesh3 mesh, bool bWeightByArea = true, int bins = 1024) : this(bins)
        {
            CountFaceNormals(mesh, bWeightByArea);
        }


        /// <summary>
        /// bin and count point, and optionally normalize
        /// </summary>
        public void Count(Vector3d pt, double weight = 1.0, bool bIsNormalized = false) {
            int bin = Points.NearestPoint(pt, bIsNormalized);
            Counts[bin] += weight;
            if (UsedBins != null)
                UsedBins.Add(bin);
        }

        /// <summary>
        /// Count all input mesh face normals
        /// </summary>
        public void CountFaceNormals(DMesh3 mesh, bool bWeightByArea = true)
        {
            foreach (int tid in mesh.TriangleIndices()) {
                if (bWeightByArea) {
                    Vector3d n, c; double area;
                    mesh.GetTriInfo(tid, out n, out area, out c);
                    Count(n, area, true);
                } else {
                    Count(mesh.GetTriNormal(tid), 1.0, true);
                }
            }
        }


        /// <summary>
        /// return (quantized) normal associated w/ maximum weight/area
        /// </summary>
        public Vector3d FindMaxNormal()
        {
            int max_i = 0;
            for ( int k = 1; k < Bins; ++k ) {
                if (Counts[k] > Counts[max_i])
                    max_i = k;
            }
            return Points[max_i];
        }


    }
}
