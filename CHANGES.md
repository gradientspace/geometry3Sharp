
================
dotnet8 branch
================

readonly tags added on many struct functions and properties


Vector3
  - added MathUtil.TryParseRealVector, Vector3d.TryParse
  - added [JSonConverter] for Vector3d (Vector3dJsonConverter) for json serialization


Deprecation of many float geometric types. double types should be used for all computation. Some float types remain for compatibility or memory storage.
  - many 2f/3f geometric types completely removed (Box2f/3f, Line2f/3f, Segment2f/3f, Triangle2f/3f, Plane3f, Ray3f)
  - Added Frame3d, replaced all Frame3f usage
  - Frame3f, Quaternionf, Matrix2f, Matrix3f are all now minimal classes meant only for storage use-cases
  - Added Matrix4d



Added IntrAxisBox2AxisBox2, IntrSegment2AxisBox2, IntrSegment2Circle2

Added OptionalValue<T>, ResultOrFail<T>

Added RandomSampling - PointInBox2, PointOnCircle2

Added TransformWrapper, support in various places
Added MeshTransforms.TransformMesh(Mesh, Matrix4d)

MeshEditor changes/improvements
  - Better AppendMesh (static, handles transform, remaps group IDs, returns maps)


DMesh3 changes
  - invalid/degenerate tris no longer assert in InsertTriangle/AppendTriangle