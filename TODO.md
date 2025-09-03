
[Cleanup]
- audit for class types that could be structs - distance/, intersection/, etc
- update implicit API to use double instead of float

[Serialization]
- add JsonConverter for various core types (Vector2/Vector3/Vector4, Quat, Frame, Transform, ... ?)
- gltf/glb import/export
- built-in geometrybuffers support
- fbx?

[GeometryCore porting]
- port PolygonMesh from GeometryCore
- port MeshTopology from GeometryCore
- add any missing functions from GeometryCore vector/box/etc types
- add split-attribute support to DMesh3 (yikes!)
- port SRGB/Linear color support from GeometryCore
- add uncompressed Image type (eg see GSImage from GeometryCore)
- GeometryCore AxisBoxTree2


[Renaming]
- rename AxisAlignedBox to AxisBox?
- 