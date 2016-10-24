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
        public int nMaterialID;

        public void clear()
        {
            nMaterialID = -1;
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

		Dictionary<string, OBJMaterial> Materials;
        Dictionary<int, string> UsedMaterials;

        bool m_bOBJHasPerVertexColors;
        int m_nUVComponents;

        private string[] splitDoubleSlash;
        private char[] splitSlash;

        public OBJReader()
        {
            this.splitDoubleSlash = new string[] { "//" };
            this.splitSlash = new char[] { '/' };
            MTLFileSearchPaths = new List<string>();
        }

		// you need to initialize this with paths if you want .MTL files to load
		public List<string> MTLFileSearchPaths { get; set; } 

        // connect to this to get warning messages
		public event ErrorEventHandler warningEvent;



        public bool HasPerVertexColors { get { return m_bOBJHasPerVertexColors; } }
        public int UVDimension{ get { return m_nUVComponents; } }


        public IOReadResult Read(BinaryReader reader, ReadOptions options, IMeshBuilder builder)
        {
            throw new NotImplementedException();
        }

        public IOReadResult Read(TextReader reader, ReadOptions options, IMeshBuilder builder)
        {
            Materials = new Dictionary<string, OBJMaterial>();
            UsedMaterials = new Dictionary<int, string>();

            var parseResult = ParseInput(reader, options);
            if (parseResult.result != ReadResult.Ok)
                return parseResult;

            var buildResult = 
                (UsedMaterials.Count > 1) ?
                    BuildMeshes_ByMaterial(options, builder) : BuildMeshes_Simple(options, builder);
            if (buildResult.result != ReadResult.Ok)
                return buildResult;

            return new IOReadResult(ReadResult.Ok, "");
        }



        int append_vertex(IMeshBuilder builder, int nVtx, bool bHaveNormals, bool bHaveColors )
        {
            // [TODO] support non-per-vertex normals/colors

            int i = 3 * nVtx;
            if (bHaveColors && bHaveNormals) {
                return builder.AppendVertexNC(vPositions[i], vPositions[i + 1], vPositions[i + 2],
                    vNormals[i], vNormals[i + 1], vNormals[i + 2], vColors[i], vColors[i + 1], vColors[i + 2]);
            } else if (bHaveColors) {
                return builder.AppendVertexC(vPositions[i], vPositions[i + 1], vPositions[i + 2],
                   vColors[i], vColors[i + 1], vColors[i + 2]);
            } else if (bHaveNormals) {
                return builder.AppendVertexN(vPositions[i], vPositions[i + 1], vPositions[i + 2],
                   vNormals[i], vNormals[i + 1], vNormals[i + 2]);

            } else {
                return builder.AppendVertex(vPositions[i], vPositions[i + 1], vPositions[i + 2]);
            }
        }

        unsafe int append_triangle(IMeshBuilder builder, int nTri, int[] mapV)
        {
            Triangle t = vTriangles[nTri];
            int v0 = mapV[t.vIndices[0] - 1];
            int v1 = mapV[t.vIndices[1] - 1];
            int v2 = mapV[t.vIndices[2] - 1];
            return builder.AppendTriangle(v0, v1, v2);
        }



        unsafe IOReadResult BuildMeshes_Simple(ReadOptions options, IMeshBuilder builder)
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
            for ( int k = 0; k < nVertices; ++k ) 
                mapV[k] = append_vertex(builder, k, bHaveNormals, bHaveColors);

            // [TODO] this doesn't handle missing vertices...
            for (int k = 0; k < vTriangles.Length; ++k)
                append_triangle(builder, k, mapV);

            return new IOReadResult(ReadResult.Ok, "");
        }






        unsafe IOReadResult BuildMeshes_ByMaterial(ReadOptions options, IMeshBuilder builder)
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


            foreach ( int material_id in UsedMaterials.Keys ) {
                string sMatName = UsedMaterials[material_id];
                OBJMaterial useMat = Materials[sMatName];
                int matID = builder.BuildMaterial( useMat );
                int meshID = builder.AppendNewMesh();

                // reset vtx map
                for (int k = 0; k < nVertices; ++k)
                    mapV[k] = -1;

                for ( int k = 0; k < vTriangles.Length; ++k ) {
                    Triangle t = vTriangles[k];
                    if (t.nMaterialID == material_id) {
                        Triangle t2;
                        for (int j = 0; j < 3; ++j) {
                            int i = t.vIndices[j] - 1;
                            if (mapV[i] == -1)
                                mapV[i] = append_vertex(builder, i, bHaveNormals, bHaveColors);
                            t2.vIndices[j] = mapV[i];
                        }
                        append_triangle(builder, k, mapV);
                    }
                }

                builder.AssignMaterial(matID, meshID);
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
            OBJMaterial activeMaterial = null;

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
                        if (activeMaterial != null) {
                            tri.nMaterialID = activeMaterial.id;
                            UsedMaterials[activeMaterial.id] = activeMaterial.name;
                        }
                        vTriangles.Add(tri);
                    } else {
                        // punt for now...
                    }

                } else if ( tokens[0][0] == 'g' ) {

                } else if ( tokens[0][0] == 'o' ) {

				} else if ( tokens[0] == "mtllib" && options.ReadMaterials ) {
					if ( MTLFileSearchPaths.Count == 0 )
                        emit_warning("Materials requested but Material Search Paths not initialized!");
					string sFile = FindMTLFile(tokens[1]);
                    if (sFile != null) {
                        IOReadResult result = ReadMaterials(sFile);
                        if ( result.result != ReadResult.Ok )
                            emit_warning("error parsing " + sFile + " : " + result.info);
                    } else
                        emit_warning("material file " + sFile + " could not be found in material search paths");

                } else if ( tokens[0] == "usemtl" && options.ReadMaterials ) {
                    string sName = tokens[1];
                    if (Materials.ContainsKey(sName) == false) {
                        emit_warning("unknown material " + sName + " referenced");
                    } else {
                        activeMaterial = Materials[sName];
                    }
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


		string FindMTLFile(string sMTLFilePath) {
			foreach ( string sPath in MTLFileSearchPaths ) {
				string sFullPath = Path.Combine(sPath, sMTLFilePath);
				if ( File.Exists(sFullPath) )
					return sFullPath;
			}
			return null;
		}



		public IOReadResult ReadMaterials(string sPath)
		{
            StreamReader reader;
            try {
                reader = new StreamReader(sPath);
                if (reader.EndOfStream)
                    return new IOReadResult(ReadResult.FileAccessError, "");
            } catch {
                return new IOReadResult(ReadResult.FileAccessError, "");
            }


            OBJMaterial curMaterial = null;

            while (reader.Peek() >= 0) {

                string line = reader.ReadLine();
                string[] tokens = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length == 0)
                    continue;

                if ( tokens[0][0] == '#' ) {
                    continue;
                } else if (tokens[0] == "newmtl") {
                    curMaterial = new OBJMaterial();
                    curMaterial.name = tokens[1];
                    curMaterial.id = Materials.Count;

                    if (Materials.ContainsKey(curMaterial.name))
                        emit_warning("Material file " + sPath + " / material " + curMaterial.name + " : already exists in Material set. Replacing.");

                    Materials[curMaterial.name] = curMaterial;

                } else if (tokens[0] == "Ka") {
                    if (curMaterial != null) curMaterial.Ka = parse_mtl_color(tokens);
                } else if (tokens[0] == "Kd") {
                    if (curMaterial != null) curMaterial.Kd = parse_mtl_color(tokens);
                } else if (tokens[0] == "Ks") {
                    if (curMaterial != null) curMaterial.Ks = parse_mtl_color(tokens);
                } else if (tokens[0] == "Tf") {
                    if (curMaterial != null) curMaterial.Tf = parse_mtl_color(tokens);

                } else if (tokens[0] == "illum") {
                    if (curMaterial != null) curMaterial.illum = int.Parse(tokens[1]);

                } else if (tokens[0] == "d") {
                    if (curMaterial != null) curMaterial.d = Single.Parse(tokens[1]);
                } else if (tokens[0] == "Ns") {
                    if (curMaterial != null) curMaterial.Ns = Single.Parse(tokens[1]);
                } else if (tokens[0] == "sharpness") {
                    if (curMaterial != null) curMaterial.sharpness = Single.Parse(tokens[1]);
                } else if (tokens[0] == "Ni") {
                    if (curMaterial != null) curMaterial.Ni = Single.Parse(tokens[1]);

                } else {
                    emit_warning("unknown material command " + tokens[0]);
                }

            }

            return new IOReadResult(ReadResult.Ok, "ok");
		}


        private Vector3f parse_mtl_color(string[] tokens)
        {
            if ( tokens[1] == "spectral" ) {
                emit_warning("OBJReader::parse_material_color : spectral color not supported!");
                return new Vector3f(1, 0, 0);
            } else if (tokens[1] == "xyz" ) {
                emit_warning("OBJReader::parse_material_color : xyz color not supported!");
                return new Vector3f(1, 0, 0);
            } else {
                float r = float.Parse(tokens[1]);
                float g = float.Parse(tokens[2]);
                float b = float.Parse(tokens[3]);
                return new Vector3f(r, g, b);
            }
        }




        private void emit_warning(string sMessage)
        {
            var e = warningEvent;
            if ( e != null ) 
                e(this, new ErrorEventArgs(new Exception(sMessage)));
        }


    }
}
