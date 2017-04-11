using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    public static class MeshMeasurements
    {

        // ported from WildMagic5 Wm5PolyhedralMassProperties
        //
        // Computes mass/volume, center of mass, and 3x3 intertia tensor
        // for a closed mesh. You provide an enumerator over triangles and
        // a vertex accessor.
        //
        // If bodyCoords is 'true', the inertia tensor will be relative to
        // body coordinates, if 'false' it is in world coordinates. 
        //
        // the inertia tensor is row-major
        public static void MassProperties( 
            IEnumerable<Index3i> triangle_indices,
            Func<int, Vector3d> getVertexF,
            out double mass, out Vector3d center, out double[,] inertia3x3,
            bool bodyCoords = false )
        {
            const double oneDiv6 = (1.0 / 6.0);
            const double oneDiv24 = (1.0 / 24.0);
            const double oneDiv60 = (1.0 / 60.0);
            const double oneDiv120 = (1.0 / 120.0);

            // order:  1, x, y, z, x^2, y^2, z^2, xy, yz, zx
            double[] integral = new double[10] { 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0 };

            foreach ( Index3i tri in triangle_indices ) {
                // Get vertices of triangle i.
                Vector3d v0 = getVertexF(tri.a);
                Vector3d v1 = getVertexF(tri.b);
                Vector3d v2 = getVertexF(tri.c);

                // Get cross product of edges and normal vector.
                Vector3d V1mV0 = v1 - v0;
                Vector3d V2mV0 = v2 - v0;
                Vector3d N = V1mV0.Cross(V2mV0);

                // Compute integral terms.
                double tmp0, tmp1, tmp2;
                double f1x, f2x, f3x, g0x, g1x, g2x;
                tmp0 = v0.x + v1.x;
                f1x = tmp0 + v2.x;
                tmp1 = v0.x * v0.x;
                tmp2 = tmp1 + v1.x * tmp0;
                f2x = tmp2 + v2.x * f1x;
                f3x = v0.x * tmp1 + v1.x * tmp2 + v2.x * f2x;
                g0x = f2x + v0.x * (f1x + v0.x);
                g1x = f2x + v1.x * (f1x + v1.x);
                g2x = f2x + v2.x * (f1x + v2.x);

                double f1y, f2y, f3y, g0y, g1y, g2y;
                tmp0 = v0.y + v1.y;
                f1y = tmp0 + v2.y;
                tmp1 = v0.y * v0.y;
                tmp2 = tmp1 + v1.y * tmp0;
                f2y = tmp2 + v2.y * f1y;
                f3y = v0.y * tmp1 + v1.y * tmp2 + v2.y * f2y;
                g0y = f2y + v0.y * (f1y + v0.y);
                g1y = f2y + v1.y * (f1y + v1.y);
                g2y = f2y + v2.y * (f1y + v2.y);

                double f1z, f2z, f3z, g0z, g1z, g2z;
                tmp0 = v0.z + v1.z;
                f1z = tmp0 + v2.z;
                tmp1 = v0.z * v0.z;
                tmp2 = tmp1 + v1.z * tmp0;
                f2z = tmp2 + v2.z * f1z;
                f3z = v0.z * tmp1 + v1.z * tmp2 + v2.z * f2z;
                g0z = f2z + v0.z * (f1z + v0.z);
                g1z = f2z + v1.z * (f1z + v1.z);
                g2z = f2z + v2.z * (f1z + v2.z);

                // Update integrals.
                integral[0] += N.x * f1x;
                integral[1] += N.x * f2x;
                integral[2] += N.y * f2y;
                integral[3] += N.z * f2z;
                integral[4] += N.x * f3x;
                integral[5] += N.y * f3y;
                integral[6] += N.z * f3z;
                integral[7] += N.x * (v0.y * g0x + v1.y * g1x + v2.y * g2x);
                integral[8] += N.y * (v0.z * g0y + v1.z * g1y + v2.z * g2y);
                integral[9] += N.z * (v0.x * g0z + v1.x * g1z + v2.x * g2z);
            }

            integral[0] *= oneDiv6;
            integral[1] *= oneDiv24;
            integral[2] *= oneDiv24;
            integral[3] *= oneDiv24;
            integral[4] *= oneDiv60;
            integral[5] *= oneDiv60;
            integral[6] *= oneDiv60;
            integral[7] *= oneDiv120;
            integral[8] *= oneDiv120;
            integral[9] *= oneDiv120;

            // mass
            mass = integral[0];

            // center of mass
            center = new Vector3d(integral[1], integral[2], integral[3]) / mass;

            // inertia3x3 relative to world origin
            inertia3x3 = new double[3, 3];
            inertia3x3[0,0] = integral[5] + integral[6];
            inertia3x3[0,1] = -integral[7];
            inertia3x3[0,2] = -integral[9];
            inertia3x3[1,0] = inertia3x3[0,1];
            inertia3x3[1,1] = integral[4] + integral[6];
            inertia3x3[1,2] = -integral[8];
            inertia3x3[2,0] = inertia3x3[0,2];
            inertia3x3[2,1] = inertia3x3[1,2];
            inertia3x3[2,2] = integral[4] + integral[5];

            // inertia3x3 relative to center of mass
            if (bodyCoords) {
                inertia3x3[0,0] -= mass * (center.y * center.y +
                    center.z * center.z);
                inertia3x3[0,1] += mass * center.x * center.y;
                inertia3x3[0,2] += mass * center.z * center.x;
                inertia3x3[1,0] = inertia3x3[0,1];
                inertia3x3[1,1] -= mass * (center.z * center.z +
                    center.x * center.x);
                inertia3x3[1,2] += mass * center.y * center.z;
                inertia3x3[2,0] = inertia3x3[0,2];
                inertia3x3[2,1] = inertia3x3[1,2];
                inertia3x3[2,2] -= mass * (center.x * center.x +
                    center.y * center.y);
            }
        }
        public static void MassProperties(
            DMesh3 mesh,
            out double mass, out Vector3d center, out double[,] inertia3x3,
            bool bodyCoords = false)
        {
            MassProperties(
                mesh.Triangles(),
                (vID) => { return mesh.GetVertex(vID); },
                out mass, out center, out inertia3x3, false);
        }




        public static Vector3d Centroid(IEnumerable<Vector3d> vertices)
        {
            Vector3d centroid = Vector3d.Zero;
            int N = 0;
            foreach (Vector3d v in vertices) {
                centroid += v;
                N++;
            }
            return centroid / (double)N;
        }


        public static Vector3d Centroid(DMesh3 mesh, bool bOnlyTriVertices = true)
        {
            if (bOnlyTriVertices) {
                Vector3d centroid = Vector3d.Zero;
                int N = 0;
                foreach (int vid in mesh.VertexIndices()) {
                    if (mesh.GetVtxEdgeCount(vid) > 0) {
                        centroid += mesh.GetVertex(vid);
                        N++;
                    }
                }
                return centroid / (double)N;
            } else
                return Centroid(mesh.Vertices());
        }



        public static AxisAlignedBox3d Bounds(DMesh3 mesh, Func<Vector3d, Vector3d> TransformF )
        {
            AxisAlignedBox3d bounds = AxisAlignedBox3d.Empty;
            if (TransformF == null) {
                foreach (Vector3d v in mesh.Vertices())
                    bounds.Contain(v);
            } else {
                foreach (Vector3d v in mesh.Vertices()) {
                    Vector3d vT = TransformF(v);
                    bounds.Contain(vT);
                }
            }
            return bounds;
        }
        public static AxisAlignedBox3d Bounds(IMesh mesh, Func<Vector3d, Vector3d> TransformF )
        {
            AxisAlignedBox3d bounds = AxisAlignedBox3d.Empty;
            if (TransformF == null) {
                foreach (int vID in mesh.VertexIndices())
                    bounds.Contain(mesh.GetVertex(vID));
            } else {
                foreach (int vID in mesh.VertexIndices()) {
                    Vector3d vT = TransformF(mesh.GetVertex(vID));
                    bounds.Contain(vT);
                }
            }
            return bounds;
        }



        public static AxisAlignedBox3d BoundsT(IMesh mesh, int [] triangleIndices, Func<Vector3d, Vector3d> TransformF = null )
        {
            AxisAlignedBox3d bounds = AxisAlignedBox3d.Empty;
            if (TransformF == null) {
                foreach ( int tid in triangleIndices ) {
                    Index3i tri = mesh.GetTriangle(tid);
                    for (int j = 0; j < 3; ++j)
                        bounds.Contain(mesh.GetVertex(tri[j]));
                }
            } else {
                foreach ( int tid in triangleIndices ) {
                    Index3i tri = mesh.GetTriangle(tid);
                    for (int j = 0; j < 3; ++j)
                        bounds.Contain( TransformF(mesh.GetVertex(tri[j])) );
                }
            }
            return bounds;
        }


    }
}
