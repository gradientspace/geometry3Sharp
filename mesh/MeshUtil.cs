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




        public static double OpeningAngleD(DMesh3 mesh, int eid)
        {
            Index2i et = mesh.GetEdgeT(eid);
            if (et[1] == DMesh3.InvalidID)
                return double.MaxValue;     // boundary edge!!

            Vector3d n0 = mesh.GetTriNormal(et[0]);
            Vector3d n1 = mesh.GetTriNormal(et[1]);
            return Vector3d.AngleD(n0, n1);
        }





        public static DCurve3 ExtractLoopV(IMesh mesh, IEnumerable<int> vertices) {
            DCurve3 curve = new DCurve3();
            foreach (int vid in vertices)
                curve.AppendVertex(mesh.GetVertex(vid));
            curve.Closed = true;
            return curve;
        }
        public static DCurve3 ExtractLoopV(IMesh mesh, int[] vertices) {
            DCurve3 curve = new DCurve3();
            for (int i = 0; i < vertices.Length; ++i)
                curve.AppendVertex(mesh.GetVertex(vertices[i]));
            curve.Closed = true;
            return curve;
        }

	}
}
