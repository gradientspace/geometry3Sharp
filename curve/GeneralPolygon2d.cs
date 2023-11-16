using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Unity.Collections;
using Unity.Mathematics;
using andywiecko.BurstTriangulator;


namespace g3 
{
	public class GeneralPolygon2d : IDuplicatable<GeneralPolygon2d>
	{
		Polygon2d outer;
		bool bOuterIsCW;

		List<Polygon2d> holes = new List<Polygon2d>();


		public GeneralPolygon2d() {
		}
		public GeneralPolygon2d(Polygon2d outer)
		{
			Outer = outer;
		}
		public GeneralPolygon2d(GeneralPolygon2d copy)
		{
			outer = new Polygon2d(copy.outer);
			bOuterIsCW = copy.bOuterIsCW;
			holes = new List<Polygon2d>();
			foreach (var hole in copy.holes)
				holes.Add(new Polygon2d(hole));
		}

        /// <summary>
        /// Create a Polygon 2D from a list of DCurve3's using a Frame that is as orthogonal to the dataset as poissible
        /// Return the Frame3f used and the original Vertices sorted.
        /// </summary>
        /// <param name="curve">The input IEumerable of DCurve3</param>
        /// <param name="frame">The Frame3f used</param>
        /// <param name="AllVerticesItr">The original 3D vertices usd n the same order as the AllVerticesItr of the General Polygon2D</param>
        public GeneralPolygon2d(IEnumerable<DCurve3> curves, out Frame3f frame, out IEnumerable<Vector3d> AllVerticesItr)
        {
            OrthogonalPlaneFit3 orth = new OrthogonalPlaneFit3(curves.ElementAt(0).Vertices);
            frame = new Frame3f(orth.Origin, orth.Normal);
            AllVerticesItr = null;
            int i = 0;
            foreach (DCurve3 curve in curves)
            {
                List<Vector3d> vertices = curve.Vertices.ToList();
                List<Vector2d> vertices2d = new List<Vector2d>();
                foreach (Vector3d v in vertices)
                {
                    Vector2f vertex = frame.ToPlaneUV((Vector3f)v, 3);
                    if (i != 0 && !Outer.Contains(vertex)) break;
                    vertices2d.Add(vertex);
                }
                Polygon2d p2d = new Polygon2d(vertices2d);
                if (i == 0)
                {
                    p2d = new Polygon2d(vertices2d);
                    p2d.Reverse();
                    Outer = p2d;
                    vertices.Reverse();
                    AllVerticesItr = vertices;
                }
                else
                {
                    try
                    {
                        try
                        {
                            AddHole(p2d, true, true);
                            AllVerticesItr = AllVerticesItr.Concat(vertices);
                        }
                        catch (Exception e)
                        {
                            _ = e;
                            p2d.Reverse();
                            AddHole(p2d, true, true);
                            vertices.Reverse();
                            AllVerticesItr = AllVerticesItr.Concat(vertices);
                        }
                    }
                    catch (Exception e)
                    {
                        _ = e;
                        // skip this hole
                    }
                }
                i++;
            }
        }

        public virtual GeneralPolygon2d Duplicate() {
			return new GeneralPolygon2d(this);
		}


		public Polygon2d Outer {
			get { return outer; }
			set { 
				outer = value;
				bOuterIsCW = outer.IsClockwise;
			}
		}

		public void AddHole(Polygon2d hole, bool bCheckContainment = true, bool bCheckOrientation = true) {
			if ( outer == null )
				throw new Exception("GeneralPolygon2d.AddHole: outer polygon not set!");
			if ( bCheckContainment ) {
				if ( outer.Contains(hole) == false )
					throw new Exception("GeneralPolygon2d.AddHole: outer does not contain hole!");

				// [RMS] segment/segment intersection broken?
				foreach ( var hole2 in holes ) {
					if ( hole.Intersects(hole2) )
						throw new Exception("GeneralPolygon2D.AddHole: new hole intersects existing hole!");
				}
			}
            if ( bCheckOrientation ) {
                if ((bOuterIsCW && hole.IsClockwise) || (bOuterIsCW == false && hole.IsClockwise == false))
                    throw new Exception("GeneralPolygon2D.AddHole: new hole has same orientation as outer polygon!");
            }

			holes.Add(hole);
		}

        public void ClearHoles() {
            holes.Clear();
        }


		bool HasHoles {
			get { return holes.Count > 0; }
		}

		public ReadOnlyCollection<Polygon2d> Holes {
			get { return holes.AsReadOnly(); }
		}



        public double Area
        {
            get {
                double sign = (bOuterIsCW) ? -1.0 : 1.0;
                double dArea = sign * Outer.SignedArea;
                foreach (var h in holes)
                    dArea += sign * h.SignedArea;
                return dArea;
            }
        }


        public double HoleArea
        {
            get {
                double dArea = 0;
                foreach (var h in Holes)
                    dArea += Math.Abs(h.SignedArea);
                return dArea;
            }
        }


        public double Perimeter
        {
            get {
                double dPerim = outer.Perimeter;
                foreach (var h in holes)
                    dPerim += h.Perimeter;
                return dPerim;
            }
        }


        public AxisAlignedBox2d Bounds
        {
            get {
                AxisAlignedBox2d box = outer.GetBounds();
                foreach (var h in holes)
                    box.Contain(h.GetBounds());
                return box;
            }
        }

        public int VertexCount {
            get {
                int NV = outer.VertexCount;
                foreach (var h in holes)
                    NV += h.VertexCount;
                return NV;
            }
        }


		public void Translate(Vector2d translate) {
			outer.Translate(translate);
			foreach (var h in holes)
				h.Translate(translate);
		}

        public void Rotate(Matrix2d rotation, Vector2d origin) {
            outer.Rotate(rotation, origin);
            foreach (var h in holes)
                h.Rotate(rotation, origin);
        }


        public void Scale(Vector2d scale, Vector2d origin) {
			outer.Scale(scale, origin);
			foreach (var h in holes)
				h.Scale(scale, origin);
		}

        public void Transform(Func<Vector2d,Vector2d> transformF)
        {
            outer.Transform(transformF);
            foreach (var h in holes)
                h.Transform(transformF);
        }

        public void Reverse()
        {
            Outer.Reverse();
            bOuterIsCW = Outer.IsClockwise;
            foreach (var h in Holes)
                h.Reverse();
        }


		public bool Contains(Vector2d vTest)
		{
			if (outer.Contains(vTest) == false)
				return false;
			foreach (var h in holes) {
				if (h.Contains(vTest))
					return false;
			}
			return true;
		}


        public bool Contains(Polygon2d poly) {
            if (outer.Contains(poly) == false)
                return false;
            foreach (var h in holes) {
                if (h.Contains(poly))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Checks that all points on a segment are within the area defined by the GeneralPolygon2d;
        /// holes are included in the calculation.
        /// </summary>
        public bool Contains(Segment2d seg)
        {
            if (outer.Contains(seg) == false)
                return false;
            foreach (var h in holes)
            {
                if (h.Intersects(seg))
                    return false;
            }
            return true;
        }


        public bool Intersects(Polygon2d poly)
        {
            if (outer.Intersects(poly))
                return true;
            foreach (var h in holes) {
                if (h.Intersects(poly))
                    return true;
            }
            return false;
        }


        public Vector2d PointAt(int iSegment, double fSegT, int iHoleIndex = -1)
		{
			if (iHoleIndex == -1)
				return outer.PointAt(iSegment, fSegT);
			return holes[iHoleIndex].PointAt(iSegment, fSegT);
		}

		public Segment2d Segment(int iSegment, int iHoleIndex = -1)
		{
			if (iHoleIndex == -1)
				return outer.Segment(iSegment);
			return holes[iHoleIndex].Segment(iSegment);			
		}

		public Vector2d GetNormal(int iSegment, double segT, int iHoleIndex = -1)
		{
			if (iHoleIndex == -1)
				return outer.GetNormal(iSegment, segT);
			return holes[iHoleIndex].GetNormal(iSegment, segT);
		}

		// this should be more efficient when there are holes...
		public double DistanceSquared(Vector2d p, out int iHoleIndex, out int iNearSeg, out double fNearSegT)
		{
			iNearSeg = iHoleIndex = -1;
			fNearSegT = double.MaxValue;
			double dist = outer.DistanceSquared(p, out iNearSeg, out fNearSegT);
			for (int i = 0; i < Holes.Count; ++i ) {
				int seg; double segt;
				double holedist = Holes[i].DistanceSquared(p, out seg, out segt);
				if (holedist < dist) {
					dist = holedist;
					iHoleIndex = i;
					iNearSeg = seg;
					fNearSegT = segt;
				}
			}
			return dist;
		}


		public IEnumerable<Segment2d> AllSegmentsItr()
		{
			foreach (Segment2d seg in outer.SegmentItr())
				yield return seg;
			foreach ( var hole in holes ) {
				foreach (Segment2d seg in hole.SegmentItr())
					yield return seg;
			}
		}

		public IEnumerable<Vector2d> AllVerticesItr()
		{
			foreach (Vector2d v in outer.Vertices)
				yield return v;
			foreach (var hole in holes) {
				foreach (Vector2d v in hole.Vertices)
					yield return v;
			}
		}

        public IEnumerable<(int, int)> AllEdgesItr()
        {
            int j = Outer.VertexCount;
            for (int i = 0; i < j; i++)
                yield return (i, i != j - 1 ? i + 1 : 0);
            foreach (var hole in Holes)
            {
                for (int i = 0; i < hole.VertexCount; i++)
                    yield return (j + i, i != hole.VertexCount - 1 ? j + i + 1 : j);
                j += hole.VertexCount;
            }
        }

        private NativeArray<int> ToEdges()
        {
            int edge_count = VertexCount;

            NativeArray<int> edges = new(edge_count * 2, Allocator.Persistent);

            int idx = 0;
            foreach ((int, int) edge in AllEdgesItr())
            {
                edges[idx] = edge.Item1;
                idx++;
                edges[idx] = edge.Item2;
                idx++;
            }
            return edges;
        }

        private  NativeArray<float2> ToHoleSeeds()
        {
            List<Vector2d> seeds = new();
            foreach (Polygon2d hole in Holes)
            {
                Vector2d seed = hole.Bounds.Center;
                if (hole.Contains(seed))
                    seeds.Add(seed);
                else throw new Exception("Cannot find Hole Seed");
            }
            return new NativeArray<float2>(seeds.Select(vertex => (float2)vertex).ToArray(), Allocator.Persistent);
        }

        /// <summary>
        /// Calculates the Delaunay triangulation for the Polygon, respecting the outer border and the holes
        /// </summary>
        /// <returns name="triangles"> Indes3i holding the triangles as indexes into AllVerticesItr</param>
        public Index3i[] GetMesh()
        {
            Triangulator triangulator = new Triangulator(Allocator.Persistent)
            {
                Input = {
                    Positions = new NativeArray<float2>(AllVerticesItr().Select(vertex => (float2)vertex).ToArray(), Allocator.Persistent),
                    ConstraintEdges = ToEdges(),
                    HoleSeeds = ToHoleSeeds()
                },
                Settings = {
                    RestoreBoundary = true,
                    ConstrainEdges = true
                }
            };
            try
            {

                triangulator.Run();

                if ( ! triangulator.Output.Status.IsCreated || 
                       triangulator.Output.Status.Value != Triangulator.Status.OK
                   )
                {
                    throw new Exception("Could not create Delaunay Triangulation");
                }


                // 
                // extract the triangles from the delaunay triangulation 
                //
                int[] tris = triangulator.Output.Triangles.AsArray().ToArray();
                int tri_count = tris.Length / 3;
                Index3i[] triangles = new Index3i[tri_count];
                long idx = 0;

                for ( int i = 0; i < tri_count; i++)
                {
                    triangles[i] = new(tris[idx++], tris[idx++], tris[idx++] );
                }


                triangulator.Input.Positions.Dispose();
                triangulator.Input.ConstraintEdges.Dispose();
                triangulator.Input.HoleSeeds.Dispose();
                triangulator.Dispose();
                return triangles;
            } catch
            {
                triangulator.Input.Positions.Dispose();
                triangulator.Input.ConstraintEdges.Dispose();
                triangulator.Input.HoleSeeds.Dispose();
                triangulator.Dispose();
                return default;
            }
        }

        public void Simplify(double clusterTol = 0.0001,
                              double lineDeviationTol = 0.01,
                              bool bSimplifyStraightLines = true)
        {
            // [TODO] should make sure that holes stay inside Outer!!
            Outer.Simplify(clusterTol, lineDeviationTol, bSimplifyStraightLines);
            foreach (var hole in holes)
                hole.Simplify(clusterTol, lineDeviationTol, bSimplifyStraightLines);
        }

        public bool IsOutside(Segment2d seg)
        {
            bool isOutside = true;
            if (Outer.IsMember(seg, out isOutside))
            {
                if (isOutside)
                    return true;
                else
                    return false;
            }
            foreach (Polygon2d hole in Holes)
            {
                if (hole.IsMember(seg, out isOutside))
                {
                    if (isOutside)
                        return true;
                    else
                        return false;
                }
            }
            return false;
        }

    }
}
