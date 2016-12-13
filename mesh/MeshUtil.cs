using System;
using System.Collections.Generic;

namespace g3 {
	
	public static class MeshUtil {





		// t in range [0,1]
		public static Vector3d UniformSmooth(DMesh3 mesh, int vID, double t) 
		{
			Vector3d v = mesh.GetVertex(vID);
			Vector3d c = MeshWeights.OneRingCentroid(mesh, vID);
			return (1-t)*v + (t)*c;
		}

		// t in range [0,1]
		public static Vector3d MeanValueSmooth(DMesh3 mesh, int vID, double t) 
		{
			Vector3d v = mesh.GetVertex(vID);
			Vector3d c = MeshWeights.MeanValueCentroid(mesh, vID);
			return (1-t)*v + (t)*c;
		}

		// t in range [0,1]
		public static Vector3d CotanSmooth(DMesh3 mesh, int vID, double t) 
		{
			Vector3d v = mesh.GetVertex(vID);
			Vector3d c = MeshWeights.CotanCentroid(mesh, vID);
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
