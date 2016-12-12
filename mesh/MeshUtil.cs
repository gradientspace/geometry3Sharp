using System;

namespace g3 {
	
	public static class MeshUtil {


		public static Vector3d OneRingCentroid(DMesh3 mesh, int vID) 
		{
			Vector3d vSum = Vector3d.Zero;
			int nCount = 0;
			var nbrs = mesh.GetVtxEdges(vID);
			foreach (int nbr in nbrs) {
				vSum += mesh.GetVertex(nbr);
				nCount++;
			}
			double m = 1.0 / nCount;
			vSum.x *= nCount; vSum.y *= nCount; vSum.z *= nCount;
			return vSum;
		}

		// t in range [0,1]
		public static void UniformSmooth(DMesh3 mesh, int vID, double t) 
		{
			Vector3d v = mesh.GetVertex(vID);
			Vector3d c = OneRingCentroid(mesh, vID);
			mesh.SetVertex( vID, (1-t)*v + (t)*c );
		}

	}
}
