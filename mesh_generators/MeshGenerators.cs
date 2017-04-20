using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    abstract public class MeshGenerator
    {
        public VectorArray3d vertices;
        public VectorArray2f uv;
        public VectorArray3f normals;
        public IndexArray3i triangles;
        public int[] groups;

        public bool WantUVs = true;
        public bool WantNormals = true;
        public bool WantGroups = true;

        // "normal" meshes are counter-clockwise. Unity is CW though...
        public bool Clockwise = false;



        abstract public void Generate();


        public void MakeMesh(SimpleMesh m)
        {
            m.AppendVertices(vertices, (WantNormals) ? normals : null, null, (WantUVs) ? uv : null);
            m.AppendTriangles(triangles);
        }
        public void MakeMesh(DMesh3 m)
        {
            int nV = vertices.Count;
            for (int i = 0; i < nV; ++i) {
				NewVertexInfo ni = new NewVertexInfo() { v = vertices[i] };
				if ( WantNormals ) {
					ni.bHaveN = true; 
					ni.n = normals[i];
				}
				if ( WantUVs ) {
					ni.bHaveUV = true;
					ni.uv = uv[i];
				}
                int vID = m.AppendVertex(ni);
                Util.gDevAssert(vID == i);
            }
            int nT = triangles.Count;
            if (WantGroups && groups != null && groups.Length == nT) {
                for (int i = 0; i < nT; ++i)
                    m.AppendTriangle(triangles[i], groups[i]);
            } else {
                for (int i = 0; i < nT; ++i)
                    m.AppendTriangle(triangles[i]);
            }
        }




        public struct CircularSection
        {
            public float Radius;
            public float SectionY;
            public CircularSection(float r, float y)
            {
                Radius = r;
                SectionY = y;
            }
        }


        protected void duplicate_vertex_span(int nStart, int nCount)
        {
            for (int i = 0; i < nCount; ++i) {
                vertices[(nStart + nCount) + i] = vertices[nStart + i];
                normals[(nStart + nCount) + i] = normals[nStart + i];
                uv[(nStart + nCount) + i] = uv[nStart + i];
            }
        }


        protected void append_disc(int Slices, int nCenterV, int nRingStart, bool bClosed, bool bCycle, ref int tri_counter, int groupid = -1)
        {
            int nLast = nRingStart + Slices;
            for (int k = nRingStart; k < nLast - 1; ++k) {
                if (groupid >= 0)
                    groups[tri_counter] = groupid;
                triangles.Set(tri_counter++, k, nCenterV, k + 1, bCycle);
            }
            if (bClosed) {     // close disc if we went all the way
                if (groupid >= 0)
                    groups[tri_counter] = groupid;
                triangles.Set(tri_counter++, nLast - 1, nCenterV, nRingStart, bCycle);
            }
        }

        // assumes order would be [v0,v1,v2,v3], ccw
        protected void append_rectangle(int v0, int v1, int v2, int v3, bool bCycle, ref int tri_counter, int groupid = -1)
        {
            if ( groupid >= 0 )
                groups[tri_counter] = groupid;
            triangles.Set(tri_counter++, v0, v1, v2, bCycle);
            if ( groupid >= 0 )
                groups[tri_counter] = groupid;
            triangles.Set(tri_counter++, v0, v2, v3, bCycle);
        }


        // append "disc" verts/tris between vEnd1 and vEnd2
        protected void append_2d_disc_segment(int iCenter, int iEnd1, int iEnd2, int nSteps,bool bCycle, ref int vtx_counter, ref int tri_counter, int groupid = -1, double force_r = 0)
        {
            Vector3d c = vertices[iCenter];
            Vector3d e0 = vertices[iEnd1];
            Vector3d e1 = vertices[iEnd2];
            Vector3d v0 = (e0 - c);
            double r0 = v0.Normalize();
            if (force_r > 0)
                r0 = force_r;
            double tStart = Math.Atan2(v0.z, v0.x);
            Vector3d v1 = (e1 - c);
            double r1 = v1.Normalize();
            if (force_r > 0)
                r1 = force_r;
            double tEnd = Math.Atan2(v1.z, v1.x);

            // fix angles to handle sign. **THIS ONLY WORKS IF WE ARE GOING CCW!!**
            if (tStart < 0)
                tStart += MathUtil.TwoPI;
            if (tEnd < 0)
                tEnd += MathUtil.TwoPI;
            if (tEnd < tStart)
                tEnd += MathUtil.TwoPI;

            int iPrev = iEnd1;
            for ( int i = 0; i < nSteps; ++i ) {
                double t = (double)(i+1) / (double)(nSteps + 1);
                double angle = (1 - t) * tStart + (t) * tEnd;
                Vector3d pos = c + new Vector3d(r0 * Math.Cos(angle), 0, r1 * Math.Sin(angle));
                vertices.Set(vtx_counter, pos.x, pos.y, pos.z);
                if (groupid >= 0)
                    groups[tri_counter] = groupid;
                triangles.Set(tri_counter++, iCenter, iPrev, vtx_counter, bCycle);
                iPrev = vtx_counter++;
            }
            if (groupid >= 0)
                groups[tri_counter] = groupid;
            triangles.Set(tri_counter++, iCenter, iPrev, iEnd2, bCycle);
        }

        protected Vector3f estimate_normal(int v0, int v1, int v2)
        {
            Vector3d a = vertices[v0];
            Vector3d b = vertices[v1];
            Vector3d c = vertices[v2];
            Vector3d e1 = (b - a).Normalized;
            Vector3d e2 = (c - a).Normalized;
            return new Vector3f(e1.Cross(e2));
        }
    }








}
