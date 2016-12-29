# geometry3Sharp

Open-Source (Apache 2.0) C# library for geometry things. Only uses .NET 3.5, so it works with Unity C# runtime. No dependencies.

Questions? Contact Ryan Schmidt [@rms80](http://www.twitter.com/rms80) / [gradientspace](http://www.gradientspace.com)


# Core

- **DVector**: indexed list with vector-style interface, but internally stored as separate blocks of memory
    - appending is amortized O(1), never a full buffer copy like normal list

- **RefCountVector**: track index reference counts, maintain list of free indices

- **VectorArray2/VectorArray3**: wrapper around regular array providing N-element access
    - eg operator[] gets/sets Vector3d for VectorArray3d, internally is double[3*count]


# Math

- reasonly complete set of vector-math objects, implemented as structs
    - Vector2/3, AxisAlignedBox2/3, Matrix2/3, Quaternion, Segment2/3, Line2/3, Ray3, Triangle3
    - double & float versions of vector types (and int types for vectors)
    - implicit float->double conversion operators between types, explicit double->float operators
    - transparent Unity interop (see below)

- **Colorf**: float rgba color, with many standard colors pre-defined

- **Frame3f**: position+orientation representation
    - accessors for transformed x/y/z axes 
    - frame transformations
    - free and constrained axis alignment
    - projection to/from frame for points, directions, other frames, 
    - minimum-rotation frame-to-frame alignment
    - ray-plane intersection
    - **Frames are awesome** and you should use them instead of matrices!!

- **Integrate1d**: Romberg integration, Gaussian quadrature with legendre polynomials, trapezoid rule
- **Interval1d**: 1D interval class/intersection/etc

# Queries

- 3D Line-type Distances: **DistLine3Ray3**, **DistLine3Segment3**,  **DistRay3Segment3**, **DistRay3Ray3**
- 3D Triangle distances: **DistPoint3Triangle3**, **DistLine3Triangle3**, **DistSegment3Triangle3**, **DistTriangle3Triangle3**
- 2D Intersections: **IntrLine2Line2**, **IntrSegment2Segment2**
- 3D Intersections: **IntrRay3Triangle3**
- ray-sphere and ray-cylinder intersection


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
    
- **Remesher**: basic edge split/flip/collapse remeshing

- various mesh generators
    - open & closed cylinders, disc, punctured disc, with start/end angles
    - **VerticalGeneralizedCylinderGenerator**
    - **TubeGenerator**: polygon swept along polyline
    - trivial plane
    - **Curve3Axis3RevolveGenerator**: 3D polyline revolved around 3D axis
    - **Curve3Curve3RevolveGenerator**: 3D polyline revolved around 3D polyline (!)
    
- OBJ reader/writer 
    - reader supports OBJ materials and texture maps (paths, you sort out loading images yourself)


# 2D Curves

- **Circle2d**, **Arc2d**, **Ellipse2d**, **EllipseArc2d**, **PolyLine2d** 
- **Polygon2d**: closed polyline with signed area, point-in-polygon test, polygon/polygon intersection, polygon-in-polygon, simplification
- **NURBSCurve2**: open nonuniform, closed and periodic uniform NURBS splines, derivatives up to 3rd order, curvature, total arc length and arc-length sampling. Uses **BSplineBasis** internally, which works in any dimension
- All curves implement common **IParametricCurve2d** interface, as does **Segment2d**.
- **ParametricCurveSequence2**: open or closed sequential set of connected parametric curves
- **CurveSampler2**: parameter-space or arc-length sampling of IParametricCurve2d. AutoSample function transparently handles multi-segment sequential curves. Reasonably good knot-interval sampling of NURBS curves, does the right things with sharp knots.
- **PlanarComplex2**: assembly of open and closed IParametricCurve2d curves, as well as point-samplings. Chaining of curves into sequences. Extraction of clean closed loops with interior holes, determined by polygon containment. 
- **GeneralPolygon2d**: outer polygon with interior polygonal holes, with configurable outer/inner clockwise-ness


# 3D Curves

- **DCurve3**: 3D polyline
- **CurveUtil**: queries like Ray/curve intersection based on curve thickness, nearest index, etc
- **InPlaceIterativeCurveSmooth**, **SculptMoveDeformation**, **ArcLengthSoftTranslation**: simple DCurve3 deformers
- **CurveResampler**: edge split/collapses resampling of a 3D polyline 

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

