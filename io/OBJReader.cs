using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace g3
{
    internal unsafe struct Triangle
    {
        public fixed int vIndices[3];
        public fixed int vNormals[3];
        public fixed int vUVs[3];

        public void clear()
        {
            fixed (int* v = this.vIndices) { v[0] = -1; v[1] = -1; v[2] = -1; }
            fixed (int* n = this.vIndices) { n[0] = -1; n[1] = -1; n[2] = -1; }
            fixed (int* u = this.vIndices) { u[0] = -1; u[1] = -1; u[2] = -1; }
        }
    }



    public class OBJReader : IMeshReader
    {
        DVector<double> vPositions;
        DVector<float> vNormals;
        DVector<double> vUVs;
        DVector<float> vColors;
        DVector<Triangle> vTriangles;

        bool m_bOBJHasPerVertexColors;
        int m_nUVComponents;

        private string[] splitDoubleSlash;
        private char[] splitSlash;

        public OBJReader()
        {
            this.splitDoubleSlash = new string[] { "//" };
            this.splitSlash = new char[] { '/' };
        }


        public bool HasPerVertexColors { get { return m_bOBJHasPerVertexColors; } }
        public int UVDimension{ get { return m_nUVComponents; } }


        public IOReadResult Read(BinaryReader reader, ReadOptions options, IMeshBuilder builder)
        {
            throw new NotImplementedException();
        }

        public IOReadResult Read(TextReader reader, ReadOptions options, IMeshBuilder builder)
        {
            var parseResult = ParseInput(reader, options);
            if (parseResult.result != ReadResult.Ok)
                return parseResult;

            var buildResult = BuildMeshes(options, builder);
            if (buildResult.result != ReadResult.Ok)
                return buildResult;

            return new IOReadResult(ReadResult.Ok, "");
        }

        unsafe IOReadResult BuildMeshes(ReadOptions options, IMeshBuilder builder)
        {
            if (vPositions.Length == 0)
                return new IOReadResult(ReadResult.GarbageDataError, "No vertices in file");
            if (vTriangles.Length == 0)
                return new IOReadResult(ReadResult.GarbageDataError, "No triangles in file");

            // [TODO] support non-per-vertex normals/colors
            bool bHaveNormals = (vNormals.Length == vPositions.Length);
            bool bHaveColors = (vColors.Length == vPositions.Length);

            int nVertices = vPositions.Length / 3;
            int[] mapV = new int[nVertices];

            builder.AppendNewMesh();
            for ( int k = 0; k < nVertices; ++k ) {
                int i = 3 * k;

                int iVtx = -1;
                if (bHaveColors && bHaveNormals) {
                    iVtx = builder.AppendVertexNC(vPositions[i], vPositions[i + 1], vPositions[i + 2],
                        vNormals[i], vNormals[i + 1], vNormals[i + 2], vColors[i], vColors[i + 1], vColors[i + 2]);
                } else if (bHaveColors) {
                    iVtx = builder.AppendVertexC(vPositions[i], vPositions[i + 1], vPositions[i + 2],
                       vColors[i], vColors[i + 1], vColors[i + 2]);
                } else if (bHaveNormals) {
                    iVtx = builder.AppendVertexN(vPositions[i], vPositions[i + 1], vPositions[i + 2],
                       vNormals[i], vNormals[i + 1], vNormals[i + 2]);

                } else {
                    iVtx = builder.AppendVertex(vPositions[i], vPositions[i + 1], vPositions[i + 2]);
                }
                mapV[k] = iVtx;
            }

            // [TODO] this doesn't handle missing vertices...
            for ( int k = 0; k < vTriangles.Length; ++k ) {
                Triangle t = vTriangles[k];
                int v0 = mapV[t.vIndices[0] - 1];
                int v1 = mapV[t.vIndices[1] - 1];
                int v2 = mapV[t.vIndices[2] - 1];
                builder.AppendTriangle(v0, v1, v2);
            }

            return new IOReadResult(ReadResult.Ok, "");
        }






        public IOReadResult ParseInput(TextReader reader, ReadOptions options)
        {
            vPositions = new DVector<double>();
            vNormals = new DVector<float>();
            vUVs = new DVector<double>();
            vColors = new DVector<float>();
            vTriangles = new DVector<Triangle>();

            bool bVerticesHaveColors = false;
            int nMaxUVLength = 0;

            while (reader.Peek() >= 0) {

                string line = reader.ReadLine();
                string[] tokens = line.Split( (char[])null , StringSplitOptions.RemoveEmptyEntries);

                if ( tokens[0][0] == 'v' ) {
                    if ( tokens[0].Length == 1 ) {
                        if ( tokens.Length == 4 ) {
                            vPositions.Add(Double.Parse(tokens[1]));
                            vPositions.Add(Double.Parse(tokens[2]));
                            vPositions.Add(Double.Parse(tokens[3]));

                        } else if ( tokens.Length == 7 ) {
                            vPositions.Add(Double.Parse(tokens[1]));
                            vPositions.Add(Double.Parse(tokens[2]));
                            vPositions.Add(Double.Parse(tokens[3]));

                            vColors.Add(float.Parse(tokens[4]));
                            vColors.Add(float.Parse(tokens[5]));
                            vColors.Add(float.Parse(tokens[6]));
                            bVerticesHaveColors = true;
                        }

                    } else if ( tokens[0][1] == 'n' ) {
                        vNormals.Add(float.Parse(tokens[1]));
                        vNormals.Add(float.Parse(tokens[2]));
                        vNormals.Add(float.Parse(tokens[3]));
                    } else if ( tokens[0][1] == 't' ) {
                        nMaxUVLength = Math.Max(nMaxUVLength, tokens.Length);
                        vUVs.Add(Double.Parse(tokens[1]));
                        vUVs.Add(Double.Parse(tokens[2]));
                        if ( tokens.Length == 4 )
                            vUVs.Add(Double.Parse(tokens[3]));
                    }


                } else if ( tokens[0][0] == 'f' ) {

                    if (tokens.Length == 4) {
                        Triangle tri = new Triangle();
                        parse_triangle(tokens, ref tri);
                        vTriangles.Add(tri);
                    } else {
                        // punt for now...
                    }

                } else if ( tokens[0][0] == 'g' ) {

                } else if ( tokens[0][0] == 'o' ) {

                }

            }

            m_bOBJHasPerVertexColors = bVerticesHaveColors;
            m_nUVComponents = nMaxUVLength;

            return new IOReadResult(ReadResult.Ok, "");
        }


        private unsafe void parse_triangle(string[] tokens, ref Triangle t ){
            int nMode = 0;
            if (tokens[1].IndexOf("//") != -1)
                nMode = 1;
            else if (tokens[1].IndexOf('/') != -1)
                nMode = 2;

            t.clear();

            fixed ( int *v = t.vIndices, n = t.vNormals, u = t.vUVs) {

                for (int k = 0; k < 3; ++k) {
                    if (nMode == 0) {
                        // "f v1 v2 v3"
                        v[k] = int.Parse(tokens[k+1]);

                    }  else if (nMode == 1) {
                        // "f v1//vn1 v2//vn2 v3//vn3"
                        string[] parts = tokens[k + 1].Split( this.splitDoubleSlash, StringSplitOptions.RemoveEmptyEntries);
                        v[k] = int.Parse(parts[0]);
                        n[k] = int.Parse(parts[1]);

                    } else if ( nMode == 2) {
                        string[] parts = tokens[k + 1].Split(this.splitSlash, StringSplitOptions.RemoveEmptyEntries);
                        if ( parts.Length == 2 ) {
                            // "f v1/vt1 v2/vt2 v3/vt3"
                            v[k] = int.Parse(parts[0]);
                            u[k] = int.Parse(parts[1]);

                        } else if ( parts.Length == 3 ) {
                            // "f v1/vt1/vn1 v2/vt2/vn2 v3/vt3/vn3"
                            v[k] = int.Parse(parts[0]);
                            u[k] = int.Parse(parts[1]);
                            n[k] = int.Parse(parts[2]);
                        } else {
                            throw new Exception("OBJReader::parse_triangle unexpected face component " + tokens[k]);
                        }

                    }
                }

            }
        }


    }
}
