// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Distributed under the Boost Software License, Version 1.0. http://www.boost.org/LICENSE_1_0.txt
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using g3;

namespace gs
{

    /// <summary>
    /// Given an input mesh, try to decompose it's connected components into
    /// parts with some semantics - solids, open meshes, etc.
    /// </summary>
    public class MeshAssembly
    {
        public DMesh3 SourceMesh;

        // if true, each shell is a separate solid
        public bool HasNoVoids = false;

        /*
         * Outputs
         */

        public List<DMesh3> ClosedSolids;
        public List<DMesh3> OpenMeshes;

        
        public MeshAssembly(DMesh3 sourceMesh)
        {
            SourceMesh = sourceMesh;

            ClosedSolids = new List<DMesh3>();
            OpenMeshes = new List<DMesh3>();
        }


        public void Decompose()
        {
            process();
        }



        void process()
        {
            DMesh3 useSourceMesh = SourceMesh;

            // try to do simple mesh repairs
            if ( useSourceMesh.CachedIsClosed == false ) {

                useSourceMesh = new DMesh3(SourceMesh);

                // [TODO] should remove duplicate triangles here?
                RemoveDuplicateTriangles dupes = new RemoveDuplicateTriangles(useSourceMesh);
                dupes.Apply();

                // close cracks
                MergeCoincidentEdges merge = new MergeCoincidentEdges(useSourceMesh);
                //merge.OnlyUniquePairs = true;
                merge.Apply();
            }

            //Util.WriteDebugMesh(useSourceMesh, "c:\\scratch\\__FIRST_MERGE.obj");


            DMesh3[] components = MeshConnectedComponents.Separate(useSourceMesh);

            List<DMesh3> solidComps = new List<DMesh3>();

            foreach ( DMesh3 mesh in components ) {

                // [TODO] check if this is a mesh w/ cracks, in which case we
                // can do other processing?

                bool closed = mesh.CachedIsClosed;
                if ( closed == false ) {
                    OpenMeshes.Add(mesh);
                    continue;
                }

                solidComps.Add(mesh);
            }


            if (solidComps.Count == 0)
                return;
            if ( solidComps.Count == 1 ) {
                ClosedSolids = new List<DMesh3>() { solidComps[0] };
            }


            if (HasNoVoids) {
                // each solid is a separate solid
                ClosedSolids = process_solids_novoid(solidComps);
            } else {
                ClosedSolids = process_solids(solidComps);
            }

        }



        List<DMesh3> process_solids(List<DMesh3> solid_components)
        {
            // [TODO] maybe we can have special tags that extract out certain meshes?

            DMesh3 combinedSolid = new DMesh3(SourceMesh.Components | MeshComponents.FaceGroups);
            MeshEditor editor = new MeshEditor(combinedSolid);
            foreach (DMesh3 solid in solid_components) {
                editor.AppendMesh(solid, combinedSolid.AllocateTriangleGroup());
            }

            return new List<DMesh3>() { combinedSolid };
        }



        List<DMesh3> process_solids_novoid(List<DMesh3> solid_components)
        {
            return solid_components;
        }




    }
}
