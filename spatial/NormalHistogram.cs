using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    /// <summary>
    /// Construct "histogram" of normals of mesh. Basically each normal is scaled up
    /// and then rounded to int. This is not a great strategy, but it works for 
    /// finding planes/etc.
    /// 
    /// [TODO] variant that bins normals based on semi-regular mesh of sphere
    /// </summary>
    public class NormalHistogram
    {
        public DMesh3 Mesh;

        public int IntScale = 256;
        public bool UseAreaWeighting = true;
        public Dictionary<Vector3i, double> Histogram;


        public NormalHistogram(DMesh3 mesh)
        {
            Mesh = mesh;
            Histogram = new Dictionary<Vector3i, double>();
            build();
        }


        /// <summary>
        /// return (rounded) normal associated w/ maximum weight/area
        /// </summary>
        public Vector3d FindMaxNormal()
        {
            Vector3i maxN = Vector3i.AxisY; double maxArea = 0;
            foreach (var pair in Histogram) {
                if (pair.Value > maxArea) {
                    maxArea = pair.Value;
                    maxN = pair.Key;
                }
            }
            Vector3d n = new Vector3d(maxN.x, maxN.y, maxN.z);
            n.Normalize();
            return n;
        }




        void build()
        {
            foreach (int tid in Mesh.TriangleIndices()) {
                double w = (UseAreaWeighting) ? Mesh.GetTriArea(tid) : 1.0;

                Vector3d n = Mesh.GetTriNormal(tid);

                Vector3i up = new Vector3i((int)(n.x * IntScale), (int)(n.y * IntScale), (int)(n.z * IntScale));

                if (Histogram.ContainsKey(up))
                    Histogram[up] += w;
                else
                    Histogram[up] = w;
            }
        }

    }
}
