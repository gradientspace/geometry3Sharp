using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace g3
{
    /*
     * Basic cache of per-triangle information for a DMesh3
     */
    public class MeshTriInfoCache
    {
        public DVector<Vector3d> Centroids;
        public DVector<Vector3d> Normals;
        public DVector<double> Areas;

        public MeshTriInfoCache(DMesh3 mesh)
        {
            int NT = mesh.TriangleCount;
            Centroids = new DVector<Vector3d>(); Centroids.resize(NT);
            Normals = new DVector<Vector3d>(); Normals.resize(NT);
            Areas = new DVector<double>(); Areas.resize(NT);
            gParallel.ForEach(mesh.TriangleIndices(), (tid) => {
                Vector3d c, n; double a;
                mesh.GetTriInfo(tid, out n, out a, out c);
                Centroids[tid] = c;
                Normals[tid] = n;
                Areas[tid] = a;
            });
        }

        public void GetTriInfo(int tid, ref Vector3d n, ref double a, ref Vector3d c)
        {
            c = Centroids[tid];
            n = Normals[tid];
            a = Areas[tid];
        }
    }
}
