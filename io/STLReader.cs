using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace g3
{
    class STLReader : IMeshReader
    {


        // connect to this to get warning messages
		public event ErrorEventHandler warningEvent;

        int nWarningLevel = 0;      // 0 == no diagnostics, 1 == basic, 2 == crazy
        Dictionary<string, int> warningCount = new Dictionary<string, int>();




        class TriangleList
        {
            public string Name;
            public DVectorArray3d Vertices = new DVectorArray3d();
        }


        List<TriangleList> Objects;

        void append_vertex(float x, float y, float z)
        {

        }



        public IOReadResult Read(BinaryReader reader, ReadOptions options, IMeshBuilder builder)
        {
            throw new NotImplementedException();
        }

        public IOReadResult Read(TextReader reader, ReadOptions options, IMeshBuilder builder)
        {

            //solid "stl_ascii"
            //  facet normal 0.722390830517 -0.572606861591 0.387650430202
            //    outer loop
            //      vertex 0.00659640412778 4.19127035141 -0.244179025292
            //      vertex -0.0458636470139 4.09951019287 -0.281960010529
            //      vertex 0.0286951716989 4.14693021774 -0.350856184959
            //    endloop
            //  endfacet

            bool in_solid = false;
            bool in_facet = false;
            bool in_loop = false;
            int vertices_in_loop = 0;

            Objects = new List<TriangleList>();

            int nLines = 0;
            while (reader.Peek() >= 0) {

                string line = reader.ReadLine();
                nLines++;
                string[] tokens = line.Split( (char[])null , StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length == 0)
                    continue;

                if (tokens[0].Equals("vertex", StringComparison.OrdinalIgnoreCase)) {

                } else if (tokens[0].Equals("outer", StringComparison.OrdinalIgnoreCase)) {
                    in_loop = true;
                    vertices_in_loop = 0;

                } else if (tokens[0].Equals("endloop", StringComparison.OrdinalIgnoreCase)) {
                    in_loop = false;
                        

                } else if (tokens[0].Equals("facet", StringComparison.OrdinalIgnoreCase)) {
                    if ( in_solid == false ) {      // handle bad STL
                        Objects.Add(new TriangleList() { Name = "unknown_solid" });
                        in_solid = true;
                    }
                    in_facet = true;
                    // ignore facet normal

                } else if (tokens[0].Equals("endfacet", StringComparison.OrdinalIgnoreCase)) {
                    in_facet = false;


                } else if (tokens[0].Equals("solid", StringComparison.OrdinalIgnoreCase)) {
                    TriangleList newObj = new TriangleList();
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
            if ( e != null ) 
                e(this, new ErrorEventArgs(new Exception(sMessage)));
        }

    }
}
