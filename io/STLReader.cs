using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading;

namespace g3
{
    /// <summary>
    /// Read ASCII/Binary STL file and produce set of meshes.
    /// 
    /// Since STL is just a list of disconnected triangles, by default we try to
    /// merge vertices together. Use .RebuildStrategy to disable this and/or configure
    /// which algorithm is used. If you are using via StandardMeshReader, you can add
    /// .StrategyFlag to ReadOptions.CustomFlags to set this flag.
    /// 
    /// TODO: document welding strategies. There is no "best" one, they all fail
    /// in some cases, because STL is a stupid and horrible format.
    /// 
    /// STL Binary supports a per-triangle short-int that is usually used to specify color.
    /// However since we do not support per-triangle color in DMesh3, this color
    /// cannot be directly used. Instead of hardcoding behavior, we return the list of shorts
    /// if requested via IMeshBuilder Metadata. Set .WantPerTriAttribs=true or attach flag .PerTriAttribFlag.
    /// After the read finishes you can get the face color list via:
    ///    DVector<short> colors = Builder.Metadata[0][STLReader.PerTriAttribMetadataName] as DVector<short>;
    /// (for DMesh3Builder, which is the only builder that supports Metadata)
    /// </summary>
    public class STLReader : IMeshReader
    {

        public enum Strategy
        {
            NoProcessing = 0,           // return triangle soup
            IdenticalVertexWeld = 1,    // merge identical vertices. Logically sensible but doesn't always work on ASCII STL.
            TolerantVertexWeld = 2,     // merge vertices within .WeldTolerance

            AutoBestResult = 3          // try identical weld first, if there are holes then try tolerant weld, and return "best" result
                                        // ("best" is not well-defined...)
        }

        /// <summary>
        /// Which algorithm is used to try to reconstruct mesh topology from STL triangle soup
        /// </summary>
        public Strategy RebuildStrategy = Strategy.AutoBestResult;

        /// <summary>
        /// Vertices within this distance are considered "the same" by welding strategies.
        /// </summary>
        public double WeldTolerance = MathUtil.ZeroTolerancef;


        /// <summary>
        /// Binary STL supports per-triangle integer attribute, which is often used
        /// to store face colors. If this flag is true, we will attach these face
        /// colors to the returned mesh via IMeshBuilder.AppendMetaData
        /// </summary>
        public bool WantPerTriAttribs = false;

        /// <summary>
        /// name argument passed to IMeshBuilder.AppendMetaData
        /// </summary>
        public static string PerTriAttribMetadataName = "tri_attrib";


        /// <summary> connect to this event to get warning messages </summary>
		public event ParsingMessagesHandler warningEvent;


        //int nWarningLevel = 0;      // 0 == no diagnostics, 1 == basic, 2 == crazy
        Dictionary<string, int> warningCount = new Dictionary<string, int>();



        /// <summary> ReadOptions.CustomFlags flag for configuring .RebuildStrategy </summary>
        public const string StrategyFlag = "-stl-weld-strategy";

        /// <summary> ReadOptions.CustomFlags flag for configuring .WantPerTriAttribs </summary>
        public const string PerTriAttribFlag = "-want-tri-attrib";


        void ParseArguments(CommandArgumentSet args)
        {
            if ( args.Integers.ContainsKey(StrategyFlag) ) {
                RebuildStrategy = (Strategy)args.Integers[StrategyFlag];
            }
            if (args.Flags.ContainsKey(PerTriAttribFlag)) {
                WantPerTriAttribs = true;
            }
        }




        protected class STLSolid
        {
            public string Name;
            public DVectorArray3f Vertices = new DVectorArray3f();
            public DVector<short> TriAttribs = null;
        }


        List<STLSolid> Objects;

        void append_vertex(float x, float y, float z)
        {
            Objects.Last().Vertices.Append(x, y, z);
        }



        // entire data block for stl triangle, that we can directly convert to byte[]
        [StructLayout(LayoutKind.Sequential, Pack=1)]
        struct stl_triangle
        {
            public float nx, ny, nz;
            public float ax, ay, az;
            public float bx, by, bz;
            public float cx, cy, cz;
            public short attrib;
        }


        public IOReadResult Read(BinaryReader reader, ReadOptions options, IMeshBuilder builder)
        {
            if ( options.CustomFlags != null )
                ParseArguments(options.CustomFlags);

            /*byte[] header = */reader.ReadBytes(80);
            int totalTris = reader.ReadInt32();

            Objects = new List<STLSolid>();
            Objects.Add(new STLSolid());

            int tri_size = 50;      // bytes
            IntPtr bufptr = Marshal.AllocHGlobal(tri_size);
            stl_triangle tmp = new stl_triangle();
            Type tri_type = tmp.GetType();

            DVector<short> tri_attribs = new DVector<short>();

            try {
                for (int i = 0; i < totalTris; ++i) {
                    byte[] tri_bytes = reader.ReadBytes(50);
                    if (tri_bytes.Length < 50)
                        break;

                    Marshal.Copy(tri_bytes, 0, bufptr, tri_size);
                    stl_triangle tri = (stl_triangle)Marshal.PtrToStructure(bufptr, tri_type);

                    append_vertex(tri.ax, tri.ay, tri.az);
                    append_vertex(tri.bx, tri.by, tri.bz);
                    append_vertex(tri.cx, tri.cy, tri.cz);
                    tri_attribs.Add(tri.attrib);
                }

            } catch (Exception e) {
                return new IOReadResult(IOCode.GenericReaderError, "exception: " + e.Message);
            }

            Marshal.FreeHGlobal(bufptr);

            if (Objects.Count == 1)
                Objects[0].TriAttribs = tri_attribs;

            foreach (STLSolid solid in Objects)
                BuildMesh(solid, builder);

            return new IOReadResult(IOCode.Ok, "");
        }




        public IOReadResult Read(TextReader reader, ReadOptions options, IMeshBuilder builder)
        {
            if ( options.CustomFlags != null )
                ParseArguments(options.CustomFlags);

            // format is just this, with facet repeated N times:
            //solid "stl_ascii"
            //  facet normal 0.722390830517 -0.572606861591 0.387650430202
            //    outer loop
            //      vertex 0.00659640412778 4.19127035141 -0.244179025292
            //      vertex -0.0458636470139 4.09951019287 -0.281960010529
            //      vertex 0.0286951716989 4.14693021774 -0.350856184959
            //    endloop
            //  endfacet
            //endsolid

            bool in_solid = false;
            //bool in_facet = false;
            //bool in_loop = false;
            //int vertices_in_loop = 0;

            Objects = new List<STLSolid>();

            int nLines = 0;
            while (reader.Peek() >= 0) {

                string line = reader.ReadLine();
                nLines++;
                string[] tokens = line.Split( (char[])null , StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length == 0)
                    continue;

                if (tokens[0].Equals("vertex", StringComparison.OrdinalIgnoreCase)) {
                    float x = (tokens.Length > 1) ? Single.Parse(tokens[1]) : 0;
                    float y = (tokens.Length > 2) ? Single.Parse(tokens[2]) : 0;
                    float z = (tokens.Length > 3) ? Single.Parse(tokens[3]) : 0;
                    append_vertex(x, y, z);

                // [RMS] we don't really care about these lines...
                //} else if (tokens[0].Equals("outer", StringComparison.OrdinalIgnoreCase)) {
                //    in_loop = true;
                //    vertices_in_loop = 0;

                //} else if (tokens[0].Equals("endloop", StringComparison.OrdinalIgnoreCase)) {
                //    in_loop = false;
                        

                } else if (tokens[0].Equals("facet", StringComparison.OrdinalIgnoreCase)) {
                    if ( in_solid == false ) {      // handle bad STL
                        Objects.Add(new STLSolid() { Name = "unknown_solid" });
                        in_solid = true;
                    }
                    //in_facet = true;
                    // ignore facet normal

                // [RMS] also don't really need to do anything for this one
                //} else if (tokens[0].Equals("endfacet", StringComparison.OrdinalIgnoreCase)) {
                    //in_facet = false;


                } else if (tokens[0].Equals("solid", StringComparison.OrdinalIgnoreCase)) {
                    STLSolid newObj = new STLSolid();
                    if (tokens.Length == 2)
                        newObj.Name = tokens[1];
                    else
                        newObj.Name = "object_" + Objects.Count;
                    Objects.Add(newObj);
                    in_solid = true;


                } else if (tokens[0].Equals("endsolid", StringComparison.OrdinalIgnoreCase)) {
                    // do nothing, done object
                    in_solid = false;
                }
            }

            foreach (STLSolid solid in Objects)
                BuildMesh(solid, builder);

            return new IOReadResult(IOCode.Ok, "");
        }






        protected virtual void BuildMesh(STLSolid solid, IMeshBuilder builder)
        {
            if (RebuildStrategy == Strategy.AutoBestResult) {
                DMesh3 result = BuildMesh_Auto(solid);
                builder.AppendNewMesh(result);

            } else if (RebuildStrategy == Strategy.IdenticalVertexWeld) {
                DMesh3 result = BuildMesh_IdenticalWeld(solid);
                builder.AppendNewMesh(result);
            } else if (RebuildStrategy == Strategy.TolerantVertexWeld) {
                DMesh3 result = BuildMesh_TolerantWeld(solid, WeldTolerance);
                builder.AppendNewMesh(result);
            } else {
                BuildMesh_NoMerge(solid, builder);
            }

            if (WantPerTriAttribs && solid.TriAttribs != null && builder.SupportsMetaData)
                builder.AppendMetaData(PerTriAttribMetadataName, solid.TriAttribs);
        }


        protected virtual void BuildMesh_NoMerge(STLSolid solid, IMeshBuilder builder)
        {
            /*int meshID = */builder.AppendNewMesh(false, false, false, false);

            DVectorArray3f vertices = solid.Vertices;
            int nTris = vertices.Count / 3;
            for ( int ti = 0; ti < nTris; ++ti ) {
                Vector3f va = vertices[3 * ti];
                int a = builder.AppendVertex(va.x, va.y, va.z);
                Vector3f vb = vertices[3 * ti + 1];
                int b = builder.AppendVertex(vb.x, vb.y, vb.z);
                Vector3f vc = vertices[3 * ti + 2];
                int c = builder.AppendVertex(vc.x, vc.y, vc.z);

                builder.AppendTriangle(a, b, c);
            }
        }



        protected virtual DMesh3 BuildMesh_Auto(STLSolid solid)
        {
            DMesh3 fastWeldMesh = BuildMesh_IdenticalWeld(solid);
            int fastWeldMesh_bdryCount;
            if (check_for_cracks(fastWeldMesh, out fastWeldMesh_bdryCount, WeldTolerance)) {
                DMesh3 tolWeldMesh = BuildMesh_TolerantWeld(solid, WeldTolerance);
                int tolWeldMesh_bdryCount = count_boundary_edges(tolWeldMesh);

                if (tolWeldMesh_bdryCount < fastWeldMesh_bdryCount)
                    return tolWeldMesh;
                else
                    return fastWeldMesh;

            }

            return fastWeldMesh;
        }




        protected int count_boundary_edges(DMesh3 mesh) {
            int boundary_edge_count = 0;
            foreach (int eid in mesh.BoundaryEdgeIndices()) {
                boundary_edge_count++;
            }
            return boundary_edge_count;
        }



        protected bool check_for_cracks(DMesh3 mesh, out int boundary_edge_count, double crack_tol = MathUtil.ZeroTolerancef)
        {
            boundary_edge_count = 0;
            MeshVertexSelection boundary_verts = new MeshVertexSelection(mesh);
            foreach ( int eid in mesh.BoundaryEdgeIndices() ) {
                Index2i ev = mesh.GetEdgeV(eid);
                boundary_verts.Select(ev.a); boundary_verts.Select(ev.b);
                boundary_edge_count++;
            }
            if (boundary_verts.Count == 0)
                return false;
            

            AxisAlignedBox3d bounds = mesh.CachedBounds;
            PointHashGrid3d<int> borderV = new PointHashGrid3d<int>(bounds.MaxDim / 128, -1);
            foreach ( int vid in boundary_verts ) {
                Vector3d v = mesh.GetVertex(vid);
                var result = borderV.FindNearestInRadius(v, crack_tol, (existing_vid) => {
                    return v.Distance(mesh.GetVertex(existing_vid));
                });
                if (result.Key != -1)
                    return true;            // we found a crack vertex!
                borderV.InsertPoint(vid, v);
            }

            // found no cracks
            return false;
        }




        protected virtual DMesh3 BuildMesh_IdenticalWeld(STLSolid solid)
        {
            DMesh3Builder builder = new DMesh3Builder();
            builder.AppendNewMesh(false, false, false, false);

            DVectorArray3f vertices = solid.Vertices;
            int N = vertices.Count;
            int[] mapV = new int[N];

            Dictionary<Vector3f, int> uniqueV = new Dictionary<Vector3f, int>();
            for (int vi = 0; vi < N; ++vi) {
                Vector3f v = vertices[vi];
                int existing_idx;
                if (uniqueV.TryGetValue(v, out existing_idx)) {
                    mapV[vi] = existing_idx;
                } else {
                    int vid = builder.AppendVertex(v.x, v.y, v.z);
                    uniqueV[v] = vid;
                    mapV[vi] = vid;
                }
            }

            append_mapped_triangles(solid, builder, mapV);
            return builder.Meshes[0];
        }




        protected virtual DMesh3 BuildMesh_TolerantWeld(STLSolid solid, double weld_tolerance)
        {
            DMesh3Builder builder = new DMesh3Builder();
            builder.AppendNewMesh(false, false, false, false);

            DVectorArray3f vertices = solid.Vertices;
            int N = vertices.Count;
            int[] mapV = new int[N];


            AxisAlignedBox3d bounds = AxisAlignedBox3d.Empty;
            for (int i = 0; i < N; ++i)
                bounds.Contain(vertices[i]);

            // [RMS] because we are only searching within tiny radius, there is really no downside to
            // using lots of bins here, except memory usage. If we don't, and the mesh has a ton of triangles
            // very close together (happens all the time on big meshes!), then this step can start
            // to take an *extremely* long time!
            int num_bins = 256;
            if (N > 100000)   num_bins = 512;
            if (N > 1000000)  num_bins = 1024;
            if (N > 2000000) num_bins = 2048;
            if (N > 5000000) num_bins = 4096;

            PointHashGrid3d<int> uniqueV = new PointHashGrid3d<int>(bounds.MaxDim / (float)num_bins, -1);
            Vector3f[] pos = new Vector3f[N];
            for (int vi = 0; vi < N; ++vi) {
                Vector3f v = vertices[vi];

                var pair = uniqueV.FindNearestInRadius(v, weld_tolerance, (vid) => {
                    return v.Distance(pos[vid]);
                });
                if (pair.Key == -1) {
                    int vid = builder.AppendVertex(v.x, v.y, v.z);
                    uniqueV.InsertPoint(vid, v);
                    mapV[vi] = vid;
                    pos[vid] = v;
                } else {
                    mapV[vi] = pair.Key;
                }
            }

            append_mapped_triangles(solid, builder, mapV);
            return builder.Meshes[0];
        }



        void append_mapped_triangles(STLSolid solid, DMesh3Builder builder, int[] mapV)
        {
            int nTris = solid.Vertices.Count / 3;
            for (int ti = 0; ti < nTris; ++ti) {
                int a = mapV[3 * ti];
                int b = mapV[3 * ti + 1];
                int c = mapV[3 * ti + 2];
                if (a == b || a == c || b == c)     // don't try to add degenerate triangles
                    continue;
                builder.AppendTriangle(a, b, c);
            }
        }





        private void emit_warning(string sMessage)
        {
            string sPrefix = sMessage.Substring(0, 15);
            int nCount = warningCount.ContainsKey(sPrefix) ? warningCount[sPrefix] : 0;
            nCount++; warningCount[sPrefix] = nCount;
            if (nCount > 10)
                return;
            else if (nCount == 10)
                sMessage += " (additional message surpressed)";

            var e = warningEvent;
            if (e != null)
                e(sMessage, null);
        }

    }
}
