using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    public class SimpleMesh : IDeformableMesh
    {
        public DVector<double> Vertices;
        public DVector<float> Normals;
        public DVector<float> Colors;
        public DVector<float> UVs;

        public DVector<int> Triangles;
        public DVector<int> FaceGroups;

        int timestamp = 0;

        public SimpleMesh()
        {
            Initialize();
        }

        //public void CopyTo(SimpleMesh mTo)
        //{
        //    mTo.Vertices = Util.BufferCopy(this.Vertices, mTo.Vertices);
        //    mTo.Normals = Util.BufferCopy(this.Normals, mTo.Normals);
        //    mTo.Colors = Util.BufferCopy(this.Colors, mTo.Colors);
        //    mTo.Triangles = Util.BufferCopy(this.Triangles, mTo.Triangles);
        //    mTo.FaceGroups = Util.BufferCopy(this.FaceGroups, mTo.FaceGroups);
        //}

        //public object Clone()
        //{
        //    SimpleMesh mTo = new SimpleMesh();
        //    this.CopyTo(mTo);
        //    return mTo;
        //}

		public SimpleMesh(IMesh copy) {
			Initialize(copy.HasVertexNormals, copy.HasVertexColors, copy.HasVertexUVs, copy.HasTriangleGroups);
			int[] mapV = new int[copy.MaxVertexID];
			foreach ( int vid in copy.VertexIndices() ) {
				NewVertexInfo vi = copy.GetVertexAll(vid);
				int new_vid = AppendVertex(vi);
				mapV[vid] = new_vid;
			}
			foreach ( int tid in copy.TriangleIndices() ){
				Index3i t = copy.GetTriangle(tid);
				t[0] = mapV[t[0]];
				t[1] = mapV[t[1]];
				t[2] = mapV[t[2]];
				if ( copy.HasTriangleGroups )
					AppendTriangle(t[0],t[1],t[2], copy.GetTriangleGroup(tid));
				else
					AppendTriangle(t[0],t[1],t[2]);
			}
		}


        public void Initialize(bool bWantNormals = true, bool bWantColors = true, bool bWantUVs = true, bool bWantFaceGroups = true)
        {
            Vertices = new DVector<double>();
            Normals = (bWantNormals) ? new DVector<float>() : null;
            Colors = (bWantColors) ? new DVector<float>() : null;
            UVs = (bWantUVs) ? new DVector<float>() : null;
            Triangles = new DVector<int>();
            FaceGroups = (bWantFaceGroups) ? new DVector<int>() : null;
        }

        public void Initialize(VectorArray3d v, VectorArray3i t, 
            VectorArray3f n = null, VectorArray3f c = null, VectorArray2f uv = null, int[] g = null)
        {
            Vertices = new DVector<double>(v);
            Triangles = new DVector<int>(t);
            Normals = Colors = UVs = null;
            FaceGroups = null;
            if ( n != null ) 
                Normals = new DVector<float>(n);
            if (c != null)
                Colors = new DVector<float>(c);
            if (uv != null)
                UVs = new DVector<float>(uv);
            if (g != null)
                FaceGroups = new DVector<int>(g);
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



        /// <summary>
        /// Timestamp is incremented any time any change is made to the mesh
        /// </summary>
        public int Timestamp {
            get { return timestamp; }
        }

        void updateTimeStamp() {
            timestamp++;
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
            updateTimeStamp();
            return i;
        }
        public int AppendVertex(NewVertexInfo info)
        {
            int i = Vertices.Length / 3;

            if (info.bHaveN && HasVertexNormals) {
                Normals.Add(info.n[0]); Normals.Add(info.n[1]); Normals.Add(info.n[2]);
            } else if ( HasVertexNormals ) {
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
            updateTimeStamp();
            return i;
        }

        public void AppendVertices(VectorArray3d v, VectorArray3f n = null, VectorArray3f c = null, VectorArray2f uv = null) {
            Vertices.Add(v.array);
            if (n != null && HasVertexNormals)
                Normals.Add(n.array);
            else if (HasVertexNormals)
                Normals.Add(new float[] { 0, 1, 0 }, v.Count);
            if (c != null && HasVertexColors)
                Colors.Add(c.array);
            else if (HasVertexColors)
                Normals.Add(new float[] { 1, 1, 1 }, v.Count);
            if (uv != null && HasVertexUVs)
                UVs.Add(uv.array);
            else if (HasVertexUVs)
                UVs.Add(new float[] { 0, 0 }, v.Count);
            updateTimeStamp();
        }



        public int AppendTriangle(int i, int j, int k, int g = -1)
        {
            int ti = Triangles.Length / 3;
            if (HasTriangleGroups)
                FaceGroups.Add((g == -1) ? 0 : g);
            Triangles.Add(i); Triangles.Add(j); Triangles.Add(k);
            updateTimeStamp();
            return ti;
        }


        public void AppendTriangles(int[] vTriangles, int[] vertexMap, int g = -1)
        {
            for (int ti = 0; ti < vTriangles.Length; ++ti) {
                Triangles.Add(vertexMap[vTriangles[ti]]);
            }
            if (HasTriangleGroups) {
                for (int ti = 0; ti < vTriangles.Length / 3; ++ti)
                    FaceGroups.Add((g == -1) ? 0 : g);
            }
            updateTimeStamp();
        }

        public void AppendTriangles(IndexArray3i t, int[] groups = null)
        {
            Triangles.Add(t.array);
            if (HasTriangleGroups) {
                if (groups != null)
                    FaceGroups.Add(groups);
                else
                    FaceGroups.Add(0, t.Count);
            }
            updateTimeStamp();
        }


        /*
         * Utility / Convenience
         */

        // [RMS] this is convenience stuff...
        public void Translate(double tx, double ty, double tz)
        {
            int c = VertexCount;
            for (int i = 0; i < c; ++i) {
                this.Vertices[3 * i] += tx;
                this.Vertices[3 * i + 1] += ty;
                this.Vertices[3 * i + 2] += tz;
            }
            updateTimeStamp();
        }
        public void Scale(double sx, double sy, double sz)
        {
            int c = VertexCount;
            for (int i = 0; i < c; ++i) {
                this.Vertices[3 * i] *= sx;
                this.Vertices[3 * i + 1] *= sy;
                this.Vertices[3 * i + 2] *= sz;
            }
            updateTimeStamp();
        }
        public void Scale(double s)
        {
            Scale(s, s, s);
            updateTimeStamp();
        }


        /*
         * IMesh interface
         */


        public int VertexCount
        {
            get { return Vertices.Length / 3; }
        }
        public int TriangleCount
        {
            get { return Triangles.Length / 3; }
        }
		public int MaxVertexID
		{
			get { return VertexCount; }
		}
		public int MaxTriangleID
		{
			get { return TriangleCount; }
		}


        public bool IsVertex(int vID) {
            return vID * 3 < Vertices.Length;
        }
        public bool IsTriangle(int tID) {
            return tID * 3 < Triangles.Length;
        }

        public bool HasVertexColors
        {
            get { return Colors != null && Colors.Length == Vertices.Length; }
        }

        public bool HasVertexNormals
        {
            get { return Normals != null && Normals.Length == Vertices.Length; }
        }

        public bool HasVertexUVs
        {
            get { return UVs != null && UVs.Length/2 == Vertices.Length/3; }
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

		public NewVertexInfo GetVertexAll(int i) {
			NewVertexInfo vi = new NewVertexInfo();
			vi.v = GetVertex(i);
			if ( HasVertexNormals ) {
				vi.bHaveN = true;
				vi.n = GetVertexNormal(i);
			} else
				vi.bHaveN = false;
			if ( HasVertexColors ) {
				vi.bHaveC = true;
				vi.c = GetVertexColor(i);
			} else
				vi.bHaveC = false;
			if ( HasVertexUVs ) {
				vi.bHaveUV = true;
				vi.uv = GetVertexUV(i);
			} else
				vi.bHaveUV = false;
			return vi;
		}


        public bool HasTriangleGroups
        {
            get { return FaceGroups != null && FaceGroups.Length == Triangles.Length / 3; }
        }

        public Index3i GetTriangle(int i)
        {
            return new Index3i(Triangles[3 * i], Triangles[3 * i + 1], Triangles[3 * i + 2]);
        }

        public int GetTriangleGroup(int i)
        {
            return FaceGroups[i];
        }


        public IEnumerable<Vector3d> VerticesItr() {
            int N = VertexCount;
            for ( int i = 0; i < N; ++i )
                yield return new Vector3d(Vertices[3 * i], Vertices[3 * i + 1], Vertices[3 * i + 2]);
        }

        public IEnumerable<Vector3f> NormalsItr() {
            int N = VertexCount;
            for (int i = 0; i < N; ++i)
                yield return new Vector3f(Normals[3 * i], Normals[3 * i + 1], Normals[3 * i + 2]);
        }

        public IEnumerable<Vector3f> ColorsItr() {
            int N = VertexCount;
            for (int i = 0; i < N; ++i)
                yield return new Vector3f(Colors[3 * i], Colors[3 * i + 1], Colors[3 * i + 2]);
        }

        public IEnumerable<Vector2f> UVsItr() {
            int N = VertexCount;
            for (int i = 0; i < N; ++i)
                yield return new Vector2f(UVs[2 * i], UVs[2 * i + 1]);
        }

        public IEnumerable<Index3i> TrianglesItr()
        {
            int N = TriangleCount;
            for (int i = 0; i < N; ++i)
                yield return new Index3i(Triangles[3 * i], Triangles[3 * i + 1], Triangles[3 * i + 2]);
        }

        public IEnumerable<int> TriangleGroupsItr()
        {
            int N = TriangleCount;
            for (int i = 0; i < N; ++i)
                yield return FaceGroups[i];
        }

        public IEnumerable<int> VertexIndices() {
            int N = VertexCount;
            for (int i = 0; i < N; ++i)
                yield return i;
        }
        public IEnumerable<int> TriangleIndices() { 
            int N = TriangleCount;
            for (int i = 0; i < N; ++i)
                yield return i;
        }


        // setters

        public void SetVertex(int i, Vector3d v) {
            Vertices[3 * i] = v.x;
            Vertices[3 * i + 1] = v.y;
            Vertices[3 * i + 2] = v.z;
            updateTimeStamp();
        }

        public void SetVertexNormal(int i, Vector3f n) {
            Normals[3 * i] = n.x;
            Normals[3 * i + 1] = n.y;
            Normals[3 * i + 2] = n.z;
            updateTimeStamp();
        }

        public void SetVertexColor(int i, Vector3f c) {
            Colors[3 * i] = c.x;
            Colors[3 * i + 1] = c.y;
            Colors[3 * i + 2] = c.z;
            updateTimeStamp();
        }

        public void SetVertexUV(int i, Vector2f uv) {
            UVs[2 * i] = uv.x;
            UVs[2 * i + 1] = uv.y;
            updateTimeStamp();
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

        public int[] GetTriangleArray()
        {
            return this.Triangles.GetBuffer();
        }

        public int[] GetFaceGroupsArray()
        {
            return (this.HasTriangleGroups) ? this.FaceGroups.GetBuffer() : null;
        }




        /*
         * copy internal data into buffers. Assumes that buffers are big enough!!
         */

        public unsafe void GetVertexBuffer(double * pBuffer)
        {
            DVector<double>.FastGetBuffer(this.Vertices, pBuffer);
        }

        public unsafe void GetVertexNormalBuffer(float * pBuffer)
        {
            if ( this.HasVertexNormals )
                DVector<float>.FastGetBuffer(this.Normals, pBuffer);
        }

        public unsafe void GetVertexColorBuffer(float* pBuffer)
        {
            if (this.HasVertexColors)
                DVector<float>.FastGetBuffer(this.Colors, pBuffer);
        }

        public unsafe void GetVertexUVBuffer(float* pBuffer)
        {
            if (this.HasVertexUVs)
                DVector<float>.FastGetBuffer(this.UVs, pBuffer);
        }

        public unsafe void GetTriangleBuffer(int* pBuffer)
        {
            DVector<int>.FastGetBuffer(this.Triangles, pBuffer);
        }

        public unsafe void GetFaceGroupsBuffer(int* pBuffer)
        {
            if ( this.HasTriangleGroups)
                DVector<int>.FastGetBuffer(this.FaceGroups, pBuffer);
        }

    }






    public class SimpleMeshBuilder : IMeshBuilder
    {
        public List<SimpleMesh> Meshes;
        public List<GenericMaterial> Materials;
        public List<int> MaterialAssignment;

        int nActiveMesh;

        public SimpleMeshBuilder()
        {
            Meshes = new List<SimpleMesh>();
            Materials = new List<GenericMaterial>();
            MaterialAssignment = new List<int>();
            nActiveMesh = -1;
        }

        public int AppendNewMesh(bool bHaveVtxNormals, bool bHaveVtxColors, bool bHaveVtxUVs, bool bHaveFaceGroups)
        {
            int index = Meshes.Count;
            SimpleMesh m = new SimpleMesh();
            m.Initialize(bHaveVtxNormals, bHaveVtxColors, bHaveVtxUVs, bHaveFaceGroups);
            Meshes.Add(m);
            MaterialAssignment.Add(-1);     // no material is known
            nActiveMesh = index;
            return index;
        }

        public int AppendNewMesh(DMesh3 existingMesh)
        {
            int index = Meshes.Count;
            SimpleMesh m = new SimpleMesh(existingMesh);
            Meshes.Add(m);
            MaterialAssignment.Add(-1);     // no material is known
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
            return Meshes[nActiveMesh].AppendTriangle(i, j, k);
        }

        public int AppendTriangle(int i, int j, int k, int g)
        {
            return Meshes[nActiveMesh].AppendTriangle(i, j, k, g);
        }

        public int AppendVertex(double x, double y, double z)
        {
            return Meshes[nActiveMesh].AppendVertex(x, y, z);
        }
        public int AppendVertex(NewVertexInfo info)
        {
            return Meshes[nActiveMesh].AppendVertex(info);
        }

        public bool SupportsMetaData { get { return false; } }
        public void AppendMetaData(string identifier, object data)
        {
            throw new NotImplementedException("SimpleMeshBuilder: metadata not supported");
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

    }

}
