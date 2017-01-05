using System;
using System.Collections.Generic;

namespace g3
{
    public interface ISpatial
    {
        bool SupportsNearestTriangle { get; }
        int FindNearestTriangle(Vector3d p);
    }


    public interface IProjectionTarget
    {
        Vector3d Project(Vector3d vPoint, int identifier = -1);
    }
}
