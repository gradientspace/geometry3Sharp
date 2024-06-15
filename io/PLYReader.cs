using System;
using System.Collections.Generic;
using System.IO;

namespace VirgisGeometry
{
    //
    // Parse PLY mesh format
    // PLY format specification : http://gamma.cs.unc.edu/POWERPLANT/papers/ply.pdf
    // Currently - .ply files can only be read - not written - and this driver does not take any account of the material.
    class PLYReader : IMeshReader
    {
        /*
        * Element specification. holds the name, size and arbitraily long vector of properties
        */
        public struct element
        {
            public string name; // element name
            public List<string> properties; // the name of each property
            public List<string> types; // the type of each property
            public List<bool> list; // is the property a list
            public Int32 size; // element size

            public bool Equals(element other)
            {
                return name == other.name;
            }
        }

        static char[] TRIM_CHARS = { '\"', '/', '\\', ' ' };

        // connect to this to get warning messages
        public event ParsingMessagesHandler warningEvent;

        //int nWarningLevel = 0;      // 0 == no diagnostics, 1 == basic, 2 == crazy
        Dictionary<string, int> warningCount = new Dictionary<string, int>();

        private bool hasMesh; // used to show that at least one mesh has been found

        public IOReadResult Read(TextReader reader, ReadOptions options, IMeshBuilder builder)
        {
            string[] chunks ;
            List<element> elements = new List<element>(); // we will decant the element specifications into here
            string proj = "";
            string line;
            bool binary = false;

            // check for the magic number which in  a PLY file is "ply"
            if ( !reader.ReadLine().Contains("ply"))
            {
                return new IOReadResult(IOCode.GarbageDataError, "Not a valid PLY file - Magic number failure");
            }

            // Read header

            line = reader.ReadLine();
            if (line == null)
            {
                return new IOReadResult(IOCode.GarbageDataError, "Not a valid PLY file - header is corrupt");
            }

            /*
            * The header is a format defintion and a series of element definitions and/or comment lines
            * cycle through these until end-header
            */
            do
            {
                /*
                * iterate through all of the element blocks in the header and enumerate the number of members and the properties
                */
                if (line.StartsWith("element"))
                {
                    chunks = line.Split(' ');
                    if ((chunks.Length != 3))
                    {
                        return new IOReadResult(IOCode.GarbageDataError, "Not a valid PLY file - header is corrupt");
                    }
                    element element = new element();
                    element.properties = new List<string>();
                    element.types = new List<string>();
                    element.list = new List<bool>();
                    element.name = chunks[1];
                    element.size = ParseInt(chunks[2]);
                    do
                    {
                        line = reader.ReadLine();
                        if (line == null)
                        {
                            return new IOReadResult(IOCode.GarbageDataError, "Not a valid PLY file - header is corrupt");
                        }
                        if (line.StartsWith("property"))
                        {
                            chunks = line.Split(' ');
                            switch (chunks.Length)
                            {
                                case 3:
                                    element.properties.Add(chunks[2].ToLower());
                                    element.types.Add(chunks[1].ToLower());
                                    element.list.Add(false);
                                    break;
                                case 5:
                                    element.properties.Add(chunks[4].ToLower());
                                    element.types.Add(chunks[3].ToLower());
                                    element.list.Add(true);
                                    break;
                                default:
                                    return new IOReadResult(IOCode.GarbageDataError, "Not a valid PLY file - invalid element line" + line);
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                    while (true);
                    elements.Add(element);
                }

                // if end - stop looping
                else if (line.StartsWith("end_header"))
                {
                    break;
                }

                //if binary - give up

                else if (line.StartsWith("binary"))
                {
                    binary = true;
                }

                // if "comment crs" assume that the rest is the crs data
                else if (line.StartsWith("comment crs "))
                {
                    line.Remove(12);
                    proj = line;
                    line = reader.ReadLine();
                    if (line == null)
                    {
                        return new IOReadResult(IOCode.GarbageDataError, "Not a valid PLY file - header is corrupt");
                    }
                }

                // probably a comment line
                else
                {
                    line = reader.ReadLine();
                    if (line == null)
                    {
                        return new IOReadResult(IOCode.GarbageDataError, "Not a valid PLY file - header is corrupt");
                    }
                }
            }
            while (true);

            //
            // create new DMesh
            //

            bool bHasColors = false;
            bool bHasNormals = false;
            bool bHasUVs = false;

            for (int elid = 0; elid < elements.Count; ++elid)
            {
                element el = elements[elid];
                if (el.name == "vertex")
                {
                    if (
                            (!el.properties.Contains("x")) ||
                            (!el.properties.Contains("y")) ||
                            (!el.properties.Contains("z"))
                        )
                    {
                        return new IOReadResult(IOCode.GarbageDataError, "Verteces do not have XYZ");
                    }
                    if (
                            el.properties.Contains("red") &&
                            el.properties.Contains("blue") &&
                            el.properties.Contains("green")
                        )
                    {
                        bHasColors = true;
                    }
                    if (
                           el.properties.Contains("nx") &&
                           el.properties.Contains("ny") &&
                           el.properties.Contains("nz")
                       )
                    {
                        bHasNormals = true;
                    }
                    if (
                           el.properties.Contains("u") &&
                           el.properties.Contains("v")
                       )
                    {
                        bHasUVs = true;
                    }
                }
                if (el.name == "faces")
                {
                    if (!el.properties.Contains("vertex_indices")) {
                        return new IOReadResult(IOCode.GarbageDataError, "Faces do not have vertex indices");
                    }
                }
                if (el.name == "edges")
                {
                    if (
                        (!el.properties.Contains("vertex1")) ||
                        (!el.properties.Contains("vertex2"))
                        )
                    {
                        return new IOReadResult(IOCode.GarbageDataError, "Edges do not have vertex indices");
                    }
                }
            }

            int mesh_idx = builder.AppendNewMesh(bHasNormals, bHasColors, bHasUVs, false);

            /*
            * load the elements in order
            */
            line = reader.ReadLine();
            if (line == null)
            {
                return new IOReadResult(IOCode.GarbageDataError, "Not a valid PLY file - header is corrupt");
            }
            chunks = line.Split(' ');

            for (int elid = 0; elid < elements.Count; ++elid)
            {
                element el = elements[elid];

                // load the data
                for (int i = 0; i < el.size; ++i)
                {
                    /*
                    * set the line size - we will only deal with one list at the begining of the line
                    */
                    int nChunks = el.properties.Count;
                    if (el.list[0]) nChunks += ParseInt(chunks[0]);
                    if (chunks.Length != nChunks)
                    {
                        return new IOReadResult(IOCode.GarbageDataError, "Not a valid PLY file - contains invalid line : " + line);
                    }

                    /*
                    * Load the vertexes
                    */
                    if (el.name == "vertex")
                    {
                        Vector3d vertex = new Vector3d();
                        Vector3f normal = new Vector3f();
                        Vector3f color = new Vector3f();
                        Vector2f uvs = new Vector2f();
                        vertex.x = ParseValue(chunks[el.properties.FindIndex(item => item == "x")]);
                        vertex.y = ParseValue(chunks[el.properties.FindIndex(item => item == "y")]);
                        vertex.z = ParseValue(chunks[el.properties.FindIndex(item => item == "z")]);

                        if (bHasNormals)
                        {
                            normal.x = (float)ParseValue(chunks[el.properties.FindIndex(item => item == "nx")]);
                            normal.y = (float)ParseValue(chunks[el.properties.FindIndex(item => item == "ny")]);
                            normal.z = (float)ParseValue(chunks[el.properties.FindIndex(item => item == "nz")]);
                        }

                        if (bHasColors)
                        {
                            color.x = (float)ParseValue(chunks[el.properties.FindIndex(item => item == "red")]);
                            color.y = (float)ParseValue(chunks[el.properties.FindIndex(item => item == "blue")]);
                            color.z = (float)ParseValue(chunks[el.properties.FindIndex(item => item == "green")]);
                        }

                        if (bHasUVs)
                        {
                            uvs.x = (float)ParseValue(chunks[el.properties.FindIndex(item => item == "u")]);
                            uvs.y = (float)ParseValue(chunks[el.properties.FindIndex(item => item == "v")]);
                        }

                        append_vertex(builder, vertex, normal, color, uvs, bHasNormals, bHasColors, bHasUVs);
                    }

                    /*
                    *load the faces
                    */
                    else if (el.name == "face")
                    {
                        int faceSize = ParseInt(chunks[0]);
                        if (faceSize != 3)
                        {
                            emit_warning("[PLYReader] cann only read triangles");
                            return new IOReadResult(IOCode.FormatNotSupportedError, "Can only read tri faces");
                        }
                        Index3i tri = new Index3i();
                        tri.a = ParseInt(chunks[1]);
                        tri.b = ParseInt(chunks[2]);
                        tri.c = ParseInt(chunks[3]);

                        append_triangle(builder, tri);
                    }

                    // any other element ignore and move to the next line

                    line = reader.ReadLine();
                    if (line == null)
                    {
                        // if it is supposed to be the last line - don't look further
                        if (elid != (elements.Count - 1) && i != (el.size - 1))
                        {
                            return new IOReadResult(IOCode.GarbageDataError, " does not contain enough definitions of type " + el.name);
                        }

                    }
                    chunks = line?.Split(' ');
                }
            }

            return new IOReadResult(IOCode.Ok, "");
        }

        private int append_vertex(IMeshBuilder builder, Vector3d vtx, Vector3f norm, Vector3f cols, Vector2f uvs, bool bHaveNormals, bool bHaveColors, bool bHaveUVs)
        {

            if (bHaveNormals == false && bHaveColors == false && bHaveUVs == false)
                return builder.AppendVertex(vtx.x, vtx.y, vtx.z);

            NewVertexInfo vinfo = new NewVertexInfo();
            vinfo.bHaveC = vinfo.bHaveN = vinfo.bHaveUV = false;
            vinfo.v = vtx;
            if (bHaveNormals)
            {
                vinfo.bHaveN = true;
                vinfo.n = norm;
            }
            if (bHaveColors)
            {
                vinfo.bHaveC = true;
                vinfo.c = cols;
            }
            if (bHaveUVs)
            {
                vinfo.bHaveUV = true;
                vinfo.uv = uvs;
            }

            return builder.AppendVertex(vinfo);
        }

        int append_triangle(IMeshBuilder builder, Index3i tri)
        {
            if (tri.a < 0 || tri.b < 0 || tri.c < 0)
            {
                emit_warning(string.Format("[PLYReader] invalid triangle:  {0} {1} {2}",
                    tri.a, tri.b, tri.c));
                return -1;
            }
            return builder.AppendTriangle(tri.a, tri.b, tri.c);
        }

        public IOReadResult Read(BinaryReader reader, ReadOptions options, IMeshBuilder builder)
        {
            return new IOReadResult(IOCode.FormatNotSupportedError, "text read not supported for 3DS format");
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

        private double ParseValue(string value)
        {

            value = value.TrimStart(TRIM_CHARS).TrimEnd(TRIM_CHARS).Replace("\\", ""); // Trim characters
            double d; // Create double, to hold value if double

            if (double.TryParse(value, out d))
            {
                return d;
            }
            return 0.0;
        }

        private Int32 ParseInt(string value)
        {

            value = value.TrimStart(TRIM_CHARS).TrimEnd(TRIM_CHARS).Replace("\\", ""); // Trim characters
            Int32 u; // Create uint

            if (Int32.TryParse(value, out u))
            {
                return u;
            }
            return 0;
        }
    }
}

