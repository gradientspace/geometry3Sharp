![build](https://github.com/SoftSmile-Inc/geometry3Sharp/actions/workflows/build.yml/badge.svg)

# geometry3Sharp

Open-Source (Boost-license) C# library for geometric computing. 

geometry3Sharp is compatible with Unity. Set the G3_USING_UNITY Scripting Define and you will have transparent interop between g3 and Unity vector types (*see details at the very bottom of this README*). Although the library is written for C# 4.5, if you are using the .NET 3.5 Unity runtime, it will still work, just with a few missing features.

Currently there is a small amount of unsafe code, however this code is only used in a few fast-buffer-copy routines, which can be deleted if you need a safe version (eg for Unity web player).

[A Nuget Package is available](https://www.nuget.org/packages/geometry3Sharp). This package is updated roughly monthly from the github master branch. So, it's "more" stable. Currently this package includes .NET 4.5 and .NET Standard 2.0 dlls. If you would like others, please email and they can be added.

Questions? Contact Ryan Schmidt [@rms80](http://www.twitter.com/rms80) / [gradientspace](http://www.gradientspace.com)

# Projects using g3Sharp

* [Gradientspace Cotangent](https://www.cotangent.io/) - 3D printing and Mesh Repair/Modeling Tool
* [Nia Technologies NiaFit](https://niatech.org/technology/niafit/) - 3D-printed prosthetic and orthotic design
* [OrthoVR Project](https://orthovrproject.org/) - 3D-printed lower-leg prosthetic design in VR
* [Archform](https://www.archform.co/) - Clear Dental Aligner design/planning app
* [Your Project Here?](rms@gradientspace.com) - *we are very excited to hear about your project!*


# Credits

Many, many data structures and algorithms have been ported from the WildMagic5 and GTEngine C++ libraries, which are developed by David Eberly at [Geometric Tools](https://www.geometrictools.com/). WildMagic5 and GTEngine are distributed under the Boost license as well, available [here](https://www.geometrictools.com/Downloads/Downloads.html). Any errors in code marked as ported from WildMagic5/GTEngine are most certainly ours!

The **MeshSignedDistanceGrid** class was implemented based on the C++ [SDFGen](https://github.com/christopherbatty/SDFGen) code written by [Christopher Batty](https://cs.uwaterloo.ca/~c2batty/) and [Robert Bridson](http://www.cs.ubc.ca/~rbridson/). 



# Tutorials

Several tutorials for using g3Sharp have been posted on the Gradientspace blog:

- [Creating meshes, Mesh File I/O, Ray/Mesh Intersection and Nearest-Point](http://www.gradientspace.com/tutorials/2017/7/20/basic-mesh-creation-with-g3sharp) - Explains DMesh3 basics, StandardMeshReader, DMeshAABBTree3 ray and point queries and custom traversals
- [Mesh Simplification with Reducer class](http://www.gradientspace.com/tutorials/2017/8/30/mesh-simplification) - Reducer class, DMesh3.CheckValidity, MeshConstraints
- [Remeshing and Mesh Constraints](http://www.gradientspace.com/tutorials/2018/7/5/remeshing-and-constraints) - Remesher class, projection targets, MeshConstraints, Unity remeshing animations
- [Voxelization/Signed Distance Fields and Marching Cubes Remeshing](http://www.gradientspace.com/tutorials/2017/11/21/signed-distance-fields-tutorial) - MeshSignedDistanceGrid, MarchingCubes, DenseGridTrilinearImplicit, generating 3D lattices
- [3D Bitmaps, Minecraft Cubes, and Mesh Winding Numbers](http://www.gradientspace.com/tutorials/2017/12/14/3d-bitmaps-and-minecraft-meshes) - Bitmap3, VoxelSurfaceGenerator, DMeshAABBTree3 Mesh Winding Number, 
- [Implicit Surface Modeling](http://www.gradientspace.com/tutorials/2018/2/20/implicit-surface-modeling) - Implicit primitives, voxel/levelset/functional booleans, offsets, and blending, lattice/lightweighting demo
- [DMesh3: A Dynamic Indexed Triangle Mesh](http://www.gradientspace.com/tutorials/dmesh3) - deep dive into the DMesh3 class's internal data structures and operations
- [Surfacing Point Sets with Fast Winding Numbers](http://www.gradientspace.com/tutorials/2018/9/14/point-set-fast-winding) - tutorial on the Fast Mesh/PointSet Winding Number, and how to use the g3Sharp implementation


# Main Classes

## Core

- **DVector**: indexed list with vector-style interface, but internally stored as separate blocks of memory
    - appending is amortized O(1), never a full buffer copy like normal list
- **RefCountVector**: track index reference counts, maintain list of free indices
- **VectorArray2/VectorArray3**: wrapper around regular array providing N-element access
    - eg operator[] gets/sets Vector3d for VectorArray3d, internally is double[3*count]
- **HBitArray**: hierarchical BitArray, efficient iteration over large-but-sparse bitsets
- **Units**: enums, conversions, string representations
- **gParallel**: multi-threading utilities, including parallel *ForEach* that works w/ .Net 3.5
- **gSerialization**: binary serialization of core types (vectors, frames, polygons, DMesh3)
- **CommandArgumentSet**: string-based argument representation/parsing, useful for command line args, etc
- **DynamicPriorityQueue**: min-heap priority queue for sparse situations (ie subset of large graph). 
- **IndexPriorityQueue**: min-heap priority queue for dense situations (ie small or large number of items in queue)
- **DijkstraGraphDistance**: compute shortest-path distances between nodes in graph, from seed points. Graph is defined externally by iterators and Func's, so this class can easily be applied to many situations.
- **SmallListSet**: efficient allocation of a large number of small lists, with initial fixed-size buffer and "spilling" into linked list.
- **BufferUtil**: utilities for working with arrays. Math on float/double arrays, automatic conversions, byte[] conversions, compression
- **FileSystemUtils**: utilities for filesystem stuff
- *g3Iterators*: IEnumerable utils **ConstantItr**, **RemapItr**, IList hacks **MappedList**, **IntSequence**
- **HashUtil**: **HashBuilder** util for constructing FNV hashes of g3 types
- **MemoryPool**: basic object pool
- *ProfileUtil*: code profiling utility **LocalProfiler** supports multiple timers, accumulating, etc
- *SafeCollections*: **SafeListBuilder** multi-threaded List construction and operator-apply

## Math

- reasonably complete set of vector-math objects, implemented as structs
    - **Vector**2d/3d/4d/2f/3f, **Matrix**2d/2f/3f/3d, **Quaternionf/d**
    - **Segment**2d/3d/2f/3f, **Line**2d/3d/2f/3f, **Ray**3d/3f
    - **Triangle**2d/3d/2f/3f, **Plane**3d/3f
    - **AxisAlignedBox**2d/3d/2f/3f, (oriented) **Box**2d/3d/2f/3f
    - **Index**2/3/4, int **Vector**2i/3i, int **AxisAlignedBox**3i
    - 1D intervals **Interval1d**, and **Interval1i** which is IEnumerable
    - **VectorTuple** 2/3/4 element 2d/3d vector-tuples (convenient())
    - implicit float->double conversion operators between types, explicit double->float operators
    - transparent Unity interop (see below)
- **Frame3f**: position+orientation representation
    - accessors for transformed x/y/z axes 
    - frame transformations
    - free and constrained axis alignment
    - projection to/from frame for points, directions, other frames, 
    - minimum-rotation frame-to-frame alignment
    - ray-plane intersection
    - **Frames are awesome** and you should use them instead of matrices!!
- **MathUtil**: constants, IsFinite, EpsilonEqual, Clamp, RangeClamp, SignedClamp, ClampAngle (properly handles negative angles & zero-crossings!), 3-item Min/Max/MinMax, PlaneAngle, MostParallelAxis, Lerp, SmoothInterp, SmoothRise0To1, LinearRampT (with deadzone), Area and Normal of 3D triangle, FastNormal, VectorCot/VectorTan (fast co/tangent between 3D vectors), IsObtuse, IsLeft, SolveQuadratic
- **TransformSequence**: stack of affine transformations
- **IndexUtil**: utility functions for working with tuples/lists of indices (cycling, filtering, etc)
- **BoundsUtil**: construct bboxes from different data sources, containment tests
- **QueryTuple2d**: robust 2D triangle predicates (ported from GTEngine)
- **Integrate1d**: Romberg integration, Gaussian quadrature with legendre polynomials, trapezoid rule
- **ScalarMap**: 1D function reconstruction from sampled data



## Approximation

- **BiArcFit2**: fit 2D bi-arc to pair of points and tangents
- **QuadraticFit2**: fit general quadratic or 2D circle to set of 2D points
- **GaussPointsFit3**: fit mean/covariance of gaussian distribution to set of 3D points
- **OrthogonalPlaneFit3**: fit of plane to 3D point set


## Solvers

- basic arbitrary-size **DenseMatrix**, **DenseVector**, **DiagonalMatrix**, **SymmetricSparseMatrix** (based on Dictionary), **PackedSparseMatrix** (row arrays)
- **CholeskyDecomposition** dense-matrix Cholesky decomposition, optionally multi-threaded
- **SparseSymmetricCG** conjugate-gradient matrix solver w/ support for preconditioning, client-provided matrix/vector multiply
- **SparseSymmetricCGMultipleRHS** variant that supports multiple right-hand sides
- **SingularValueDecomposition** SVD for arbitrary matrices
- **SymmetricEigenSolver** eigensolver for symmetric matrices using Symmetric QR, ported from GTEngine.



## Color

- **Colorf**: float rgba color, with many standard colors pre-defined
- **Colorb**: byte rgba color
- **ColorHSV**: Hue-Saturation-Value color, convert to/from RGB


## Distance Queries

- 2D:
	- point/curve: **DistPoint2Circle2**
	- point/area:  **DistPoint2Box2**
	- linear/linear: **DistLine2Line2**, **DistLine2Segment2**, **DistSegment2Segment2**
- 3D 
    - point/area: **DistPoint3Triangle3**
    - point/curve: **DistPoint3Circle3**
    - point/volume: **DistPoint3Cylinder3** (signed)
    - linear/linear: **DistLine3Ray3**, **DistLine3Segment3**,  **DistRay3Segment3**, **DistRay3Ray3**
    - linear/area: **DistLine3Triangle3**, **DistSegment3Triangle3**
    - area/area: **DistTriangle3Triangle3**
    
## Intersection Queries    
    
- 2D: 
    - linear/linear: **IntrLine2Line2**, **IntrLine2Segment2**, **IntrSegment2Segment2**
    - linear/area: **IntrLine2Triangle2**, **IntrSegment2Triangle2**
    - area/area: **IntrTriangle2Triangle2**
- 3D: 
    - linear/area: **IntrRay3Triangle3**
    - linear/volume: **IntrLine3Box3**, **IntrSegment3Box3**, **IntrRay3Box3**, **IntrLine3AxisAlignedBox3**, **IntrRay3AxisAlignedBox3**
    - area/area: **IntrTriangle3Triangle3**
    - ray-sphere and ray-cylinder


## Containment
- 2D:
	- **ContMinCircle2**: compute minimal-area circle containing input point set
	- **ContMinBox2**: minimal-area box containing input point set, double & 64-bit integer
	- **TilingUtil**: rectilinear and hexagonal 2D tilings
- 3D:
	- **ContBox3**: fit oriented bounding-box to (possibly weighted) point set
	


## Meshes

- **SimpleMesh**: standard indexed mesh class
    - dense index space, backed by DVector buffers
- **DMesh3**: dynamic mesh class
    - reference-counted sparse index space
    - has edge topology, neighbour queries, etc
    - data stored as DVector buffers of POD-types
    - positions are doubles, normals/colors/uv floats  (and optional)
    - add/remove vertices and triangles, safe SetTriangle
    - manifold-preserving Split/Flip/Collapse and PokeTriangle operators
    - MergeEdges edge-welding
- **DSubmesh3**: sub-region of a DMesh3
    - creates a new DMesh3 that is a subset of triangles of input DMesh3
    - keeps track of index map relationships, region border information
- **EdgeLoop** / **EdgeSpan**: explicit representation of mesh edge structures in a DMesh3
- **Remesher**: edge split/flip/collapse + vtx smooth remeshing
    - entire mesh can be constrained to lie on an IProjectionTarget (eg for reprojection onto initial surface)
    - use **MeshConstraints** to preserve features
	 - individual edge split/flip/collapse restrictions
	 - vertices can be pinned to fixed positions
	 - vertices can be constrained to an IProjectionTarget - eg 3D polylines, smooth curves, surfaces, etc
    - **MeshConstraintUtil** constructs common constraint situations
- **RemesherPro**: extension of Remesher that can remesh much more quickly
    - FastestRemesh() uses active-set queue to converge, instead of fixed full-mesh passes
    - SharpEdgeReprojectionRemesh() tries to remesh while aligning triangle face normals to the projection target, in an attempt to preserve sharp edges
    - FastSplitIteration() quickly splits edges to increase available vertex resolution
- **RegionRemesher**: applies *Remesher* to sub-region of a *DMesh3*, via *DSubmesh3*
    - boundary of sub-region automatically preserved
    - *BackPropropagate()* function integrates submesh back into input mesh
- **EdgeLoopRemesher**: variant of **Remesher** that remeshes around an mesh border
- **Reducer**: edge-collapse mesh simplification using QEM (Quadric Error Metric)
    - entire mesh can be constrained to lie on an IProjectionTarget (eg for reprojection onto initial surface)
    - uses same **MeshConstraints** system as Remesher
- **MeshEditor**: low-level mesh editing operations
    - operations check that they can be applied and most will back themselves out if operation fails
    - *AppendMesh*, *AddTriangleFan*, *DuplicateTriangles*, *ReverseTriangles*, *RemoveTriangles*, *SeparateTriangles*
    - *StitchLoop*, *StitchSpan*, *StitchUnorderedEdges*
    - *ReinsertSubmesh* can re-insert modified submesh via *DSubmesh3*
    - *RemoveAllBowtieVertices* removes neighbourhoods around bowtie vertices
    - *AppendBox* (useful for debugging!)
- **MeshTransforms**: mesh Translate/Rotate/Scale, map to/from *Frame3*, convert Y/Z up, Left/Right-handedness
- **MeshMeasurements**: mesh Genus, Volume, Center of Mass, inertia tensor, Centroid, bounds under arbitrary transforms
- **MeshNormals**: estimate vertex normals
- **MeshWeights**: vertex one-ring operations based on different weighting schemes
    - *OneRingCentroid*, *CotanCentroid*, *VoronoiArea*, *MeanValueCentroid*
- **FaceGroupOptimizer**: clean up facegroup boundary toppology, dilate/contract
- **FaceGroupUtil**: utility functions for querying/manipulating mesh face/triangle groups
- **MeshUtil**: utility functions for mesh operations
- **MeshIterators**: various useful mesh iterators (eg boundary verts, interior verts, etc)
- **MeshDecomposition**: breaks large mesh up into smaller submeshes of maximum size, eg for use in rendering or parallel computation
    - produces *Component* objects that can track associations
    - client provides *IMeshComponentManager* implementation that implements desired submesh functionality
    - currently only supports decomposition via a linear axis sorting
- various mesh generators in **/mesh_generators**
    - most mesh generators support generating shared or not-shared vertices along sharp edges, UV seams, etc
    - some support generating sections of shape (eg wedge-shaped portion of cylinder)
    - **TrivialBox3Generator**, **GridBox3Generator** (subdivided box)
    - **SphereGenerator** (normalized gridded box)
    - **OpenCylinderGenerator**, **CappedCylinderGenerator**, **ConeGenerator**  (support start/end angles)
    - **TrivialDiscGenerator**, **PuncturedDiscGenerator**, **TrivialRectGenerator**, **RoundRectGenerator**
    - **VerticalGeneralizedCylinderGenerator**
    - **TubeGenerator**: polygon swept along polyline
    - **Curve3Axis3RevolveGenerator**: 3D polyline revolved around 3D axis
    - **Curve3Curve3RevolveGenerator**: 3D polyline revolved around 3D polyline (!)
    - **TriangulatedPolygonGenerator**: triangulate 2D polygon-with-holes
    - **VoxelSurfaceGenerator**: generates minecraft-y voxel mesh surface
    - **MarchingCubes**: multi-threaded triangulation of implicit functions / scalar fields
    - **MarchingCubesPro**: continuation-method approach to marching cubes that explores isosurface from seed points (more efficient but may miss things if seed points are insufficient)
    

## Mesh Selections

- **MeshVertexSelection**: create/manipulate set of vertices. grow by one-rings, tris-to-verts, etc
- **MeshFaceSelection**: similiar. *LocalOptimize()* 'cleans up' irregular selection boundaries.
- **MeshEdgeSelection**: also similar. 
- **MeshConnectedComponents**: find connected components, with configurable seed and filter functions
- **MeshBoundaryLoops**: find set of closed boundary edge loops in DMesh3, output as **EdgeLoop** objects
	- will find smallest loops in cases where boundary has "bowtie" vertices
	- supports filtering via EdgeFilterF, to restrict search area
	- can also output open **EdgeSpan**s that may occur when filtering
- **MeshRegionBoundaryLoops**: finds boundary loops around subset of triangles in mesh
- **MeshFacesFromLoop**: finds set of faces containd in 3D curve embedded in mesh



## Mesh Operations

- **LaplacianMeshDeformer**: basic laplacian mesh deformation, currently only symmetrized uniform weights, conjugate-gradient solve
- **LaplacianMeshSmoother**: laplacian mesh smoother w/ per-vertex soft constraints, CG-solve
- **MeshExtrudeFaces**: offset a subset of faces of a mesh and connect w/ triangle strip
- **MeshExtrudeLoop**: offset a boundary loop of mesh and connect w/ triangle strip
- **MeshExtrudeMesh**: extrude all faces of mesh and stitch boundaries w/ triangle strips
- **MeshICP**: basic iterative-closest-point alignment to target surface
- **MeshInsertUVPolyCurve**: insert a 2D polyline (optionally closed) into a 2D mesh
- **MeshInsertPolygon**: insert a 2D polygon-with-holes into a 2D mesh and return set of triangles "inside" polygon
- **MeshInsertProjectedPolygon**: variant of MeshInsertPolygon that inserts 2D polygon onto 3D mesh surface via projection plane
- **MeshIterativeSmooth**: standard iterative vertex-laplacian smoothing with uniform, cotan, mean-value weights
- **MeshLocalParam**: calculate Discrete Exponential Map uv-coords around a point on mesh
- **MeshLoopClosure**: cap open region of mesh with a plane
- **MeshLoopSmooth**: smooth an embedded *EdgeLoop* of a mesh
- **MeshPlaneCut**: cut a mesh with a plane, return new **EdgeLoop**s and **EdgeSpans**, and optionally fill holes
- **RegionOperator**: support class that makes it easy to extract a submesh and safely re-integrate it back into base mesh. IE like RegionRemesher, but you can do arbitrary changes to the submesh (as long as you preserve boundary).
- **MeshStitchLoops**: Stitch together two edge loops without any constraint that they have the same vertex count
- **MeshTrimLoop**: trim mesh with 3D polyline curve lying on mesh faces (approximately)
- **MeshIsoCurve**: compute piecewise-linear iso-curves of a function on a mesh, as a **DGraph3**
- **MeshTopology**: Extract mesh sharp-edge-path topology based on crease angle
- **MeshAssembly**: Decompose mesh into submeshes based on connected solids and open patches
- **MeshSpatialSort**: sorts set of mesh components into "solids" (each solid is outer mesh and contained cavity meshes)
- **MeshMeshCut**: Cut one mesh with another, and optionally remove contained regions
- **MeshBoolean**: Apply **MeshMeshCut** to each of a pair of meshes, and then try to resample cut boundaries so they have same vertices. **This is not a robust mesh boolean!**
- **SimpleHoleFiller**: topological filling of an open boundary edge loop. No attempt to preserve shape whatsoever!
- **SmoothedHoleFill**: fill hole in mesh smoothly, ie with (approximate) boundary tangent continuity
- **MinimalHoleFill**: construct "minimal" fill that is often developable (recovers sharp edges well)
- **PlanarHoleFiller**: fill planar holes in mesh by mapping to 2D, handles nested holes (eg from plane cut through torus)
- **PlanarSpansFiller**: try to fill disconnected set of planar spans, by chaining them (WIP)
- **MeshRepairOrientation**: make triangle winding order consistent across mesh connected components (if possible), and then assign global orientation via spatial sorting/nesting
- **MergeCoincidentEdges**: weld coincident open boundary edges of mesh (more robust than weld vertices!)
- **RemoveDuplicateTriangles**: remove duplicate triangles of mesh
- **RemoteOccludedTriangles**: remove triangles that are "occluded" under various definitions
- **MeshAutoRepair**: apply many of the above algorithms in an attempt to automatically "repair" an input mesh, where "repaired" means the mesh is closed and manifold.


## Spatial Data Structures

- **DMeshAABBTree3**: triangle mesh axis-aligned bounding box tree
	- bottom-up construction using mesh topology to accelerate leaf node layer
	- generic traversal interface DoTraversal(TreeTraversal)
	- FindNearestTriangle(point), FindNearestHitTriangle(ray) and FindAllHitTriangles(ray), FindNearestVertex(point)
	- FindNearestTriangles(other_tree)
	- TestIntersection(triangle), TestIntersection(other_tree), FindAllIntersections(other_tree)
	- IsInside(point), WindingNumber(point), FastWindingNumber(point)
- **PointAABBTree3**: point variant of DMeshAABBTree3, with PointSet Fast Winding Number
- **Polygon2dBoxTree**: 2D segment bbox-tree, distance query
- **PointHashGrid2d**, **SegmentHashGrid2d**: hash tables for 2D geometry elements
- **PointHashGrid3d**: hash tables for 3D geometry elements
- **GridIndexing**/**GridIndexing2**: various interfaces/classes for mapping between 3D spaces and uniform grid indices
- **DSparseGrid3**: allocate-on-demand sparse 3D grid
- **Bitmap3**: 3D dense bitmap
- **BiGrid3**: two-level DSparseGrid3
- **MeshSignedDistanceGrid**: 3D fast-marching construction of narrow-band level set / voxel-distance-field for mesh
- **MeshScalarSamplingGrid**: Samples scalar function on 3D grid. Can sample full grid or narrow band around specific iso-contour
- **MeshWindingNumberGrid**: MeshScalarSamplingGrid variant specifically for computing narrow-band Mesh Winding Number field on meshes with holes (finds narrow-band in hole regions via flood-fill)
- **CachingMeshSDF**: variant of MeshSignedDistanceGrid that does lazy evaluation of distances (eg for use with continuation-method MarchingCubesPro)
- **IProjectionTarget** implementations for DCurve3, DMesh3, Plane3, Circle3d, Cylinder3d, etc, for use w/ reprojection in Remesher and other algorithms
- **IIntersectionTarget** implementations for DMesh3, transformed DMesh3, Plane3



## 2D Curves

- **Circle2d**, **Arc2d**, **Ellipse2d**, **EllipseArc2d**, **PolyLine2d**
- **Polygon2d**: closed polyline with signed area, point-in-polygon test, polygon/polygon intersection, polygon-in-polygon, simplification
- **NURBSCurve2**: open nonuniform, closed and periodic uniform NURBS splines, derivatives up to 3rd order, curvature, total arc length and arc-length sampling. Uses **BSplineBasis** internally, which works in any dimension
- All curves implement common **IParametricCurve2d** interface, as does **Segment2d**.
- **ParametricCurveSequence2**: open or closed sequential set of connected parametric curves
- **CurveSampler2**: parameter-space or arc-length sampling of IParametricCurve2d. AutoSample function transparently handles multi-segment sequential curves. Reasonably good knot-interval sampling of NURBS curves, does the right things with sharp knots.
- **PlanarComplex2**: assembly of open and closed IParametricCurve2d curves, as well as point-samplings. Chaining of curves into sequences. Extraction of clean closed loops with interior holes, determined by polygon containment. 
- **GeneralPolygon2d**: outer polygon with interior polygonal holes, with configurable outer/inner clockwise-ness
- **PlanarSolid2d**: parametric variant of GeneralPolygon2D
- **SampledArcLengthParam**: arc-length parameterization of polylines
- **DGraph2**: dynamic arbitrary-topology 2D graph (nodes and edges). 2D variant of **DMesh3**.
- **DGraph2Resampler**: remesher for DGraph2
- **DGraph2Util**: utilities for DGraph2, ExtractCurves, DisconnectJunctions, ...
- **Hexagon2**: hexagon type w/ hex-math
- **PolygonFont2d**: GPolygon2d representation of font outlines, generate fonts with **gsPolyFontGenerator** tool in [gsMeshUtilities](https://github.com/gradientspace/gsMeshUtilities).

## 2D Computational Geometry

- **ConvexHull2**: 2D convex hull, compute w/ doubles or 64-bit integers
- **Arrangement2d**: compute 2D line-segmenent *arrangement*, ie find split inserted line segments at intersection points
- **GraphSplitter2D**: Bisect existing DGraph w/ infinite lines (simpler than Arrangment2d)
- **GraphCells2D**: extract enclosed regions ("cells") from a DGraph2, as boundary loops


## 3D Curves

- **DCurve3**: 3D polyline
- **CurveUtil**: queries like Ray/curve intersection based on curve thickness, nearest index, etc
- **InPlaceIterativeCurveSmooth**, **SculptMoveDeformation**, **ArcLengthSoftTranslation**: simple DCurve3 deformers
- **CurveResampler**: edge split/collapses resampling of a 3D polyline 
- **Circle3d**
- **SampledArcLengthParam**: arc-length parameterization discrete-sampled 3D curve
- **DGraph3**: dynamic arbitrary-topology 3D graph (nodes and edges), 3D variant of DGraph2
- **DGraph3Util**: ExtractCurves, DisconnectJunctions, etc

## 3D Solids

- **Cylinder3d**
- **DenseGridTrilinearImplicit**: trilinear interpolant of 3D grid
- **CachingDenseGridTrilinearImplicit**: variant of DenseGridTrilinearImplicit that does lazy evaluation of grid values based on an implicit function


## I/O    
    
- format-agnostic **StandardMeshReader** and **StandardMeshWriter**
    - can register additional format handlers beyond supported defaults
    - constructs mesh via generic interface, **SimpleMeshBuilder** and **DMesh3Builder** provided
- readers & writers configurable via **ReadOptions** and **WriteOptions**
- **OBJReader/Writer** - supports vertex colors extension, read/write face groups, UVs, OBJ .mtl files
    - stores texture map paths but you have to load images yourself
    - currently **cannot** produce meshes with multiple UVs per vertex (not supported in DMesh3), vertices will be duplicated along UV seams
- **STLReader/Writer**: STL format, basic vertex welding to reconstruct topology
- **OFFReader/Writer**: OFF file format
- **gSerialization**: binary Store/Restore functions for many g3 types / data structures
- **SVGWriter**: write 2D geometric elements in svg format




## Misc

- 2D implicit blobs
- 2D Marching Quads




# Unity Interop

geometry3Sharp supports transparent conversion with Unity types.
To enable this, define **G3_USING_UNITY** in your Unity project, by adding this
string to the **Scripting Define Symbols** box in the **Player Settings**.  

Once enabled, code like this will work transparently:

~~~~
Vector3 unityVec;
Vector3f g3Vec;
unityVec = g3vec;
g3vec = unityVec;
~~~~

float->double types will work transparently, while double->float will require an explicit cast:

~~~~
Vector3d g3vecd;
g3vecd = gameObject.transform.position;
gameObject.transform.position = (Vector3)g3vecd;
~~~~

This will work for **Vector2**, **Vector3**, **Quaterion**, **Ray**, **Color**, and **Bounds** (w/ AxisAlignedBox3f)
Note that these conversions will **not** work for equations, so to add a Vector3f and a Vector3, you
will need to explicitly cast one to the other.

