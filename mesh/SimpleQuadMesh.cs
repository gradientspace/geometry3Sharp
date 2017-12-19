using System;
using System.Collections.Generic;
using System.IO;

namespace g3
{
    /// <summary>
    /// SimpleTriangleMesh but for quads. Data packed into buffers, no dynamics.
    /// Supports Per-Vertex Normals, Colors, UV, and Per-Quad Facegroup.
    /// 
    /// use static WriteOBJ() to save. No loading, for now. 
    /// 
    /// </summary>
    public class SimpleQuadMesh
    {
        public DVector<double> Vertices;
        public DVector<float> Normals;
        public DVector<float> Colors;
        public DVector<float> UVs;

        public DVector<int> Quads;
        public DVector<int> FaceGroups;

        public SimpleQuadMesh()
        {
            Initialize();
        }

        public void Initialize(bool bWantNormals = true, bool bWantColors = true, bool bWantUVs = true, bool bWantFaceGroups = true)
        {
            Vertices = new DVector<double>();
            Normals = (bWantNormals) ? new DVector<float>() : null;
            Colors = (bWantColors) ? new DVector<float>() : null;
            UVs = (bWantUVs) ? new DVector<float>() : null;
            Quads = new DVector<int>();
            FaceGroups = (bWantFaceGroups) ? new DVector<int>() : null;
        }

        public MeshComponents Components {
            get {
                MeshComponents c = 0;
                if (Normals != null) c |= MeshComponents.VertexNormals;
                if (Colors != null) c |= MeshComponents.VertexColors;
                if (UVs != null) c |= MeshComponents.VertexUVs;
                if (FaceGroups != null) c |= MeshComponents.FaceGroups;
                return c;
            }
        }


        /*
         * Construction
         */
        public int AppendVertex(double x, double y, double z)
        {
            int i = Vertices.Length / 3;
            if (HasVertexNormals) {
                Normals.Add(0); Normals.Add(1); Normals.Add(0);
            }
            if (HasVertexColors) {
                Colors.Add(1); Colors.Add(1); Colors.Add(1);
            }
            if (HasVertexUVs) {
                UVs.Add(0); UVs.Add(0);
            }
            Vertices.Add(x); Vertices.Add(y); Vertices.Add(z);
            return i;
        }
        public int AppendVertex(Vector3d v) {
            return AppendVertex(v.x, v.y, v.z);
        }

        public int AppendVertex(NewVertexInfo info)
        {
            int i = Vertices.Length / 3;

            if (info.bHaveN && HasVertexNormals) {
                Normals.Add(info.n[0]); Normals.Add(info.n[1]); Normals.Add(info.n[2]);
            } else if (HasVertexNormals) {
                Normals.Add(0); Normals.Add(1); Normals.Add(0);
            }
            if (info.bHaveC && HasVertexColors) {
                Colors.Add(info.c[0]); Colors.Add(info.c[1]); Colors.Add(info.c[2]);
            } else if (HasVertexColors) {
                Colors.Add(1); Colors.Add(1); Colors.Add(1);
            }
            if (info.bHaveUV && HasVertexUVs) {
                UVs.Add(info.uv[0]); UVs.Add(info.uv[1]);
            } else if (HasVertexUVs) {
                UVs.Add(0); UVs.Add(0);
            }

            Vertices.Add(info.v[0]); Vertices.Add(info.v[1]); Vertices.Add(info.v[2]);
            return i;
        }


        public int AppendQuad(int i, int j, int k, int l, int g = -1)
        {
            int qi = Quads.Length / 4;
            if (HasFaceGroups)
                FaceGroups.Add((g == -1) ? 0 : g);
            Quads.Add(i); Quads.Add(j); Quads.Add(k); Quads.Add(l);
            return qi;
        }


        public int VertexCount {
            get { return Vertices.Length / 3; }
        }
        public int QuadCount {
            get { return Quads.Length / 4; }
        }
        public int MaxVertexID {
            get { return VertexCount; }
        }
        public int MaxQuadID {
            get { return QuadCount; }
        }


        public bool IsVertex(int vID)
        {
            return vID * 3 < Vertices.Length;
        }
        public bool IsQuad(int qID)
        {
            return qID * 4 < Quads.Length;
        }

        public bool HasVertexColors {
            get { return Colors != null && Colors.Length == Vertices.Length; }
        }

        public bool HasVertexNormals {
            get { return Normals != null && Normals.Length == Vertices.Length; }
        }

        public bool HasVertexUVs {
            get { return UVs != null && UVs.Length / 2 == Vertices.Length / 3; }
        }

        public Vector3d GetVertex(int i)
        {
            return new Vector3d(Vertices[3 * i], Vertices[3 * i + 1], Vertices[3 * i + 2]);
        }

        public Vector3f GetVertexNormal(int i)
        {
            return new Vector3f(Normals[3 * i], Normals[3 * i + 1], Normals[3 * i + 2]);
        }

        public Vector3f GetVertexColor(int i)
        {
            return new Vector3f(Colors[3 * i], Colors[3 * i + 1], Colors[3 * i + 2]);
        }

        public Vector2f GetVertexUV(int i)
        {
            return new Vector2f(UVs[2 * i], UVs[2 * i + 1]);
        }

        public NewVertexInfo GetVertexAll(int i)
        {
            NewVertexInfo vi = new NewVertexInfo();
            vi.v = GetVertex(i);
            if (HasVertexNormals) {
                vi.bHaveN = true;
                vi.n = GetVertexNormal(i);
            } else
                vi.bHaveN = false;
            if (HasVertexColors) {
                vi.bHaveC = true;
                vi.c = GetVertexColor(i);
            } else
                vi.bHaveC = false;
            if (HasVertexUVs) {
                vi.bHaveUV = true;
                vi.uv = GetVertexUV(i);
            } else
                vi.bHaveUV = false;
            return vi;
        }


        public bool HasFaceGroups {
            get { return FaceGroups != null && FaceGroups.Length == Quads.Length / 4; }
        }

        public Index4i GetQuad(int i)
        {
            return new Index4i(Quads[4 * i], Quads[4 * i + 1], Quads[4 * i + 2], Quads[4 * i + 3]);
        }

        public int GetFaceGroup(int i)
        {
            return FaceGroups[i];
        }


        public IEnumerable<Vector3d> VerticesItr()
        {
            int N = VertexCount;
            for (int i = 0; i < N; ++i)
                yield return new Vector3d(Vertices[3 * i], Vertices[3 * i + 1], Vertices[3 * i + 2]);
        }

        public IEnumerable<Vector3f> NormalsItr()
        {
            int N = VertexCount;
            for (int i = 0; i < N; ++i)
                yield return new Vector3f(Normals[3 * i], Normals[3 * i + 1], Normals[3 * i + 2]);
        }

        public IEnumerable<Vector3f> ColorsItr()
        {
            int N = VertexCount;
            for (int i = 0; i < N; ++i)
                yield return new Vector3f(Colors[3 * i], Colors[3 * i + 1], Colors[3 * i + 2]);
        }

        public IEnumerable<Vector2f> UVsItr()
        {
            int N = VertexCount;
            for (int i = 0; i < N; ++i)
                yield return new Vector2f(UVs[2 * i], UVs[2 * i + 1]);
        }

        public IEnumerable<Index4i> QuadsItr()
        {
            int N = QuadCount;
            for (int i = 0; i < N; ++i)
                yield return new Index4i(Quads[4 * i], Quads[4 * i + 1], Quads[4 * i + 2], Quads[4 * i + 3]);
        }

        public IEnumerable<int> FaceGroupsItr()
        {
            int N = QuadCount;
            for (int i = 0; i < N; ++i)
                yield return FaceGroups[i];
        }

        public IEnumerable<int> VertexIndices()
        {
            int N = VertexCount;
            for (int i = 0; i < N; ++i)
                yield return i;
        }
        public IEnumerable<int> QuadIndices()
        {
            int N = QuadCount;
            for (int i = 0; i < N; ++i)
                yield return i;
        }


        // setters

        public void SetVertex(int i, Vector3d v)
        {
            Vertices[3 * i] = v.x;
            Vertices[3 * i + 1] = v.y;
            Vertices[3 * i + 2] = v.z;
        }

        public void SetVertexNormal(int i, Vector3f n)
        {
            Normals[3 * i] = n.x;
            Normals[3 * i + 1] = n.y;
            Normals[3 * i + 2] = n.z;
        }

        public void SetVertexColor(int i, Vector3f c)
        {
            Colors[3 * i] = c.x;
            Colors[3 * i + 1] = c.y;
            Colors[3 * i + 2] = c.z;
        }

        public void SetVertexUV(int i, Vector2f uv)
        {
            UVs[2 * i] = uv.x;
            UVs[2 * i + 1] = uv.y;
        }


        /*
         * Array-based access (allocates arrays automatically)
         */
        public double[] GetVertexArray()
        {
            return this.Vertices.GetBuffer();
        }
        public float[] GetVertexArrayFloat()
        {
            float[] buf = new float[this.Vertices.Length];
            for (int i = 0; i < this.Vertices.Length; ++i)
                buf[i] = (float)this.Vertices[i];
            return buf;
        }

        public float[] GetVertexNormalArray()
        {
            return (this.HasVertexNormals) ? this.Normals.GetBuffer() : null;
        }

        public float[] GetVertexColorArray()
        {
            return (this.HasVertexColors) ? this.Colors.GetBuffer() : null;
        }

        public float[] GetVertexUVArray()
        {
            return (this.HasVertexUVs) ? this.UVs.GetBuffer() : null;
        }

        public int[] GetQuadArray()
        {
            return this.Quads.GetBuffer();
        }

        public int[] GetFaceGroupsArray()
        {
            return (this.HasFaceGroups) ? this.FaceGroups.GetBuffer() : null;
        }





        public static IOWriteResult WriteOBJ(SimpleQuadMesh mesh, string sPath, WriteOptions options)
        {
            StreamWriter writer = new StreamWriter(sPath);
            if (writer.BaseStream == null)
                return new IOWriteResult(IOCode.FileAccessError, "Could not open file " + sPath + " for writing");

            bool bVtxColors = options.bPerVertexColors && mesh.HasVertexColors;
            bool bNormals = options.bPerVertexNormals && mesh.HasVertexNormals;

            // use separate UV set if we have it, otherwise write per-vertex UVs if we have those
            bool bVtxUVs = options.bPerVertexUVs && mesh.HasVertexUVs;
            if (mesh.UVs != null)
                bVtxUVs = false;

            int[] mapV = new int[mesh.MaxVertexID];

            int nAccumCountV = 1;       // OBJ indices always start at 1

            // write vertices for this mesh
            foreach (int vi in mesh.VertexIndices()) {
                mapV[vi] = nAccumCountV++;
                Vector3d v = mesh.GetVertex(vi);
                if (bVtxColors) {
                    Vector3d c = mesh.GetVertexColor(vi);
                    writer.WriteLine("v {0} {1} {2} {3:F8} {4:F8} {5:F8}", v[0], v[1], v[2], c[0], c[1], c[2]);
                } else {
                    writer.WriteLine("v {0} {1} {2}", v[0], v[1], v[2]);
                }

                if (bNormals) {
                    Vector3d n = mesh.GetVertexNormal(vi);
                    writer.WriteLine("vn {0:F10} {1:F10} {2:F10}", n[0], n[1], n[2]);
                }

                if (bVtxUVs) {
                    Vector2f uv = mesh.GetVertexUV(vi);
                    writer.WriteLine("vt {0:F10} {1:F10}", uv.x, uv.y);
                }
            }

            foreach (int ti in mesh.QuadIndices()) {
                Index4i q = mesh.GetQuad(ti);
                q[0] = mapV[q[0]];
                q[1] = mapV[q[1]];
                q[2] = mapV[q[2]];
                q[3] = mapV[q[3]];
                write_quad(writer, ref q, bNormals, bVtxUVs, ref q);
            }

            writer.Close();

            return IOWriteResult.Ok;
        }



        // actually write triangle line, in proper OBJ format
        static void write_quad(TextWriter writer, ref Index4i q, bool bNormals, bool bUVs, ref Index4i tuv)
        {
            if (bNormals == false && bUVs == false) {
                writer.WriteLine("f {0} {1} {2} {3}", q[0], q[1], q[2], q[3]);
            } else if (bNormals == true && bUVs == false) {
                writer.WriteLine("f {0}//{0} {1}//{1} {2}//{2} {3}//{3}", q[0], q[1], q[2], q[3]);
            } else if (bNormals == false && bUVs == true) {
                writer.WriteLine("f {0}/{4} {1}/{5} {2}/{6} {3}/{7}", q[0], q[1], q[2], q[3], tuv[0], tuv[1], tuv[2], tuv[3]);
            } else {
                writer.WriteLine("f {0}/{4}/{0} {1}/{5}/{1} {2}/{6}/{2} {3}/{7}/{3}", q[0], q[1], q[2], q[3], tuv[0], tuv[1], tuv[2], tuv[3]);
            }
        }

    }





}



    

