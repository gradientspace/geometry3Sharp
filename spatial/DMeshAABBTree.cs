using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

namespace g3
{
    /// <summary>
    /// Hierarchical Axis-Aligned-Bounding-Box tree for a DMesh3 mesh.
    /// This class supports a variety of spatial queries, listed below.
    /// 
    /// Various construction strategies are also available, the default is the
    /// fastest to build but if you are doing a *lot* of queries, you might experiment
    /// with the others (eg TopDownMedian)
    /// 
    /// Available queries:
    ///   - FindNearestTriangle(point, maxdist)
    ///   - FindNearestHitTriangle(ray, maxdist)
    ///   - FindAllHitTriangles(ray, maxdist)
    ///   - TestIntersection(triangle)
    ///   - TestIntersection(mesh)
    ///   - TestIntersection(otherAABBTree)
    ///   - FindAllIntersections(otherAABBTree)
    ///   - FindNearestTriangles(otherAABBTree, maxdist)
    ///   - IsInside(point)
    ///   - WindingNumber(point)
    ///   - FastWindingNumber(point)
    ///   - DoTraversal(generic_traversal_object)
    /// 
    /// </summary>
    public class DMeshAABBTree3 : ISpatial
    {
        protected DMesh3 mesh;
        protected int mesh_timestamp;

        public DMeshAABBTree3(DMesh3 m, bool autoBuild = false)
        {
            mesh = m;
            if (autoBuild)
                Build();
        }


        public DMesh3 Mesh { get { return mesh; } }


        /// <summary>
        /// If non-null, only triangle IDs that pass this filter (ie filter is true) are considered
        /// </summary>
        public Func<int, bool> TriangleFilterF = null;


        // Top-down build strategies will put at most this many triangles into a box.
        // Larger value == shallower trees, but leaves cost more to test
        public int TopDownLeafMaxTriCount = 4;

        // bottom-up FastVolumeMetric cluster policy sorts available boxes along an axis
        // and then proceeds from min to max, greedily grouping best-pairs. This value determines
        // the range of the greedy search. Larger == slower but better bounding
        // (at least in theory...)
        public int BottomUpClusterLookahead = 10;


        // how should we build the tree?
        public enum BuildStrategy
        {
            Default,                // currently TopDownMidpoint

            TopDownMidpoint,        // Recursively split triangle set by midpoint of axis interval.
                                    //   This is the fastest and usually produces lower total-volume trees than bottom-up.
                                    //   Resulting trees are unbalanced, though.
            BottomUpFromOneRings,   // Construct leaf layer based on triangle one-rings, and then
                                    //   cluster boxes to build tree upward. Various cluster policies (below).
                                    //   About 2.5x slower than TopDownMidpoint. Trees are basically balanced, although
                                    //   current approach to clustering odd-count layers is to duplicate the extra box.
            TopDownMedian           // Like TopDownMidpoint except we sort the triangle lists at each step and split on the median.
                                    //   2-4x slower than TopDownMidpoint, but trees are generally more efficient and balanced.
        }

        public enum ClusterPolicy
        {
            Default,               // currently FastVolumeMetric
            Fastest,               // sort list and then just cluster sequential boxes. 
                                   //   Tree efficiency suffers, but fast.
            FastVolumeMetric,      // sort list and then check next N boxes for best cluster. Only slightly slower than
                                   //   sequential clustering but trees are often quite a bit more efficient.
            MinimalVolume          // compute full pair matrix at each step (N^2), and sequentially pick out best pairs.
                                   //   this usually does quite a bit better job, but it is unusable for large tri counts.
        }


        // Build the tree. Policy only matters for bottom-up strategies
        public void Build(BuildStrategy eStrategy = BuildStrategy.TopDownMidpoint, 
                          ClusterPolicy ePolicy = ClusterPolicy.Default)
        {
            if (eStrategy == BuildStrategy.BottomUpFromOneRings)
                build_by_one_rings(ePolicy);
            else if (eStrategy == BuildStrategy.TopDownMedian)
                build_top_down(true);
            else if (eStrategy == BuildStrategy.TopDownMidpoint)
                build_top_down(false);
            else if (eStrategy == BuildStrategy.Default)
                build_top_down(false);
            mesh_timestamp = mesh.ShapeTimestamp;
        }


        public bool IsValid { get { return mesh_timestamp == mesh.ShapeTimestamp; } }


        /// <summary>
        /// Does this ISpatial implementation support nearest-point query? (yes)
        /// </summary>
        public bool SupportsNearestTriangle { get { return true; } }


        /// <summary>
        /// Find the triangle closest to p, within distance fMaxDist, or return InvalidID
        /// Use MeshQueries.TriangleDistance() to get more information
        /// </summary>
        public virtual int FindNearestTriangle(Vector3d p, double fMaxDist = double.MaxValue)
        {
            if (mesh_timestamp != mesh.ShapeTimestamp)
                throw new Exception("DMeshAABBTree3.FindNearestTriangle: mesh has been modified since tree construction");

            double fNearestSqr = (fMaxDist < double.MaxValue) ? fMaxDist * fMaxDist : double.MaxValue;
            int tNearID = DMesh3.InvalidID;
            find_nearest_tri(root_index, p, ref fNearestSqr, ref tNearID);
            return tNearID;
        }
        /// <summary>
        /// Find the triangle closest to p, and distance to it, within distance fMaxDist, or return InvalidID
        /// Use MeshQueries.TriangleDistance() to get more information
        /// </summary>
        public virtual int FindNearestTriangle(Vector3d p, out double fNearestDistSqr, double fMaxDist = double.MaxValue)
        {
            if (mesh_timestamp != mesh.ShapeTimestamp)
                throw new Exception("DMeshAABBTree3.FindNearestTriangle: mesh has been modified since tree construction");

            fNearestDistSqr = (fMaxDist < double.MaxValue) ? fMaxDist * fMaxDist : double.MaxValue;
            int tNearID = DMesh3.InvalidID;
            find_nearest_tri(root_index, p, ref fNearestDistSqr, ref tNearID);
            return tNearID;
        }
        protected void find_nearest_tri(int iBox, Vector3d p, ref double fNearestSqr, ref int tID)
        {
            int idx = box_to_index[iBox];
            if ( idx < triangles_end ) {            // triange-list case, array is [N t1 t2 ... tN]
                int num_tris = index_list[idx];
                for (int i = 1; i <= num_tris; ++i) {
                    int ti = index_list[idx + i];
                    if (TriangleFilterF != null && TriangleFilterF(ti) == false)
                        continue;
                    double fTriDistSqr = MeshQueries.TriDistanceSqr(mesh, ti, p);
                    if ( fTriDistSqr < fNearestSqr ) {
                        fNearestSqr = fTriDistSqr;
                        tID = ti;
                    }
                }

            } else {                                // internal node, either 1 or 2 child boxes
                int iChild1 = index_list[idx];
                if ( iChild1 < 0 ) {                 // 1 child, descend if nearer than cur min-dist
                    iChild1 = (-iChild1) - 1;
                    double fChild1DistSqr = box_distance_sqr(iChild1, p);
                    if ( fChild1DistSqr <= fNearestSqr )
                        find_nearest_tri(iChild1, p, ref fNearestSqr, ref tID);

                } else {                            // 2 children, descend closest first
                    iChild1 = iChild1 - 1;
                    int iChild2 = index_list[idx + 1] - 1;

                    double fChild1DistSqr = box_distance_sqr(iChild1, p);
                    double fChild2DistSqr = box_distance_sqr(iChild2, p);
                    if (fChild1DistSqr < fChild2DistSqr) {
                        if (fChild1DistSqr < fNearestSqr) {
                            find_nearest_tri(iChild1, p, ref fNearestSqr, ref tID);
                            if (fChild2DistSqr < fNearestSqr)
                                find_nearest_tri(iChild2, p, ref fNearestSqr, ref tID);
                        }
                    } else {
                        if (fChild2DistSqr < fNearestSqr) {
                            find_nearest_tri(iChild2, p, ref fNearestSqr, ref tID);
                            if (fChild1DistSqr < fNearestSqr)
                                find_nearest_tri(iChild1, p, ref fNearestSqr, ref tID);
                        }
                    }

                }
            }
        }



        /// <summary>
        /// Find the vertex closest to p, within distance fMaxDist, or return InvalidID
        /// </summary>
        public virtual int FindNearestVertex(Vector3d p, double fMaxDist = double.MaxValue)
        {
            if (mesh_timestamp != mesh.ShapeTimestamp)
                throw new Exception("DMeshAABBTree3.FindNearestVertex: mesh has been modified since tree construction");

            double fNearestSqr = (fMaxDist < double.MaxValue) ? fMaxDist * fMaxDist : double.MaxValue;
            int vNearID = DMesh3.InvalidID;
            find_nearest_vtx(root_index, p, ref fNearestSqr, ref vNearID);
            return vNearID;
        }
        protected void find_nearest_vtx(int iBox, Vector3d p, ref double fNearestSqr, ref int vid)
        {
            int idx = box_to_index[iBox];
            if (idx < triangles_end) {            // triange-list case, array is [N t1 t2 ... tN]
                int num_tris = index_list[idx];
                for (int i = 1; i <= num_tris; ++i) {
                    int ti = index_list[idx + i];
                    if (TriangleFilterF != null && TriangleFilterF(ti) == false)
                        continue;
                    Vector3i tv = mesh.GetTriangle(ti);
                    for ( int j = 0; j < 3; ++j ) {
                        double dsqr = mesh.GetVertex(tv[j]).DistanceSquared(ref p);
                        if (  dsqr < fNearestSqr ) {
                            fNearestSqr = dsqr;
                            vid = tv[j];
                        }
                    }
                }

            } else {                                // internal node, either 1 or 2 child boxes
                int iChild1 = index_list[idx];
                if (iChild1 < 0) {                 // 1 child, descend if nearer than cur min-dist
                    iChild1 = (-iChild1) - 1;
                    double fChild1DistSqr = box_distance_sqr(iChild1, p);
                    if (fChild1DistSqr <= fNearestSqr)
                        find_nearest_vtx(iChild1, p, ref fNearestSqr, ref vid);

                } else {                            // 2 children, descend closest first
                    iChild1 = iChild1 - 1;
                    int iChild2 = index_list[idx + 1] - 1;

                    double fChild1DistSqr = box_distance_sqr(iChild1, p);
                    double fChild2DistSqr = box_distance_sqr(iChild2, p);
                    if (fChild1DistSqr < fChild2DistSqr) {
                        if (fChild1DistSqr < fNearestSqr) {
                            find_nearest_vtx(iChild1, p, ref fNearestSqr, ref vid);
                            if (fChild2DistSqr < fNearestSqr)
                                find_nearest_vtx(iChild2, p, ref fNearestSqr, ref vid);
                        }
                    } else {
                        if (fChild2DistSqr < fNearestSqr) {
                            find_nearest_vtx(iChild2, p, ref fNearestSqr, ref vid);
                            if (fChild1DistSqr < fNearestSqr)
                                find_nearest_vtx(iChild1, p, ref fNearestSqr, ref vid);
                        }
                    }

                }
            }
        }





        /// <summary>
        /// Does this ISpatial implementation support ray-triangle intersection? (yes)
        /// </summary>
        public bool SupportsTriangleRayIntersection { get { return true; } }

        /// <summary>
        /// find id of first triangle that ray hits, within distance fMaxDist, or return DMesh3.InvalidID
        /// Use MeshQueries.TriangleIntersection() to get more information
        /// </summary>
        public virtual int FindNearestHitTriangle(Ray3d ray, double fMaxDist = double.MaxValue)
        {
            if (mesh_timestamp != mesh.ShapeTimestamp)
                throw new Exception("DMeshAABBTree3.FindNearestHitTriangle: mesh has been modified since tree construction");
            if (ray.Direction.IsNormalized == false)
                throw new Exception("DMeshAABBTree3.FindNearestHitTriangle: ray direction is not normalized");

            // [RMS] note: using float.MaxValue here because we need to use <= to compare box hit
            //   to fNearestT, and box hit returns double.MaxValue on no-hit. So, if we set
            //   nearestT to double.MaxValue, then we will test all boxes (!)
            double fNearestT = (fMaxDist < double.MaxValue) ? fMaxDist : float.MaxValue;
            int tNearID = DMesh3.InvalidID;
            find_hit_triangle(root_index, ref ray, ref fNearestT, ref tNearID);
            return tNearID;
        }

        protected void find_hit_triangle(int iBox, ref Ray3d ray, ref double fNearestT, ref int tID)
        {
            int idx = box_to_index[iBox];
            if ( idx < triangles_end ) {            // triange-list case, array is [N t1 t2 ... tN]
                Triangle3d tri = new Triangle3d();
                int num_tris = index_list[idx];
                for (int i = 1; i <= num_tris; ++i) {
                    int ti = index_list[idx + i];
                    if (TriangleFilterF != null && TriangleFilterF(ti) == false)
                        continue;

                    mesh.GetTriVertices(ti, ref tri.V0, ref tri.V1, ref tri.V2);
                    double rayt;
                    if (IntrRay3Triangle3.Intersects(ref ray, ref tri.V0, ref tri.V1, ref tri.V2, out rayt)) {
                        if (rayt < fNearestT) {
                            fNearestT = rayt;
                            tID = ti;
                        }
                    }
                    //IntrRay3Triangle3 ray_tri_hit = new IntrRay3Triangle3(ray, tri);
                    //if ( ray_tri_hit.Find() ) {
                    //    if ( ray_tri_hit.RayParameter < fNearestT ) {
                    //        fNearestT = ray_tri_hit.RayParameter;
                    //        tID = ti;
                    //    }
                    //}
                }

            } else {                                // internal node, either 1 or 2 child boxes
                double e = MathUtil.ZeroTolerancef;

                int iChild1 = index_list[idx];
                if ( iChild1 < 0 ) {                 // 1 child, descend if nearer than cur min-dist
                    iChild1 = (-iChild1) - 1;
                    double fChild1T = box_ray_intersect_t(iChild1, ray);
                    if (fChild1T <= fNearestT + e) {
                        find_hit_triangle(iChild1, ref ray, ref fNearestT, ref tID);
                    }

                } else {                            // 2 children, descend closest first
                    iChild1 = iChild1 - 1;
                    int iChild2 = index_list[idx + 1] - 1;

                    double fChild1T = box_ray_intersect_t(iChild1, ray);
                    double fChild2T = box_ray_intersect_t(iChild2, ray);
                    if (fChild1T < fChild2T) {
                        if (fChild1T <= fNearestT + e) {
                            find_hit_triangle(iChild1, ref ray, ref fNearestT, ref tID);
                            if (fChild2T <= fNearestT + e) {
                                find_hit_triangle(iChild2, ref ray, ref fNearestT, ref tID);
                            }
                        }
                    } else {
                        if (fChild2T <= fNearestT + e) {
                            find_hit_triangle(iChild2, ref ray, ref fNearestT, ref tID);
                            if (fChild1T <= fNearestT + e) {
                                find_hit_triangle(iChild1, ref ray, ref fNearestT, ref tID);
                            }
                        }
                    }

                }
            }
        }








        /// <summary>
        /// Find the ids of all the triangles that they ray intersects, within distance fMaxDist from ray origin
        /// Returns count of triangles.
        /// </summary>
        public virtual int FindAllHitTriangles(Ray3d ray, List<int> hitTriangles = null, double fMaxDist = double.MaxValue)
        {
            if (mesh_timestamp != mesh.ShapeTimestamp)
                throw new Exception("DMeshAABBTree3.FindNearestHitTriangle: mesh has been modified since tree construction");
            if (ray.Direction.IsNormalized == false)
                throw new Exception("DMeshAABBTree3.FindNearestHitTriangle: ray direction is not normalized");

            // [RMS] note: using float.MaxValue here because we need to use <= to compare box hit
            //   to fNearestT, and box hit returns double.MaxValue on no-hit. So, if we set
            //   nearestT to double.MaxValue, then we will test all boxes (!)
            double fUseMaxDist = (fMaxDist < double.MaxValue) ? fMaxDist : float.MaxValue;
            int nCount = find_all_hit_triangles(root_index, hitTriangles, ref ray, fUseMaxDist);
            return nCount;
        }

        protected int find_all_hit_triangles(int iBox, List<int> hitTriangles, ref Ray3d ray, double fMaxDist)
        {
            int hit_count = 0;

            int idx = box_to_index[iBox];
            if ( idx < triangles_end ) {            // triange-list case, array is [N t1 t2 ... tN]
                Triangle3d tri = new Triangle3d();
                int num_tris = index_list[idx];
                for (int i = 1; i <= num_tris; ++i) {
                    int ti = index_list[idx + i];
                    if (TriangleFilterF != null && TriangleFilterF(ti) == false)
                        continue;

                    mesh.GetTriVertices(ti, ref tri.V0, ref tri.V1, ref tri.V2);
                    double rayt;
                    if (IntrRay3Triangle3.Intersects(ref ray, ref tri.V0, ref tri.V1, ref tri.V2, out rayt)) {
                        if (rayt < fMaxDist) {
                            if (hitTriangles != null)
                                hitTriangles.Add(ti);
                            hit_count++;
                        }
                    }
                    //IntrRay3Triangle3 ray_tri_hit = new IntrRay3Triangle3(ray, tri);
                    //if (ray_tri_hit.Find()) {
                    //    if (ray_tri_hit.RayParameter < fMaxDist) {
                    //        if (hitTriangles != null)
                    //            hitTriangles.Add(ti);
                    //        hit_count++;
                    //    }
                    //}
                }

            } else {                                // internal node, either 1 or 2 child boxes
                double e = MathUtil.ZeroTolerancef;

                int iChild1 = index_list[idx];
                if ( iChild1 < 0 ) {                 // 1 child, descend if nearer than cur min-dist
                    iChild1 = (-iChild1) - 1;
                    double fChild1T = box_ray_intersect_t(iChild1, ray);
                    if (fChild1T <= fMaxDist + e)
                        hit_count += find_all_hit_triangles(iChild1, hitTriangles, ref ray, fMaxDist);

                } else {                            // 2 children, descend closest first
                    iChild1 = iChild1 - 1;
                    int iChild2 = index_list[idx + 1] - 1;

                    double fChild1T = box_ray_intersect_t(iChild1, ray);
                    if (fChild1T <= fMaxDist + e)
                        hit_count += find_all_hit_triangles(iChild1, hitTriangles, ref ray, fMaxDist);

                    double fChild2T = box_ray_intersect_t(iChild2, ray);
                    if (fChild2T <= fMaxDist + e) 
                        hit_count += find_all_hit_triangles(iChild2, hitTriangles, ref ray, fMaxDist);
                }
            }

            return hit_count;
        }




        /// <summary>
        /// return true if *any* triangle of testMesh intersects with our tree.
        /// Use TransformF to transform vertices of testMesh into space of this tree.
        /// if boundsCheck is false, we skip bbox/bbox early-out
        /// </summary>
        public virtual bool TestIntersection(IMesh testMesh, Func<Vector3d, Vector3d> TransformF = null, bool bBoundsCheck = true)
        {
            if (mesh_timestamp != mesh.ShapeTimestamp)
                throw new Exception("DMeshAABBTree3.TestIntersection: mesh has been modified since tree construction");

            if (bBoundsCheck) {
                AxisAlignedBox3d meshBox = MeshMeasurements.Bounds(testMesh, TransformF);
                if (box_box_intersect(root_index, ref meshBox) == false )
                    return false;
            }

            if (TransformF == null)
                TransformF = (x) => { return x; };

            Triangle3d test_tri = new Triangle3d();
            foreach (int tid in testMesh.TriangleIndices()) {
                Index3i tri = testMesh.GetTriangle(tid);
                test_tri.V0 = TransformF(testMesh.GetVertex(tri.a));
                test_tri.V1 = TransformF(testMesh.GetVertex(tri.b));
                test_tri.V2 = TransformF(testMesh.GetVertex(tri.c));
                if (TestIntersection(test_tri))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Returns true if triangle intersects any triangle of our mesh
        /// </summary>
        public virtual bool TestIntersection(Triangle3d triangle)
        {
            if (mesh_timestamp != mesh.ShapeTimestamp)
                throw new Exception("DMeshAABBTree3.TestIntersection: mesh has been modified since tree construction");

            AxisAlignedBox3d triBounds = BoundsUtil.Bounds(ref triangle);
            int interTri = find_any_intersection(root_index, ref triangle, ref triBounds);
            return (interTri >= 0);
        }


        protected int find_any_intersection(int iBox, ref Triangle3d triangle, ref AxisAlignedBox3d triBounds)
        {
            int idx = box_to_index[iBox];
            if ( idx < triangles_end ) {            // triange-list case, array is [N t1 t2 ... tN]
                Triangle3d box_tri = new Triangle3d();
                int num_tris = index_list[idx];
                for (int i = 1; i <= num_tris; ++i) {
                    int ti = index_list[idx + i];
                    if (TriangleFilterF != null && TriangleFilterF(ti) == false)
                        continue;
                    mesh.GetTriVertices(ti, ref box_tri.V0, ref box_tri.V1, ref box_tri.V2);
                    if ( IntrTriangle3Triangle3.Intersects(ref triangle, ref box_tri))
                        return ti;
                }
            } else {                                // internal node, either 1 or 2 child boxes
                int iChild1 = index_list[idx];
                if ( iChild1 < 0 ) {                 // 1 child, descend if nearer than cur min-dist
                    iChild1 = (-iChild1) - 1;
                    if ( box_box_intersect(iChild1, ref triBounds) )
                        return find_any_intersection(iChild1, ref triangle, ref triBounds);

                } else {                            // 2 children, descend closest first
                    iChild1 = iChild1 - 1;
                    int iChild2 = index_list[idx + 1] - 1;

                    int interTri = -1;
                    if ( box_box_intersect(iChild1, ref triBounds) ) 
                        interTri = find_any_intersection(iChild1, ref triangle, ref triBounds);
                    if ( interTri == -1 && box_box_intersect(iChild2, ref triBounds) )
                        interTri = find_any_intersection(iChild2, ref triangle, ref triBounds);
                    return interTri;
                }
            }

            return -1;
        }






        /// <summary>
        /// Returns true if there is *any* intersection between our mesh and 'other' mesh.
        /// TransformF takes vertices of otherTree into our tree - can be null if in same coord space
        /// </summary>
        public virtual bool TestIntersection(DMeshAABBTree3 otherTree, Func<Vector3d, Vector3d> TransformF = null)
        {
            if (mesh_timestamp != mesh.ShapeTimestamp)
                throw new Exception("DMeshAABBTree3.TestIntersection: mesh has been modified since tree construction");

            if (find_any_intersection(root_index, otherTree, TransformF, otherTree.root_index, 0))
                return true;

            return false;
        }

        protected bool find_any_intersection(int iBox, DMeshAABBTree3 otherTree, Func<Vector3d, Vector3d> TransformF, int oBox, int depth)
        {
            int idx = box_to_index[iBox];
            int odx = otherTree.box_to_index[oBox];

            if (idx < triangles_end && odx < otherTree.triangles_end) {
                // ok we are at triangles for both trees, do triangle-level testing
                Triangle3d tri = new Triangle3d(), otri = new Triangle3d();
                int num_tris = index_list[idx], onum_tris = otherTree.index_list[odx];

                // can re-use because Test() doesn't cache anything
                //IntrTriangle3Triangle3 intr = new IntrTriangle3Triangle3(new Triangle3d(), new Triangle3d());

                // outer iteration is "other" tris that need to be transformed (more expensive)
                for (int j = 1; j <= onum_tris; ++j) {
                    int tj = otherTree.index_list[odx + j];
                    if (otherTree.TriangleFilterF != null && otherTree.TriangleFilterF(tj) == false)
                        continue;
                    otherTree.mesh.GetTriVertices(tj, ref otri.V0, ref otri.V1, ref otri.V2);
                    if (TransformF != null) {
                        otri.V0 = TransformF(otri.V0);
                        otri.V1 = TransformF(otri.V1);
                        otri.V2 = TransformF(otri.V2);
                    }

                    // inner iteration over "our" triangles
                    for (int i = 1; i <= num_tris; ++i) {
                        int ti = index_list[idx + i];
                        if (TriangleFilterF != null && TriangleFilterF(ti) == false)
                            continue;
                        mesh.GetTriVertices(ti, ref tri.V0, ref tri.V1, ref tri.V2);
                        if (IntrTriangle3Triangle3.Intersects(ref otri, ref tri))
                            return true;
                    }
                }
                return false;
            }

            // we either descend "our" tree or the other tree
            //   - if we have hit triangles on "our" tree, we have to descend other
            //   - if we hit triangles on "other", we have to descend ours
            //   - otherwise, we alternate at each depth. This produces wider
            //     branching but is significantly faster (~10x) for both hits and misses
            bool bDescendOther = (idx < triangles_end || depth % 2 == 0);
            if (bDescendOther && odx < otherTree.triangles_end)
                bDescendOther = false;      // can't
            
            if (bDescendOther) {
                // ok we hit triangles on our side but we need to still reach triangles on
                // the other side, so we descend "their" children

                // [TODO] could we do efficient box.intersects(transform(box)) test?
                //   ( Contains() on each xformed point? )
                AxisAlignedBox3d bounds = get_boxd(iBox);

                int oChild1 = otherTree.index_list[odx];
                if ( oChild1 < 0 ) {                 // 1 child, descend if nearer than cur min-dist
                    oChild1 = (-oChild1) - 1;
                    AxisAlignedBox3d oChild1Box = otherTree.get_boxd(oChild1, TransformF);
                    if (oChild1Box.Intersects(bounds) )
                        return find_any_intersection(iBox, otherTree, TransformF, oBox, depth + 1);

                } else {                            // 2 children
                    oChild1 = oChild1 - 1;          // [TODO] could descend one w/ larger overlap volume first??
                    int oChild2 = otherTree.index_list[odx + 1] - 1;

                    bool intersects = false;
                    AxisAlignedBox3d oChild1Box = otherTree.get_boxd(oChild1, TransformF);
                    if ( oChild1Box.Intersects(bounds) ) 
                        intersects = find_any_intersection(iBox, otherTree, TransformF, oChild1, depth + 1);

                    if (intersects == false) {
                        AxisAlignedBox3d oChild2Box = otherTree.get_boxd(oChild2, TransformF);
                        if ( oChild2Box.Intersects(bounds) )
                            intersects = find_any_intersection(iBox, otherTree, TransformF, oChild2, depth + 1);
                    }
                    return intersects;
                }


            } else {
                // descend our tree nodes if they intersect w/ current bounds of other tree
                AxisAlignedBox3d oBounds = otherTree.get_boxd(oBox, TransformF);

                int iChild1 = index_list[idx];
                if ( iChild1 < 0 ) {                 // 1 child, descend if nearer than cur min-dist
                    iChild1 = (-iChild1) - 1;
                    if ( box_box_intersect(iChild1, ref oBounds) )
                        return find_any_intersection(iChild1, otherTree, TransformF, oBox, depth + 1);

                } else {                            // 2 children
                    iChild1 = iChild1 - 1;          // [TODO] could descend one w/ larger overlap volume first??
                    int iChild2 = index_list[idx + 1] - 1;

                    bool intersects = false;
                    if ( box_box_intersect(iChild1, ref oBounds) ) 
                        intersects = find_any_intersection(iChild1, otherTree, TransformF, oBox, depth + 1);
                    if ( intersects == false && box_box_intersect(iChild2, ref oBounds) )
                        intersects = find_any_intersection(iChild2, otherTree, TransformF, oBox, depth + 1);
                    return intersects;
                }

            }
            return false;
        }





        public struct PointIntersection
        {
            public int t0, t1;
            public Vector3d point;
        }
        public struct SegmentIntersection
        {
            public int t0, t1;
            public Vector3d point0, point1;
        }
        public class IntersectionsQueryResult
        {
            public List<PointIntersection> Points;
            public List<SegmentIntersection> Segments;
        }


        /// <summary>
        /// Compute all intersections between two Meshes. 
        /// TransformF argument transforms vertices of otherTree to our tree (can be null if in same coord space)
        /// Returns pairs of intersecting triangles, which could intersect in either point or segment
        /// Currently *does not* return coplanar intersections.
        /// </summary>
        public virtual IntersectionsQueryResult FindAllIntersections(DMeshAABBTree3 otherTree, Func<Vector3d, Vector3d> TransformF = null)
        {
            if (mesh_timestamp != mesh.ShapeTimestamp)
                throw new Exception("DMeshAABBTree3.FindIntersections: mesh has been modified since tree construction");

            IntersectionsQueryResult result = new IntersectionsQueryResult();
            result.Points = new List<PointIntersection>();
            result.Segments = new List<SegmentIntersection>();

            IntrTriangle3Triangle3 intr = new IntrTriangle3Triangle3(new Triangle3d(), new Triangle3d());
            find_intersections(root_index, otherTree, TransformF, otherTree.root_index, 0, intr, result);

            return result;
        }

        protected void find_intersections(int iBox, DMeshAABBTree3 otherTree, Func<Vector3d, Vector3d> TransformF, 
                                          int oBox, int depth,
                                          IntrTriangle3Triangle3 intr, IntersectionsQueryResult result)
        {
            int idx = box_to_index[iBox];
            int odx = otherTree.box_to_index[oBox];

            if (idx < triangles_end && odx < otherTree.triangles_end) {
                // ok we are at triangles for both trees, do triangle-level testing
                Triangle3d tri = new Triangle3d(), otri = new Triangle3d();
                int num_tris = index_list[idx], onum_tris = otherTree.index_list[odx];

                // outer iteration is "other" tris that need to be transformed (more expensive)
                for (int j = 1; j <= onum_tris; ++j) {
                    int tj = otherTree.index_list[odx + j];
                    if (otherTree.TriangleFilterF != null && otherTree.TriangleFilterF(tj) == false)
                        continue;
                    otherTree.mesh.GetTriVertices(tj, ref otri.V0, ref otri.V1, ref otri.V2);
                    if (TransformF != null) {
                        otri.V0 = TransformF(otri.V0);
                        otri.V1 = TransformF(otri.V1);
                        otri.V2 = TransformF(otri.V2);
                    }
                    intr.Triangle0 = otri;

                    // inner iteration over "our" triangles
                    for (int i = 1; i <= num_tris; ++i) {
                        int ti = index_list[idx + i];
                        if (TriangleFilterF != null && TriangleFilterF(ti) == false)
                            continue;
                        mesh.GetTriVertices(ti, ref tri.V0, ref tri.V1, ref tri.V2);
                        intr.Triangle1 = tri;

                        // [RMS] Test() is much faster than Find() so it makes sense to call it first, as most
                        // triangles will not intersect (right?)
                        if (intr.Test()) {
                            if ( intr.Find() ) { 
                                if (intr.Quantity == 1) {
                                    result.Points.Add(new PointIntersection() 
                                            { t0 = ti, t1 = tj, point = intr.Points[0] });
                                } else if (intr.Quantity == 2) {
                                    result.Segments.Add( new SegmentIntersection() 
                                            { t0 = ti, t1 = tj, point0 = intr.Points[0], point1 = intr.Points[1] });
                                } else {
                                    throw new Exception("DMeshAABBTree.find_intersections: found quantity " + intr.Quantity );
                                }
                                }
                        }
                    }
                }

                // done these nodes
                return;
            }

            // we either descend "our" tree or the other tree
            //   - if we have hit triangles on "our" tree, we have to descend other
            //   - if we hit triangles on "other", we have to descend ours
            //   - otherwise, we alternate at each depth. This produces wider
            //     branching but is significantly faster (~10x) for both hits and misses
            bool bDescendOther = (idx < triangles_end || depth % 2 == 0);
            if (bDescendOther && odx < otherTree.triangles_end)
                bDescendOther = false;      // can't
            
            if (bDescendOther) {
                // ok we hit triangles on our side but we need to still reach triangles on
                // the other side, so we descend "their" children

                // [TODO] could we do efficient box.intersects(transform(box)) test?
                //   ( Contains() on each xformed point? )
                AxisAlignedBox3d bounds = get_boxd(iBox);

                int oChild1 = otherTree.index_list[odx];
                if ( oChild1 < 0 ) {                 // 1 child, descend if nearer than cur min-dist
                    oChild1 = (-oChild1) - 1;
                    AxisAlignedBox3d oChild1Box = otherTree.get_boxd(oChild1, TransformF);
                    if (oChild1Box.Intersects(bounds) )
                        find_intersections(iBox, otherTree, TransformF, oChild1, depth + 1, intr, result);

                } else {                            // 2 children
                    oChild1 = oChild1 - 1;

                    AxisAlignedBox3d oChild1Box = otherTree.get_boxd(oChild1, TransformF);
                    if ( oChild1Box.Intersects(bounds) ) 
                        find_intersections(iBox, otherTree, TransformF, oChild1, depth + 1, intr, result);

                    int oChild2 = otherTree.index_list[odx + 1] - 1;
                    AxisAlignedBox3d oChild2Box = otherTree.get_boxd(oChild2, TransformF);
                    if ( oChild2Box.Intersects(bounds) )
                        find_intersections(iBox, otherTree, TransformF, oChild2, depth + 1, intr, result);
                }

            } else {
                // descend our tree nodes if they intersect w/ current bounds of other tree
                AxisAlignedBox3d oBounds = otherTree.get_boxd(oBox, TransformF);

                int iChild1 = index_list[idx];
                if ( iChild1 < 0 ) {                 // 1 child, descend if nearer than cur min-dist
                    iChild1 = (-iChild1) - 1;
                    if ( box_box_intersect(iChild1, ref oBounds) )
                        find_intersections(iChild1, otherTree, TransformF, oBox, depth + 1, intr, result);

                } else {                            // 2 children
                    iChild1 = iChild1 - 1;          
                    if ( box_box_intersect(iChild1, ref oBounds) ) 
                        find_intersections(iChild1, otherTree, TransformF, oBox, depth + 1, intr, result);

                    int iChild2 = index_list[idx + 1] - 1;
                    if ( box_box_intersect(iChild2, ref oBounds) )
                        find_intersections(iChild2, otherTree, TransformF, oBox, depth + 1, intr, result);
                }

            }
        }






        /// <summary>
        /// Find nearest pair of triangles on this tree with otherTree, within max_dist.
        /// TransformF transforms vertices of otherTree into our coordinates. can be null.
        /// returns triangle-id pair (my_tri,other_tri), or Index2i.Max if not found within max_dist
        /// Use MeshQueries.TrianglesDistance() to get more information
        /// </summary>
        public virtual Index2i FindNearestTriangles(DMeshAABBTree3 otherTree, Func<Vector3d, Vector3d> TransformF, out double distance, double max_dist = double.MaxValue)
        {
            if (mesh_timestamp != mesh.ShapeTimestamp)
                throw new Exception("DMeshAABBTree3.TestIntersection: mesh has been modified since tree construction");

            double nearest_sqr = double.MaxValue;
            if (max_dist < double.MaxValue)
                nearest_sqr = max_dist * max_dist;
            Index2i nearest_pair = Index2i.Max;

            find_nearest_triangles(root_index, otherTree, TransformF, otherTree.root_index, 0, ref nearest_sqr, ref nearest_pair);
            distance = (nearest_sqr < double.MaxValue) ? Math.Sqrt(nearest_sqr) : double.MaxValue;
            return nearest_pair;
        }

        protected void find_nearest_triangles(int iBox, DMeshAABBTree3 otherTree, Func<Vector3d, Vector3d> TransformF, int oBox, int depth, ref double nearest_sqr, ref Index2i nearest_pair)
        {
            int idx = box_to_index[iBox];
            int odx = otherTree.box_to_index[oBox];

            if (idx < triangles_end && odx < otherTree.triangles_end) {
                // ok we are at triangles for both trees, do triangle-level testing
                Triangle3d tri = new Triangle3d(), otri = new Triangle3d();
                int num_tris = index_list[idx], onum_tris = otherTree.index_list[odx];

                DistTriangle3Triangle3 dist = new DistTriangle3Triangle3(new Triangle3d(), new Triangle3d());

                // outer iteration is "other" tris that need to be transformed (more expensive)
                for (int j = 1; j <= onum_tris; ++j) {
                    int tj = otherTree.index_list[odx + j];
                    if (otherTree.TriangleFilterF != null && otherTree.TriangleFilterF(tj) == false)
                        continue;
                    otherTree.mesh.GetTriVertices(tj, ref otri.V0, ref otri.V1, ref otri.V2);
                    if (TransformF != null) {
                        otri.V0 = TransformF(otri.V0);
                        otri.V1 = TransformF(otri.V1);
                        otri.V2 = TransformF(otri.V2);
                    }
                    dist.Triangle0 = otri;

                    // inner iteration over "our" triangles
                    for (int i = 1; i <= num_tris; ++i) {
                        int ti = index_list[idx + i];
                        if (TriangleFilterF != null && TriangleFilterF(ti) == false)
                            continue;
                        mesh.GetTriVertices(ti, ref tri.V0, ref tri.V1, ref tri.V2);
                        dist.Triangle1 = tri;
                        double dist_sqr = dist.GetSquared();
                        if ( dist_sqr < nearest_sqr ) {
                            nearest_sqr = dist_sqr;
                            nearest_pair = new Index2i(ti, tj);
                        }
                    }
                }

                return;
            }

            // we either descend "our" tree or the other tree
            //   - if we have hit triangles on "our" tree, we have to descend other
            //   - if we hit triangles on "other", we have to descend ours
            //   - otherwise, we alternate at each depth. This produces wider
            //     branching but is significantly faster (~10x) for both hits and misses
            bool bDescendOther = (idx < triangles_end || depth % 2 == 0);
            if (bDescendOther && odx < otherTree.triangles_end)
                bDescendOther = false;      // can't

            if (bDescendOther) {
                // ok we reached triangles on our side but we need to still reach triangles on
                // the other side, so we descend "their" children
                AxisAlignedBox3d bounds = get_boxd(iBox);

                int oChild1 = otherTree.index_list[odx];
                if (oChild1 < 0) {                 // 1 child, descend if nearer than cur min-dist
                    oChild1 = (-oChild1) - 1;
                    AxisAlignedBox3d oChild1Box = otherTree.get_boxd(oChild1, TransformF);
                    if (oChild1Box.DistanceSquared(ref bounds) < nearest_sqr)
                        find_nearest_triangles(iBox, otherTree, TransformF, oChild1, depth + 1, ref nearest_sqr, ref nearest_pair);

                } else {                            // 2 children
                    oChild1 = oChild1 - 1;          
                    int oChild2 = otherTree.index_list[odx + 1] - 1;

                    AxisAlignedBox3d oChild1Box = otherTree.get_boxd(oChild1, TransformF);
                    AxisAlignedBox3d oChild2Box = otherTree.get_boxd(oChild2, TransformF);

                    // descend closer box first
                    double d1Sqr = oChild1Box.DistanceSquared(ref bounds);
                    double d2Sqr = oChild2Box.DistanceSquared(ref bounds);
                    if (d2Sqr < d1Sqr) {
                        if (d2Sqr < nearest_sqr)
                            find_nearest_triangles(iBox, otherTree, TransformF, oChild2, depth + 1, ref nearest_sqr, ref nearest_pair);
                        if (d1Sqr < nearest_sqr)
                            find_nearest_triangles(iBox, otherTree, TransformF, oChild1, depth + 1, ref nearest_sqr, ref nearest_pair);
                    } else {
                        if (d1Sqr < nearest_sqr)
                            find_nearest_triangles(iBox, otherTree, TransformF, oChild1, depth + 1, ref nearest_sqr, ref nearest_pair);
                        if (d2Sqr < nearest_sqr)
                            find_nearest_triangles(iBox, otherTree, TransformF, oChild2, depth + 1, ref nearest_sqr, ref nearest_pair);
                    }

                }

            } else {
                // descend our tree nodes if they intersect w/ current bounds of other tree
                AxisAlignedBox3d oBounds = otherTree.get_boxd(oBox, TransformF);

                int iChild1 = index_list[idx];
                if (iChild1 < 0) {                 // 1 child, descend if nearer than cur min-dist
                    iChild1 = (-iChild1) - 1;
                    if (box_box_distsqr(iChild1, ref oBounds) < nearest_sqr)
                        find_nearest_triangles(iChild1, otherTree, TransformF, oBox, depth + 1, ref nearest_sqr, ref nearest_pair);

                } else {                            // 2 children
                    iChild1 = iChild1 - 1;
                    int iChild2 = index_list[idx + 1] - 1;

                    // descend closer box first
                    double d1Sqr = box_box_distsqr(iChild1, ref oBounds);
                    double d2Sqr = box_box_distsqr(iChild2, ref oBounds);
                    if ( d2Sqr < d1Sqr ) {
                        if ( d2Sqr < nearest_sqr )
                            find_nearest_triangles(iChild2, otherTree, TransformF, oBox, depth + 1, ref nearest_sqr, ref nearest_pair);
                        if ( d1Sqr < nearest_sqr )
                            find_nearest_triangles(iChild1, otherTree, TransformF, oBox, depth + 1, ref nearest_sqr, ref nearest_pair);
                    } else {
                        if (d1Sqr < nearest_sqr)
                            find_nearest_triangles(iChild1, otherTree, TransformF, oBox, depth + 1, ref nearest_sqr, ref nearest_pair);
                        if (d2Sqr < nearest_sqr)
                            find_nearest_triangles(iChild2, otherTree, TransformF, oBox, depth + 1, ref nearest_sqr, ref nearest_pair);
                    }

                }

            }
        }







        /// <summary>
        /// Does this ISpatial support IsInside() test (yes!)
        /// </summary>
        public bool SupportsPointContainment { get { return true; } }

        /// <summary>
        /// Returns true if point p is inside this mesh.
        /// </summary>
        public virtual bool IsInside(Vector3d p)
        {
            // This is a raycast crossing-count test, which is not ideal!
            // Only works for closed meshes.

            //AxisAlignedBox3f bounds = get_box(root_index);
            //Vector3d outside = bounds.Center + 2 * bounds.Diagonal;

            //Vector3d rayDir = Vector3d.AxisX;

            // [RMS] this is just a random direction I picked...
            Vector3d rayDir = new Vector3d(0.331960519038825,0.462531727525156,0.822111072077288);

            //Vector3d rayOrigin = p - 2 * bounds.Width * rayDir;
            Vector3d rayOrigin = p;

            Ray3d ray = new Ray3d(rayOrigin, rayDir);
            int nHits = FindAllHitTriangles(ray, null);

            return (nHits % 2) != 0;
        }







        /// <summary>
        /// Instances of this class can be passed in to the DoTraversal() function to implement your
        /// own tree-traversal queries.
        /// NextBoxF() is called for each box node. Return false from this function to halt terminate 
        /// that branch of the traversal, or true to descend into that box's children (boxes or triangles).
        /// NextTriangleF() is called for each triangle.
        /// </summary>
        public class TreeTraversal
        {
            // return false to terminate this branch
            // arguments are box and depth in tree
            public Func<AxisAlignedBox3f, int, bool> NextBoxF = (box,depth) => { return true; };

            public Action<int> NextTriangleF = (tID) => { };
        }


        /// <summary>
        /// Hierarchically descend through the tree nodes, calling the TreeTrversal functions at each level
        /// </summary>
        public virtual void DoTraversal(TreeTraversal traversal)
        {
            if (mesh_timestamp != mesh.ShapeTimestamp)
                throw new Exception("DMeshAABBTree3.DoTraversal: mesh has been modified since tree construction");

            tree_traversal(root_index, 0, traversal);
        }

        // traversal implementation. you can override to customize this if necessary.
        protected virtual void tree_traversal(int iBox, int depth, TreeTraversal traversal)
        {
            int idx = box_to_index[iBox];

            if ( idx < triangles_end ) {
                // triange-list case, array is [N t1 t2 ... tN]
                int n = index_list[idx];
                for ( int i = 1; i <= n; ++i ) {
                    int ti = index_list[idx + i];
                    if (TriangleFilterF != null && TriangleFilterF(ti) == false)
                        continue;
                    traversal.NextTriangleF(ti);
                }
            } else {
                int i0 = index_list[idx];
                if ( i0 < 0 ) {
                    // negative index means we only have one 'child' box to descend into
                    i0 = (-i0) - 1;
                    if ( traversal.NextBoxF(get_box(i0), depth+1) )
                        tree_traversal(i0, depth+1, traversal);
                } else {
                    // positive index, two sequential child box indices to descend into
                    i0 = i0 - 1;
                    if ( traversal.NextBoxF(get_box(i0), depth+1) )
                        tree_traversal(i0, depth+1, traversal);
                    int i1 = index_list[idx + 1] - 1;
                    if ( traversal.NextBoxF(get_box(i1), depth+1) )
                        tree_traversal(i1, depth+1, traversal);
                }
            }
        }





        /*
         *  Hierarchical Mesh Winding Number computation
         */


        /// <summary>
        /// Evaluate the mesh winding number at point. To do this, we must construct additional
        /// information to short-circuit tree branches. This happens on the first evaluation.
        /// This does consume some additional memory, mainly temporary memory during construction.
        /// (eg on a 500k sphere, about 30mb to construct, but then only 2-5mb is stored at the end)
        /// If you don't want this, just use Mesh.WindingNumber() directly. Also note that if you
        /// are only evaluating a few times, it is not sensible - assume you need at least 
        /// hundreds of evaluations to see speed improvements.
        /// </summary>
        public virtual double WindingNumber(Vector3d p)
        {
            if (mesh_timestamp != mesh.ShapeTimestamp)
                throw new Exception("DMeshAABBTree3.WindingNumber: mesh has been modified since tree construction");

            if (WindingCache == null || winding_cache_timestamp != mesh.ShapeTimestamp) {
                build_winding_cache();
                winding_cache_timestamp = mesh.ShapeTimestamp;
            }

            double sum = branch_winding_num(root_index, p);
            return sum / (4.0 * Math.PI);
        }

        // evaluate winding number contribution for all triangles below iBox
        protected double branch_winding_num(int iBox, Vector3d p)
        {
            Vector3d a = Vector3d.Zero, b = Vector3d.Zero, c = Vector3d.Zero;
            double branch_sum = 0;

            int idx = box_to_index[iBox];
            if (idx < triangles_end) {            // triange-list case, array is [N t1 t2 ... tN]
                int num_tris = index_list[idx];
                for (int i = 1; i <= num_tris; ++i) {
                    int ti = index_list[idx + i];
                    mesh.GetTriVertices(ti, ref a, ref b, ref c);
                    branch_sum += MathUtil.TriSolidAngle(a, b, c, ref p);
                }

            } else {                                // internal node, either 1 or 2 child boxes
                int iChild1 = index_list[idx];
                if (iChild1 < 0) {                 // 1 child, descend if nearer than cur min-dist
                    iChild1 = (-iChild1) - 1;

                    // if we have winding cache, we can more efficiently compute contribution of all triangles
                    // below this box. Otherwise, recursively descend tree.
                    bool contained = box_contains(iChild1, p);
                    if (contained == false && WindingCache.ContainsKey(iChild1))
                        branch_sum += evaluate_box_winding_cache(iChild1, p);
                    else
                        branch_sum += branch_winding_num(iChild1, p);

                } else {                            // 2 children, descend closest first
                    iChild1 = iChild1 - 1;
                    int iChild2 = index_list[idx + 1] - 1;

                    bool contained1 = box_contains(iChild1, p);
                    if (contained1 == false && WindingCache.ContainsKey(iChild1))
                        branch_sum += evaluate_box_winding_cache(iChild1, p);
                    else
                        branch_sum += branch_winding_num(iChild1, p);

                    bool contained2 = box_contains(iChild2, p);
                    if (contained2 == false && WindingCache.ContainsKey(iChild2))
                        branch_sum += evaluate_box_winding_cache(iChild2, p);
                    else
                        branch_sum += branch_winding_num(iChild2, p);
                }
            }

            return branch_sum;
        }


        Dictionary<int, List<int>> WindingCache;
        int winding_cache_timestamp = -1;

        protected void build_winding_cache()
        {
            // The basic strategy to build the winding cache is to descend the tree until we hit a node with N
            // triangles below it, then build a cache for those triangles. We also (currently) build all caches
            // above such a node, because it makes a big speed difference. Changing this threshold does not appear
            // to make a big difference in query speed, but it does affect the build time and memory usage.
            // If the mesh is large, we can use larger caches, but on a small mesh it may result in
            // not actually getting that many caches, which is les compute-efficient. 
            // So, we step up as the threshold the mesh gets larger.
            // [TODO] profile this? would be nice to have a functional relationship, but it is not linear...
            int WINDING_CACHE_THRESH = 100;
            if (Mesh.TriangleCount > 250000)
                WINDING_CACHE_THRESH = 500;
            if (Mesh.TriangleCount > 1000000)
                WINDING_CACHE_THRESH = 1000;

            WindingCache = new Dictionary<int, List<int>>();
            HashSet<int> root_hash;
            build_winding_cache(root_index, 0, WINDING_CACHE_THRESH, out root_hash);

            // [RMS] some debugging info
            //int cache_count = 0;
            //foreach (var value in WindingCache.Values)
            //    cache_count += value.Count;
            //System.Console.WriteLine("total cached kb: {0}  tricount {1} caches {2} boxes {3}  ", cache_count*sizeof(int)/1024, Mesh.TriangleCount, WindingCache.Count, box_centers.size);
        }
        protected int build_winding_cache(int iBox, int depth, int tri_count_thresh, out HashSet<int> tri_hash)
        {
            tri_hash = null;

            int idx = box_to_index[iBox];
            if (idx < triangles_end) {            // triange-list case, array is [N t1 t2 ... tN]
                int num_tris = index_list[idx];
                return num_tris;

            } else {                                // internal node, either 1 or 2 child boxes
                int iChild1 = index_list[idx];
                if (iChild1 < 0) {                 // 1 child, descend if nearer than cur min-dist
                    iChild1 = (-iChild1) - 1;
                    int num_child_tris = build_winding_cache(iChild1, depth+1, tri_count_thresh, out tri_hash);

                    // if count in child is large enough, we already built a cache at lower node
                    return num_child_tris;

                } else {                            // 2 children, descend closest first
                    iChild1 = iChild1 - 1;
                    int iChild2 = index_list[idx + 1] - 1;

                    // let each child build its own cache if it wants. If so, it will return the
                    // list of its child tris
                    HashSet<int> child2_hash;
                    int num_tris_1 = build_winding_cache(iChild1, depth+1, tri_count_thresh, out tri_hash);
                    int num_tris_2 = build_winding_cache(iChild2, depth+1, tri_count_thresh, out child2_hash);
                    bool build_cache = (num_tris_1 + num_tris_2 > tri_count_thresh);

                    if (depth == 0)
                        return num_tris_1 + num_tris_2;  // cannot build cache at level 0...

                    // collect up the triangles we need. there are various cases depending on what children already did
                    if ( tri_hash != null || child2_hash != null || build_cache ) {
                        if ( tri_hash == null && child2_hash != null ) {
                            collect_triangles(iChild1, child2_hash);
                            tri_hash = child2_hash;
                        } else {
                            if (tri_hash == null) {
                                tri_hash = new HashSet<int>();
                                collect_triangles(iChild1, tri_hash);
                            }
                            if (child2_hash == null)
                                collect_triangles(iChild2, tri_hash);
                            else
                                tri_hash.UnionWith(child2_hash);
                        }
                    }
                    if ( build_cache )
                        make_box_winding_cache(iBox, tri_hash);

                    return (num_tris_1 + num_tris_2);
                }
            }
        }

        /// collect all triangles under iBox, find open edges [a,b],
        /// and add them all to a list associated with iBox
        protected void make_box_winding_cache(int iBox, HashSet<int> triangles)
        {
            Util.gDevAssert(WindingCache.ContainsKey(iBox) == false);

            List<int> edges = new List<int>();
            foreach ( int tid in triangles ) {
                Index3i tri = Mesh.GetTriangle(tid);
                Index3i nbr_tris = Mesh.GetTriNeighbourTris(tid);
                for ( int j = 0; j < 3; ++j) {
                    if ( nbr_tris[j] == DMesh3.InvalidID || triangles.Contains(nbr_tris[j]) == false ) {
                        edges.Add(tri[(j+1) % 3]);
                        edges.Add(tri[j]);
                    }
                }
            }
            WindingCache[iBox] = edges;
        }

        // evaluate the winding cache for iBox
        protected double evaluate_box_winding_cache(int iBox, Vector3d p)
        {
            List<int> boxcache = WindingCache[iBox];
            int N = boxcache.Count / 2;
            // evaluate winding calc over arbitrary triangle fan that "closes" 
            // the open mesh below this box. 
            Vector3d c = box_centers[iBox];
            double cluster_sum = 0;
            for (int i = 0; i < N; ++i) {
                Vector3d a = Mesh.GetVertex(boxcache[2 * i]);
                Vector3d b = Mesh.GetVertex(boxcache[2 * i + 1]);
                cluster_sum += MathUtil.TriSolidAngle(a, b, c, ref p);
            }
            // contribution of open mesh is -sum over fan
            return -cluster_sum;
        }


        // collect all the triangles below iBox in a hash
        protected void collect_triangles(int iBox, HashSet<int> triangles)
        {
            int idx = box_to_index[iBox];
            if (idx < triangles_end) {            // triange-list case, array is [N t1 t2 ... tN]
                int num_tris = index_list[idx];
                for (int i = 1; i <= num_tris; ++i)
                    triangles.Add(index_list[idx + i]);
            } else {
                int iChild1 = index_list[idx];
                if (iChild1 < 0) {                 // 1 child, descend if nearer than cur min-dist
                    collect_triangles((-iChild1) - 1, triangles);
                } else {                           // 2 children, descend closest first
                    collect_triangles(iChild1 - 1, triangles);
                    collect_triangles(index_list[idx + 1] - 1, triangles);
                }
            }
        }









        /*
          *  Fast Mesh Winding Number computation
          */

        /// <summary>
        /// FWN beta parameter - is 2.0 in paper
        /// </summary>
        public double FWNBeta = 2.0;

        /// <summary>
        /// FWN approximation order. can be 1 or 2. 2 is more accurate, obviously.
        /// </summary>
        public int FWNApproxOrder = 2;


        /// <summary>
        /// Fast approximation of winding number using far-field approximations
        /// </summary>
        public virtual double FastWindingNumber(Vector3d p)
        {
            if (mesh_timestamp != mesh.ShapeTimestamp)
                throw new Exception("DMeshAABBTree3.FastWindingNumber: mesh has been modified since tree construction");

            if (FastWindingCache == null || fast_winding_cache_timestamp != mesh.ShapeTimestamp) {
                build_fast_winding_cache();
                fast_winding_cache_timestamp = mesh.ShapeTimestamp;
            }

            double sum = branch_fast_winding_num(root_index, p);
            return sum;
        }

        // evaluate winding number contribution for all triangles below iBox
        protected double branch_fast_winding_num(int iBox, Vector3d p)
        {
            Vector3d a = Vector3d.Zero, b = Vector3d.Zero, c = Vector3d.Zero;
            double branch_sum = 0;

            int idx = box_to_index[iBox];
            if (idx < triangles_end) {            // triange-list case, array is [N t1 t2 ... tN]
                int num_tris = index_list[idx];
                for (int i = 1; i <= num_tris; ++i) {
                    int ti = index_list[idx + i];
                    mesh.GetTriVertices(ti, ref a, ref b, ref c);
                    branch_sum += MathUtil.TriSolidAngle(a, b, c, ref p) / MathUtil.FourPI;
                }

            } else {                                // internal node, either 1 or 2 child boxes
                int iChild1 = index_list[idx];
                if (iChild1 < 0) {                 // 1 child, descend if nearer than cur min-dist
                    iChild1 = (-iChild1) - 1;

                    // if we have winding cache, we can more efficiently compute contribution of all triangles
                    // below this box. Otherwise, recursively descend tree.
                    bool contained = box_contains(iChild1, p);
                    if (contained == false && can_use_fast_winding_cache(iChild1, ref p))
                        branch_sum += evaluate_box_fast_winding_cache(iChild1, ref p);
                    else
                        branch_sum += branch_fast_winding_num(iChild1, p);

                } else {                            // 2 children, descend closest first
                    iChild1 = iChild1 - 1;
                    int iChild2 = index_list[idx + 1] - 1;

                    bool contained1 = box_contains(iChild1, p);
                    if (contained1 == false && can_use_fast_winding_cache(iChild1, ref p))
                        branch_sum += evaluate_box_fast_winding_cache(iChild1, ref p);
                    else
                        branch_sum += branch_fast_winding_num(iChild1, p);

                    bool contained2 = box_contains(iChild2, p);
                    if (contained2 == false && can_use_fast_winding_cache(iChild2, ref p))
                        branch_sum += evaluate_box_fast_winding_cache(iChild2, ref p);
                    else
                        branch_sum += branch_fast_winding_num(iChild2, p);
                }
            }

            return branch_sum;
        }


        struct FWNInfo
        {
            public Vector3d Center;
            public double R;
            public Vector3d Order1Vec;
            public Matrix3d Order2Mat;
        }

        Dictionary<int, FWNInfo> FastWindingCache;
        int fast_winding_cache_timestamp = -1;

        protected void build_fast_winding_cache()
        {
            // set this to a larger number to ignore caches if number of triangles is too small.
            // (seems to be no benefit to doing this...is holdover from tree-decomposition FWN code)
            int WINDING_CACHE_THRESH = 1;

            //MeshTriInfoCache triCache = null;
            MeshTriInfoCache triCache = new MeshTriInfoCache(mesh);

            FastWindingCache = new Dictionary<int, FWNInfo>();
            HashSet<int> root_hash;
            build_fast_winding_cache(root_index, 0, WINDING_CACHE_THRESH, out root_hash, triCache);
        }
        protected int build_fast_winding_cache(int iBox, int depth, int tri_count_thresh, out HashSet<int> tri_hash, MeshTriInfoCache triCache)
        {
            tri_hash = null;

            int idx = box_to_index[iBox];
            if (idx < triangles_end) {            // triange-list case, array is [N t1 t2 ... tN]
                int num_tris = index_list[idx];
                return num_tris;

            } else {                                // internal node, either 1 or 2 child boxes
                int iChild1 = index_list[idx];
                if (iChild1 < 0) {                 // 1 child, descend if nearer than cur min-dist
                    iChild1 = (-iChild1) - 1;
                    int num_child_tris = build_fast_winding_cache(iChild1, depth + 1, tri_count_thresh, out tri_hash, triCache);

                    // if count in child is large enough, we already built a cache at lower node
                    return num_child_tris;

                } else {                            // 2 children, descend closest first
                    iChild1 = iChild1 - 1;
                    int iChild2 = index_list[idx + 1] - 1;

                    // let each child build its own cache if it wants. If so, it will return the
                    // list of its child tris
                    HashSet<int> child2_hash;
                    int num_tris_1 = build_fast_winding_cache(iChild1, depth + 1, tri_count_thresh, out tri_hash, triCache);
                    int num_tris_2 = build_fast_winding_cache(iChild2, depth + 1, tri_count_thresh, out child2_hash, triCache);
                    bool build_cache = (num_tris_1 + num_tris_2 > tri_count_thresh);

                    if (depth == 0)
                        return num_tris_1 + num_tris_2;  // cannot build cache at level 0...

                    // collect up the triangles we need. there are various cases depending on what children already did
                    if (tri_hash != null || child2_hash != null || build_cache) {
                        if (tri_hash == null && child2_hash != null) {
                            collect_triangles(iChild1, child2_hash);
                            tri_hash = child2_hash;
                        } else {
                            if (tri_hash == null) {
                                tri_hash = new HashSet<int>();
                                collect_triangles(iChild1, tri_hash);
                            }
                            if (child2_hash == null)
                                collect_triangles(iChild2, tri_hash);
                            else
                                tri_hash.UnionWith(child2_hash);
                        }
                    }
                    if (build_cache)
                        make_box_fast_winding_cache(iBox, tri_hash, triCache);

                    return (num_tris_1 + num_tris_2);
                }
            }
        }


        // check if we can use fwn 
        protected bool can_use_fast_winding_cache(int iBox, ref Vector3d q)
        {
            FWNInfo cacheInfo;
            if (FastWindingCache.TryGetValue(iBox, out cacheInfo) == false)
                return false;

            double dist_qp = cacheInfo.Center.Distance(ref q);
            if (dist_qp > FWNBeta * cacheInfo.R)
                return true;

            return false;
        }


        // compute FWN cache for all triangles underneath this box
        protected void make_box_fast_winding_cache(int iBox, IEnumerable<int> triangles, MeshTriInfoCache triCache)
        {
            Util.gDevAssert(FastWindingCache.ContainsKey(iBox) == false);

            // construct cache
            FWNInfo cacheInfo = new FWNInfo();
            FastTriWinding.ComputeCoeffs(Mesh, triangles, ref cacheInfo.Center, ref cacheInfo.R, ref cacheInfo.Order1Vec, ref cacheInfo.Order2Mat, triCache);

            FastWindingCache[iBox] = cacheInfo;
        }

        // evaluate the FWN cache for iBox
        protected double evaluate_box_fast_winding_cache(int iBox, ref Vector3d q)
        {
            FWNInfo cacheInfo = FastWindingCache[iBox];

            if (FWNApproxOrder == 2)
                return FastTriWinding.EvaluateOrder2Approx(ref cacheInfo.Center, ref cacheInfo.Order1Vec, ref cacheInfo.Order2Mat, ref q);
            else
                return FastTriWinding.EvaluateOrder1Approx(ref cacheInfo.Center, ref cacheInfo.Order1Vec, ref q);
        }










        /// <summary>
        /// Total sum of volumes of all boxes in the tree. Mainly useful to evaluate tree quality.
        /// </summary>
        public double TotalVolume()
        {
            double volSum = 0;
            TreeTraversal t = new TreeTraversal() {
                NextBoxF = (box, depth) => {
                    volSum += box.Volume;
                    return true;
                }
            };
            DoTraversal(t);
            return volSum;
        }

        /// <summary>
        /// Total sum-of-extents over all boxes in the tree. Mainly useful to evaluate tree quality.
        /// </summary>
        public double TotalExtentSum()
        {
            double extSum = 0;
            TreeTraversal t = new TreeTraversal() {
                NextBoxF = (box, depth) => {
                    extSum += box.Extents.LengthL1;
                    return true;
                }
            };
            DoTraversal(t);
            return extSum;
        }


        /// <summary>
        /// Root bounding box of tree (note: tree must be generated by calling a query function first!)
        /// </summary>
        public AxisAlignedBox3d Bounds {
            get { return get_box(root_index); }
        }



        //
        // Internals - data structures, construction, etc
        //




        // storage for box nodes. 
        //   - box_to_index is a pointer into index_list
        //   - box_centers and box_extents are the centers/extents of the bounding boxes
        protected DVector<int> box_to_index;
        protected DVector<Vector3f> box_centers;
        protected DVector<Vector3f> box_extents;

        // list of indices for a given box. There is *no* marker/sentinel between
        // boxes, you have to get the starting index from box_to_index[]
        //
        // There are three kinds of records:
        //   - if i < triangles_end, then the list is a number of triangles,
        //       stored as [N t1 t2 t3 ... tN]
        //   - if i > triangles_end and index_list[i] < 0, this is a single-child
        //       internal box, with index (-index_list[i])-1     (shift-by-one in case actual value is 0!)
        //   - if i > triangles_end and index_list[i] > 0, this is a two-child
        //       internal box, with indices index_list[i]-1 and index_list[i+1]-1
        protected DVector<int> index_list;

        // index_list[i] for i < triangles_end is a triangle-index list, otherwise box-index pair/single
        protected int triangles_end = -1;

        // box_to_index[root_index] is the root node of the tree
        protected int root_index = -1;








        void build_top_down(bool bSorted)
        {
            // build list of valid triangles & centers. We skip any
            // triangles that have infinite/garbage vertices...
            int i = 0;
            int[] triangles = new int[mesh.TriangleCount];
            Vector3d[] centers = new Vector3d[mesh.TriangleCount];
            foreach ( int ti in mesh.TriangleIndices()) {
                Vector3d centroid = mesh.GetTriCentroid(ti);
                double d2 = centroid.LengthSquared;
                bool bInvalid = double.IsNaN(d2) || double.IsInfinity(d2);
                Debug.Assert(bInvalid == false);
                if (bInvalid == false) {
                    triangles[i] = ti;
                    centers[i] = mesh.GetTriCentroid(ti);
                    i++;
                } // otherwise skip this tri
            }

            boxes_set tris = new boxes_set();
            boxes_set nodes = new boxes_set();
            AxisAlignedBox3f rootBox;
            int rootnode = (bSorted) ?
                split_tri_set_sorted(triangles, centers, 0, mesh.TriangleCount, 0, TopDownLeafMaxTriCount, tris, nodes, out rootBox)
                : split_tri_set_midpoint(triangles, centers, 0, mesh.TriangleCount, 0, TopDownLeafMaxTriCount, tris, nodes, out rootBox);

            box_to_index = tris.box_to_index;
            box_centers = tris.box_centers;
            box_extents = tris.box_extents;
            index_list = tris.index_list;
            triangles_end = tris.iIndicesCur;
            int iIndexShift = triangles_end;
            int iBoxShift = tris.iBoxCur;

            // ok now append internal node boxes & index ptrs
            for ( i = 0; i < nodes.iBoxCur; ++i ) {
                box_centers.insert(nodes.box_centers[i], iBoxShift + i);
                box_extents.insert(nodes.box_extents[i], iBoxShift + i);
                // internal node indices are shifted
                box_to_index.insert(iIndexShift + nodes.box_to_index[i], iBoxShift + i);
            }

            // now append index list
            for ( i = 0; i < nodes.iIndicesCur; ++i ) {
                int child_box = nodes.index_list[i];
                if ( child_box < 0 ) {        // this is a triangles box
                    child_box = (-child_box) - 1;
                } else {
                    child_box += iBoxShift;
                }
                child_box = child_box + 1;
                index_list.insert(child_box, iIndexShift + i);
            }

            root_index = rootnode + iBoxShift;
        }



        class AxisComp : IComparer<Vector3d>
        {
            public int Axis = 0;
            // Compares by Height, Length, and Width.
            public int Compare(Vector3d a, Vector3d b) {
                return a[Axis].CompareTo(b[Axis]);
            }
        }

        // returns box id

        class boxes_set
        {
            public DVector<int> box_to_index = new DVector<int>();
            public DVector<Vector3f> box_centers = new DVector<Vector3f>();
            public DVector<Vector3f> box_extents = new DVector<Vector3f>();
            public DVector<int> index_list = new DVector<int>();
            public int iBoxCur = 0;
            public int iIndicesCur = 0;
        }

        int split_tri_set_sorted(int[] triangles, Vector3d[] centers, int iStart, int iCount, int depth, int minTriCount,
            boxes_set tris, boxes_set nodes, out AxisAlignedBox3f box)
        {
            box = AxisAlignedBox3f.Empty;
            int iBox = -1;

            if ( iCount < minTriCount ) {
                // append new triangles box
                iBox = tris.iBoxCur++;
                tris.box_to_index.insert(tris.iIndicesCur, iBox);

                tris.index_list.insert(iCount, tris.iIndicesCur++);
                for (int i = 0; i < iCount; ++i) {
                    tris.index_list.insert(triangles[iStart+i], tris.iIndicesCur++);
                    box.Contain(mesh.GetTriBounds(triangles[iStart + i]));
                }

                tris.box_centers.insert(box.Center, iBox);
                tris.box_extents.insert(box.Extents, iBox);
                
                return -(iBox+1);
            }

            AxisComp c = new AxisComp() { Axis = depth % 3 };
            Array.Sort(centers, triangles, iStart, iCount, c);
            int mid = iCount / 2;
            int n0 = mid;
            int n1 = iCount - mid;

            // create child boxes
            AxisAlignedBox3f box1;
            int child0 = split_tri_set_sorted(triangles, centers, iStart, n0, depth + 1, minTriCount, tris, nodes, out box);
            int child1 = split_tri_set_sorted(triangles, centers, iStart+mid, n1, depth + 1, minTriCount, tris, nodes, out box1);
            box.Contain(box1);

            // append new box
            iBox = nodes.iBoxCur++;
            nodes.box_to_index.insert(nodes.iIndicesCur, iBox);

            nodes.index_list.insert(child0, nodes.iIndicesCur++);
            nodes.index_list.insert(child1, nodes.iIndicesCur++);

            nodes.box_centers.insert(box.Center, iBox);
            nodes.box_extents.insert(box.Extents, iBox);

            return iBox;
        }








        int split_tri_set_midpoint(int[] triangles, Vector3d[] centers, int iStart, int iCount, int depth, int minTriCount,
            boxes_set tris, boxes_set nodes, out AxisAlignedBox3f box)
        {
            box = AxisAlignedBox3f.Empty;
            int iBox = -1;

            if ( iCount < minTriCount ) {
                // append new triangles box
                iBox = tris.iBoxCur++;
                tris.box_to_index.insert(tris.iIndicesCur, iBox);

                tris.index_list.insert(iCount, tris.iIndicesCur++);
                for (int i = 0; i < iCount; ++i) {
                    tris.index_list.insert(triangles[iStart+i], tris.iIndicesCur++);
                    box.Contain(mesh.GetTriBounds(triangles[iStart + i]));
                }

                tris.box_centers.insert(box.Center, iBox);
                tris.box_extents.insert(box.Extents, iBox);
                
                return -(iBox+1);
            }

            //compute interval along an axis and find midpoint
            int axis = depth % 3;
            Interval1d interval = Interval1d.Empty;
            for ( int i = 0; i < iCount; ++i ) 
                interval.Contain(centers[iStart + i][axis]);
            double midpoint = interval.Center;

            int n0, n1;
            if (Math.Abs(interval.a - interval.b) > MathUtil.ZeroTolerance) {
                // we have to re-sort the centers & triangles lists so that centers < midpoint
                // are first, so that we can recurse on the two subsets. We walk in from each side,
                // until we find two out-of-order locations, then we swap them.
                int l = 0;
                int r = iCount - 1;
                while (l < r) {
                    // [RMS] is <= right here? if v.axis == midpoint, then this loop
                    //   can get stuck unless one of these has an equality test. But
                    //   I did not think enough about if this is the right thing to do...
                    while (centers[iStart + l][axis] <= midpoint)
                        l++;
                    while (centers[iStart + r][axis] > midpoint)
                        r--;
                    if (l >= r)
                        break;      //done!
                    //swap
                    Vector3d tmpc = centers[iStart + l]; centers[iStart + l] = centers[iStart + r];  centers[iStart + r] = tmpc;
                    int tmpt = triangles[iStart + l]; triangles[iStart + l] = triangles[iStart + r]; triangles[iStart + r] = tmpt;
                }

                n0 = l;
                n1 = iCount - n0;
                Debug.Assert(n0 >= 1 && n1 >= 1);
            } else {
                // interval is near-empty, so no point trying to do sorting, just split half and half
                n0 = iCount / 2;
                n1 = iCount - n0;
            }

            // create child boxes
            AxisAlignedBox3f box1;
            int child0 = split_tri_set_midpoint(triangles, centers, iStart, n0, depth + 1, minTriCount, tris, nodes, out box);
            int child1 = split_tri_set_midpoint(triangles, centers, iStart+n0, n1, depth + 1, minTriCount, tris, nodes, out box1);
            box.Contain(box1);

            // append new box
            iBox = nodes.iBoxCur++;
            nodes.box_to_index.insert(nodes.iIndicesCur, iBox);

            nodes.index_list.insert(child0, nodes.iIndicesCur++);
            nodes.index_list.insert(child1, nodes.iIndicesCur++);

            nodes.box_centers.insert(box.Center, iBox);
            nodes.box_extents.insert(box.Extents, iBox);

            return iBox;
        }









        // strategy here is:
        //  1) partition triangles by vertex one-rings into leaf boxes
        //      1a) first pass where we skip one-rings that have < 3 free tris
        //      1b) second pass where we handle any missed tris
        //  2) sequentially combine N leaf boxes into (N/2 + N%2) layer 2 boxes
        //  3) repeat until layer K has only 1 box, which is root of tree
        void build_by_one_rings(ClusterPolicy ePolicy)
        {
            box_to_index = new DVector<int>();
            box_centers = new DVector<Vector3f>();
            box_extents = new DVector<Vector3f>();
            int iBoxCur = 0;

            index_list = new DVector<int>();
            int iIndicesCur = 0;

            // replace w/ BitArray ?
            byte[] used_triangles = new byte[mesh.MaxTriangleID];
            Array.Clear(used_triangles, 0, used_triangles.Length);

            // temporary buffer
            int nMaxEdgeCount = mesh.GetMaxVtxEdgeCount();
            int[] temp_tris = new int[2*nMaxEdgeCount];

            // first pass: cluster by one-ring, but if # of free tris
            //  in a ring is small (< 3), push onto spill list to try again,
            //  because those tris might be picked up by a bigger cluster
            DVector<int> spill = new DVector<int>();
            foreach ( int vid in mesh.VertexIndices() ) {
                int tCount = add_one_ring_box(vid, used_triangles, temp_tris,
                    ref iBoxCur, ref iIndicesCur, spill, 3);
                if (tCount < 3)
                    spill.Add(vid);
            }

            // second pass: check any spill vertices. Most are probably gone 
            // now, but a few stray triangles might still exist
            int N = spill.Length;
            for ( int si = 0; si < N; ++si ) {
                int vid = spill[si];
                add_one_ring_box(vid, used_triangles, temp_tris,
                    ref iBoxCur, ref iIndicesCur, null, 0);
            }


            // [RMS] test code to make sure each triangle is in exactly one list
            //foreach ( int tid in mesh.TriangleIndices() ) {
            //    int n = used_triangles[tid];
            //    if (n != 1)
            //        Util.gBreakToDebugger();
            //}

            // keep track of where triangle lists end
            triangles_end = iIndicesCur;

            // this defines ClusterPolicy.Default
            //ClusterFunctionType clusterF = cluster_boxes;
            //ClusterFunctionType clusterF = cluster_boxes_matrix;
            ClusterFunctionType clusterF = cluster_boxes_nearsearch;

            if (ePolicy == ClusterPolicy.Fastest)
                clusterF = cluster_boxes;
            else if (ePolicy == ClusterPolicy.MinimalVolume)
                clusterF = cluster_boxes_matrix;
            else if (ePolicy == ClusterPolicy.FastVolumeMetric)
                clusterF = cluster_boxes_nearsearch;

            // ok, now repeatedly cluster current layer of N boxes into N/2 + N%2 boxes,
            // until we hit a 1-box layer, which is root of the tree
            int nPrevEnd = iBoxCur;
            int nLayerSize = clusterF(0, iBoxCur, ref iBoxCur, ref iIndicesCur);
            int iStart = nPrevEnd;
            int iCount = iBoxCur - nPrevEnd;
            while ( nLayerSize > 1 ) {
                nPrevEnd = iBoxCur;
                nLayerSize = clusterF(iStart, iCount, ref iBoxCur, ref iIndicesCur);
                iStart = nPrevEnd;
                iCount = iBoxCur - nPrevEnd;
            }

            root_index = iBoxCur - 1;
        }



        // Appends a box that contains free triangles in one-ring of vertex vid.
        // If tri count is < spill threshold, push onto spill list instead.
        // Returns # of free tris found.
        int add_one_ring_box(int vid, byte[] used_triangles, int[] temp_tris, 
            ref int iBoxCur, ref int iIndicesCur,
            DVector<int> spill, int nSpillThresh )
        {
            // collect free triangles
            int num_free = 0;
            foreach ( int tid in mesh.VtxTrianglesItr(vid) ) {
                if ( used_triangles[tid] == 0 ) 
                    temp_tris[num_free++] = tid;
            }

            // none free, get out
            if (num_free == 0)
                return 0;

            // if we only had a couple free triangles, wait and see if
            // they get picked up by another vert
            if (num_free < nSpillThresh) {
                spill.Add(vid);
                return num_free;
            }

            // append new box
            AxisAlignedBox3f box = AxisAlignedBox3f.Empty;
            int iBox = iBoxCur++;
            box_to_index.insert(iIndicesCur, iBox);

            index_list.insert(num_free, iIndicesCur++);
            for (int i = 0; i < num_free; ++i) {
                index_list.insert(temp_tris[i], iIndicesCur++);
                used_triangles[temp_tris[i]]++;     // incrementing for sanity check below, just need to set to 1
                box.Contain(mesh.GetTriBounds(temp_tris[i]));
            }

            box_centers.insert(box.Center, iBox);
            box_extents.insert(box.Extents, iBox);
            return num_free;
        }


        public delegate int ClusterFunctionType(int iStart, int iCount, ref int iBoxCur, ref int iIndicesCur);

        // Turn a span of N boxes into N/2 boxes, by pairing boxes
        // Except, of course, if N is odd, then we get N/2+1, where the +1
        // box has a single child box (ie just a copy).
        // [TODO] instead merge that extra box into on of parents? Reduces tree depth by 1
        int cluster_boxes(int iStart, int iCount, ref int iBoxCur, ref int iIndicesCur)
        {
            int[] indices = new int[iCount];
            for (int i = 0; i < iCount; ++i)
                indices[i] = iStart + i;

            int nDim = 0;
            Array.Sort(indices, (a, b) => {
                float axis_min_a = box_centers[a][nDim] - box_extents[a][nDim];
                float axis_min_b = box_centers[b][nDim] - box_extents[b][nDim];
                return (axis_min_a == axis_min_b) ? 0 :
                            (axis_min_a < axis_min_b) ? -1 : 1;
            });

            int nPairs = iCount / 2;
            int nLeft = iCount - 2 * nPairs;

            // this is dumb! but lets us test the rest...
            for ( int pi = 0; pi < nPairs; pi++ ) {
                int i0 = indices[2*pi];
                int i1 = indices[2*pi + 1];

                Vector3f center, extent;
                get_combined_box(i0, i1, out center, out extent);

                // append new box
                int iBox = iBoxCur++;
                box_to_index.insert(iIndicesCur, iBox);

                index_list.insert(i0+1, iIndicesCur++);
                index_list.insert(i1+1, iIndicesCur++);

                box_centers.insert(center, iBox);
                box_extents.insert(extent, iBox);
            }

            // [todo] could we merge with last other box? need a way to tell
            //   that there are 3 children though...could use negative index for that?
            if ( nLeft > 0 ) {
                if (nLeft > 1)
                    Util.gBreakToDebugger();
                int iLeft = indices[2*nPairs];
                duplicate_box(iLeft, ref iBoxCur, ref iIndicesCur);
            }

            return nPairs + nLeft;
        }






        // Turn a span of N boxes into N/2 boxes, by pairing boxes
        // Except, of course, if N is odd, then we get N/2+1, where the +1
        // box has a single child box (ie just a copy).
        // [TODO] instead merge that extra box into on of parents? Reduces tree depth by 1
        int cluster_boxes_nearsearch(int iStart, int iCount, ref int iBoxCur, ref int iIndicesCur)
        {
            int[] indices = new int[iCount];
            for (int i = 0; i < iCount; ++i)
                indices[i] = iStart + i;

            Func<int, int, double> boxMetric = combined_box_volume;
            //Func<int, int, double> boxMetric = combined_box_length;

            // sort indices by x axis
            // cycling axes (ie at each depth) seems to produce much worse results...
            int nDim = 0;
            Array.Sort(indices, (a, b) => {
                float axis_min_a = box_centers[a][nDim] - box_extents[a][nDim];
                float axis_min_b = box_centers[b][nDim] - box_extents[b][nDim];
                return (axis_min_a == axis_min_b) ? 0 :
                            (axis_min_a < axis_min_b) ? -1 : 1;
            });

            int nPairs = iCount / 2;
            int nLeft = iCount - 2 * nPairs;

            // bounded greedy clustering. 
            // Search ahead next N boxes in sorted-by-axis list, and find
            // the one that creates minimal box metric when combined with us.
            int N = BottomUpClusterLookahead;
            int[] nextNi = new int[N];
            double[] nextNc = new double[N];
            int pj;
            for ( int pi = 0; pi < iCount-1; pi++ ) {
                int i0 = indices[pi];
                if (i0 < 0)
                    continue;
                int nStop = Math.Min(N, iCount - pi - 1);
                for ( int k = 0; k < nStop; ++k ) {
                    pj = pi + k + 1;
                    nextNi[k] = pj;
                    int ik = indices[pj];
                    if (ik < 0)
                        nextNc[k] = double.MaxValue;
                    else
                        nextNc[k] =  boxMetric(i0, ik);
                }
                Array.Sort(nextNc, nextNi, 0, nStop);
                if (nextNc[0] == double.MaxValue)
                    continue;

                pj = nextNi[0];
                int i1 = indices[pj];
                if (i1 < 0)
                    Util.gBreakToDebugger();

                Vector3f center, extent;
                get_combined_box(i0, i1, out center, out extent);

                // append new box
                int iBox = iBoxCur++;
                box_to_index.insert(iIndicesCur, iBox);

                index_list.insert(i0+1, iIndicesCur++);
                index_list.insert(i1+1, iIndicesCur++);

                box_centers.insert(center, iBox);
                box_extents.insert(extent, iBox);

                indices[pi] = -(indices[pi]+1);
                indices[pj] = -(indices[pj]+1);
            }

            // [todo] could we merge with last other box? need a way to tell
            //   that there are 3 children though...could use negative index for that?
            if (nLeft > 0) {
                int iLeft = -1;
                for (int i = 0; iLeft < 0 && i < indices.Length; ++i)
                    if (indices[i] >= 0)
                        iLeft = indices[i];
                duplicate_box(iLeft, ref iBoxCur, ref iIndicesCur);
            }

            return nPairs + nLeft;
        }








        static double find_smallest_upper(double[,] m, ref int ii, ref int jj)
        {
            double v = double.MaxValue;
            int rows = m.GetLength(0);
            int cols = m.GetLength(1);
            for (int i = 0; i < rows; ++i) {
                for (int j = i+1; j < cols; ++j) {
                    if ( m[i,j] < v ) {
                        v = m[i, j];
                        ii = i;
                        jj = j;
                    }
                }
            }
            return v;
        }


        // greedy-optimal clustering of boxes. Compute all-pairs distance matrix, and 
        // then incrementally pull out minimal pairs. This does a great job but it
        // gets insanely slow because the all-pairs matrix takes too long...
        //   (actually the real cost is probably find_smallest_upper_matrix, which maybe
        //    we could avoid by sorting rows? not sure...)
        int cluster_boxes_matrix(int iStart, int iCount, ref int iBoxCur, ref int iIndicesCur)
        {
            int[] indices = new int[iCount];
            for (int i = 0; i < iCount; ++i)
                indices[i] = iStart + i;

            Func<int, int, double> boxMetric = combined_box_volume;

            double[,] matrix = new double[iCount, iCount];
            for (int i = 0; i < iCount; ++i) {
                for (int j = 0; j <= i; ++j)
                    matrix[i, j] = double.MaxValue;
                for (int j = i + 1; j < iCount; ++j) {
                    matrix[i, j] = boxMetric(indices[i], indices[j]);
                }
            }

            int nPairs = iCount / 2;
            int nLeft = iCount - 2 * nPairs;

            for (int k = 0; k < nPairs; ++k ) {
                int si = 0, sj = 0;
                bool bFound = false;
                while (!bFound) {
                    /*double s = */find_smallest_upper(matrix, ref si, ref sj);
                    if (indices[si] >= 0 && indices[sj] >= 0)
                        bFound = true;
                    matrix[si, sj] = double.MaxValue;
                }

                int i0 = indices[si];
                int i1 = indices[sj];

                Vector3f center, extent;
                get_combined_box(i0, i1, out center, out extent);

                // append new box
                int iBox = iBoxCur++;
                box_to_index.insert(iIndicesCur, iBox);

                index_list.insert(i0 + 1, iIndicesCur++);
                index_list.insert(i1 + 1, iIndicesCur++);

                box_centers.insert(center, iBox);
                box_extents.insert(extent, iBox);

                indices[si] = -(indices[si]+1);
                indices[sj] = -(indices[sj]+1);
            }

            // [todo] could we merge with last other box? need a way to tell
            //   that there are 3 children though...could use negative index for that?
            if (nLeft > 0) {
                int iLeft = -1;
                for (int i = 0; iLeft < 0 && i < indices.Length; ++i)
                    if (indices[i] >= 0)
                        iLeft = indices[i];
                duplicate_box(iLeft, ref iBoxCur, ref iIndicesCur);
            }

            return nPairs + nLeft;
        }






        void duplicate_box(int i, ref int iBoxCur, ref int iIndicesCur)
        {
            // duplicate box at this level... ?
            int iBox = iBoxCur++;
            box_to_index.insert(iIndicesCur, iBox);

            // negative index means only one child
            index_list.insert(-(i+1), iIndicesCur++);
                
            box_centers.insert(box_centers[i], iBox);
            box_extents.insert(box_extents[i], iBox);
        }




        // construct box that contains two boxes
        void get_combined_box(int b0, int b1, out Vector3f center, out Vector3f extent)
        {
            Vector3f c0 = box_centers[b0];
            Vector3f e0 = box_extents[b0];
            Vector3f c1 = box_centers[b1];
            Vector3f e1 = box_extents[b1];

            float minx = Math.Min(c0.x - e0.x, c1.x - e1.x);
            float maxx = Math.Max(c0.x + e0.x, c1.x + e1.x);
            float miny = Math.Min(c0.y - e0.y, c1.y - e1.y);
            float maxy = Math.Max(c0.y + e0.y, c1.y + e1.y);
            float minz = Math.Min(c0.z - e0.z, c1.z - e1.z);
            float maxz = Math.Max(c0.z + e0.z, c1.z + e1.z);

            center = new Vector3f(0.5f * (minx + maxx), 0.5f * (miny + maxy), 0.5f * (minz + maxz));
            extent = new Vector3f(0.5f * (maxx - minx), 0.5f * (maxy - miny), 0.5f * (maxz - minz));
        }


        const float box_eps = 50.0f * MathUtil.Epsilonf;

        AxisAlignedBox3f get_box(int iBox)
        {
            Vector3f c = box_centers[iBox];
            Vector3f e = box_extents[iBox];
            return new AxisAlignedBox3f(ref c, e.x + box_eps, e.y + box_eps, e.z + box_eps);
        }
        AxisAlignedBox3d get_boxd(int iBox)
        {
            Vector3d c = (Vector3d)box_centers[iBox];
            Vector3f e = box_extents[iBox];
            return new AxisAlignedBox3d(ref c, e.x + box_eps, e.y + box_eps, e.z + box_eps);
        }
        AxisAlignedBox3d get_boxd(int iBox, Func<Vector3d, Vector3d> TransformF)
        {
            if (TransformF != null) {
                AxisAlignedBox3d box = get_boxd(iBox);
                return BoundsUtil.Bounds(ref box, TransformF);
            } else {
                return get_boxd(iBox);
            }
        }

        double box_ray_intersect_t(int iBox, Ray3d ray)
        {
            Vector3d c = (Vector3d)box_centers[iBox];
            Vector3f e = box_extents[iBox];
            AxisAlignedBox3d box = new AxisAlignedBox3d(ref c, e.x + box_eps, e.y + box_eps, e.z + box_eps);

            double ray_t = double.MaxValue;
            if (IntrRay3AxisAlignedBox3.FindRayIntersectT(ref ray, ref box, out ray_t)) {
                return ray_t;
            } else {
                return double.MaxValue;
            }
        }

        bool box_box_intersect(int iBox, ref AxisAlignedBox3d testBox)
        {
            // [TODO] could compute this w/o constructing box
            Vector3d c = (Vector3d)box_centers[iBox];
            Vector3f e = box_extents[iBox];
            AxisAlignedBox3d box = new AxisAlignedBox3d(ref c, e.x + box_eps, e.y + box_eps, e.z + box_eps);

            return box.Intersects(testBox);
        }

        double box_box_distsqr(int iBox, ref AxisAlignedBox3d testBox)
        {
            // [TODO] could compute this w/o constructing box
            Vector3d c = (Vector3d)box_centers[iBox];
            Vector3f e = box_extents[iBox];
            AxisAlignedBox3d box = new AxisAlignedBox3d(ref c, e.x + box_eps, e.y + box_eps, e.z + box_eps);
            return box.DistanceSquared(ref testBox);
        }


        double box_distance_sqr(int iBox, Vector3d p)
        {
            // [TODO] could compute this w/o constructing box
            Vector3d c = (Vector3d)box_centers[iBox];
            Vector3f e = box_extents[iBox];
            AxisAlignedBox3d box = new AxisAlignedBox3d(ref c, e.x + box_eps, e.y + box_eps, e.z + box_eps);

            return box.DistanceSquared(p);
        }


        protected bool box_contains(int iBox, Vector3d p)
        {
            // [TODO] this could be way faster...
            Vector3d c = (Vector3d)box_centers[iBox];
            Vector3f e = box_extents[iBox];
            AxisAlignedBox3d box = new AxisAlignedBox3d(ref c, e.x + box_eps, e.y + box_eps, e.z + box_eps);
            return box.Contains(p);
        }


        double combined_box_volume(int b0, int b1)
        {
            Vector3f c0 = box_centers[b0];
            Vector3f e0 = box_extents[b0];
            Vector3f c1 = box_centers[b1];
            Vector3f e1 = box_extents[b1];
            float minx = Math.Min(c0.x - e0.x, c1.x - e1.x);
            float maxx = Math.Max(c0.x + e0.x, c1.x + e1.x);
            float miny = Math.Min(c0.y - e0.y, c1.y - e1.y);
            float maxy = Math.Max(c0.y + e0.y, c1.y + e1.y);
            float minz = Math.Min(c0.z - e0.z, c1.z - e1.z);
            float maxz = Math.Max(c0.z + e0.z, c1.z + e1.z);
            return (maxx - minx) * (maxy - miny) * (maxz - minz);
        }
        double combined_box_length(int b0, int b1)
        {
            Vector3f c0 = box_centers[b0];
            Vector3f e0 = box_extents[b0];
            Vector3f c1 = box_centers[b1];
            Vector3f e1 = box_extents[b1];
            float minx = Math.Min(c0.x - e0.x, c1.x - e1.x);
            float maxx = Math.Max(c0.x + e0.x, c1.x + e1.x);
            float miny = Math.Min(c0.y - e0.y, c1.y - e1.y);
            float maxy = Math.Max(c0.y + e0.y, c1.y + e1.y);
            float minz = Math.Min(c0.z - e0.z, c1.z - e1.z);
            float maxz = Math.Max(c0.z + e0.z, c1.z + e1.z);
            return (maxx - minx)*(maxx - minx) + (maxy - miny)*(maxy - miny) + (maxz - minz)*(maxz - minz);
        }



        // 1) make sure we can reach every tri in mesh through tree (also demo of how to traverse tree...)
        // 2) make sure that triangles are contained in parent boxes
        public void TestCoverage()
        {
            int[] tri_counts = new int[mesh.MaxTriangleID];
            Array.Clear(tri_counts, 0, tri_counts.Length);
            int[] parent_indices = new int[box_to_index.Length];
            Array.Clear(parent_indices, 0, parent_indices.Length);

            test_coverage(tri_counts, parent_indices, root_index);

            foreach (int ti in mesh.TriangleIndices())
                if (tri_counts[ti] != 1)
                    Util.gBreakToDebugger();
        }

        // accumulate triangle counts and track each box-parent index. 
        // also checks that triangles are contained in boxes
        private void test_coverage(int[] tri_counts, int[] parent_indices, int iBox)
        {
            int idx = box_to_index[iBox];

            debug_check_child_tris_in_box(iBox);

            if ( idx < triangles_end ) {
                // triange-list case, array is [N t1 t2 ... tN]
                int n = index_list[idx];
                AxisAlignedBox3f box = get_box(iBox);
                for ( int i = 1; i <= n; ++i ) {
                    int ti = index_list[idx + i];
                    tri_counts[ti]++;

                    Index3i tv = mesh.GetTriangle(ti);
                    for ( int j = 0; j < 3; ++j ) {
                        Vector3f v = (Vector3f)mesh.GetVertex(tv[j]);
                        if (!box.Contains(v))
                            Util.gBreakToDebugger();
                    }
                }

            } else {
                int i0 = index_list[idx];
                if ( i0 < 0 ) {
                    // negative index means we only have one 'child' box to descend into
                    i0 = (-i0) - 1;
                    parent_indices[i0] = iBox;
                    test_coverage(tri_counts, parent_indices, i0);
                } else {
                    // positive index, two sequential child box indices to descend into
                    i0 = i0 - 1;
                    parent_indices[i0] = iBox;
                    test_coverage(tri_counts, parent_indices, i0);
                    int i1 = index_list[idx + 1];
                    i1 = i1 - 1;
                    parent_indices[i1] = iBox;
                    test_coverage(tri_counts, parent_indices, i1);
                }
            }
        }
        // do full tree traversal below iBox and make sure that all triangles are further
        // than box-distance-sqr
        void debug_check_child_tri_distances(int iBox, Vector3d p)
        {
            double fBoxDistSqr = box_distance_sqr(iBox, p);

            TreeTraversal t = new TreeTraversal() {
                NextTriangleF = (tID) => {
                    double fTriDistSqr = MeshQueries.TriDistanceSqr(mesh, tID, p);
                    if (fTriDistSqr < fBoxDistSqr)
                        if ( Math.Abs(fTriDistSqr - fBoxDistSqr) > MathUtil.ZeroTolerance*100 )
                            Util.gBreakToDebugger();
                }
            };
            tree_traversal(iBox, 0, t);
        }

        // do full tree traversal below iBox to make sure that all child triangles are contained
        void debug_check_child_tris_in_box(int iBox)
        {
            AxisAlignedBox3f box = get_box(iBox);
            TreeTraversal t = new TreeTraversal() {
                NextTriangleF = (tID) => {
                    Index3i tv = mesh.GetTriangle(tID);
                    for (int j = 0; j < 3; ++j) {
                        Vector3f v = (Vector3f)mesh.GetVertex(tv[j]);
                        if (box.Contains(v) == false)
                            Util.gBreakToDebugger();
                    }
                }
            };
            tree_traversal(iBox, 0, t);
        }



    }
}
