// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Distributed under the Boost Software License, Version 1.0. http://www.boost.org/LICENSE_1_0.txt
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using g3;

namespace gs
{
    /// <summary>
    /// For use case where we are making local edits to a source mesh. We mask out
    /// removed triangles from base mesh SpatialDS, and raycast new triangles separately.
    /// </summary>
    public class EditMeshSpatial : ISpatial
    {
        public DMesh3 SourceMesh;
        public DMeshAABBTree3 SourceSpatial;
        public DMesh3 EditMesh;

        HashSet<int> RemovedT = new HashSet<int>();
        HashSet<int> AddedT = new HashSet<int>();

        public void RemoveTriangle(int tid)
        {
            if ( AddedT.Contains(tid) ) {
                AddedT.Remove(tid);
            } else {
                RemovedT.Add(tid);
            }
        }
        
        public void AddTriangle(int tid)
        {
            AddedT.Add(tid);
        }


        public bool SupportsNearestTriangle { get { return false; } }
        public int FindNearestTriangle(Vector3d p, double fMaxDist = double.MaxValue) {
            return DMesh3.InvalidID;
        }

        public bool SupportsPointContainment { get { return false; } }
        public bool IsInside(Vector3d p) { return false; }


        public bool SupportsTriangleRayIntersection { get { return true; } }

        public int FindNearestHitTriangle(Ray3d ray, double fMaxDist = double.MaxValue)
        {
            var save_filter = SourceSpatial.TriangleFilterF;
            SourceSpatial.TriangleFilterF = source_filter;
            int hit_source_tid = SourceSpatial.FindNearestHitTriangle(ray);
            SourceSpatial.TriangleFilterF = save_filter;

            int hit_edit_tid;
            IntrRay3Triangle3 edit_hit = find_added_hit(ref ray, out hit_edit_tid);

            if (hit_source_tid == DMesh3.InvalidID && hit_edit_tid == DMesh3.InvalidID)
                return DMesh3.InvalidID;
            else if (hit_source_tid == DMesh3.InvalidID)
                return hit_edit_tid;
            else if (hit_edit_tid == DMesh3.InvalidID)
                return hit_source_tid;

            IntrRay3Triangle3 source_hit = (hit_source_tid != -1) ?
                MeshQueries.TriangleIntersection(SourceMesh, hit_source_tid, ray) : null;
            return (edit_hit.RayParameter < source_hit.RayParameter) ?
                hit_edit_tid : hit_source_tid;
        }

        bool source_filter(int tid)
        {
            return RemovedT.Contains(tid) == false;
        }


        IntrRay3Triangle3 find_added_hit(ref Ray3d ray, out int hit_tid)
        {
            hit_tid = DMesh3.InvalidID;
            IntrRay3Triangle3 nearest = null;
            double dNearT = double.MaxValue;

            Triangle3d tri = new Triangle3d();
            foreach ( int tid in AddedT) {
                Index3i tv = EditMesh.GetTriangle(tid);
                tri.V0 = EditMesh.GetVertex(tv.a);
                tri.V1 = EditMesh.GetVertex(tv.b);
                tri.V2 = EditMesh.GetVertex(tv.c);
                IntrRay3Triangle3 intr = new IntrRay3Triangle3(ray, tri);
                if ( intr.Find() && intr.RayParameter < dNearT ) {
                    dNearT = intr.RayParameter;
                    hit_tid = tid;
                    nearest = intr;
                }
            }
            return nearest;
        }



    }
}
