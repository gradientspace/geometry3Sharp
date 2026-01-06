
[Cleanup]
- [?] audit for class types that could be structs - distance/, intersection/, etc
	- [x] distance: MeshQueries return null (lines 17, 65, and 123), structs cannot be null. Wrapping in null puts them on heap? returning new() for now. considering out parameter? ResultOrFail? Or a TriangleId struct that checks if it's a valid triangle at creation: public ResultOrFail<TriangleId> DMesh3.CreateTriangleId(int index)?
	- [ ] intersection
- [x] update implicit API to use double instead of float
- [ ] remove gs namespace

[Serialization]
- [?] add JsonConverter for various core types (Vector2/Vector3/Vector4, Quat, Frame, Transform, ... ?)
- [x] gltf/glb import/export
- [ ] built-in geometrybuffers support
- [ ] fbx?

[GeometryCore porting]
- [ ] port PolygonMesh from GeometryCore
- [x] port MeshTopology from GeometryCore
- [ ] add any missing functions from GeometryCore vector/box/etc types
- [ ] add split-attribute support to DMesh3 (yikes!)
- [ ] port SRGB/Linear color support from GeometryCore
- [x] add uncompressed Image type (eg see GSImage from GeometryCore)
- [ ] GeometryCore AxisBoxTree2


[Renaming]
- [ ] rename AxisAlignedBox to AxisBox?
- [ ] 