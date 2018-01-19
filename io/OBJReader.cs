using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace g3
{

	/// <summary>
	/// gradientspace OBJ mesh format parser
	/// 
	/// Basic structure is:
	///   1) parse OBJ into internal data structures that represent OBJ exactly
	///   2) convert to mesh objects based on options/etc
	/// 
	/// [TODO] major current limitation is that we do not support multiple UVs per-vertex
	///   (this is a limitation of DMesh3). Similarly no multiple normals per-vertex. So,
	///   in the current code, such vertices are duplicated. See append_vertex() and use
	///   of Index3i triplet (vi,ni,ui) to represent vertex in BuildMeshes_X() functions
	/// 
	/// [TODO] only a single material can be assigned to a mesh, this is a current limitation
	///   of DMesh3Builder. So, in this case we are splitting the input mesh by material, IE
	///   multiple meshes are returned for a single input mesh, each with one material.
	/// 
	/// 
	/// </summary>


    internal struct Triangle
    {
        public const int InvalidMaterialID = -1;
        public const int InvalidGroupID = -1;

        public Index3i vIndices;
        public Index3i vNormals;
        public Index3i vUVs;
        public int nMaterialID;
        public int nGroupID;

        public void clear()
        {
            nMaterialID = InvalidMaterialID;
            nGroupID = InvalidGroupID;
            vIndices = vNormals = vUVs = new Index3i(-1, -1, -1);
        }

        public void set_vertex(int j, int vi, int ni = -1, int ui = -1)
        {
            vIndices[j] = vi;
            if (ni != -1) vNormals[j] = ni;
            if (ui != -1) vUVs[j] = ui;
        }

        public void move_vertex(int jFrom, int jTo)
        {
            vIndices[jTo] = vIndices[jFrom];
            vNormals[jTo] = vNormals[jFrom];
            vUVs[jTo] = vUVs[jFrom];
        }

        public bool is_complex()
        {
            for ( int j = 0; j < 3; ++j ) {
                if (vNormals[j] != -1 && vNormals[j] != vNormals[j])
                    return true;
                if (vUVs[j] != -1 && vUVs[j] != vUVs[j])
                    return true;
            }
            return false;
        }
    }





    public class OBJReader : IMeshReader
    {
        DVector<double> vPositions;
        DVector<float> vNormals;
        DVector<float> vUVs;
        DVector<float> vColors;
        DVector<Triangle> vTriangles;

		Dictionary<string, OBJMaterial> Materials;
        Dictionary<int, string> UsedMaterials;

        bool m_bOBJHasPerVertexColors;
        int m_nUVComponents;

        bool m_bOBJHasTriangleGroups;
        int m_nSetInvalidGroupsTo;

        private string[] splitDoubleSlash;
        private char[] splitSlash;

        int nWarningLevel = 0;      // 0 == no diagnostics, 1 == basic, 2 == crazy
        Dictionary<string, int> warningCount = new Dictionary<string, int>();

        public OBJReader()
        {
            this.splitDoubleSlash = new string[] { "//" };
            this.splitSlash = new char[] { '/' };
            MTLFileSearchPaths = new List<string>();
        }

		// you need to initialize this with paths if you want .MTL files to load
		public List<string> MTLFileSearchPaths { get; set; } 

        // connect to this to get warning messages
		public event ParsingMessagesHandler warningEvent;


        public bool HasPerVertexColors { get { return m_bOBJHasPerVertexColors; } }
        public int UVDimension{ get { return m_nUVComponents; } }

        public bool HasTriangleGroups { get { return m_bOBJHasTriangleGroups; } }


        // if this is true, means during parsing we found vertices of faces that
        //  had different indices for vtx/normal/uv
        public bool HasComplexVertices { get; set; }


        public IOReadResult Read(BinaryReader reader, ReadOptions options, IMeshBuilder builder)
        {
            throw new NotImplementedException();
        }

        public IOReadResult Read(TextReader reader, ReadOptions options, IMeshBuilder builder)
        {
            Materials = new Dictionary<string, OBJMaterial>();
            UsedMaterials = new Dictionary<int, string>();
            HasComplexVertices = false;

            if (nWarningLevel >= 1)
                emit_warning("[OBJReader] starting parse");

            var parseResult = ParseInput(reader, options);
            if (parseResult.code != IOCode.Ok)
                return parseResult;

            if (nWarningLevel >= 1)
                emit_warning("[OBJReader] completed parse. building.");

            var buildResult = 
                (UsedMaterials.Count > 1 || HasComplexVertices) ?
                    BuildMeshes_ByMaterial(options, builder) : BuildMeshes_Simple(options, builder);

            if (nWarningLevel >= 1)
                emit_warning("[OBJReader] build complete.");

            if (buildResult.code != IOCode.Ok)
                return buildResult;

            return new IOReadResult(IOCode.Ok, "");
        }







		int append_vertex(IMeshBuilder builder, Index3i vertIdx, bool bHaveNormals, bool bHaveColors, bool bHaveUVs )
        {
            int vi = 3 * vertIdx.a;
            if ( vertIdx.a < 0 || vertIdx.a >= vPositions.Length/3 ) {
                emit_warning("[OBJReader] append_vertex() referencing invalid vertex " + vertIdx.a.ToString());
                return -1;
            }

            if ( bHaveNormals == false && bHaveColors == false && bHaveUVs == false )
                return builder.AppendVertex(vPositions[vi], vPositions[vi + 1], vPositions[vi + 2]);

            NewVertexInfo vinfo = new NewVertexInfo();
            vinfo.bHaveC = vinfo.bHaveN = vinfo.bHaveUV = false;
            vinfo.v = new Vector3d(vPositions[vi], vPositions[vi + 1], vPositions[vi + 2]);
            if ( bHaveNormals ) {
                vinfo.bHaveN = true;
                int ni = 3 * vertIdx.b;
                vinfo.n = new Vector3f(vNormals[ni], vNormals[ni + 1], vNormals[ni + 2]);
            }
            if ( bHaveColors ) {
                vinfo.bHaveC = true;
                vinfo.c = new Vector3f(vColors[vi], vColors[vi + 1], vColors[vi + 2]);
            }
            if ( bHaveUVs ) {
                vinfo.bHaveUV = true;
                int ui = 2 * vertIdx.c;
                vinfo.uv = new Vector2f(vUVs[ui], vUVs[ui + 1]);
            }

            return builder.AppendVertex(vinfo);
        }



        int append_triangle(IMeshBuilder builder, int nTri, int[] mapV)
        {
            Triangle t = vTriangles[nTri];
            int v0 = mapV[t.vIndices[0] - 1];
            int v1 = mapV[t.vIndices[1] - 1];
            int v2 = mapV[t.vIndices[2] - 1];
            if ( v0 == -1 || v1 == -1 || v2 == -1 ) {
                emit_warning(string.Format("[OBJReader] invalid triangle:  {0} {1} {2}  mapped to {3} {4} {5}",
                    t.vIndices[0], t.vIndices[1], t.vIndices[2], v0, v1, v2));
                return -1;
            }
            int gid = (vTriangles[nTri].nGroupID == Triangle.InvalidGroupID) ? 
                m_nSetInvalidGroupsTo : vTriangles[nTri].nGroupID;
            return builder.AppendTriangle(v0, v1, v2, gid);
        }
        int append_triangle(IMeshBuilder builder, Triangle t)
        {
            if ( t.vIndices[0] < 0 || t.vIndices[1] < 0 || t.vIndices[2] < 0 ) {
                emit_warning(string.Format("[OBJReader] invalid triangle:  {0} {1} {2}",
                    t.vIndices[0], t.vIndices[1], t.vIndices[2]));
                return -1;
            }
            int gid = (t.nGroupID == Triangle.InvalidGroupID) ? 
                m_nSetInvalidGroupsTo : t.nGroupID;
            return builder.AppendTriangle(t.vIndices[0], t.vIndices[1], t.vIndices[2], gid);
        }


        IOReadResult BuildMeshes_Simple(ReadOptions options, IMeshBuilder builder)
        {
            if (vPositions.Length == 0)
                return new IOReadResult(IOCode.GarbageDataError, "No vertices in file");
            if (vTriangles.Length == 0)
                return new IOReadResult(IOCode.GarbageDataError, "No triangles in file");

            // [TODO] support non-per-vertex normals/colors
            bool bHaveNormals = (vNormals.Length == vPositions.Length);
            bool bHaveColors = (vColors.Length == vPositions.Length);
            bool bHaveUVs = (vUVs.Length/2 == vPositions.Length/3);

            int nVertices = vPositions.Length / 3;
            int[] mapV = new int[nVertices];

            int meshID = builder.AppendNewMesh(bHaveNormals, bHaveColors, bHaveUVs, m_bOBJHasTriangleGroups);
            for (int k = 0; k < nVertices; ++k) {
				Index3i vk = new Index3i(k,k,k);
                mapV[k] = append_vertex(builder, vk, bHaveNormals, bHaveColors, bHaveUVs);
            }

            // [TODO] this doesn't handle missing vertices...
            for (int k = 0; k < vTriangles.Length; ++k)
                append_triangle(builder, k, mapV);

            if ( UsedMaterials.Count == 1 ) {       // [RMS] should not be in here otherwise
                int material_id = UsedMaterials.Keys.First();
                string sMatName = UsedMaterials[material_id];
                OBJMaterial useMat = Materials[sMatName];
                int matID = builder.BuildMaterial(useMat);
                builder.AssignMaterial(matID, meshID);
            }

            return new IOReadResult(IOCode.Ok, "");
        }






        IOReadResult BuildMeshes_ByMaterial(ReadOptions options, IMeshBuilder builder)
        {
            if (vPositions.Length == 0)
                return new IOReadResult(IOCode.GarbageDataError, "No vertices in file");
            if (vTriangles.Length == 0)
                return new IOReadResult(IOCode.GarbageDataError, "No triangles in file");

            bool bHaveNormals = (vNormals.Length > 0);
            bool bHaveColors = (vColors.Length > 0);
            bool bHaveUVs = (vUVs.Length > 0);

            List<int> usedMaterialIDs = new List<int>(UsedMaterials.Keys);
            usedMaterialIDs.Add(Triangle.InvalidMaterialID);
            foreach ( int material_id in usedMaterialIDs) {
                int matID = Triangle.InvalidMaterialID;
                if (material_id != Triangle.InvalidMaterialID) {
                    string sMatName = UsedMaterials[material_id];
                    OBJMaterial useMat = Materials[sMatName];
                    matID = builder.BuildMaterial(useMat);
                }
                bool bMatHaveUVs = (material_id == Triangle.InvalidMaterialID) ? false : bHaveUVs;

				// don't append mesh until we actually see triangles
				int meshID = -1;

                Dictionary<Index3i, int> mapV = new Dictionary<Index3i, int>();

                for ( int k = 0; k < vTriangles.Length; ++k ) {

                    Triangle t = vTriangles[k];
                    if (t.nMaterialID == material_id) {

						if ( meshID == -1 )
							meshID = builder.AppendNewMesh(bHaveNormals, bHaveColors, bMatHaveUVs, false);
						
                        Triangle t2 = new Triangle();
                        for (int j = 0; j < 3; ++j) {
                            Index3i vk = new Index3i(
								t.vIndices[j] - 1, t.vNormals[j] - 1, t.vUVs[j] - 1 );

                            int use_vtx = -1;
                            if (mapV.ContainsKey(vk) == false) {
                                use_vtx = append_vertex(builder, vk, bHaveNormals, bHaveColors, bMatHaveUVs);
                                mapV[vk] = use_vtx;
                            } else
                                use_vtx = mapV[vk];

                            t2.vIndices[j] = use_vtx;
                        }
                        append_triangle(builder, t2);
                    }
                }

                if ( matID != Triangle.InvalidMaterialID )
                    builder.AssignMaterial(matID, meshID);
            }

            return new IOReadResult(IOCode.Ok, "");
        }





        public IOReadResult ParseInput(TextReader reader, ReadOptions options)
        {
            vPositions = new DVector<double>();
            vNormals = new DVector<float>();
            vUVs = new DVector<float>();
            vColors = new DVector<float>();
            vTriangles = new DVector<Triangle>();

            bool bVerticesHaveColors = false;
            int nMaxUVLength = 0;
            OBJMaterial activeMaterial = null;

            Dictionary<string, int> GroupNames = new Dictionary<string, int>();
            int nGroupCounter = 0;
            int nActiveGroup = Triangle.InvalidGroupID;

            int nLines = 0;
            while (reader.Peek() >= 0) {

                string line = reader.ReadLine();
                nLines++;
                string[] tokens = line.Split( (char[])null , StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length == 0)
                    continue;

                // [RMS] this will hang VS on large models...
                //if (nWarningLevel >= 2)
                //    emit_warning("Parsing line " + line);
                try {

                    if (tokens[0][0] == 'v') {
                        if (tokens[0].Length == 1) {
                            if (tokens.Length == 7) {
                                vPositions.Add(Double.Parse(tokens[1]));
                                vPositions.Add(Double.Parse(tokens[2]));
                                vPositions.Add(Double.Parse(tokens[3]));

                                vColors.Add(float.Parse(tokens[4]));
                                vColors.Add(float.Parse(tokens[5]));
                                vColors.Add(float.Parse(tokens[6]));
                                bVerticesHaveColors = true;
                            } else if (tokens.Length >= 4) {
                                vPositions.Add(Double.Parse(tokens[1]));
                                vPositions.Add(Double.Parse(tokens[2]));
                                vPositions.Add(Double.Parse(tokens[3]));

                            } 
                            if ( tokens.Length != 4 && tokens.Length != 7)
                                emit_warning("[OBJReader] vertex has unknown format: " + line);

                        } else if (tokens[0][1] == 'n') {
                            if (tokens.Length >= 4) {
                                vNormals.Add(float.Parse(tokens[1]));
                                vNormals.Add(float.Parse(tokens[2]));
                                vNormals.Add(float.Parse(tokens[3]));
                            } 
                            if (tokens.Length != 4)
                                emit_warning("[OBJReader] normal has more than 3 coordinates: " + line);

                        } else if (tokens[0][1] == 't') {
                            if (tokens.Length >= 3) {
                                vUVs.Add(float.Parse(tokens[1]));
                                vUVs.Add(float.Parse(tokens[2]));
                                nMaxUVLength = Math.Max(nMaxUVLength, tokens.Length);
                            } 
                            if ( tokens.Length != 3 )
                                emit_warning("[OBJReader] UV has unknown format: " + line);
                        }


                    } else if (tokens[0][0] == 'f') {
                        if ( tokens.Length < 4 ) {
                            emit_warning("[OBJReader] degenerate face specified : " + line);
                        } else if (tokens.Length == 4) {
                            Triangle tri = new Triangle();
                            parse_triangle(tokens, ref tri);

                            tri.nGroupID = nActiveGroup;

                            if (activeMaterial != null) {
                                tri.nMaterialID = activeMaterial.id;
                                UsedMaterials[activeMaterial.id] = activeMaterial.name;
                            }

                            vTriangles.Add(tri);
                            if (tri.is_complex())
                                HasComplexVertices = true;

                        } else {
                            append_face(tokens, activeMaterial, nActiveGroup);
                        }

                    } else if (tokens[0][0] == 'g') {
                        string sGroupName = (tokens.Length == 2) ? tokens[1] : line.Substring( line.IndexOf(tokens[1]) );
                        if ( GroupNames.ContainsKey(sGroupName) ) {
                            nActiveGroup = GroupNames[sGroupName];
                        } else {
                            nActiveGroup = nGroupCounter;
                            GroupNames[sGroupName] = nGroupCounter++;
                        }

                    } else if (tokens[0][0] == 'o') {
                        // TODO multi-object support

                    } else if (tokens[0] == "mtllib" && options.ReadMaterials) {
                        if (MTLFileSearchPaths.Count == 0)
                            emit_warning("Materials requested but Material Search Paths not initialized!");
						string sMTLPathString = (tokens.Length == 2) ? tokens[1] :
							line.Substring( line.IndexOf(tokens[1]) );
						string sFile = FindMTLFile(sMTLPathString);
                        if (sFile != null) {
                            IOReadResult result = ReadMaterials(sFile);
                            if (result.code != IOCode.Ok)
                                emit_warning("error parsing " + sFile + " : " + result.message);
                        } else
							emit_warning("material file " + sMTLPathString + " could not be found in material search paths");

                    } else if (tokens[0] == "usemtl" && options.ReadMaterials) {
                        activeMaterial = find_material(tokens[1]);
                    }

                } catch (Exception e) {
                    emit_warning("error parsing line " + nLines.ToString() + ": " + line + ", exception " + e.Message);

                }

            }

            m_bOBJHasPerVertexColors = bVerticesHaveColors;
            m_bOBJHasTriangleGroups = (nActiveGroup != Triangle.InvalidGroupID);
            m_nSetInvalidGroupsTo = nGroupCounter++;
            m_nUVComponents = nMaxUVLength;

            return new IOReadResult(IOCode.Ok, "");
        }


        private int parse_v(string sToken)
        {
            int vi = int.Parse(sToken);
            if (vi < 0)
                vi = (vPositions.Length / 3) + vi + 1;
            return vi;
        }
        private int parse_n(string sToken)
        {
            int vi = int.Parse(sToken);
            if (vi < 0)
                vi = (vNormals.Length / 3) + vi + 1;
            return vi;
        }
        private int parse_u(string sToken)
        {
            int vi = int.Parse(sToken);
            if (vi < 0)
                vi = (vUVs.Length / 2) + vi + 1;
            return vi;
        }

        private void append_face(string[] tokens, OBJMaterial activeMaterial, int nActiveGroup)
        {
            int nMode = 0;
            if (tokens[1].IndexOf("//") != -1)
                nMode = 1;
            else if (tokens[1].IndexOf('/') != -1)
                nMode = 2;

            Triangle t = new Triangle();
            t.clear();
            for ( int ti = 0; ti < tokens.Length-1; ++ti) {
                int j = (ti < 3) ? ti : 2;
                if (ti >= 3)
                    t.move_vertex(2, 1);

                // parse next vertex
                if (nMode == 0) {
                    // "f v1 v2 v3"
                    t.set_vertex(j, parse_v(tokens[ti + 1]));

                } else if (nMode == 1) {
                    // "f v1//vn1 v2//vn2 v3//vn3"
                    string[] parts = tokens[ti + 1].Split(this.splitDoubleSlash, StringSplitOptions.RemoveEmptyEntries);
                    t.set_vertex(j, parse_v(parts[0]), parse_n(parts[1]));

                } else if (nMode == 2) {
                    string[] parts = tokens[ti + 1].Split(this.splitSlash, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2) {
                        // "f v1/vt1 v2/vt2 v3/vt3"
                        t.set_vertex(j, parse_v(parts[0]), -1, parse_u(parts[1]));
                    } else if (parts.Length == 3) {
                        // "f v1/vt1/vn1 v2/vt2/vn2 v3/vt3/vn3"
                        t.set_vertex(j, parse_v(parts[0]), parse_n(parts[2]), parse_u(parts[1]));
                    } else {
                        emit_warning("parse_triangle unexpected face component " + tokens[j]);
                    }
                }


                // do append
                if (ti >= 2) {
                    if (activeMaterial != null) {
                        t.nMaterialID = activeMaterial.id;
                        UsedMaterials[activeMaterial.id] = activeMaterial.name;
                    }
                    t.nGroupID = nActiveGroup;
                    vTriangles.Add(t);
                    if (t.is_complex())
                        HasComplexVertices = true;
                }
            }
        }

        private void parse_triangle(string[] tokens, ref Triangle t ){
            int nMode = 0;
            if (tokens[1].IndexOf("//") != -1)
                nMode = 1;
            else if (tokens[1].IndexOf('/') != -1)
                nMode = 2;

            t.clear();

            for (int j = 0; j < 3; ++j) {
                if (nMode == 0) {
                    // "f v1 v2 v3"
                    t.set_vertex(j, parse_v(tokens[j + 1]));

                } else if (nMode == 1) {
                    // "f v1//vn1 v2//vn2 v3//vn3"
                    string[] parts = tokens[j + 1].Split(this.splitDoubleSlash, StringSplitOptions.RemoveEmptyEntries);
                    t.set_vertex(j, parse_v(parts[0]), parse_n(parts[1]));

                } else if (nMode == 2) {
                    string[] parts = tokens[j + 1].Split(this.splitSlash, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2) {
                        // "f v1/vt1 v2/vt2 v3/vt3"
                        t.set_vertex(j, parse_v(parts[0]), -1, parse_u(parts[1]));
                    } else if (parts.Length == 3) {
                        // "f v1/vt1/vn1 v2/vt2/vn2 v3/vt3/vn3"
                        t.set_vertex(j, parse_v(parts[0]), parse_n(parts[2]), parse_u(parts[1]));
                    } else {
                        emit_warning("parse_triangle unexpected face component " + tokens[j]);
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
            if (nWarningLevel >= 1)
                emit_warning("[OBJReader] ReadMaterials " + sPath);

            StreamReader reader;
            try {
                reader = new StreamReader(sPath);
                if (reader.EndOfStream)
                    return new IOReadResult(IOCode.FileAccessError, "");
            } catch {
                return new IOReadResult(IOCode.FileAccessError, "");
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
                    if (nWarningLevel >= 1)
                        emit_warning("[OBJReader] parsing material " + curMaterial.name);

                    Materials[curMaterial.name] = curMaterial;

                } else if (tokens[0] == "Ka") {
                    if (curMaterial != null) curMaterial.Ka = parse_mtl_color(tokens);
                } else if (tokens[0] == "Kd") {
                    if (curMaterial != null) curMaterial.Kd = parse_mtl_color(tokens);
                } else if (tokens[0] == "Ks") {
                    if (curMaterial != null) curMaterial.Ks = parse_mtl_color(tokens);
                } else if (tokens[0] == "Ke") {
                    if (curMaterial != null) curMaterial.Ke = parse_mtl_color(tokens);
                } else if (tokens[0] == "Tf") {
                    if (curMaterial != null) curMaterial.Tf = parse_mtl_color(tokens);

                } else if (tokens[0] == "illum") {
                    if (curMaterial != null) curMaterial.illum = int.Parse(tokens[1]);

                } else if (tokens[0] == "d") {
                    if (curMaterial != null) curMaterial.d = Single.Parse(tokens[1]);
                } else if (tokens[0] == "Tr") {     // alternate to d/alpha, [Tr]ansparency is 1-d
                    if (curMaterial != null) curMaterial.d = 1.0f - Single.Parse(tokens[1]);
                } else if (tokens[0] == "Ns") {
                    if (curMaterial != null) curMaterial.Ns = Single.Parse(tokens[1]);
                } else if (tokens[0] == "sharpness") {
                    if (curMaterial != null) curMaterial.sharpness = Single.Parse(tokens[1]);
                } else if (tokens[0] == "Ni") {
                    if (curMaterial != null) curMaterial.Ni = Single.Parse(tokens[1]);

                } else if (tokens[0] == "map_Ka") {
					if (curMaterial != null) curMaterial.map_Ka = parse_mtl_path(line,tokens);
                } else if (tokens[0] == "map_Kd") {
					if (curMaterial != null) curMaterial.map_Kd = parse_mtl_path(line,tokens);
                } else if (tokens[0] == "map_Ks") {
					if (curMaterial != null) curMaterial.map_Ks = parse_mtl_path(line,tokens);
                } else if (tokens[0] == "map_Ke") {
					if (curMaterial != null) curMaterial.map_Ke = parse_mtl_path(line,tokens);
                } else if (tokens[0] == "map_d") {
					if (curMaterial != null) curMaterial.map_d = parse_mtl_path(line,tokens);
                } else if (tokens[0] == "map_Ns") {
					if (curMaterial != null) curMaterial.map_Ns = parse_mtl_path(line,tokens);

                } else if (tokens[0] == "bump" || tokens[0] == "map_bump") {
					if (curMaterial != null) curMaterial.bump = parse_mtl_path(line,tokens);
                } else if (tokens[0] == "disp") {
					if (curMaterial != null) curMaterial.disp = parse_mtl_path(line,tokens);
                } else if (tokens[0] == "decal") {
					if (curMaterial != null) curMaterial.decal = parse_mtl_path(line,tokens);
                } else if (tokens[0] == "refl") {
					if (curMaterial != null) curMaterial.refl = parse_mtl_path(line,tokens);
                } else {
                    emit_warning("unknown material command " + tokens[0]);
                }

            }

            if (nWarningLevel >= 1)
                emit_warning("[OBJReader] ReadMaterials completed");

            return new IOReadResult(IOCode.Ok, "ok");
		}


		private string parse_mtl_path(string line, string[] tokens) {
			if ( tokens.Length == 2 )
				return tokens[1];
			else
				return line.Substring( line.IndexOf(tokens[1]) );
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



        private OBJMaterial find_material(string sName)
        {
            if (Materials.ContainsKey(sName))
                return Materials[sName];

            // try case-insensitive search
            try {
                return Materials.First(x => String.Equals(x.Key, sName, StringComparison.OrdinalIgnoreCase)).Value;
            } catch {
                // didn't work
            }

            emit_warning("unknown material " + sName + " referenced");
            return null;
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
                e(sMessage, null);
        }


    }
}
