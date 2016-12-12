using System;

namespace g3 {
	
	public static class MeshUtil {


		public static Vector3d OneRingCentroid(DMesh3 mesh, int vID) 
		{
			Vector3d vSum = Vector3d.Zero;
			int nCount = 0;
			foreach ( int nbr in mesh.VtxVerticesItr(vID) ) {
				vSum += mesh.GetVertex(nbr);
				nCount++;
			}
			double m = 1.0 / nCount;
			vSum.x *= m; vSum.y *= m; vSum.z *= m;
			return vSum;
		}

		// t in range [0,1]
		public static Vector3d UniformSmooth(DMesh3 mesh, int vID, double t) 
		{
			Vector3d v = mesh.GetVertex(vID);
			Vector3d c = OneRingCentroid(mesh, vID);
			return (1-t)*v + (t)*c;
		}


		public static void ScaleMesh(DMesh3 mesh, Frame3f f, Vector3f vScale) {
			foreach ( int vid in mesh.VertexIndices() ) {
				Vector3d v = mesh.GetVertex(vid);
				Vector3f vScaledInF = f.ToFrameP((Vector3f)v) * vScale;
				Vector3d vNew = f.FromFrameP(vScaledInF);
				mesh.SetVertex(vid, vNew);

				// TODO: normals
			}
		}


	}
}
