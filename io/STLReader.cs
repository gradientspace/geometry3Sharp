using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading;

namespace g3
{
    public class STLReader : IMeshReader
    {

        public enum Strategy
        {
            NoProcessing = 0,
            IdenticalVertexWeld = 1
        }
        public Strategy RebuildStrategy = Strategy.IdenticalVertexWeld;


        // connect to this to get warning messages
		public event ParsingMessagesHandler warningEvent;


        //int nWarningLevel = 0;      // 0 == no diagnostics, 1 == basic, 2 == crazy
        Dictionary<string, int> warningCount = new Dictionary<string, int>();



        public const string StrategyFlag = "-stl-weld-strategy";
        void ParseArguments(CommandArgumentSet args)
        {
            if ( args.Integers.ContainsKey(StrategyFlag) ) {
                RebuildStrategy = (Strategy)args.Integers[StrategyFlag];
            }
        }




        protected class STLSolid
        {
            public string Name;
            public DVectorArray3f Vertices = new DVectorArray3f();
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

            try {
                for (int i = 0; i < totalTris; ++i) {
                    byte[] tri_bytes = reader.ReadBytes(50);

                    Marshal.Copy(tri_bytes, 0, bufptr, tri_size);
                    stl_triangle tri = (stl_triangle)Marshal.PtrToStructure(bufptr, tri_type);

                    append_vertex(tri.ax, tri.ay, tri.az);
                    append_vertex(tri.bx, tri.by, tri.bz);
                    append_vertex(tri.cx, tri.cy, tri.cz);
                }

            } catch (Exception e) {
                return new IOReadResult(IOCode.GenericReaderError, "exception: " + e.Message);
            }

            Marshal.FreeHGlobal(bufptr);

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
                    float x = Single.Parse(tokens[1]);
                    float y = Single.Parse(tokens[2]);
                    float z = Single.Parse(tokens[3]);
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
            if (RebuildStrategy == Strategy.IdenticalVertexWeld)
                BuildMesh_IdenticalWeld(solid, builder);
            else
                BuildMesh_NoMerge(solid, builder);
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



        // [TODO] is there any way we could use a HashSet<Vector3f> here, instead of Dictionary?
        protected virtual void BuildMesh_IdenticalWeld(STLSolid solid, IMeshBuilder builder)
        {
            /*int meshID = */builder.AppendNewMesh(false, false, false, false);

            DVectorArray3f vertices = solid.Vertices;
            int N = vertices.Count;
            int[] mapV = new int[N];

            Dictionary<Vector3f, int> uniqueV = new Dictionary<Vector3f, int>();

            for ( int vi = 0; vi < N; ++vi ) {
                Vector3f v = vertices[vi];
                int existing_idx;
                if ( uniqueV.TryGetValue(v, out existing_idx) ) {
                    mapV[vi] = existing_idx;
                } else {
                    int vid = builder.AppendVertex(v.x, v.y, v.z);
                    uniqueV[v] = vid;
                    mapV[vi] = vid;
                }
            }


            int nTris = N / 3;
            for ( int ti = 0; ti < nTris; ++ti ) {
                int a = mapV[3 * ti];
                int b = mapV[3 * ti + 1];
                int c = mapV[3 * ti + 2];
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
