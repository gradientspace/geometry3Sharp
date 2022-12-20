// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Distributed under the Boost Software License, Version 1.0. http://www.boost.org/LICENSE_1_0.txt
using System;
using System.Collections.Generic;
using System.Threading;
using g3;

namespace gs
{
    /// <summary>
    /// This class sorts a set of mesh components.
    /// </summary>
    public class MeshSpatialSort
    {
        // ComponentMesh is a wrapper around input meshes
        public List<ComponentMesh> Components;

        // a MeshSolid is an "Outer" mesh and a set of "Cavity" meshes 
        // (the cavity list includes contained open meshes, though)
        public List<MeshSolid> Solids;

        public bool AllowOpenContainers = false;
        public double FastWindingIso = 0.5f;


        public MeshSpatialSort()
        {
            Components = new List<ComponentMesh>();
        }


        public void AddMesh(DMesh3 mesh, object identifier, DMeshAABBTree3 spatial = null)
        {
            ComponentMesh comp = new ComponentMesh(mesh, identifier, spatial);
            if (spatial == null) {
                if (comp.IsClosed || AllowOpenContainers)
                    comp.Spatial = new DMeshAABBTree3(mesh, true);
            }

            Components.Add(comp);
        }




        public class ComponentMesh
        {
            public object Identifier;
            public DMesh3 Mesh;
            public bool IsClosed;
            public DMeshAABBTree3 Spatial;
            public AxisAlignedBox3d Bounds;

            // meshes that contain this one
            public List<ComponentMesh> InsideOf = new List<ComponentMesh>();

            // meshes that are inside of this one
            public List<ComponentMesh> InsideSet = new List<ComponentMesh>();

            public ComponentMesh(DMesh3 mesh, object identifier, DMeshAABBTree3 spatial)
            {
                this.Mesh = mesh;
                this.Identifier = identifier;
                this.IsClosed = mesh.IsClosed();
                this.Spatial = spatial;
                Bounds = mesh.CachedBounds;
            }

            public bool Contains(ComponentMesh mesh2, double fIso = 0.5f)
            {
                if (this.Spatial == null)
                    return false;
                // make sure FWN is available
                this.Spatial.FastWindingNumber(Vector3d.Zero);

                // block-parallel iteration provides a reasonable speedup
                int NV = mesh2.Mesh.VertexCount;
                bool contained = true;
                gParallel.BlockStartEnd(0, NV - 1, (a, b) => {
                    if (contained == false)
                        return;
                    for (int vi = a; vi <= b && contained; vi++) {
                        Vector3d v = mesh2.Mesh.GetVertex(vi);
                        if ( Math.Abs(Spatial.FastWindingNumber(v)) < fIso) { 
                            contained = false;
                            break;
                        }
                    }
                }, 100);

                return contained;
            }
        }



        public class MeshSolid
        {
            public ComponentMesh Outer;
            public List<ComponentMesh> Cavities = new List<ComponentMesh>();
        }







        public void Sort()
        {
            int N = Components.Count;

            ComponentMesh[] comps = Components.ToArray();

            // sort by bbox containment to speed up testing (does it??)
            Array.Sort(comps, (i,j) => {
                return i.Bounds.Contains(j.Bounds) ? -1 : 1;
            });

            // containment sets
            bool[] bIsContained = new bool[N];
            Dictionary<int, List<int>> ContainSets = new Dictionary<int, List<int>>();
            Dictionary<int, List<int>> ContainedParents = new Dictionary<int, List<int>>();
            SpinLock dataLock = new SpinLock();

            // [TODO] this is 90% of compute time...
            //   - if I know X contains Y, and Y contains Z, then I don't have to check that X contains Z
            //   - can we exploit this somehow?
            //   - if j contains i, then it cannot be that i contains j. But we are
            //     not checking for this!  (although maybe bbox check still early-outs it?)

            // construct containment sets
            gParallel.ForEach(Interval1i.Range(N), (i) => {
                ComponentMesh compi = comps[i];

                if (compi.IsClosed == false && AllowOpenContainers == false)
                    return;

                for (int j = 0; j < N; ++j) {
                    if (i == j)
                        continue;
                    ComponentMesh compj = comps[j];

                    // cannot be contained if bounds are not contained
                    if (compi.Bounds.Contains(compj.Bounds) == false)
                        continue;

                    // any other early-outs??
                    if (compi.Contains(compj)) {

                        bool entered = false;
                        dataLock.Enter(ref entered);

                        compj.InsideOf.Add(compi);
                        compi.InsideSet.Add(compj);

                        if (ContainSets.ContainsKey(i) == false)
                            ContainSets.Add(i, new List<int>());
                        ContainSets[i].Add(j);
                        bIsContained[j] = true;
                        if (ContainedParents.ContainsKey(j) == false)
                            ContainedParents.Add(j, new List<int>());
                        ContainedParents[j].Add(i);

                        dataLock.Exit();
                    }

                }
            });


            List<MeshSolid> solids = new List<MeshSolid>();
            HashSet<ComponentMesh> used = new HashSet<ComponentMesh>();

            Dictionary<ComponentMesh, int> CompToOuterIndex = new Dictionary<ComponentMesh, int>();

            List<int> ParentsToProcess = new List<int>();


            // The following is a lot of code but it is very similar, just not clear how
            // to refactor out the common functionality
            //   1) we find all the top-level uncontained polys and add them to the final polys list
            //   2a) for any poly contained in those parent-polys, that is not also contained in anything else,
            //       add as hole to that poly
            //   2b) remove all those used parents & holes from consideration
            //   2c) now find all the "new" top-level polys
            //   3) repeat 2a-c until done all polys
            //   4) any remaining polys must be interior solids w/ no holes
            //          **or** weird leftovers like intersecting polys...

            // add all top-level uncontained polys
            for (int i = 0; i < N; ++i) {
                ComponentMesh compi = comps[i];
                if (bIsContained[i])
                    continue;

                MeshSolid g = new MeshSolid() { Outer = compi };

                int idx = solids.Count;
                CompToOuterIndex[compi] = idx;
                used.Add(compi);

                if (ContainSets.ContainsKey(i))
                    ParentsToProcess.Add(i);

                solids.Add(g);
            }


            // keep iterating until we processed all parents
            while (ParentsToProcess.Count > 0) {
                List<int> ContainersToRemove = new List<int>();

                // now for all top-level components that contain children, add those children
                // as long as they do not have multiple contain-parents
                foreach (int i in ParentsToProcess) {
                    ComponentMesh parentComp = comps[i];
                    int outer_idx = CompToOuterIndex[parentComp];

                    List<int> children = ContainSets[i];
                    foreach (int childj in children) {
                        ComponentMesh childComp = comps[childj];
                        Util.gDevAssert(used.Contains(childComp) == false);

                        // skip multiply-contained children
                        List<int> parents = ContainedParents[childj];
                        if (parents.Count > 1)
                            continue;

                        solids[outer_idx].Cavities.Add(childComp);

                        used.Add(childComp);
                        if (ContainSets.ContainsKey(childj))
                            ContainersToRemove.Add(childj);
                    }
                    ContainersToRemove.Add(i);
                }

                // remove all containers that are no longer valid
                foreach (int ci in ContainersToRemove) {
                    ContainSets.Remove(ci);

                    // have to remove from each ContainedParents list
                    List<int> keys = new List<int>(ContainedParents.Keys);
                    foreach (int j in keys) {
                        if (ContainedParents[j].Contains(ci))
                            ContainedParents[j].Remove(ci);
                    }
                }

                ParentsToProcess.Clear();

                // ok now find next-level uncontained parents...
                for (int i = 0; i < N; ++i) {
                    ComponentMesh compi = comps[i];
                    if (used.Contains(compi))
                        continue;
                    if (ContainSets.ContainsKey(i) == false)
                        continue;
                    List<int> parents = ContainedParents[i];
                    if (parents.Count > 0)
                        continue;

                    MeshSolid g = new MeshSolid() { Outer = compi };

                    int idx = solids.Count;
                    CompToOuterIndex[compi] = idx;
                    used.Add(compi);

                    if (ContainSets.ContainsKey(i))
                        ParentsToProcess.Add(i);

                    solids.Add(g);
                }
            }


            // any remaining components must be top-level
            for (int i = 0; i < N; ++i) {
                ComponentMesh compi = comps[i];
                if (used.Contains(compi))
                    continue;
                MeshSolid g = new MeshSolid() { Outer = compi };
                solids.Add(g);
            }

            Solids = solids;
        }


    }
}
