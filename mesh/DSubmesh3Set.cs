using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;


namespace g3
{

    /// <summary>
    /// A set of submeshes of a base mesh. You provide a set of keys, and a Func
    /// that returns the triangle index list for a given key. The set of DSubmesh3
    /// objects are computed on construction.
    /// </summary>
    public class DSubmesh3Set : IEnumerable<DSubmesh3>
    {
        public DMesh3 Mesh;

        public IEnumerable<object> TriangleSetKeys;
        public Func<object, IEnumerable<int>> TriangleSetF;

        // outputs

        /// <summary> List of computed submeshes </summary>
        public List<DSubmesh3> Submeshes;

        /// <summary> Mapping from keys to submeshes </summary>
        public Dictionary<object, DSubmesh3> KeyToMesh;


        /// <summary>
        /// Construct submesh set from given keys and key-to-indices Func
        /// </summary>
        public DSubmesh3Set(DMesh3 mesh, IEnumerable<object> keys, Func<object,IEnumerable<int>> indexSetsF)
        {
            Mesh = mesh;
            TriangleSetKeys = keys;
            TriangleSetF = indexSetsF;

            ComputeSubMeshes();
        }


        /// <summary>
        /// Construct submesh set for an already-computed MeshConnectedComponents instance
        /// </summary>
        public DSubmesh3Set(DMesh3 mesh, MeshConnectedComponents components)
        {
            Mesh = mesh;

            TriangleSetF = (idx) => {
                return components.Components[(int)idx].Indices;
            };
            List<object> keys = new List<object>();
            for (int k = 0; k < components.Count; ++k)
                keys.Add(k);
            TriangleSetKeys = keys;

            ComputeSubMeshes();
        }


        public IEnumerator<DSubmesh3> GetEnumerator() {
            return Submeshes.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator() {
            return Submeshes.GetEnumerator();
        }



        virtual protected void ComputeSubMeshes()
        {
            Submeshes = new List<DSubmesh3>();
            KeyToMesh = new Dictionary<object, DSubmesh3>();

            SpinLock data_lock = new SpinLock();

            gParallel.ForEach(TriangleSetKeys, (obj) => {
                DSubmesh3 submesh = new DSubmesh3(Mesh, TriangleSetF(obj), 0);

                bool taken = false;
                data_lock.Enter(ref taken);
                Submeshes.Add(submesh);
                KeyToMesh[obj] = submesh;
                data_lock.Exit();
            });
        }

    }
}
