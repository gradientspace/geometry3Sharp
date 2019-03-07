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
    /// Inserts a polygon into a mesh using a planar projection. You provide a
    /// projection frame and either the polygon in the frame's XY-coordinate system,
    /// or a DCurve3 space curve that will be projected. 
    /// 
    /// Currently you must also provide a seed triangle, that intersects the curve.
    /// We flood-fill from the vertices of that triangle to find the interior vertices,
    /// and hence the set of faces that are modified.
    /// 
    /// The insertion operation splits the existing mesh edges, so the inserted polygon
    /// will have more segments than the input polygon, in general. If you set
    /// SimplifyInsertion = true, then we collapse these extra edges, so you (should)
    /// get back an edge loop with the same number of vertices. However, on a non-planar
    /// mesh this means the edges will no longer lie on the input surface.
    /// 
    /// If RemovePolygonInterior = true, the faces inside the polygon are deleted
    /// 
    /// returns:
    ///   ModifiedRegion: this is the RegionOperator created to subset the mesh for editing.
    ///       You can use this to access the modified mesh
    ///       
    ///   InsertedPolygonVerts: the output vertex ID for Polygon[i]. This *does not* 
    ///   include the intermediate vertices, it's a 1-1 correspondence.
    ///       
    ///   InsertedLoop: inserted edge loop on output mesh
    ///       
    ///   InteriorTriangles: the triangles inside the polygon, null if RemovePolygonInterior=true
    ///   
    /// 
    /// If you would like to change the behavior after the insertion is computed, you can 
    /// subclass and override BackPropagate().
    /// 
    /// 
    /// [TODO] currently we construct a planar BVTree (but 3D) to map the new vertices to
    /// 3D via barycentric interpolation. However we could do this inline. MeshInsertUVPolyCurve 
    /// needs to fully support working on separate coordinate set (it tries via Get/Set PointF, but
    /// it is not 100% working), and it needs to let client know about poke and split events, w/
    /// bary-coords, so that we can compute the new 3D positions. 
    /// 
    /// </summary>
    public class MeshInsertProjectedPolygon
    {
        public DMesh3 Mesh;
        public int SeedTriangle = -1;   // you must provide this so that we can efficiently
                                        // find region of mesh to insert into
        public Frame3f ProjectFrame;    // assumption is that Z is plane normal

        // if true, we call Simply() on the inserted UV-curve, which means the
        // resulting insertion should have as many vertices as Polygon, and
        // the InsertedPolygonVerts list should be verts of a valid edge-loop
        public bool SimplifyInsertion = true;

        // if true, we delete triangles on polygon interior
        public bool RemovePolygonInterior = true;


        // internally a RegionOperator is constructed and the insertion is done
        // on a submesh. This is that submesh, provided for your convenience
        public RegionOperator ModifiedRegion;

        // vertex IDs of inserted polygon vertices in output mesh. 
        public int[] InsertedPolygonVerts;

        // inserted edge loop
        public EdgeLoop InsertedLoop;

        // set of triangles inside polygon. null if RemovePolgonInterior = true
        public int[] InteriorTriangles;

        // inserted polygon, in case you did not save a reference
        public Polygon2d Polygon;



        /// <summary>
        /// insert polygon in given frame
        /// </summary>
        public MeshInsertProjectedPolygon(DMesh3 mesh, Polygon2d poly, Frame3f frame, int seedTri)
        {
            Mesh = mesh;
            Polygon = new Polygon2d(poly);
            ProjectFrame = frame;
            SeedTriangle = seedTri;
        }

        /// <summary>
        /// create Polygon by projecting polygon3 into frame
        /// </summary>
        public MeshInsertProjectedPolygon(DMesh3 mesh, DCurve3 polygon3, Frame3f frame, int seedTri )
        {
            if (polygon3.Closed == false)
                throw new Exception("MeshInsertPolyCurve(): only closed polygon3 supported for now");

            Mesh = mesh;
            ProjectFrame = frame;
            SeedTriangle = seedTri;

            Polygon = new Polygon2d();
            foreach (Vector3d v3 in polygon3.Vertices) {
                Vector2f uv = frame.ToPlaneUV((Vector3f)v3, 2);
                Polygon.AppendVertex(uv);
            }
        }


        public virtual ValidationStatus Validate()
        {
            if (Mesh.IsTriangle(SeedTriangle) == false)
                return ValidationStatus.NotATriangle;

            return ValidationStatus.Ok;
        }


        public bool Insert()
        {
            Func<int, bool> is_contained_v = (vid) => {
                Vector3d v = Mesh.GetVertex(vid);
                Vector2f vf2 = ProjectFrame.ToPlaneUV((Vector3f)v, 2);
                return Polygon.Contains(vf2);
            };

            MeshVertexSelection vertexROI = new MeshVertexSelection(Mesh);
            Index3i seedT = Mesh.GetTriangle(SeedTriangle);

            // if a seed vert of seed triangle is containd in polygon, we will
            // flood-fill out from there, this gives a better ROI. 
            // If not, we will try flood-fill from the seed triangles.
            List<int> seed_verts = new List<int>();
            for ( int j = 0; j < 3; ++j ) {
                if ( is_contained_v(seedT[j]) )
                    seed_verts.Add(seedT[j]);
            }
            if (seed_verts.Count == 0) {
                seed_verts.Add(seedT.a);
                seed_verts.Add(seedT.b);
                seed_verts.Add(seedT.c);
            }

            // flood-fill out from seed vertices until we have found all vertices
            // contained in polygon
            vertexROI.FloodFill(seed_verts.ToArray(), is_contained_v);

            // convert vertex ROI to face ROI
            MeshFaceSelection faceROI = new MeshFaceSelection(Mesh, vertexROI, 1);
            faceROI.ExpandToOneRingNeighbours();
            faceROI.FillEars(true);    // this might be a good idea...

            // construct submesh
            RegionOperator regionOp = new RegionOperator(Mesh, faceROI);
            DSubmesh3 roiSubmesh = regionOp.Region;
            DMesh3 roiMesh = roiSubmesh.SubMesh;

            // save 3D positions of unmodified mesh
            Vector3d[] initialPositions = new Vector3d[roiMesh.MaxVertexID];

            // map roi mesh to plane
            MeshTransforms.PerVertexTransform(roiMesh, roiMesh.VertexIndices(), (v, vid) => {
                Vector2f uv = ProjectFrame.ToPlaneUV((Vector3f)v, 2);
                initialPositions[vid] = v;
                return new Vector3d(uv.x, uv.y, 0);
            });

            // save a copy of 2D mesh and construct bvtree. we will use
            // this later to project back to 3d
            // [TODO] can we use a better spatial DS here, that takes advantage of 2D?
            DMesh3 projectMesh = new DMesh3(roiMesh);
            DMeshAABBTree3 projecter = new DMeshAABBTree3(projectMesh, true);

            MeshInsertUVPolyCurve insertUV = new MeshInsertUVPolyCurve(roiMesh, Polygon);
            //insertUV.Validate()
            bool bOK = insertUV.Apply();
            if (!bOK)
                throw new Exception("insertUV.Apply() failed");

            if ( SimplifyInsertion )
                insertUV.Simplify();

            int[] insertedPolyVerts = insertUV.CurveVertices;

            // grab inserted loop, assuming it worked
            EdgeLoop insertedLoop = null;
            if ( insertUV.Loops.Count == 1 ) {
                insertedLoop = insertUV.Loops[0];
            }

            // find interior triangles
            List<int> interiorT = new List<int>();
            foreach (int tid in roiMesh.TriangleIndices()) {
                Vector3d centroid = roiMesh.GetTriCentroid(tid);
                if (Polygon.Contains(centroid.xy))
                    interiorT.Add(tid);
            }
            if (RemovePolygonInterior) {
                MeshEditor editor = new MeshEditor(roiMesh);
                editor.RemoveTriangles(interiorT, true);
                InteriorTriangles = null;
            } else {
                InteriorTriangles = interiorT.ToArray();
            }


            // map back to 3d
            Vector3d a = Vector3d.Zero, b = Vector3d.Zero, c = Vector3d.Zero;
            foreach ( int vid in roiMesh.VertexIndices() ) {
                
                // [TODO] somehow re-use exact positions from regionOp maps?

                // construct new 3D pos w/ barycentric interpolation
                Vector3d v = roiMesh.GetVertex(vid);
                int tid = projecter.FindNearestTriangle(v);
                Index3i tri = projectMesh.GetTriangle(tid);
                projectMesh.GetTriVertices(tid, ref a, ref b, ref c);
                Vector3d bary = MathUtil.BarycentricCoords(ref v, ref a, ref b, ref c);
                Vector3d pos = bary.x * initialPositions[tri.a] + bary.y * initialPositions[tri.b] + bary.z * initialPositions[tri.c];

                roiMesh.SetVertex(vid, pos);
            }

            bOK = BackPropagate(regionOp, insertedPolyVerts, insertedLoop);

            return bOK;
        }



        protected virtual bool BackPropagate(RegionOperator regionOp, int[] insertedPolyVerts, EdgeLoop insertedLoop)
        {
            bool bOK = regionOp.BackPropropagate();
            if (bOK) {
                ModifiedRegion = regionOp;

                IndexUtil.Apply(insertedPolyVerts, regionOp.ReinsertSubToBaseMapV);
                InsertedPolygonVerts = insertedPolyVerts;

                if (insertedLoop != null) {
                    InsertedLoop = MeshIndexUtil.MapLoopViaVertexMap(regionOp.ReinsertSubToBaseMapV,
                        regionOp.Region.SubMesh, regionOp.Region.BaseMesh, insertedLoop);
                    if (RemovePolygonInterior)
                        InsertedLoop.CorrectOrientation();
                }
            }
            return bOK;
        }


 

    }
}
