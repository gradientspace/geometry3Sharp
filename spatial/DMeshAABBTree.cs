using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

namespace g3
{
    public class DMeshAABBTree3 : ISpatial
    {
        DMesh3 mesh;
        int mesh_timestamp;

        public DMeshAABBTree3(DMesh3 m)
        {
            mesh = m;
        }


        public DMesh3 Mesh { get { return mesh; } }


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



        public bool SupportsNearestTriangle { get { return true; } }
        public int FindNearestTriangle(Vector3d p, double fMaxDist = double.MaxValue)
        {
            if (mesh_timestamp != mesh.ShapeTimestamp)
                throw new Exception("DMeshAABBTree3.FindNearestTriangle: mesh has been modified since tree construction");

            double fNearestSqr = (fMaxDist < double.MaxValue) ? fMaxDist * fMaxDist : double.MaxValue;
            int tNearID = DMesh3.InvalidID;
            find_nearest_tri(root_index, p, ref fNearestSqr, ref tNearID);
            return tNearID;
        }
        void find_nearest_tri(int iBox, Vector3d p, ref double fNearestSqr, ref int tID)
        {
            int idx = box_to_index[iBox];
            if ( idx < triangles_end ) {            // triange-list case, array is [N t1 t2 ... tN]
                int num_tris = index_list[idx];
                for (int i = 1; i <= num_tris; ++i) {
                    int ti = index_list[idx + i];
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




        public bool SupportsTriangleRayIntersection { get { return true; } }
        public int FindNearestHitTriangle(Ray3d ray, double fMaxDist = double.MaxValue)
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
        void find_hit_triangle(int iBox, ref Ray3d ray, ref double fNearestT, ref int tID)
        {
            int idx = box_to_index[iBox];
            if ( idx < triangles_end ) {            // triange-list case, array is [N t1 t2 ... tN]
                Triangle3d tri = new Triangle3d();
                int num_tris = index_list[idx];
                for (int i = 1; i <= num_tris; ++i) {
                    int ti = index_list[idx + i];

                    // [TODO] optimize this
                    mesh.GetTriVertices(ti, ref tri.V0, ref tri.V1, ref tri.V2);
                    IntrRay3Triangle3 ray_tri_hit = new IntrRay3Triangle3(ray, tri);
                    if ( ray_tri_hit.Find() ) {
                        if ( ray_tri_hit.RayParameter < fNearestT ) {
                            fNearestT = ray_tri_hit.RayParameter;
                            tID = ti;
                        }
                    }
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








        // returns cout
        public int FindAllHitTriangles(Ray3d ray, List<int> hitTriangles = null, double fMaxDist = double.MaxValue)
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
        int find_all_hit_triangles(int iBox, List<int> hitTriangles, ref Ray3d ray, double fMaxDist)
        {
            int hit_count = 0;

            int idx = box_to_index[iBox];
            if ( idx < triangles_end ) {            // triange-list case, array is [N t1 t2 ... tN]
                Triangle3d tri = new Triangle3d();
                int num_tris = index_list[idx];
                for (int i = 1; i <= num_tris; ++i) {
                    int ti = index_list[idx + i];

                    // [TODO] optimize this
                    mesh.GetTriVertices(ti, ref tri.V0, ref tri.V1, ref tri.V2);
                    IntrRay3Triangle3 ray_tri_hit = new IntrRay3Triangle3(ray, tri);
                    if (ray_tri_hit.Find()) {
                        if (ray_tri_hit.RayParameter < fMaxDist) {
                            if (hitTriangles != null)
                                hitTriangles.Add(ti);
                            hit_count++;
                        }
                    }
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




        // TransformF takes vertices of testMesh to our tree
        public bool TestIntersection(IMesh testMesh, Func<Vector3d, Vector3d> TransformF, bool bBoundsCheck = true)
        {
            if (mesh_timestamp != mesh.ShapeTimestamp)
                throw new Exception("DMeshAABBTree3.TestIntersection: mesh has been modified since tree construction");

            if (bBoundsCheck) {
                AxisAlignedBox3d meshBox = MeshMeasurements.Bounds(testMesh, TransformF);
                if (box_box_intersect(root_index, ref meshBox) == false )
                    return false;
            }

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
        public bool TestIntersection(Triangle3d triangle)
        {
            if (mesh_timestamp != mesh.ShapeTimestamp)
                throw new Exception("DMeshAABBTree3.TestIntersection: mesh has been modified since tree construction");

            AxisAlignedBox3d triBounds = BoundsUtil.Bounds(ref triangle);
            int interTri = find_any_intersection(root_index, ref triangle, ref triBounds);
            return (interTri >= 0);
        }
        int find_any_intersection(int iBox, ref Triangle3d triangle, ref AxisAlignedBox3d triBounds)
        {
            int idx = box_to_index[iBox];
            if ( idx < triangles_end ) {            // triange-list case, array is [N t1 t2 ... tN]
                Triangle3d box_tri = new Triangle3d();
                int num_tris = index_list[idx];
                for (int i = 1; i <= num_tris; ++i) {
                    int ti = index_list[idx + i];
                    mesh.GetTriVertices(ti, ref box_tri.V0, ref box_tri.V1, ref box_tri.V2);

                    IntrTriangle3Triangle3 intr = new IntrTriangle3Triangle3(triangle, box_tri);
                    if (intr.Test())
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






        // Returns true if there is any intersection between our mesh and 'other' mesh, via AABBs
        // TransformF takes vertices of otherTree to our tree - can be null if in same coord space
        public bool TestIntersection(DMeshAABBTree3 otherTree, Func<Vector3d, Vector3d> TransformF)
        {
            if (mesh_timestamp != mesh.ShapeTimestamp)
                throw new Exception("DMeshAABBTree3.TestIntersection: mesh has been modified since tree construction");

            if (find_any_intersection(root_index, otherTree, TransformF, otherTree.root_index, 0))
                return true;

            return false;
        }
        bool find_any_intersection(int iBox, DMeshAABBTree3 otherTree, Func<Vector3d, Vector3d> TransformF, int oBox, int depth)
        {
            int idx = box_to_index[iBox];
            int odx = otherTree.box_to_index[oBox];

            if (idx < triangles_end && odx < otherTree.triangles_end) {
                // ok we are at triangles for both trees, do triangle-level testing
                Triangle3d tri = new Triangle3d(), otri = new Triangle3d();
                int num_tris = index_list[idx], onum_tris = otherTree.index_list[odx];

                // can re-use because Test() doesn't cache anything
                IntrTriangle3Triangle3 intr = new IntrTriangle3Triangle3(new Triangle3d(), new Triangle3d());

                // outer iteration is "other" tris that need to be transformed (more expensive)
                for (int j = 1; j <= onum_tris; ++j) {
                    int tj = otherTree.index_list[odx + j];
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
                        mesh.GetTriVertices(ti, ref tri.V0, ref tri.V1, ref tri.V2);
                        intr.Triangle1 = tri;
                        if (intr.Test())
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
                    AxisAlignedBox3d oChild1Box = otherTree.get_boxd(oChild1);
                    if ( TransformF != null )
                        oChild1Box = BoundsUtil.Bounds(ref oChild1Box, TransformF);
                    if ( box_box_intersect(oChild1, ref bounds) )
                        return find_any_intersection(oChild1, otherTree, TransformF, oBox, depth + 1);

                } else {                            // 2 children
                    oChild1 = oChild1 - 1;          // [TODO] could descend one w/ larger overlap volume first??
                    int oChild2 = otherTree.index_list[odx + 1] - 1;

                    bool intersects = false;
                    AxisAlignedBox3d oChild1Box = otherTree.get_boxd(oChild1);
                    if ( TransformF != null )
                        oChild1Box = BoundsUtil.Bounds(ref oChild1Box, TransformF);
                    if ( oChild1Box.Intersects(bounds) ) 
                        intersects = find_any_intersection(iBox, otherTree, TransformF, oChild1, depth + 1);

                    if (intersects == false) {
                        AxisAlignedBox3d oChild2Box = otherTree.get_boxd(oChild2);
                        if ( TransformF != null )
                            oChild2Box = BoundsUtil.Bounds(ref oChild2Box, TransformF);
                        if ( oChild2Box.Intersects(bounds) )
                            intersects = find_any_intersection(iBox, otherTree, TransformF, oChild2, depth + 1);
                    }
                    return intersects;
                }


            } else {

                // descend our tree nodes if they intersect w/ current bounds of other tree
                AxisAlignedBox3d oBounds = otherTree.get_boxd(oBox);
                oBounds = BoundsUtil.Bounds(ref oBounds, TransformF);

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


        // Compute all intersections between two Meshes via AABB's
        // TransformF argument transforms vertices of otherTree to our tree (can be null if in same coord space)
        // Returns pairs of intersecting triangles, which could intersect in either point or segment
        public IntersectionsQueryResult FindIntersections(DMeshAABBTree3 otherTree, Func<Vector3d, Vector3d> TransformF)
        {
            if (mesh_timestamp != mesh.ShapeTimestamp)
                throw new Exception("DMeshAABBTree3.FindIntersections: mesh has been modified since tree construction");

            IntersectionsQueryResult result = new IntersectionsQueryResult();
            result.Points = new List<PointIntersection>();
            result.Segments = new List<SegmentIntersection>();

            find_intersections(root_index, otherTree, TransformF, otherTree.root_index, 0, result);

            return result;
        }
        void find_intersections(int iBox, DMeshAABBTree3 otherTree, Func<Vector3d, Vector3d> TransformF, 
                                int oBox, int depth, IntersectionsQueryResult result)
        {
            int idx = box_to_index[iBox];
            int odx = otherTree.box_to_index[oBox];

            if (idx < triangles_end && odx < otherTree.triangles_end) {
                // ok we are at triangles for both trees, do triangle-level testing
                Triangle3d tri = new Triangle3d(), otri = new Triangle3d();
                int num_tris = index_list[idx], onum_tris = otherTree.index_list[odx];

                // can re-use
                IntrTriangle3Triangle3 intr = new IntrTriangle3Triangle3(new Triangle3d(), new Triangle3d());

                // outer iteration is "other" tris that need to be transformed (more expensive)
                for (int j = 1; j <= onum_tris; ++j) {
                    int tj = otherTree.index_list[odx + j];
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
                        mesh.GetTriVertices(ti, ref tri.V0, ref tri.V1, ref tri.V2);
                        intr.Triangle1 = tri;

                        if (intr.Find()) {
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
                    AxisAlignedBox3d oChild1Box = otherTree.get_boxd(oChild1);
                    if ( TransformF != null )
                        oChild1Box = BoundsUtil.Bounds(ref oChild1Box, TransformF);
                    if ( box_box_intersect(oChild1, ref bounds) )
                        find_intersections(oChild1, otherTree, TransformF, oBox, depth + 1, result);

                } else {                            // 2 children
                    oChild1 = oChild1 - 1;

                    AxisAlignedBox3d oChild1Box = otherTree.get_boxd(oChild1);
                    if ( TransformF != null )
                        oChild1Box = BoundsUtil.Bounds(ref oChild1Box, TransformF);
                    if ( oChild1Box.Intersects(bounds) ) 
                        find_intersections(iBox, otherTree, TransformF, oChild1, depth + 1, result);

                    int oChild2 = otherTree.index_list[odx + 1] - 1;
                    AxisAlignedBox3d oChild2Box = otherTree.get_boxd(oChild2);
                    if ( TransformF != null )
                        oChild2Box = BoundsUtil.Bounds(ref oChild2Box, TransformF);
                    if ( oChild2Box.Intersects(bounds) )
                        find_intersections(iBox, otherTree, TransformF, oChild2, depth + 1, result);
                }

            } else {
                // descend our tree nodes if they intersect w/ current bounds of other tree
                AxisAlignedBox3d oBounds = otherTree.get_boxd(oBox);
                oBounds = BoundsUtil.Bounds(ref oBounds, TransformF);

                int iChild1 = index_list[idx];
                if ( iChild1 < 0 ) {                 // 1 child, descend if nearer than cur min-dist
                    iChild1 = (-iChild1) - 1;
                    if ( box_box_intersect(iChild1, ref oBounds) )
                        find_intersections(iChild1, otherTree, TransformF, oBox, depth + 1, result);

                } else {                            // 2 children
                    iChild1 = iChild1 - 1;          
                    if ( box_box_intersect(iChild1, ref oBounds) ) 
                        find_intersections(iChild1, otherTree, TransformF, oBox, depth + 1, result);

                    int iChild2 = index_list[idx + 1] - 1;
                    if ( box_box_intersect(iChild2, ref oBounds) )
                        find_intersections(iChild2, otherTree, TransformF, oBox, depth + 1, result);
                }

            }
        }








        public bool SupportsPointContainment { get { return true; } }
        public bool IsInside(Vector3d p)
        {
            // This is a raycast crossing-count test, which is not ideal!
            // Only works for closed meshes.

            //AxisAlignedBox3f bounds = get_box(root_index);
            //Vector3d outside = bounds.Center + 2 * bounds.Diagonal;

            Vector3d rayDir = Vector3d.AxisX;
            //Vector3d rayOrigin = p - 2 * bounds.Width * rayDir;
            Vector3d rayOrigin = p;

            Ray3d ray = new Ray3d(rayOrigin, rayDir);
            int nHits = FindAllHitTriangles(ray, null);

            return (nHits % 2) != 0;
        }








        // DoTraversal function will walk through tree and call NextBoxF for each
        //  internal box node, and NextTriangleF for each triangle. 
        //  You can prune branches by returning false from NextBoxF
        public class TreeTraversal
        {
            // return false to terminate this branch
            // arguments are box and depth in tree
            public Func<AxisAlignedBox3f, int, bool> NextBoxF = (box,depth) => { return true; };

            public Action<int> NextTriangleF = (tID) => { };
        }


        // walk over tree, calling functions in TreeTraversal object for internal nodes and triangles
        public void DoTraversal(TreeTraversal traversal)
        {
            if (mesh_timestamp != mesh.ShapeTimestamp)
                throw new Exception("DMeshAABBTree3.DoTraversal: mesh has been modified since tree construction");

            tree_traversal(root_index, 0, traversal);
        }

        // traversal implementation
        private void tree_traversal(int iBox, int depth, TreeTraversal traversal)
        {
            int idx = box_to_index[iBox];

            if ( idx < triangles_end ) {
                // triange-list case, array is [N t1 t2 ... tN]
                int n = index_list[idx];
                for ( int i = 1; i <= n; ++i ) {
                    int ti = index_list[idx + i];
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




        //
        // Internals - data structures, construction, etc
        //




        // storage for box nodes. 
        //   - box_to_index is a pointer into index_list
        //   - box_centers and box_extents are the centers/extents of the bounding boxes
        DVector<int> box_to_index;
        DVector<Vector3f> box_centers;
        DVector<Vector3f> box_extents;

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
        DVector<int> index_list;

        // index_list[i] for i < triangles_end is a triangle-index list, otherwise box-index pair/single
        int triangles_end = -1;

        // box_to_index[root_index] is the root node of the tree
        int root_index = -1;








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


        AxisAlignedBox3f get_box(int iBox)
        {
            Vector3f c = box_centers[iBox];
            Vector3f e = box_extents[iBox];
            e += 10.0f*MathUtil.Epsilonf;      // because of float/double casts, box may drift to the point
                                               // where mesh vertex will be slightly outside box
            return new AxisAlignedBox3f(c - e, c + e);
        }
        AxisAlignedBox3d get_boxd(int iBox)
        {
            Vector3f c = box_centers[iBox];
            Vector3f e = box_extents[iBox];
            e += 10.0f*MathUtil.Epsilonf;      // because of float/double casts, box may drift to the point
                                               // where mesh vertex will be slightly outside box
            return new AxisAlignedBox3d(c - e, c + e);
        }


        double box_ray_intersect_t(int iBox, Ray3d ray)
        {
            Vector3d c = box_centers[iBox];
            Vector3d e = box_extents[iBox];
            AxisAlignedBox3d box = new AxisAlignedBox3d(c - e, c + e);
            IntrRay3AxisAlignedBox3 intr = new IntrRay3AxisAlignedBox3(ray, box);
            if (intr.Find()) {
                return intr.RayParam0;
            } else {
                Debug.Assert(intr.Result != IntersectionResult.InvalidQuery);
                return double.MaxValue;
            }
        }

        bool box_box_intersect(int iBox, ref AxisAlignedBox3d testBox)
        {
            Vector3d c = box_centers[iBox];
            Vector3d e = box_extents[iBox];
            AxisAlignedBox3d box = new AxisAlignedBox3d(c - e, c + e);
            return box.Intersects(testBox);
        }


        double box_distance_sqr(int iBox, Vector3d p)
        {
            Vector3d c = box_centers[iBox];
            Vector3d e = box_extents[iBox];
            AxisAlignedBox3d box = new AxisAlignedBox3d(c - e, c + e);
            return box.DistanceSquared(p);
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
