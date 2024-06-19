using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Mathematics;
using andywiecko.BurstTriangulator;
using static UnityEngine.GraphicsBuffer;


namespace VirgisGeometry
{
    public class DMesh3Builder : IMeshBuilder
    {
        public enum AddTriangleFailBehaviors
        {
            DiscardTriangle = 0,
            DuplicateAllVertices = 1
        }

        /// <summary>
        /// What should we do when AddTriangle() fails because triangle is non-manifold?
        /// </summary>
        public AddTriangleFailBehaviors NonManifoldTriBehavior = AddTriangleFailBehaviors.DuplicateAllVertices;

        /// <summary>
        /// What should we do when AddTriangle() fails because the triangle already exists?
        /// </summary>
        public AddTriangleFailBehaviors DuplicateTriBehavior = AddTriangleFailBehaviors.DiscardTriangle;




        public List<DMesh3> Meshes;
        public List<GenericMaterial> Materials;

        // this is a map from index into Meshes to index into Materials (-1 if no material)
        //  (so, currently we can only have 1 material per mesh!)
        public List<int> MaterialAssignment;

        public List<Dictionary<string, object>> Metadata;

        int nActiveMesh;

        public DMesh3Builder()
        {
            Meshes = new List<DMesh3>();
            Materials = new List<GenericMaterial>();
            MaterialAssignment = new List<int>();
            Metadata = new List<Dictionary<string, object>>();
            nActiveMesh = -1;
        }

        public int AppendNewMesh(bool bHaveVtxNormals, bool bHaveVtxColors, bool bHaveVtxUVs, bool bHaveFaceGroups)
        {
            int index = Meshes.Count;
            DMesh3 m = new DMesh3(bHaveVtxNormals, bHaveVtxColors, bHaveVtxUVs, bHaveFaceGroups);
            Meshes.Add(m);
            MaterialAssignment.Add(-1);     // no material is known
            Metadata.Add(new Dictionary<string, object>());
            nActiveMesh = index;
            return index;
        }

        public int AppendNewMesh(DMesh3 existingMesh)
        {
            int index = Meshes.Count;
            Meshes.Add(existingMesh);
            MaterialAssignment.Add(-1);     // no material is known
            Metadata.Add(new Dictionary<string, object>());
            nActiveMesh = index;
            return index;
        }


        public void SetActiveMesh(int id)
        {
            if (id >= 0 && id < Meshes.Count)
                nActiveMesh = id;
            else
                throw new ArgumentOutOfRangeException("active mesh id is out of range");
        }

        public int AppendTriangle(int i, int j, int k)
        {
            return AppendTriangle(i, j, k, -1);
        }

        public int AppendTriangle(int i, int j, int k, int g)
        {
            // [RMS] What to do here? We definitely do not want to add a duplicate triangle!!
            //   But is silently ignoring the right thing to do?
            int existing_tid = Meshes[nActiveMesh].FindTriangle(i, j, k);
            if (existing_tid != DMesh3.InvalidID) {
                if (DuplicateTriBehavior == AddTriangleFailBehaviors.DuplicateAllVertices)
                    return append_duplicate_triangle(i, j, k, g);
                else
                    return existing_tid;
            }

            int tid = Meshes[nActiveMesh].AppendTriangle(i, j, k, g);
            if ( tid == DMesh3.NonManifoldID ) {
                if (NonManifoldTriBehavior == AddTriangleFailBehaviors.DuplicateAllVertices)
                    return append_duplicate_triangle(i, j, k, g);
                else
                    return DMesh3.NonManifoldID;
            }
            return tid;
        }
        int append_duplicate_triangle(int i, int j, int k, int g)
        {
            NewVertexInfo vinfo = new NewVertexInfo();
            Meshes[nActiveMesh].GetVertex(i, ref vinfo, true, true, true);
            int new_i = Meshes[nActiveMesh].AppendVertex(vinfo);
            Meshes[nActiveMesh].GetVertex(j, ref vinfo, true, true, true);
            int new_j = Meshes[nActiveMesh].AppendVertex(vinfo);
            Meshes[nActiveMesh].GetVertex(k, ref vinfo, true, true, true);
            int new_k = Meshes[nActiveMesh].AppendVertex(vinfo);
            return Meshes[nActiveMesh].AppendTriangle(new_i, new_j, new_k, g);
        }



        public int AppendVertex(double x, double y, double z)
        {
            return Meshes[nActiveMesh].AppendVertex(new Vector3d(x, y, z));
        }
        public int AppendVertex(NewVertexInfo info)
        {
            return Meshes[nActiveMesh].AppendVertex(info);
        }

        public bool SupportsMetaData { get { return true; } }
        public void AppendMetaData(string identifier, object data)
        {
            Metadata[nActiveMesh].Add(identifier, data);
        }

        public void SetVertexUV(int vID, Vector2f UV) {
            Meshes[nActiveMesh].SetVertexUV(vID, UV);
        }


        // just store GenericMaterial object, we can't use it here
        public int BuildMaterial(GenericMaterial m)
        {
            int id = Materials.Count;
            Materials.Add(m);
            return id;
        }

        // do material assignment to mesh
        public void AssignMaterial(int materialID, int meshID)
        {
            if (meshID >= MaterialAssignment.Count || materialID >= Materials.Count)
                throw new ArgumentOutOfRangeException("[SimpleMeshBuilder::AssignMaterial] meshID or materialID are out-of-range");
            MaterialAssignment[meshID] = materialID;
        }





        //
        // DMesh3 construction utilities
        //

        /// <summary>
        /// ultimate generic mesh-builder, pass it arrays of floats/doubles, or lists
        /// of Vector3d, or anything in-between. Will figure out how to interpret
        /// </summary>
        public static DMesh3 Build<VType,TType,NType>(IEnumerable<VType> Vertices,  
                                                      IEnumerable<TType> Triangles, 
                                                      IEnumerable<NType> Normals = null,
                                                      IEnumerable<int> TriGroups = null, 
                                                      AxisOrder ax = default)
        {
            DMesh3 mesh = new DMesh3(Normals != null, false, false, TriGroups != null);
            if (ax == default) mesh.axisOrder = AxisOrder.ENU; else mesh.axisOrder = ax;

            Vector3d[] v = BufferUtil.ToVector3d(Vertices);
            for (int i = 0; i < v.Length; ++i)
                mesh.AppendVertex(v[i], true);

            if ( Normals != null ) {
                Vector3f[] n = BufferUtil.ToVector3f(Normals);
                if ( n.Length != v.Length )
                    throw new Exception("DMesh3Builder.Build: incorrect number of normals provided");
                for (int i = 0; i < n.Length; ++i)
                    mesh.SetVertexNormal(i, n[i]);
            }

            Index3i[] t = BufferUtil.ToIndex3i(Triangles);
            for (int i = 0; i < t.Length; ++i)
                mesh.AppendTriangle(t[i]);

            if ( TriGroups != null ) {
                List<int> groups = new List<int>(TriGroups);
                if (groups.Count != t.Length)
                    throw new Exception("DMesh3Builder.Build: incorect number of triangle groups");
                for (int i = 0; i < t.Length; ++i)
                    mesh.SetTriangleGroup(i, groups[i]);
            }
            return mesh;
        }


        /// <summary>
        /// Create a Dmesh3 from the vertices. This routine will only "work" if the INTENDED output mesh is planar. 
        /// This routine does not support creating holes in the mesh.
        /// 
        /// Pass it arrays of floats/doubles, or lists
        /// of Vector3d, or anything in-between. Will figure out how to interpret.
        /// 
        /// If you do NOT want the convex hull of the points, you must pass it a set of edge constraints.
        /// 
        /// 
        /// The mesh is triangulated using a Frame that is as orthogonal to the dataset as possible. 
        /// The Dmesh3 UVs are created in this frame
        /// 
        /// NOTE:
        /// - vertices cannot be repeated
        /// - constraint edges cannot intersect or be duplicated (even if reversed)
        /// - constraint edges cannot intesect any other vertex except the the vertices for which they are defined
        /// </summary>
        /// <param name="vertices">IEnumberable of type VType containing the vertices</param>
        /// <param name="constraint_edges">IEnumerable of type EType containg the constraint edges</param>
        public static DMesh3 Build<VType, EType>(IEnumerable<VType> vertices,
                                                 IEnumerable<EType> constraint_edges = null,
                                                 AxisOrder ax = default)
        {
            Vector3d[] vertices3d = BufferUtil.ToVector3d(vertices);
            Index3i[] triangles;

            OrthogonalPlaneFit3 orth = new (vertices3d);
            Frame3f frame = new (orth.Origin, orth.Normal);

            List<Vector2d> vertices2d = new ();
            foreach (Vector3d v in vertices3d)
            {
                Vector2f vertex = frame.ToPlaneUV((Vector3f)v, 3);
                vertices2d.Add(vertex);
            }

            Triangulator triangulator = new (Allocator.Persistent)
            {
                Settings = {
                    RestoreBoundary = true,
                }
            };
            Triangulator.InputData input = new ()
            {
                Positions = new NativeArray<float2>(vertices2d.Select(vertex => (float2)vertex).ToArray(), Allocator.Persistent),
            };

            if (constraint_edges != null)
            {
                NativeArray<int> edges = new(constraint_edges.Count() * 2, Allocator.Persistent);

                int idx = 0;
                foreach (Index2i edge in BufferUtil.ToIndex2i(constraint_edges))
                {
                    edges[idx] = edge.a;
                    idx++;
                    edges[idx] = edge.b;
                    idx++;
                }
                input.ConstraintEdges = edges;
            }

            triangulator.Input = input;
            try
            {

                triangulator.Run();

                if (!triangulator.Output.Status.IsCreated ||
                       triangulator.Output.Status.Value != Triangulator.Status.OK
                   )
                {
                    throw new Exception("Could not create Delaunay Triangulation");
                }


                // 
                // extract the triangles from the delaunay triangulation 
                //
                int[] tris = triangulator.Output.Triangles.AsArray().ToArray();
                int tri_count = tris.Length / 3;
                triangles = new Index3i[tri_count];
                long idx = 0;

                for (int i = 0; i < tri_count; i++)
                {
                    triangles[i] = new(tris[idx++], tris[idx++], tris[idx++]);
                }


                triangulator.Input.Positions.Dispose();
                triangulator.Input.ConstraintEdges.Dispose();
                triangulator.Dispose();
            }
            catch
            {
                triangulator.Input.Positions.Dispose();
                triangulator.Input.ConstraintEdges.Dispose();
                triangulator.Dispose();
                throw new Exception("DMesh3 creation Failed");
            }

            DMesh3 res = Build<Vector3d, Index3i, Vector3d>(vertices3d, triangles, null, null, ax);
            for (int i = 0; i < vertices2d.Count; i++)
            {
                res.SetVertexUV(i, (Vector2f)vertices2d[i]);
            }
            return res;
        }
    }
}
