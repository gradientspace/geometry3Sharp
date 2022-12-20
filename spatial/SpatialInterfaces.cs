using System;
using System.Collections.Generic;

namespace g3
{
    // [TODO] this should be called IMeshSpatial? it is specific to triangles.
    public interface ISpatial
    {
        bool SupportsNearestTriangle { get; }

        /// <summary>
        /// Find id of triangle nearest to p within distance fMaxDist, or return DMesh3.InvalidID if not found
        /// </summary>
        int FindNearestTriangle(Vector3d p, double fMaxDist = double.MaxValue);

        bool SupportsTriangleRayIntersection{ get; }

        /// <summary>
        /// Find id of triangle intersected by ray, where intersection point is within distance fMaxDist, or return DMesh3.InvalidID if not found
        /// </summary>
        int FindNearestHitTriangle(Ray3d ray, double fMaxDist = double.MaxValue);

        bool SupportsPointContainment { get; }

        /// <summary>
        /// return true if query point is inside mesh
        /// </summary>
        bool IsInside(Vector3d p);
    }


    public interface IProjectionTarget
    {
        Vector3d Project(Vector3d vPoint, int identifier = -1);
    }

    public interface IOrientedProjectionTarget : IProjectionTarget
    {
        Vector3d Project(Vector3d vPoint, out Vector3d vProjectNormal, int identifier = -1);
    }

    public interface IIntersectionTarget
    {
        bool HasNormal { get; }
        bool RayIntersect(Ray3d ray, out Vector3d vHit, out Vector3d vHitNormal);
    }

}
