using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace g3
{
	/// <summary>
	/// gradientspace OBJ writer
	/// 
	/// [TODO] if mesh has groups, usemtl lines will not be written (see TODO below)
	/// [TODO] options to preserve vertex and triangle indices
	/// 
	/// </summary>
    public class OBJWriter : IMeshWriter
    {
        // stream-opener. Override to write to something other than a file.
        public Func<string, Stream> OpenStreamF = (sFilename) => {
            return File.Open(sFilename, FileMode.Create);
        };
        public Action<Stream> CloseStreamF = (stream) => {
            stream.Dispose();
        };

        public string GroupNamePrefix = "mmGroup";   // default, compatible w/ meshmixer
        public Func<int, string> GroupNameF = null;  // use this to replace standard group names w/ your own

        public IOWriteResult Write(BinaryWriter writer, List<WriteMesh> vMeshes, WriteOptions options)
        {
            // [RMS] not supported
            throw new NotImplementedException();
        }

        public IOWriteResult Write(TextWriter writer, List<WriteMesh> vMeshes, WriteOptions options)
        {
            if (options.groupNamePrefix != null)
                GroupNamePrefix = options.groupNamePrefix;
            if (options.GroupNameF != null)
                GroupNameF = options.GroupNameF;

            int nAccumCountV = 1;       // OBJ indices always start at 1
            int nAccumCountUV = 1;

			// collect materials
			string sMaterialLib = "";
			int nHaveMaterials = 0;
			if ( options.bWriteMaterials && options.MaterialFilePath.Length > 0 ) {
				List<GenericMaterial> vMaterials = MeshIOUtil.FindUniqueMaterialList(vMeshes);
				IOWriteResult ok = write_materials(vMaterials, options);
				if ( ok.code == IOCode.Ok ) {
					sMaterialLib = Path.GetFileName(options.MaterialFilePath);
					nHaveMaterials = vMeshes.Count;
				}
			}


            if ( options.AsciiHeaderFunc != null)
                writer.WriteLine( options.AsciiHeaderFunc());

			if ( sMaterialLib != "" )
				writer.WriteLine("mtllib {0}", sMaterialLib);

            for (int mi = 0; mi < vMeshes.Count; ++mi) {
                IMesh mesh = vMeshes[mi].Mesh;

                if (options.ProgressFunc != null)
                    options.ProgressFunc(mi, vMeshes.Count);

                bool bVtxColors = options.bPerVertexColors && mesh.HasVertexColors;
                bool bNormals = options.bPerVertexNormals && mesh.HasVertexNormals;

				// use separate UV set if we have it, otherwise write per-vertex UVs if we have those
				bool bVtxUVs = options.bPerVertexUVs && mesh.HasVertexUVs;
				if ( vMeshes[mi].UVs != null )
					bVtxUVs = false;

				int[] mapV = new int[mesh.MaxVertexID];

				// write vertices for this mesh
                foreach ( int vi in mesh.VertexIndices() ) { 
					mapV[vi] = nAccumCountV++;
                    Vector3d v = mesh.GetVertex(vi);
                    if ( bVtxColors ) {
                        Vector3d c = mesh.GetVertexColor(vi);
                        writer.WriteLine("v {0} {1} {2} {3:F8} {4:F8} {5:F8}", v[0], v[1], v[2], c[0],c[1],c[2]);
                    } else {
                        writer.WriteLine("v {0} {1} {2}", v[0], v[1], v[2]);
                    }

					if ( bNormals ) {
                        Vector3d n = mesh.GetVertexNormal(vi);
                        writer.WriteLine("vn {0:F10} {1:F10} {2:F10}", n[0], n[1], n[2]);
                    }

					if ( bVtxUVs ) {
						Vector2f uv = mesh.GetVertexUV(vi);
						writer.WriteLine("vt {0:F10} {1:F10}", uv.x, uv.y);
					}
                }

                // write independent UVs for this mesh, if we have them
				IIndexMap mapUV = (bVtxUVs) ? new IdentityIndexMap() : null;   
                DenseUVMesh uvSet = null;
                if ( vMeshes[mi].UVs != null ) {
                    uvSet = vMeshes[mi].UVs;
                    int nUV = uvSet.UVs.Length;
					IndexMap fullMap = new IndexMap(false, nUV);   // [TODO] do we really need a map here? is just integer shift, no?
                    for (int ui = 0; ui < nUV; ++ui) {
                        writer.WriteLine("vt {0:F8} {1:F8}", uvSet.UVs[ui].x, uvSet.UVs[ui].y);
						fullMap[ui] = nAccumCountUV++;
                    }
					mapUV = fullMap;
                }

				// check if we need to write usemtl lines for this mesh
				bool bWriteMaterials = nHaveMaterials > 0 
                        && vMeshes[mi].TriToMaterialMap != null 
                        && vMeshes[mi].Materials != null ;

				// various ways we can write triangles to minimize state changes...
				// [TODO] support writing materials when mesh has groups!!
                if (options.bWriteGroups && mesh.HasTriangleGroups)
                    write_triangles_bygroup(writer, mesh, mapV, uvSet, mapUV, bNormals);
                else
					write_triangles_flat(writer, vMeshes[mi], mapV, uvSet, mapUV, bNormals, bWriteMaterials);

                if (options.ProgressFunc != null)
                    options.ProgressFunc(mi+1, vMeshes.Count);
            }


            return new IOWriteResult(IOCode.Ok, "");
        }



		// write triangles of mesh with re-ordering to minimize group changes
		// (note: this may mean lots of material changes, depending on mesh...)
		void write_triangles_bygroup(TextWriter writer, IMesh mesh, int[] mapV, DenseUVMesh uvSet, IIndexMap mapUV, bool bNormals)
        {
            // This makes N passes over mesh indices, but doesn't use much extra memory.
            // would there be a faster way? could construct integer-pointer-list during initial
            // scan, this would need O(N) memory but then write is effectively O(N) instead of O(N*k)

            bool bUVs = (mapUV != null);

            HashSet<int> vGroups = new HashSet<int>();
            foreach (int ti in mesh.TriangleIndices())
                vGroups.Add(mesh.GetTriangleGroup(ti));

            List<int> sortedGroups = new List<int>(vGroups);
            sortedGroups.Sort();
            foreach ( int g in sortedGroups ) {
                string group_name = GroupNamePrefix;
                if (GroupNameF != null) {
                    group_name = GroupNameF(g);
                } else {
                    group_name = string.Format("{0}{1}", GroupNamePrefix, g);
                }
                writer.WriteLine("g " + group_name);

                foreach (int ti in mesh.TriangleIndices() ) {
                    if (mesh.GetTriangleGroup(ti) != g)
                        continue;

                    Index3i t = mesh.GetTriangle(ti);
				    t[0] = mapV[t[0]];
				    t[1] = mapV[t[1]];
				    t[2] = mapV[t[2]];

                    if (bUVs) {
						Index3i tuv = (uvSet != null) ? uvSet.TriangleUVs[ti] : t;
                        tuv[0] = mapUV[tuv[0]];
                        tuv[1] = mapUV[tuv[1]];
                        tuv[2] = mapUV[tuv[2]];
                        write_tri(writer, ref t, bNormals, true, ref tuv);
                    } else {
                        write_tri(writer, ref t, bNormals, false, ref t);
                    }

                }
            }
        }


		// sequential write of input mesh triangles. preserves triangle IDs up to constant shift.
		void write_triangles_flat(TextWriter writer, WriteMesh write_mesh, int[] mapV, DenseUVMesh uvSet, IIndexMap mapUV, bool bNormals, bool bMaterials)
        {
            bool bUVs = (mapUV != null);

			int cur_material = -1;

			IMesh mesh = write_mesh.Mesh;
            foreach (int ti in mesh.TriangleIndices() ) { 
				if ( bMaterials )
					set_current_material(writer, ti, write_mesh, ref cur_material);

                Index3i t = mesh.GetTriangle(ti);
				t[0] = mapV[t[0]];
				t[1] = mapV[t[1]];
				t[2] = mapV[t[2]];

                if (bUVs) {
					Index3i tuv = (uvSet != null) ? uvSet.TriangleUVs[ti] : t;
                    tuv[0] = mapUV[tuv[0]];
                    tuv[1] = mapUV[tuv[1]];
                    tuv[2] = mapUV[tuv[2]];
                    write_tri(writer, ref t, bNormals, true, ref tuv);
                } else {
                    write_tri(writer, ref t, bNormals, false, ref t);
                }
            }
        }

		// update material state if necessary
		public void set_current_material(TextWriter writer, int ti, WriteMesh mesh, ref int cur_material) 
		{
			int mi = mesh.TriToMaterialMap[ti];
			if ( mi != cur_material && mi >= 0 && mi < mesh.Materials.Count ) {
				writer.WriteLine("usemtl " + mesh.Materials[mi].name );
				cur_material = mi;
			}
		}


		// actually write triangle line, in proper OBJ format
        void write_tri(TextWriter writer, ref Index3i t, bool bNormals, bool bUVs, ref Index3i tuv)
        {
            if ( bNormals == false && bUVs == false ) {
                writer.WriteLine("f {0} {1} {2}", t[0], t[1], t[2]);
            } else if ( bNormals == true && bUVs == false ) {
                writer.WriteLine("f {0}//{0} {1}//{1} {2}//{2}", t[0], t[1], t[2]);
            } else if ( bNormals == false && bUVs == true ) {
                writer.WriteLine("f {0}/{3} {1}/{4} {2}/{5}", t[0], t[1], t[2], tuv[0], tuv[1], tuv[2]);
            } else {
                writer.WriteLine("f {0}/{3}/{0} {1}/{4}/{1} {2}/{5}/{2}", t[0], t[1], t[2], tuv[0], tuv[1], tuv[2]);
            }
        }



		// write .mtl file
		IOWriteResult write_materials(List<GenericMaterial> vMaterials, WriteOptions options) 
		{
            Stream stream = OpenStreamF(options.MaterialFilePath);
            if (stream == null)
                return new IOWriteResult(IOCode.FileAccessError, "Could not open file " + options.MaterialFilePath + " for writing");


            try { 
                StreamWriter w = new StreamWriter(stream);

			    foreach ( GenericMaterial gmat in vMaterials ) {
				    if ( gmat is OBJMaterial == false )
					    continue;
				    OBJMaterial mat = gmat as OBJMaterial;

				    w.WriteLine("newmtl {0}", mat.name);
				    if ( mat.Ka != GenericMaterial.Invalid )
					    w.WriteLine("Ka {0} {1} {2}", mat.Ka.x, mat.Ka.y, mat.Ka.z);
				    if ( mat.Kd != GenericMaterial.Invalid)
					    w.WriteLine("Kd {0} {1} {2}", mat.Kd.x, mat.Kd.y, mat.Kd.z);
				    if ( mat.Ks != GenericMaterial.Invalid )
					    w.WriteLine("Ks {0} {1} {2}", mat.Ks.x, mat.Ks.y, mat.Ks.z);
				    if ( mat.Ke != GenericMaterial.Invalid )
					    w.WriteLine("Ke {0} {1} {2}", mat.Ke.x, mat.Ke.y, mat.Ke.z);
				    if ( mat.Tf != GenericMaterial.Invalid )
					    w.WriteLine("Tf {0} {1} {2}", mat.Tf.x, mat.Tf.y, mat.Tf.z);
				    if ( mat.d != Single.MinValue )
					    w.WriteLine("d {0}", mat.d);
				    if ( mat.Ns != Single.MinValue )
					    w.WriteLine("Ns {0}", mat.Ns);
				    if ( mat.Ni != Single.MinValue )
					    w.WriteLine("Ni {0}", mat.Ni);
				    if ( mat.sharpness != Single.MinValue )
					    w.WriteLine("sharpness {0}", mat.sharpness);
				    if ( mat.illum != -1 )
					    w.WriteLine("illum {0}", mat.illum);

				    if ( mat.map_Ka != null && mat.map_Ka != "" )
					    w.WriteLine("map_Ka {0}", mat.map_Ka);
				    if ( mat.map_Kd != null && mat.map_Kd != "" )
					    w.WriteLine("map_Kd {0}", mat.map_Kd);
				    if ( mat.map_Ks != null && mat.map_Ks != "" )
					    w.WriteLine("map_Ks {0}", mat.map_Ks);
				    if ( mat.map_Ke != null && mat.map_Ke != "" )
					    w.WriteLine("map_Ke {0}", mat.map_Ke);
				    if ( mat.map_d != null && mat.map_d != "" )
					    w.WriteLine("map_d {0}", mat.map_d);
				    if ( mat.map_Ns != null && mat.map_Ns != "" )
					    w.WriteLine("map_Ns {0}", mat.map_Ns);
				
				    if ( mat.bump != null && mat.bump != "" )
					    w.WriteLine("bump {0}", mat.bump);
				    if ( mat.disp != null && mat.disp != "" )
					    w.WriteLine("disp {0}", mat.disp);				
				    if ( mat.decal != null && mat.decal != "" )
					    w.WriteLine("decal {0}", mat.decal);
				    if ( mat.refl != null && mat.refl != "" )
					    w.WriteLine("refl {0}", mat.refl);
			    }

            } finally {
                CloseStreamF(stream);
            }

			return IOWriteResult.Ok;
		}


    }
}
