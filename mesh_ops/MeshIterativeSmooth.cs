using System;
using System.Collections.Generic;

namespace g3
{
    public class MeshIterativeSmooth
    {
        public DMesh3 Mesh;
        public int[] Vertices;

        public double Alpha = 0.25f;
        public int Rounds = 10;

		public enum SmoothTypes {
			Uniform, Cotan, MeanValue
		};
		public SmoothTypes SmoothType = SmoothTypes.Uniform;

        // reproject smoothed position to new location
        public Func<Vector3d, Vector3f, int, Vector3d> ProjectF;

        Vector3d[] SmoothedPostions;

        public MeshIterativeSmooth(DMesh3 mesh, int[] vertices, bool bOwnVertices = false)
        {
            Mesh = mesh;
            Vertices = (bOwnVertices) ? vertices : (int[])vertices.Clone();

            SmoothedPostions = new Vector3d[Vertices.Length];

            ProjectF = null;
        }


        public virtual ValidationStatus Validate()
        {
            return ValidationStatus.Ok;
        }


        public virtual bool Smooth()
        {
            int NV = Vertices.Length;

            double a = MathUtil.Clamp(Alpha, 0, 1);
            double num_rounds = MathUtil.Clamp(Rounds, 0, 10000);

            Func<DMesh3, int, double, Vector3d> smoothFunc = MeshUtil.UniformSmooth;
            if (SmoothType == SmoothTypes.MeanValue)
                smoothFunc = MeshUtil.MeanValueSmooth;
            else if (SmoothType == SmoothTypes.Cotan)
                smoothFunc = MeshUtil.CotanSmooth;

            Action<int> smooth = (i) => {
                int vID = Vertices[i];
                SmoothedPostions[i] = smoothFunc(Mesh, vID, a);
            };
            Action<int> project = (i) => {
                Vector3d pos = SmoothedPostions[i];
                SmoothedPostions[i] = ProjectF(pos, Vector3f.AxisY, Vertices[i]);
            };

            IndexRangeEnumerator indices = new IndexRangeEnumerator(0, NV);

            for (int round = 0; round < num_rounds; ++round) {

                gParallel.ForEach<int>(indices, smooth);
                if ( ProjectF != null )
                    gParallel.ForEach<int>(indices, project);

                // bake
                for (int i = 0; i < NV; ++i)
                    Mesh.SetVertex(Vertices[i], SmoothedPostions[i]);
            }

            return true;
        }
    }




}
