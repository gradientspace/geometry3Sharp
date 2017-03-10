using System;
using System.Collections.Generic;

namespace g3
{
    public interface ISpatial
    {
        bool SupportsNearestTriangle { get; }
        int FindNearestTriangle(Vector3d p, double fMaxDist = double.MaxValue);

        bool SupportsTriangleRayIntersection{ get; }
        int FindNearestHitTriangle(Ray3d ray, double fMaxDist = double.MaxValue);

        bool SupportsPointContainment { get; }
        bool IsInside(Vector3d p);
    }


    public interface IProjectionTarget
    {
        Vector3d Project(Vector3d vPoint, int identifier = -1);
    }

    public interface IIntersectionTarget
    {
        bool HasNormal { get; }
        bool RayIntersect(Ray3d ray, out Vector3d vHit, out Vector3d vHitNormal);
    }

}
