using System;

namespace g3 {

	// partially based on WildMagic5 Box3
	public struct Box3d
	{
		// A box has center C, axis directions U[0], U[1], and U[2] (mutually
		// perpendicular unit-length vectors), and extents e[0], e[1], and e[2]
		// (all nonnegative numbers).  A point X = C+y[0]*U[0]+y[1]*U[1]+y[2]*U[2]
		// is inside or on the box whenever |y[i]| <= e[i] for all i.

		public Vector3d Center;
		public Vector3d AxisX;
		public Vector3d AxisY;
		public Vector3d AxisZ;
		public Vector3d Extent;

		public Box3d(Vector3d center) {
			Center = center;
			AxisX = Vector3d.AxisX;
			AxisY = Vector3d.AxisY;
			AxisZ = Vector3d.AxisZ;
			Extent = Vector3d.Zero;
		}
		public Box3d(Vector3d center, Vector3d x, Vector3d y, Vector3d z,
		                 Vector3d extent) {
			Center = center;
			AxisX = x; AxisY = y; AxisZ = z;
			Extent = extent;
		}
		public Box3d(AxisAlignedBox3d aaBox) {
			Extent= 0.5*aaBox.Diagonal;
			Center = aaBox.Min + Extent;
			AxisX = Vector3d.AxisX;
			AxisY = Vector3d.AxisY;
			AxisZ = Vector3d.AxisZ;
		}

		public static readonly Box3d Empty = new Box3d(Vector3d.Zero);


		public Vector3d Axis(int i)
		{
			return (i == 0) ? AxisX : (i == 1) ? AxisY : AxisZ;
		}


		public Vector3d[] ComputeVertices() {
			Vector3d[] v = new Vector3d[8];
			ComputeVertices(v);
			return v;
		}
		public void ComputeVertices (Vector3d[] vertex) {
			Vector3d extAxis0 = Extent.x*AxisX;
			Vector3d extAxis1 = Extent.y*AxisY;
			Vector3d extAxis2 = Extent.z*AxisZ;
			vertex[0] = Center - extAxis0 - extAxis1 - extAxis2;
			vertex[1] = Center + extAxis0 - extAxis1 - extAxis2;
			vertex[2] = Center + extAxis0 + extAxis1 - extAxis2;
			vertex[3] = Center - extAxis0 + extAxis1 - extAxis2;
			vertex[4] = Center - extAxis0 - extAxis1 + extAxis2;
			vertex[5] = Center + extAxis0 - extAxis1 + extAxis2;
			vertex[6] = Center + extAxis0 + extAxis1 + extAxis2;
			vertex[7] = Center - extAxis0 + extAxis1 + extAxis2;			
		}


		// g3 extensions
		public double MaxExtent {
			get { return Math.Max(Extent.x, Math.Max(Extent.y, Extent.z) ); }
		}
		public double MinExtent {
			get { return Math.Min(Extent.x, Math.Max(Extent.y, Extent.z) ); }
		}
		public Vector3d Diagonal {
			get { return (Extent.x*AxisX + Extent.y*AxisY + Extent.z*AxisZ) - 
				(-Extent.x*AxisX - Extent.y*AxisY - Extent.z*AxisZ); }
		}
		public double Volume {
			get { return 2*Extent.x + 2*Extent.y * 2*Extent.z; }
		}

		public void Contain( Vector3d v) {
			Vector3d lv = v - Center;
			for (int k = 0; k < 3; ++k) {
				double t = lv.Dot(Axis(k));
				if ( Math.Abs(t) > Extent[k]) {
					double min = -Extent[k], max = Extent[k];
					if ( t < min )
						min = t;
					else if ( t > max )
						max = t;
					Extent[k] = (max-min) * 0.5;
					Center = Center + ((max+min) * 0.5) * Axis(k);
				}
			}			
		}

		// I think this can be more efficient...no? At least could combine
		// all the axis-interval updates before updating Center...
		public void Contain( Box3d o ) {
			Vector3d[] v = o.ComputeVertices();
			for (int k = 0; k < 8; ++k) 
				Contain(v[k]);
		}

		public bool Contained( Vector3d v ) {
			Vector3d lv = v - Center;
			return (Math.Abs(lv.Dot(AxisX)) <= Extent.x) &&
				(Math.Abs(lv.Dot(AxisY)) <= Extent.y) &&
				(Math.Abs(lv.Dot(AxisZ)) <= Extent.z);
		}

		public void Expand(double f) {
			Extent += f;
		}

		public void Translate( Vector3d v ) {
			Center += v;
		}

	}












	// partially based on WildMagic5 Box3
	public struct Box3f
	{
		// A box has center C, axis directions U[0], U[1], and U[2] (mutually
		// perpendicular unit-length vectors), and extents e[0], e[1], and e[2]
		// (all nonnegative numbers).  A point X = C+y[0]*U[0]+y[1]*U[1]+y[2]*U[2]
		// is inside or on the box whenever |y[i]| <= e[i] for all i.

		public Vector3f Center;
		public Vector3f AxisX;
		public Vector3f AxisY;
		public Vector3f AxisZ;
		public Vector3f Extent;

		public Box3f(Vector3f center) {
			Center = center;
			AxisX = Vector3f.AxisX;
			AxisY = Vector3f.AxisY;
			AxisZ = Vector3f.AxisZ;
			Extent = Vector3f.Zero;
		}
		public Box3f(Vector3f center, Vector3f x, Vector3f y, Vector3f z,
		             Vector3f extent) {
			Center = center;
			AxisX = x; AxisY = y; AxisZ = z;
			Extent = extent;
		}
		public Box3f(AxisAlignedBox3f aaBox) {
			Extent= 0.5f*aaBox.Diagonal;
			Center = aaBox.Min + Extent;
			AxisX = Vector3f.AxisX;
			AxisY = Vector3f.AxisY;
			AxisZ = Vector3f.AxisZ;
		}

		public static readonly Box3f Empty = new Box3f(Vector3f.Zero);


		public Vector3f Axis(int i)
		{
			return (i == 0) ? AxisX : (i == 1) ? AxisY : AxisZ;
		}


		public Vector3f[] ComputeVertices() {
			Vector3f[] v = new Vector3f[8];
			ComputeVertices(v);
			return v;
		}
		public void ComputeVertices (Vector3f[] vertex) {
			Vector3f extAxis0 = Extent.x*AxisX;
			Vector3f extAxis1 = Extent.y*AxisY;
			Vector3f extAxis2 = Extent.z*AxisZ;
			vertex[0] = Center - extAxis0 - extAxis1 - extAxis2;
			vertex[1] = Center + extAxis0 - extAxis1 - extAxis2;
			vertex[2] = Center + extAxis0 + extAxis1 - extAxis2;
			vertex[3] = Center - extAxis0 + extAxis1 - extAxis2;
			vertex[4] = Center - extAxis0 - extAxis1 + extAxis2;
			vertex[5] = Center + extAxis0 - extAxis1 + extAxis2;
			vertex[6] = Center + extAxis0 + extAxis1 + extAxis2;
			vertex[7] = Center - extAxis0 + extAxis1 + extAxis2;			
		}


		// g3 extensions
		public double MaxExtent {
			get { return Math.Max(Extent.x, Math.Max(Extent.y, Extent.z) ); }
		}
		public double MinExtent {
			get { return Math.Min(Extent.x, Math.Max(Extent.y, Extent.z) ); }
		}
		public Vector3f Diagonal {
			get { return (Extent.x*AxisX + Extent.y*AxisY + Extent.z*AxisZ) - 
				(-Extent.x*AxisX - Extent.y*AxisY - Extent.z*AxisZ); }
		}
		public double Volume {
			get { return 2*Extent.x + 2*Extent.y * 2*Extent.z; }
		}

		public void Contain( Vector3f v) {
			Vector3f lv = v - Center;
			for (int k = 0; k < 3; ++k) {
				double t = lv.Dot(Axis(k));
				if ( Math.Abs(t) > Extent[k]) {
					double min = -Extent[k], max = Extent[k];
					if ( t < min )
						min = t;
					else if ( t > max )
						max = t;
					Extent[k] = (float)(max-min) * 0.5f;
					Center = Center + ((float)(max+min) * 0.5f) * Axis(k);
				}
			}			
		}

		// I think this can be more efficient...no? At least could combine
		// all the axis-interval updates before updating Center...
		public void Contain( Box3f o ) {
			Vector3f[] v = o.ComputeVertices();
			for (int k = 0; k < 8; ++k) 
				Contain(v[k]);
		}

		public bool Contained( Vector3f v ) {
			Vector3f lv = v - Center;
			return (Math.Abs(lv.Dot(AxisX)) <= Extent.x) &&
				(Math.Abs(lv.Dot(AxisY)) <= Extent.y) &&
				(Math.Abs(lv.Dot(AxisZ)) <= Extent.z);
		}

		public void Expand(float f) {
			Extent += f;
		}

		public void Translate( Vector3f v ) {
			Center += v;
		}

	}




}
