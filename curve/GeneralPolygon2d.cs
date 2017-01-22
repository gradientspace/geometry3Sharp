using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace g3 
{
	public class GeneralPolygon2d 
	{
		Polygon2d outer;
		bool bOuterIsCW;

		List<Polygon2d> holes = new List<Polygon2d>();


		public GeneralPolygon2d() {
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
	}
}
