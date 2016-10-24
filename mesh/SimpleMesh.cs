using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    public class SimpleMesh : IMesh
    {
        public DVector<double> Vertices;
        public DVector<float> Normals;
        public DVector<float> Colors;

        public DVector<int> Triangles;
        public DVector<int> FaceGroups;

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


        public void Initialize(bool bWantNormals = true, bool bWantColors = true, bool bWantFaceGroups = true)
        {
            Vertices = new DVector<double>();
            Normals = (bWantNormals) ? new DVector<float>() : null;
            Colors = (bWantColors) ? new DVector<float>() : null;
            Triangles = new DVector<int>();
            FaceGroups = (bWantFaceGroups) ? new DVector<int>() : null;
        }



        public double[] GetVertex(int i)
        {
            return new[] { Vertices[3 * i], Vertices[3 * i + 1], Vertices[3 * i + 2] };
        }
        public float[] GetNormal(int i)
        {
            return new[] { Normals[3 * i], Normals[3 * i + 1], Normals[3 * i + 2] };
        }
        public float[] GetColor(int i)
        {
            return new[] { Colors[3 * i], Colors[3 * i + 1], Colors[3 * i + 2] };
        }

        public int[] GetTriangle(int i)
        {
            return new[] { Triangles[3 * i], Triangles[3 * i + 1], Triangles[3 * i + 2] };
        }
        public int GetFaceGroup(int i)
        {
            return FaceGroups[i];
        }



        /*
         * Construction
         */
        public int AppendVertex(double x, double y, double z)
        {
            int i = Vertices.Length / 3;
            if (HasVertexNormals) {
                Normals.Add(0); Normals.Add(0); Normals.Add(0);
            }
            if (HasVertexColors) {
                Colors.Add(1); Colors.Add(1); Colors.Add(1);
            }
            Vertices.Add(x); Vertices.Add(y); Vertices.Add(z);
            return i;
        }
        public int AppendVertex(double x, double y, double z, float nx, float ny, float nz, float r, float g, float b)
        {
            int vi = Vertices.Length / 3;
            Normals.Add(nx); Normals.Add(ny); Normals.Add(nz);
            Colors.Add(r); Colors.Add(g); Colors.Add(b);
            Vertices.Add(x); Vertices.Add(y); Vertices.Add(z);
            return vi;
        }
        public int AppendTriangle(int i, int j, int k, int g = -1)
        {
            int ti = Triangles.Length / 3;
            if (HasTriangleGroups)
                FaceGroups.Add((g == -1) ? 0 : g);
            Triangles.Add(i); Triangles.Add(j); Triangles.Add(k);
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
        }
        public void Scale(double sx, double sy, double sz)
        {
            int c = VertexCount;
            for (int i = 0; i < c; ++i) {
                this.Vertices[3 * i] *= sx;
                this.Vertices[3 * i + 1] *= sy;
                this.Vertices[3 * i + 2] *= sz;
            }
        }
        public void Scale(double s)
        {
            Scale(s, s, s);
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


        public bool HasVertexColors
        {
            get { return Colors != null && Colors.Length == Vertices.Length; }
        }

        public bool HasVertexNormals
        {
            get { return Normals != null && Normals.Length == Vertices.Length; }
        }

        Vector3d IMesh.GetVertex(int i)
        {
            return new Vector3d(Vertices[3 * i], Vertices[3 * i + 1], Vertices[3 * i + 2]);
        }

        public Vector3d GetVertexNormal(int i)
        {
            return new Vector3d(Normals[3 * i], Normals[3 * i + 1], Normals[3 * i + 2]);
        }

        public Vector3d GetVertexColor(int i)
        {
            return new Vector3d(Colors[3 * i], Colors[3 * i + 1], Colors[3 * i + 2]);
        }

        public bool HasTriangleGroups
        {
            get { return FaceGroups.Length == Triangles.Length / 3; }
        }

        Vector3i IMesh.GetTriangle(int i)
        {
            return new Vector3i(Triangles[3 * i], Triangles[3 * i + 1], Triangles[3 * i + 2]);
        }

        public int GetTriangleGroup(int i)
        {
            return FaceGroups[i];
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


        public SimpleMeshBuilder()
        {
            Meshes = new List<SimpleMesh>();
        }

        public void AppendNewMesh()
        {
            SimpleMesh m = new SimpleMesh();
            m.Initialize();
            Meshes.Add(m);
        }

        public int AppendTriangle(int i, int j, int k)
        {
            return Meshes.Last().AppendTriangle(i, j, k);
        }

        public int AppendTriangle(int i, int j, int k, int g)
        {
            return Meshes.Last().AppendTriangle(i, j, k, g);
        }

        public int AppendVertex(double x, double y, double z)
        {
            return Meshes.Last().AppendVertex(x, y, z);
        }

        public int AppendVertexC(double x, double y, double z, float r, float g, float b)
        {
            return Meshes.Last().AppendVertex(x, y, z, 0,0,0, r, g, b);
        }

        public int AppendVertexN(double x, double y, double z, float nx, float ny, float nz)
        {
            return Meshes.Last().AppendVertex(x, y, z, nx, ny, nz, 1.0f, 1.0f, 1.0f);
        }

        public int AppendVertexNC(double x, double y, double z, float nx, float ny, float nz, float r, float g, float b)
        {
            return Meshes.Last().AppendVertex(x, y, z, nx, ny, nz, r, g, b);
        }
    }

}
