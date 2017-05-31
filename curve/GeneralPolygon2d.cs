using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

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
			holes = new List<Polygon2d>(copy.holes);
			foreach (var hole in copy.holes)
				holes.Add(new Polygon2d(hole));
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


		public void AddHole(Polygon2d hole, bool bCheck = true) {
			if ( outer == null )
				throw new Exception("GeneralPolygon2d.AddHole: outer polygon not set!");
			if ( bCheck ) {
				if ( outer.Contains(hole) == false )
					throw new Exception("GeneralPolygon2d.AddHole: outer does not contain hole!");

				// [RMS] segment/segment intersection broken?
				foreach ( var hole2 in holes ) {
					if ( hole.Intersects(hole2) )
						throw new Exception("GeneralPolygon2D.AddHole: new hole intersects existing hole!");
				}
			}

			if ( (bOuterIsCW && hole.IsClockwise) || (bOuterIsCW == false && hole.IsClockwise == false) )
				throw new Exception("GeneralPolygon2D.AddHole: new hole has same orientation as outer polygon!");

			holes.Add(hole);
		}


		bool HasHoles {
			get { return Holes.Count > 0; }
		}

		public ReadOnlyCollection<Polygon2d> Holes {
			get { return holes.AsReadOnly(); }
		}



        public double Area
        {
            get {
                double sign = (bOuterIsCW) ? -1.0 : 1.0;
                double dArea = sign * Outer.SignedArea;
                foreach (var h in Holes)
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
                foreach (var h in Holes)
                    dPerim += h.Perimeter;
                return dPerim;
            }
        }


        public AxisAlignedBox2d Bounds
        {
            get {
                AxisAlignedBox2d box = outer.GetBounds();
                foreach (var h in Holes)
                    box.Contain(h.GetBounds());
                return box;
            }
        }


		public void Translate(Vector2d translate) {
			outer.Translate(translate);
			foreach (var h in holes)
				h.Translate(translate);
		}

		public void Scale(Vector2d scale, Vector2d origin) {
			outer.Scale(scale, origin);
			foreach (var h in holes)
				h.Scale(scale, origin);
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


	}
}
