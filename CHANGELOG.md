# Version 4.0.0

The manin changes in this Version are that the Namespace has been changed from `g3` to `VirgisGeometry` and there are many new integrations into Unity.

- Namespace changed from `g3` to `VirgisGeometry`
- Add Matrix4d Type
- Add Cast from Unity Matrix4x4 to Matrix4d and double4x4 to Matrix4d. This allows the use of Unity Transforms in VirgisGeometry.
- Add Casts of Vector2d and Vector3d as (homogeneous coordinates) in Vector3d and Vector4d
- Added management of AxisOrder for Vector3d, Vector3f and DMesh3.
- Added arbitrary data objects to DCurve3 to allow per vertex data - e.g. feature IDs
- Added the ability to triangulate a mesh from vertices in DMesh3Builder.

# Version 3.1.2

- Updated Burst Triangulator to version 2.5.0

# Version 3.1.1

- added a 6-color colorisation routine to DMesh3

# Version 3.1.0

- Added support for Unity.Mathematics primities using implict and explicit conversions.

- improved coverage of primative conversions

- added implicit conversions to Unity Mesh

- added Polygon mesh creation routines using Delaunay Triangulation


# Version 3.0.0

The first version under the ViRGiS Geometry Package name. Renoved the need for the Unity Ccompile symbol.
