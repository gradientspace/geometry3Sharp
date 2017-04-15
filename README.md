# geometry3Sharp

Open-Source (Boost-license) C# library for geometric computing. 

geometry3Sharp only uses C# language features available in .NET 3.5, so it works with the Mono C# runtime used in Unity 5.x (*NOTE: you must configure Unity for this to work, see note at bottom of this file*). 

Currently there is a small amount of unsafe code, however this code is only used in the OBJ reader and a few fast-buffer-copy routines, which can be deleted if you need a safe version (eg for Unity web player).

Some portions of the code are ported from the WildMagic5 C++ library, developed by David Eberly at [Geometric Tools](https://www.geometrictools.com/). WildMagic5 is distributed under the Boost license as well, available [here](https://www.geometrictools.com/Downloads/Downloads.html). Any errors in code marked as ported from WildMagic5 are most certainly ours!

Questions? Contact Ryan Schmidt [@rms80](http://www.twitter.com/rms80) / [gradientspace](http://www.gradientspace.com)


# Core

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


# Math

- reasonably complete set of vector-math objects, implemented as structs
    - Vector2/3, Matrix2/3, Quaternion, Segment2/3, Line2/3, Ray3, Triangle2/3, AxisAlignedBox2/3, (oriented) Box2/3
    - Index2/3/4
    - Interval1d, and Interval1i which is IEnumerable
    - double & float versions of vector/line/ray/segment/box types (and int types for vectors)
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

- **TransformSequence**: stack of affine transformations
- **IndexUtil**: utility functions for working with tuples/lists of indices (cycling, filtering, etc)
- **BoundsUtil**: construct bboxes from different data sources, containment tests

- **Integrate1d**: Romberg integration, Gaussian quadrature with legendre polynomials, trapezoid rule
- **Interval1d**: 1D interval class/intersection/etc


# Solvers

- basic arbitrary-size **DenseMatrix**, **DiagonalMatrix**, **SymmetricSparseMatrix**
- **SparseSymmetricCG** conjugate-gradient matrix solver
- **SingularValueDecomposition** SVD for arbitrary matrices



# Color

- **Colorf**: float rgba color, with many standard colors pre-defined
- **Colorb**: byte rgba color
- **ColorHSV**: Hue-Saturation-Value color, convert to/from RGB


# Distance Queries

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
    
# Intersection Queries    
    
- 2D: 
    - linear/linear: **IntrLine2Line2**, **IntrSegment2Segment2**
    - linear/area: **IntrLine2Triangle2**, **IntrSegment2Triangle2**
    - area/area: **IntrTriangle2Triangle2**
- 3D: 
    - linear/area: **IntrRay3Triangle3**
    - linear/volume: **IntrLine3Box3**, **IntrSegment3Box3**, **IntrRay3Box3**, **IntrLine3AxisAlignedBox3**, **IntrRay3AxisAlignedBox3**
    - area/area: **IntrTriangle3Triangle3**
    - ray-sphere and ray-cylinder


# Meshes

- **SimpleMesh**: standard indexed mesh class
    - dense index space, backed by DVector buffers

- **DMesh3**: dynamic mesh class
    - reference-counted sparse index space
    - has edge topology, neighbour queries, etc
    - data stored as DVector buffers of POD-types
    - positions are doubles, normals/colors/uv floats  (and optional)
    - add/remove vertices
    - manifold-preserving Split/Flip/Collapse operators
    
- **DSubmesh3**: sub-region of a DMesh3
    - creates a new DMesh3 that is a subset of triangles of input DMesh3
    - keeps track of index map relationships, region border information

- **Remesher**: edge split/flip/collapse + vtx smooth remeshing
    - entire mesh can be constrained to lie on an IProjectionTarget (eg for reprojection onto initial surface)
    - use **MeshConstraints** to preserve features
	 - individual edge split/flip/collapse restrictions
	 - vertices can be pinned to fixed positions
	 - vertices can be constrained to an IProjectionTarget - eg 3D polylines, smooth curves, surfaces, etc
    - **MeshConstraintUtil** constructs common constraint situations

- **RegionRemesher**: applies *Remesher* to sub-region of a *DMesh3*, via *DSubmesh3*
    - boundary of sub-region automatically preserved
    - *BackPropropagate()* function integrates submesh back into input mesh
    
- Mesh manipulation/query utilities
    - **MeshEditor**: low-level mesh editing operations
        - operations check that they can be applied and most will back themselves out if operation fails
        - *RemoveTriangles*, *AddTriangleFan*, *Stitch Loops*, *AppendMesh*
        - *ReinsertSubmesh* can re-insert modified submesh via *DSubmesh3*
        - *RemoveAllBowtieVertices* removes neighbourhoods around bowtie vertices
    - **MeshVertexSelection**: create/manipulate set of vertices. grow by one-rings, tris-to-verts, etc
    - **MeshFaceSelection**: similiar. *LocalOptimize()* 'cleans up' irregular selection boundaries.
    - **MeshConnectedComponents**: find connected components, with configurable seed and filter functions
    - **MeshTransforms**: mesh Translate/Rotate/Scale, map to/from *Frame3*, convert Y/Z up, Left/Right-handedness
    - **MeshMeasurements**: mesh volume, center of mass, inertia tensor, centroid, bounds under arbitrary transforms
    - **MeshNormals**: estimate vertex normals
    - **MeshWeights**: vertex one-ring operations based on different weighting schemes
        - *OneRingCentroid*, *CotanCentroid*, *VoronoiArea*, *MeanValueCentroid*, 
    - **MeshBoundaryLoops**: find set of closed boundary edge loops in DMesh3, output as **EdgeLoop** objects
	- will find smallest loops in cases where boundary has "bowtie" vertices
    - **FaceGroupUtil**: utility functions for manipulating mesh face/triangle groups
    - **MeshUtil**: utility functions for mesh operations

- **MeshDecomposition**: breaks large mesh up into smaller submeshes of maximum size, eg for use in rendering or parallel computation
    - produces *Component* objects that can track associations
    - client provides *IMeshComponentManager* implementation that implements desired submesh functionality
    - currently only supports decomposition via a linear axis sorting

- various mesh generators in **/mesh_generators**
    - most mesh generators support generating shared or not-shared vertices along sharp edges, UV seams, etc
    - some support generating sections of shape (eg wedge-shaped portion of cylinder)
    - **TrivialBox3Generator**
    - **OpenCylinderGenerator**, **CappedCylinderGenerator**, **ConeGenerator**  (support start/end angles)
    - **TrivialDiscGenerator**, **PuncturedDiscGenerator**, **TrivialRectGenerator**, **RoundRectGenerator**
    - **VerticalGeneralizedCylinderGenerator**
    - **TubeGenerator**: polygon swept along polyline
    - **Curve3Axis3RevolveGenerator**: 3D polyline revolved around 3D axis
    - **Curve3Curve3RevolveGenerator**: 3D polyline revolved around 3D polyline (!)
    


# Mesh Operations

- **LaplacianMeshDeformer**: basic laplacian mesh deformation, currently only symmetrized uniform weights, conjugate-gradient solve
- **MeshExtrusion**: duplicate existing boundary edge loop, offset to new position, stitch together with original
- **MeshIterativeSmooth**: standard iterative vertex-laplacian smoothing with uniform, cotan, mean-value weights
- **SimpleHoleFiller**: topological filling of an open boundary edge loop. No attempt to preserve shape whatsoever!
- **MeshICP**: basic iterative-closest-point alignment to target surface
- **MeshLoopSmooth**: smooth embedded *EdgeLoop* of mesh


# Spatial Data Structures

- **DMeshAABBTree**: triangle mesh axis-aligned bounding box tree
	- bottom-up construction using mesh topology to accelerate leaf node layer
	- generic traversal interface
	- Queries for NearestTriangle, FindNearestHitTriangle (raycast), (more to come)


# 2D Curves

- **Circle2d**, **Arc2d**, **Ellipse2d**, **EllipseArc2d**, **PolyLine2d** 
- **Polygon2d**: closed polyline with signed area, point-in-polygon test, polygon/polygon intersection, polygon-in-polygon, simplification
- **NURBSCurve2**: open nonuniform, closed and periodic uniform NURBS splines, derivatives up to 3rd order, curvature, total arc length and arc-length sampling. Uses **BSplineBasis** internally, which works in any dimension
- All curves implement common **IParametricCurve2d** interface, as does **Segment2d**.
- **ParametricCurveSequence2**: open or closed sequential set of connected parametric curves
- **CurveSampler2**: parameter-space or arc-length sampling of IParametricCurve2d. AutoSample function transparently handles multi-segment sequential curves. Reasonably good knot-interval sampling of NURBS curves, does the right things with sharp knots.
- **PlanarComplex2**: assembly of open and closed IParametricCurve2d curves, as well as point-samplings. Chaining of curves into sequences. Extraction of clean closed loops with interior holes, determined by polygon containment. 
- **GeneralPolygon2d**: outer polygon with interior polygonal holes, with configurable outer/inner clockwise-ness
- **PlanarSolid2d**: parametric variant of GeneralPolygon2D


# 3D Curves

- **DCurve3**: 3D polyline
- **CurveUtil**: queries like Ray/curve intersection based on curve thickness, nearest index, etc
- **InPlaceIterativeCurveSmooth**, **SculptMoveDeformation**, **ArcLengthSoftTranslation**: simple DCurve3 deformers
- **CurveResampler**: edge split/collapses resampling of a 3D polyline 
- **Circle3d**
- **SampledArcLengthParam**: arc-length parameterization discrete-sampled 3D curve

# 3D Solids

- **Cylinder3d**


# I/O    
    
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





# Misc

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

