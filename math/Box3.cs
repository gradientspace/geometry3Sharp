using System;
using System.Collections.Generic;

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
		public Box3d(Vector3d center, Vector3d extent) {
			Center = center;
			Extent = extent;
			AxisX = Vector3d.AxisX;
			AxisY = Vector3d.AxisY;
			AxisZ = Vector3d.AxisZ;
		}
		public Box3d(AxisAlignedBox3d aaBox) {
            // [RMS] this should produce Empty for aaBox.Empty...
            Extent = new Vector3f(aaBox.Width * 0.5, aaBox.Height * 0.5, aaBox.Depth * 0.5);
            Center = aaBox.Center;
            AxisX = Vector3d.AxisX;
			AxisY = Vector3d.AxisY;
			AxisZ = Vector3d.AxisZ;
		}
        public Box3d(Frame3f frame, Vector3d extent)
        {
            Center = frame.Origin;
            AxisX = frame.X;
            AxisY = frame.Y;
            AxisZ = frame.Z;
            Extent = extent;
        }
        public Box3d(Segment3d seg)
        {
            Center = seg.Center;
            AxisZ = seg.Direction;
            Vector3d.MakePerpVectors(ref AxisZ, out AxisX, out AxisY);
            Extent = new Vector3d(0, 0, seg.Extent);
        }

        public static readonly Box3d Empty = new Box3d(Vector3d.Zero);
        public static readonly Box3d UnitZeroCentered = new Box3d(Vector3d.Zero, 0.5 * Vector3d.One);
        public static readonly Box3d UnitPositive = new Box3d(0.5 * Vector3d.One, 0.5 * Vector3d.One);


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


        public IEnumerable<Vector3d> VerticesItr()
        {
            Vector3d extAxis0 = Extent.x * AxisX;
            Vector3d extAxis1 = Extent.y * AxisY;
            Vector3d extAxis2 = Extent.z * AxisZ;
            yield return Center - extAxis0 - extAxis1 - extAxis2;
            yield return Center + extAxis0 - extAxis1 - extAxis2;
            yield return Center + extAxis0 + extAxis1 - extAxis2;
            yield return Center - extAxis0 + extAxis1 - extAxis2;
            yield return Center - extAxis0 - extAxis1 + extAxis2;
            yield return Center + extAxis0 - extAxis1 + extAxis2;
            yield return Center + extAxis0 + extAxis1 + extAxis2;
            yield return Center - extAxis0 + extAxis1 + extAxis2;
        }


        public AxisAlignedBox3d ToAABB()
        {
            // [TODO] probably more efficient way to do this...at minimum can move center-shift
            // to after the containments...
 			Vector3d extAxis0 = Extent.x*AxisX;
			Vector3d extAxis1 = Extent.y*AxisY;
			Vector3d extAxis2 = Extent.z*AxisZ;
            AxisAlignedBox3d result = new AxisAlignedBox3d(Center - extAxis0 - extAxis1 - extAxis2);
			result.Contain(Center + extAxis0 - extAxis1 - extAxis2);
            result.Contain(Center + extAxis0 + extAxis1 - extAxis2);
			result.Contain(Center - extAxis0 + extAxis1 - extAxis2);
			result.Contain(Center - extAxis0 - extAxis1 + extAxis2);
			result.Contain(Center + extAxis0 - extAxis1 + extAxis2);
			result.Contain(Center + extAxis0 + extAxis1 + extAxis2);
            result.Contain(Center - extAxis0 + extAxis1 + extAxis2);
            return result;
        }



        // corners [ (-x,-y), (x,-y), (x,y), (-x,y) ], -z, then +z
        //
        //   7---6     +z       or        3---2     -z
        //   |\  |\                       |\  |\
        //   4-\-5 \                      0-\-1 \
        //    \ 3---2                      \ 7---6   
        //     \|   |                       \|   |
        //      0---1  -z                    4---5  +z
        //
        // Note that in RHS system (which is our default), +z is "forward" so -z in this diagram 
        // is actually the back of the box (!) This is odd but want to keep consistency w/ ComputeVertices(),
        // and the implementation there needs to stay consistent w/ C++ Wildmagic5
        public Vector3d Corner(int i)
        {
            Vector3d c = Center;
            c += (  ((i&1) != 0) ^ ((i&2) != 0) ) ? (Extent.x * AxisX) : (-Extent.x * AxisX);
            c += ( (i / 2) % 2 == 0 ) ? (-Extent.y * AxisY) : (Extent.y * AxisY);
            c += (i < 4) ? (-Extent.z * AxisZ) : (Extent.z * AxisZ);
            return c;
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
			get { return 2*Extent.x * 2*Extent.y * 2*Extent.z; }
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


        /// <summary>
        /// update the box to contain set of input points. More efficient tha ncalling Contain() many times
        /// code ported from GTEngine GteContOrientedBox3.h 
        /// </summary>
        public void Contain(IEnumerable<Vector3d> points)
        {
            // Let C be the box center and let U0, U1, and U2 be the box axes.
            // Each input point is of the form X = C + y0*U0 + y1*U1 + y2*U2.
            // The following code computes min(y0), max(y0), min(y1), max(y1),
            // min(y2), and max(y2).  The box center is then adjusted to be
            //   C' = C + 0.5*(min(y0)+max(y0))*U0 + 0.5*(min(y1)+max(y1))*U1 + 0.5*(min(y2)+max(y2))*U2
            IEnumerator<Vector3d> points_itr = points.GetEnumerator();
            points_itr.MoveNext();

            Vector3d diff = points_itr.Current - Center;
            Vector3d pmin = new Vector3d( diff.Dot(AxisX), diff.Dot(AxisY), diff.Dot(AxisZ));
            Vector3d pmax = pmin;
            while (points_itr.MoveNext()) {
                diff = points_itr.Current - Center;

                double dotx = diff.Dot(AxisX);
                if (dotx < pmin[0]) pmin[0] = dotx;
                else if (dotx > pmax[0]) pmax[0] = dotx;

                double doty = diff.Dot(AxisY);
                if (doty < pmin[1]) pmin[1] = doty;
                else if (doty > pmax[1]) pmax[1] = doty;

                double dotz = diff.Dot(AxisZ);
                if (dotz < pmin[2]) pmin[2] = dotz;
                else if (dotz > pmax[2]) pmax[2] = dotz;
            }
            for (int j = 0; j < 3; ++j) {
                Center += (((double)0.5) * (pmin[j] + pmax[j])) * Axis(j);
                Extent[j] = ((double)0.5) * (pmax[j] - pmin[j]);
            }
        }



		// I think this can be more efficient...no? At least could combine
		// all the axis-interval updates before updating Center...
		public void Contain( Box3d o ) {
			Vector3d[] v = o.ComputeVertices();
			for (int k = 0; k < 8; ++k) 
				Contain(v[k]);
		}

		public bool Contains( Vector3d v ) {
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

        public void Scale(Vector3d s)
        {
            Center *= s;
            Extent *= s;
            AxisX *= s; AxisX.Normalize();
            AxisY *= s; AxisY.Normalize();
            AxisZ *= s; AxisZ.Normalize();
        }

        public void ScaleExtents(Vector3d s)
        {
            Extent *= s;
        }




        /// <summary>
        /// Returns distance to box, or 0 if point is inside box.
        /// Ported from WildMagic5 Wm5DistPoint3Box3.cpp
        /// </summary>
        public double DistanceSquared(Vector3d v)
        {
            // Work in the box's coordinate system.
            v -= this.Center;

            // Compute squared distance and closest point on box.
            double sqrDistance = 0;
            double delta;
            Vector3d closest = new Vector3d();
            int i;
            for (i = 0; i < 3; ++i) {
                closest[i] = Axis(i).Dot(ref v);
                if (closest[i] < -Extent[i]) {
                    delta = closest[i] + Extent[i];
                    sqrDistance += delta * delta;
                    closest[i] = -Extent[i];
                } else if (closest[i] > Extent[i]) {
                    delta = closest[i] - Extent[i];
                    sqrDistance += delta * delta;
                    closest[i] = Extent[i];
                }
            }

            return sqrDistance;
        }



        /// <summary>
        /// Returns distance to box, or 0 if point is inside box.
        /// Ported from WildMagic5 Wm5DistPoint3Box3.cpp
        /// </summary>
        public Vector3d ClosestPoint(Vector3d v)
        {
            // Work in the box's coordinate system.
            v -= this.Center;

            // Compute squared distance and closest point on box.
            double sqrDistance = 0;
            double delta;
            Vector3d closest = new Vector3d();
            for (int i = 0; i < 3; ++i) {
                closest[i] = Axis(i).Dot(ref v);
                double extent = Extent[i];
                if (closest[i] < -extent) {
                    delta = closest[i] + extent;
                    sqrDistance += delta * delta;
                    closest[i] = -extent;
                } else if (closest[i] > extent) {
                    delta = closest[i] - extent;
                    sqrDistance += delta * delta;
                    closest[i] = extent;
                }
            }

            return Center + closest.x*AxisX + closest.y*AxisY + closest.z*AxisZ;
        }





        // ported from WildMagic5 Wm5ContBox3.cpp::MergeBoxes
        public static Box3d Merge(ref Box3d box0, ref Box3d box1)
        {
            // Construct a box that contains the input boxes.
            Box3d box = new Box3d();

            // The first guess at the box center.  This value will be updated later
            // after the input box vertices are projected onto axes determined by an
            // average of box axes.
            box.Center = 0.5 * (box0.Center + box1.Center);

            // A box's axes, when viewed as the columns of a matrix, form a rotation
            // matrix.  The input box axes are converted to quaternions.  The average
            // quaternion is computed, then normalized to unit length.  The result is
            // the slerp of the two input quaternions with t-value of 1/2.  The result
            // is converted back to a rotation matrix and its columns are selected as
            // the merged box axes.
            Quaterniond q0 = new Quaterniond(), q1 = new Quaterniond();
            Matrix3d rot0 = new Matrix3d(ref box0.AxisX, ref box0.AxisY, ref box0.AxisZ, false);
            q0.SetFromRotationMatrix(ref rot0);
            Matrix3d rot1 = new Matrix3d(ref box1.AxisX, ref box1.AxisY, ref box1.AxisZ, false);
            q1.SetFromRotationMatrix(ref rot1);
            if (q0.Dot(q1) < 0) {
                q1 = -q1;
            }

            Quaterniond q = q0 + q1;
            double invLength = 1.0 / Math.Sqrt(q.Dot(q));
            q = q * invLength;
            Matrix3d q_mat = q.ToRotationMatrix();
            box.AxisX = q_mat.Column(0); box.AxisY = q_mat.Column(1); box.AxisZ = q_mat.Column(2);  //q.ToRotationMatrix(box.Axis); 

            // Project the input box vertices onto the merged-box axes.  Each axis
            // D[i] containing the current center C has a minimum projected value
            // min[i] and a maximum projected value max[i].  The corresponding end
            // points on the axes are C+min[i]*D[i] and C+max[i]*D[i].  The point C
            // is not necessarily the midpoint for any of the intervals.  The actual
            // box center will be adjusted from C to a point C' that is the midpoint
            // of each interval,
            //   C' = C + sum_{i=0}^2 0.5*(min[i]+max[i])*D[i]
            // The box extents are
            //   e[i] = 0.5*(max[i]-min[i])

            int i, j;
            double dot;
            Vector3d[] vertex = new Vector3d[8];
            Vector3d pmin = Vector3d.Zero;
            Vector3d pmax = Vector3d.Zero;

            box0.ComputeVertices(vertex);
            for (i = 0; i < 8; ++i) {
                Vector3d diff = vertex[i] - box.Center;
                for (j = 0; j < 3; ++j) {
                    dot = box.Axis(j).Dot(ref diff);
                    if (dot > pmax[j]) {
                        pmax[j] = dot;
                    } else if (dot < pmin[j]) {
                        pmin[j] = dot;
                    }
                }
            }

            box1.ComputeVertices(vertex);
            for (i = 0; i < 8; ++i) {
                Vector3d diff = vertex[i] - box.Center;
                for (j = 0; j < 3; ++j) {
                    dot = box.Axis(j).Dot(ref diff);
                    if (dot > pmax[j]) {
                        pmax[j] = dot;
                    } else if (dot < pmin[j]) {
                        pmin[j] = dot;
                    }
                }
            }

            // [min,max] is the axis-aligned box in the coordinate system of the
            // merged box axes.  Update the current box center to be the center of
            // the new box.  Compute the extents based on the new center.
            for (j = 0; j < 3; ++j) {
                box.Center += (0.5*(pmax[j] + pmin[j])) * box.Axis(j);
                box.Extent[j] = 0.5*(pmax[j] - pmin[j]);
            }

            return box;
        }








        public static implicit operator Box3d(Box3f v)
        {
            return new Box3d(v.Center, v.AxisX, v.AxisY, v.AxisZ, v.Extent);
        }
        public static explicit operator Box3f(Box3d v)
        {
            return new Box3f((Vector3f)v.Center, (Vector3f)v.AxisX, (Vector3f)v.AxisY, (Vector3f)v.AxisZ, (Vector3f)v.Extent);
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
		public Box3f(Vector3f center, Vector3f extent) {
			Center = center;
			Extent = extent;
			AxisX = Vector3f.AxisX;
			AxisY = Vector3f.AxisY;
			AxisZ = Vector3f.AxisZ;
		}
		public Box3f(AxisAlignedBox3f aaBox) {
            // [RMS] this should produce Empty for aaBox.Empty...
            Extent = new Vector3f(aaBox.Width * 0.5f, aaBox.Height * 0.5f, aaBox.Depth * 0.5f);
            Center = aaBox.Center;
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


        public IEnumerable<Vector3f> VerticesItr()
        {
            Vector3f extAxis0 = Extent.x * AxisX;
            Vector3f extAxis1 = Extent.y * AxisY;
            Vector3f extAxis2 = Extent.z * AxisZ;
            yield return Center - extAxis0 - extAxis1 - extAxis2;
            yield return Center + extAxis0 - extAxis1 - extAxis2;
            yield return Center + extAxis0 + extAxis1 - extAxis2;
            yield return Center - extAxis0 + extAxis1 - extAxis2;
            yield return Center - extAxis0 - extAxis1 + extAxis2;
            yield return Center + extAxis0 - extAxis1 + extAxis2;
            yield return Center + extAxis0 + extAxis1 + extAxis2;
            yield return Center - extAxis0 + extAxis1 + extAxis2;
        }


        public AxisAlignedBox3f ToAABB()
        {
            // [TODO] probably more efficient way to do this...at minimum can move center-shift
            // to after the containments...
 			Vector3f extAxis0 = Extent.x*AxisX;
			Vector3f extAxis1 = Extent.y*AxisY;
			Vector3f extAxis2 = Extent.z*AxisZ;
            AxisAlignedBox3f result = new AxisAlignedBox3f(Center - extAxis0 - extAxis1 - extAxis2);
			result.Contain(Center + extAxis0 - extAxis1 - extAxis2);
            result.Contain(Center + extAxis0 + extAxis1 - extAxis2);
			result.Contain(Center - extAxis0 + extAxis1 - extAxis2);
			result.Contain(Center - extAxis0 - extAxis1 + extAxis2);
			result.Contain(Center + extAxis0 - extAxis1 + extAxis2);
			result.Contain(Center + extAxis0 + extAxis1 + extAxis2);
            result.Contain(Center - extAxis0 + extAxis1 + extAxis2);
            return result;
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
			get { return 2*Extent.x * 2*Extent.y * 2*Extent.z; }
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

		public bool Contains( Vector3f v ) {
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

        public void Scale(Vector3f s)
        {
            Center *= s;
            Extent *= s;
            AxisX *= s; AxisX.Normalize();
            AxisY *= s; AxisY.Normalize();
            AxisZ *= s; AxisZ.Normalize();
        }

        public void ScaleExtents(Vector3f s)
        {
            Extent *= s;
        }

	}




}
