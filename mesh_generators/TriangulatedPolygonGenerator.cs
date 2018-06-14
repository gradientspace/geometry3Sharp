using System;

namespace g3
{
    /// <summary>
    /// Triangulate a 2D polygon-with-holes by inserting it's edges into a meshed rectangle
    /// and then removing the triangles outside the polygon.
    /// </summary>
    public class TriangulatedPolygonGenerator : MeshGenerator
    {
        public GeneralPolygon2d Polygon;
        public Vector3f FixedNormal = Vector3f.AxisZ;

        public TrivialRectGenerator.UVModes UVMode = TrivialRectGenerator.UVModes.FullUVSquare;

		public int Subdivisions = 1;

        override public MeshGenerator Generate()
        {
            MeshInsertPolygon insert;
            DMesh3 base_mesh = ComputeResult(out insert);

            DMesh3 compact = new DMesh3(base_mesh, true);

            int NV = compact.VertexCount;
            vertices = new VectorArray3d(NV);
            uv = new VectorArray2f(NV);
            normals = new VectorArray3f(NV);
            for (int vi = 0; vi < NV; ++vi) {
                vertices[vi] = compact.GetVertex(vi);
                uv[vi] = compact.GetVertexUV(vi);
                normals[vi] = FixedNormal;
            }

            int NT = compact.TriangleCount;
            triangles = new IndexArray3i(NT);
            for (int ti = 0; ti < NT; ++ti)
                triangles[ti] = compact.GetTriangle(ti);

            return this;
        }




        /// <summary>
        /// Actually computes the insertion. In some cases we would like more info
        /// coming back than we get by using Generate() api. Note that resulting
        /// mesh is *not* compacted.
        /// </summary>
        public DMesh3 ComputeResult(out MeshInsertPolygon insertion)
        {
            AxisAlignedBox2d bounds = Polygon.Bounds;
            double padding = 0.1 * bounds.DiagonalLength;
            bounds.Expand(padding);

			TrivialRectGenerator rectgen = (Subdivisions == 1) ?
				new TrivialRectGenerator() : new GriddedRectGenerator() { EdgeVertices = Subdivisions };

			rectgen.Width = (float)bounds.Width;
			rectgen.Height = (float)bounds.Height;
			rectgen.IndicesMap = new Index2i(1, 2);
			rectgen.UVMode = UVMode;
			rectgen.Clockwise = true;   // MeshPolygonInserter assumes mesh faces are CW? (except code says CCW...)
			rectgen.Generate();
			DMesh3 base_mesh = new DMesh3();
			rectgen.MakeMesh(base_mesh);

            GeneralPolygon2d shiftPolygon = new GeneralPolygon2d(Polygon);
            Vector2d shift = bounds.Center;
            shiftPolygon.Translate(-shift);

            MeshInsertPolygon insert = new MeshInsertPolygon() {
                Mesh = base_mesh, Polygon = shiftPolygon
            };
            bool bOK = insert.Insert();
            if (!bOK)
                throw new Exception("TriangulatedPolygonGenerator: failed to Insert()");

            MeshFaceSelection selected = insert.InteriorTriangles;
            MeshEditor editor = new MeshEditor(base_mesh);
            editor.RemoveTriangles((tid) => { return selected.IsSelected(tid) == false; }, true);

            Vector3d shift3 = new Vector3d(shift.x, shift.y, 0);
            MeshTransforms.Translate(base_mesh, shift3);

            insertion = insert;
            return base_mesh;
        }




    }





}
