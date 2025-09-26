// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Distributed under the Boost Software License, Version 1.0. http://www.boost.org/LICENSE_1_0.txt
using System;
using System.Text;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;


#nullable enable

namespace g3
{
    public static class USDFile
    {
        // notes on USD transform order, matrix storage, and coordinate systems:
        // https://openusd.org/dev/api/usd_geom_page_front.html#UsdGeom_LinAlgBasics


        /// <summary>
        /// USDScene is what results from parsing a USD file. 
        /// Conceivably this is analogous to a USD Stage.
        /// </summary>
        public class USDScene
        {
            required public USDPrim Root;
        }


        /// <summary>
        /// A USDPrim is a node in the USD scene hierarchy.
        /// </summary>
        public class USDPrim
        {
            public USDPath Path = USDPath.MakeRoot();

            public EDefType PrimType;
            public string? CustomPrimTypeName = null;

            public USDAttrib[] Attribs = Array.Empty<USDAttrib>();

            public List<USDPrim> Children = new List<USDPrim>();

            public string ShortName => Path.ShortName;
            public string FullPath => Path.FullPath;


            public override string ToString() {
                string useName = (PrimType != EDefType.Unknown) ?
                    DefTypeTokens[(int)PrimType] : (CustomPrimTypeName??"(null)");
                return  $"[{useName}] {FullPath}";
            }

            public USDAttrib? FindAttribByName(string name)
            {
                return Array.Find(Attribs, (attrib) => { return attrib.Name == name; });
            }
        }


        /// <summary>
        /// A USDAttrib is a typed name/value pair on a Prim.
        /// The .Data is stored as an object?, use the .USDType and .IsArray to determine/cast as necessary
        /// </summary>
        public class USDAttrib
        {
            public string Name = "";
            public USDValue Value;

            public EUSDType USDType => Value.TypeInfo.USDType;
            public bool IsArray => Value.TypeInfo.bIsArray;
            public object? Data => Value.data;

            public override string ToString() {
                string typeString = FieldTypeTokens[(int)Value.TypeInfo.USDType];
                return $"{typeString} {Name} = {Value}";
            }
        }


        /// <summary>
        /// USDPath stores a path in the USD file. All Prims and Attribs have unique paths,
        /// and the Prim hierarchy is 1-1 with the path structure.
        /// </summary>
        public class USDPath
        {
            // todo as we build paths we want to keep track of parent/child relationship,
            // this will make it easier to build the scene...

            protected string prim = "";
            protected string prop = "";
            protected string local = "";
            protected bool bValid = false;

            // todo these are only used by USDCReader to build hierarchy and should be internal there
            public int index = -1;
            public int parent_index = -1;

            public USDPath() { bValid = false; }
            public USDPath(string prim)
            {
                this.prim = prim;
                this.local = prim;
                bValid = true;
            }

            public bool IsValid => bValid;
            public bool IsEmpty => string.IsNullOrEmpty(prim) && string.IsNullOrEmpty(prop);

            public string FullPath => $"{valid_prefix}{prim}{prop_suffix}";
            public string ShortName => local;
            public override string ToString() { return FullPath; }
            private string valid_prefix => (bValid ? "" : "INVALID#");
            private string prop_suffix => (string.IsNullOrEmpty(prop) ? "" : "." + prop);

            public USDPath Duplicate() {
                return new USDPath() { prim = this.prim, prop = this.prop, local = this.local, bValid = this.bValid };
            }

            public static USDPath MakeRoot() { return new USDPath("/"); }

            public static USDPath MakeInvalid(USDPath srcPath)
            {
                Debugger.Break();
                return new USDPath() { prim = srcPath.prim, prop = srcPath.prop, local = srcPath.local, bValid = false };
            }
            public static USDPath CombineProperty(USDPath srcPath, string prop)
            {
                if (string.IsNullOrEmpty(prop) || prop[0] == '{' || prop[0] == '[' || prop[0] == '.')
                    return MakeInvalid(srcPath);
                return new USDPath() { prim = srcPath.prim, prop = prop, local = prop, bValid = srcPath.bValid, parent_index = srcPath.index };
            }
            public static USDPath CombineElement(USDPath srcPath, string elem)
            {
                if (string.IsNullOrEmpty(elem) || elem[0] == '{' || elem[0] == '[' || elem[0] == '.')
                    return MakeInvalid(srcPath);

                string new_path = srcPath.prim;
                new_path += ((new_path.Length == 1) && (new_path[0] == '/')) ? elem : ('/' + elem);

                return new USDPath() { prim = new_path, prop = srcPath.prop, local = elem, bValid = srcPath.bValid, parent_index = srcPath.index };
            }
        }






        // to support:
        // 

        public enum EDefType
        {
            Unknown, 
            NoType,
            PsuedoRoot,
            Scope,

            XForm,
            Sphere,
            Cube,
            Cylinder,
            Cone,
            Mesh,
            GeomSubset,

            Points,
            PointInstancer,

            BasisCurves,

            Camera,
            Shader,
            Material,

            Skeleton,
            SkelRoot,
            SkelAnimation,
            BlendShape,

            SphereLight,
            DomeLight,
            CylinderLight,
            DiskLight,
            DistantLight

        }
        public static readonly string[] DefTypeTokens = [
            "Unknown",
            "NoType",
            "PsuedoRoot",
            "Scope",

            "Xform",
            "Sphere",
            "Cube",
            "Cylinder",
            "Cone",
            "Mesh",
            "GeomSubset",

            "Points",
            "PointInstancer",

            "BasisCurves",

            "Camera",
            "Shader",
            "Material",

            "SkelRoot",
            "Skeleton",
            "SkelAnimation",
            "BlendShape",

            "SphereLight",
            "DomeLight",
            "CylinderLight",
            "DiskLight",
            "DistantLight"

            ];



        // https://openusd.org/dev/api/_usd__page__datatypes.html
        public enum EUSDType
        {
            Unknown = 0,

            // basic data types
            // https://openusd.org/dev/api/_usd__page__datatypes.html#Usd_Basic_Datatypes

            Bool,
            UChar,      // 8 bit unsigned integer
            Int,        // 32 bit signed integer
            UInt,       // 32 bit unsigned integer
            Int64,      // 64 bit signed integer
            UInt64,     // 64 bit unsigned integer
            Half,       // 16 bit floating point
            Float,      // 32 bit floating point
            Double,     // 64 bit floating point
            String,     // string
            Token,      // interned string with fast comparison and hashing
            Asset,      // represents a resolvable path to another asset
            Matrix2d,   // 2x2 matrix of doubles
            Matrix3d,   // 3x3 matrix of doubles
            Matrix4d,   // 4x4 matrix of doubles
            Quatd,      // double-precision quaternion
            Quatf,      // single-precision quaternion
            Quath,      // half-precision quaternion
            Double2,    // vector of 2 doubles
            Float2,     // vector of 2 floats
            Half2,      // vector of 2 half's
            Int2,       // vector of 2 ints
            Double3,    // vector of 3 doubles
            Float3,     // vector of 3 floats
            Half3,      // vector of 3 half's
            Int3,       // vector of 3 ints
            Double4,    // vector of 4 doubles
            Float4,     // vector of 4 floats
            Half4,      // vector of 4 half's
            Int4,       // vector of 4 ints


            Dictionary = 31,

            TokenListOp = 32,
            StringListOp = 33,
            PathListOp = 34,
            ReferenceListOp = 35,
            IntListOp = 36,
            Int64ListOp = 37,
            UIntListOp = 38,
            UInt64ListOp = 39,

            PathVector = 40,
            TokenVector = 41,

            Specifier = 42,
            Permission = 43,
            Variability = 44,

            VariantSelectionMap = 45,
            TimeSamples = 46,
            Payload = 47,

            DoubleVector = 48,
            LayerOffsetVector = 49,
            StringVector = 50,

            ValueBlock = 51,
            Value = 52,

            UnregisteredValue = 53,
            UnregisteredValueListOp = 54,
            PayloadListOp = 55,

            TimeCode = 56,
            PathExpression = 57,

            Relocates = 58,
            Spline = 59,
            AnimationBlock = 60,


            // role data types
            // https://openusd.org/dev/api/_usd__page__datatypes.html#Usd_Roles

            Point3d,    // double3 Point   transform as a position
            Point3f,    // float3  Point   transform as a position
            Point3h,    // half3   Point   transform as a position
            Normal3d,   // double Normal  transform as a normal
            Normal3f,   // float3  Normal transform as a normal
            Normal3h,   // half3   Normal transform as a normal
            Vector3d,   // double3 Vector transform as a direction
            Vector3f,   // float3  Vector transform as a direction
            Vector3h,   // half3   Vector transform as a direction
            Color3d,    // double3 Color energy-linear RGB
            Color3f,    // float3  Color energy-linear RGB
            Color3h,    // half3   Color energy-linear RGB
            Color4d,    // double4 Color energy-linear RGBA, not pre-alpha multiplied
            Color4f,    // float4  Color energy-linear RGBA, not pre-alpha multiplied
            Color4h,    // half4   Color energy-linear RGBA, not pre-alpha multiplied
            Frame4d,    // matrix4d    Frame defines a coordinate frame
            TexCoord2d, // double2 TextureCoordinate	2D uv texture coordinate
            TexCoord2f, // float2 TextureCoordinate	2D uv texture coordinate
            TexCoord2h, // half2 TextureCoordinate	2D uv texture coordinate
            TexCoord3d, // double3 TextureCoordinate	3D uvw texture coordinate
            TexCoord3f, // float3 TextureCoordinate	3D uvw texture coordinate
            TexCoord3h, // half3 TextureCoordinate	3D uvw texture coordinate

            Group,      // opaque Group   used as a grouping mechanism for namespaced properties
            Rel,
            Opaque,     // represents a value that can't be serialized

            LastType
        };
        // string tokens for above type enum - order must stay the same!
        public static readonly string[] FieldTypeTokens = [
            "unknown",

            "bool",
            "uchar",
            "int",
            "uint",
            "int64",
            "uint64",
            "half",
            "float",
            "double",
            "string",
            "token",
            "asset",
            "matrix2d",
            "matrix3d",
            "matrix4d",
            "quatd",
            "quatf",
            "quath",
            "double2",
            "float2",
            "half2",
            "int2",
            "double3",
            "float3",
            "half3",
            "int3",
            "double4",
            "float4",
            "half4",
            "int4",

            // not sure about the strings in this next block...
            "dict",
            "tokenListOp",
            "stringListOp",
            "pathListOp",
            "referenceListOp",
            "intListOp",
            "int64ListOp",
            "uintListOp",
            "uint64ListOp",
            "pathVector",
            "tokenVector",
            "specifier",
            "permission",
            "variability",
            "variantSelectionMap",
            "timeSamples",
            "payload",
            "doubleVector",
            "layerOffsetVector",
            "stringVector",
            "valueBlock",
            "value",
            "unregisteredValue",
            "unregisteredValueListOp",
            "payloadListOp",
            "timecode",
            "pathExpression",
            "relocates",
            "spline",
            "animationBlock",


            "point3d",
            "point3f",
            "point3h",
            "normal3d",
            "normal3f",
            "normal3h",
            "vector3d",
            "vector3f",
            "vector3h",
            "color3d",
            "color3f",
            "color3h",
            "color4d",
            "color4f",
            "color4h",
            "frame4d",
            "texCoord2d",
            "texCoord2f",
            "texCoord2h",
            "texCoord3d",
            "texCoord3f",
            "texCoord3h",

            "group",
            "rel",
            "opaque",


            ];

        public static EUSDType GetGenericType(EUSDType specificType)
        {
            switch (specificType) {
                case EUSDType.TexCoord2f:
                    return EUSDType.Float2;
                case EUSDType.TexCoord2h:
                    return EUSDType.Half2;
                case EUSDType.TexCoord2d:
                    return EUSDType.Double2;
                case EUSDType.Point3f:
                case EUSDType.Color3f:
                case EUSDType.Vector3f:
                case EUSDType.Normal3f:
                case EUSDType.TexCoord3f:
                    return EUSDType.Float3;
                case EUSDType.Point3h:
                case EUSDType.Color3h:
                case EUSDType.Vector3h:
                case EUSDType.Normal3h:
                case EUSDType.TexCoord3h:
                    return EUSDType.Half3;
                case EUSDType.Vector3d:
                case EUSDType.Normal3d:
                case EUSDType.Point3d:
                case EUSDType.Color3d:
                case EUSDType.TexCoord3d:
                    return EUSDType.Double3;
                case EUSDType.Color4f:
                    return EUSDType.Float4;
                case EUSDType.Color4h:
                    return EUSDType.Half4;
                case EUSDType.Color4d:
                    return EUSDType.Double4;
                case EUSDType.Frame4d:
                    return EUSDType.Matrix4d;
            }
            return specificType;
        }
        public static bool IsTypeCompatible(EUSDType genericType, EUSDType specificType)
        {
            if (genericType == specificType) return true;
            return GetGenericType(specificType) == genericType;
        }


        public struct USDTypeInfo
        {
            public EUSDType USDType = EUSDType.Unknown;
            public bool bIsArray = false;
            public bool bUniform = false;
            public bool bCustom = false;

            public override string ToString()
            {
                StringBuilder sb = new StringBuilder();
                if (bUniform) sb.Append("uniform ");
                if (bCustom) sb.Append("custom ");
                sb.Append(FieldTypeTokens[(int)USDType]);
                if (bIsArray)
                    sb.Append("[]");
                return sb.ToString();
            }

            public USDTypeInfo() { }
        }

        public struct USDValue
        {
            public USDTypeInfo TypeInfo;

            public bool bIsConnect = false;
            public object? data = null;

            public bool IsValid => data != null;

            public USDValue() { }

            public override string ToString()
            {
                if (data == null) return "null";
                else if (TypeInfo.bIsArray) {
                    if (data is string[] stringList) {
                        StringBuilder result = new StringBuilder();
                        result.Append('[');
                        for (int i = 0; i < stringList.Length; ++i) {
                            result.Append($"\"{stringList[i]}\"");
                            if (i < stringList.Length-1) result.Append(", ");
                        }
                        result.Append(']');
                        return result.ToString();
                    } else {
                        string typeString = FieldTypeTokens[(int)TypeInfo.USDType];
                        int NumElems = (data as Array)!.Length;
                        return $"{typeString}[{NumElems}]";
                    }
                } else {
                    return (data.ToString() ?? "(unknown)");
                }
            }
        }


        // these should probably be promoted to general g3sharp utility types
        [System.Runtime.CompilerServices.InlineArray(16)]
        public struct real_list16 {
            private double _element0;
        }
        [System.Runtime.CompilerServices.InlineArray(8)]
        public struct int64_list8 {
            private long _element0;
        }


        public struct vec2i
        {
            public int x, y;
            public vec2i(int xx = 0, int yy = 0) { x = xx; y = yy; }
            public vec2i(in int64_list8 l) { x = (int)l[0]; y = (int)l[1]; }
            public vec2i(ReadOnlySpan<int> vv) { x = vv[0]; y = vv[1]; }
            public override string ToString() { return $"({x},{y})"; }
        }
        public struct vec3i
        {
            public int x, y, z;
            public vec3i(int xx = 0, int yy = 0, int zz = 0) { x = xx; y = yy; z = zz; }
            public vec3i(in int64_list8 l) { x = (int)l[0]; y = (int)l[1]; z = (int)l[2]; }
            public vec3i(ReadOnlySpan<int> v) { x = v[0]; y = v[1]; z = v[2]; }
            public override string ToString() { return $"({x},{y},{z})"; }
        }
        public struct vec4i
        {
            public int x, y, z, w;
            public vec4i(int xx = 0, int yy = 0, int zz = 0, int ww = 0) { x = xx; y = yy; z = zz; w = ww; }
            public vec4i(in int64_list8 l) { x = (int)l[0]; y = (int)l[1]; z = (int)l[2]; w = (int)l[3]; }
            public vec4i(ReadOnlySpan<int> v) { x = v[0]; y = v[1]; z = v[2]; w = v[3]; }
            public override string ToString() { return $"({x},{y},{z},{w})"; }
        }

        public struct vec2f
        {
            public float u, v;
            public vec2f(float uu = 0, float vv = 0) { u = uu; v = vv; }
            public vec2f(in real_list16 l) { u = (float)l[0]; v = (float)l[1]; }
            public vec2f(ReadOnlySpan<float> vv) { u = vv[0]; v = vv[1]; }
            public override string ToString() { return $"({u},{v})"; }
            public static implicit operator Vector2f(vec2f v) { return new Vector2f(v.u, v.v); }
            public static implicit operator Vector2d(vec2f v) { return new Vector2d(v.u, v.v); }
        }
        public struct vec2d
        {
            public double u, v;
            public vec2d(double uu = 0, double vv = 0) { u = uu; v = vv; }
            public vec2d() { u = 0; v = 0; }
            public vec2d(in real_list16 l) { u = l[0]; v = l[1]; }
            public vec2d(ReadOnlySpan<double> vv) { u = vv[0]; v = vv[1]; }
            public override string ToString() { return $"({u},{v})"; }
            public static implicit operator Vector2d(vec2d v) { return new Vector2d(v.u, v.v); }
        }

        public struct vec3f
        {
            public float x, y, z;
            public vec3f(float xx = 0, float yy = 0, float zz = 0) { x = xx; y = yy; z = zz; }
            public vec3f(in real_list16 l) { x = (float)l[0]; y = (float)l[1]; z = (float)l[2]; }
            public vec3f(ReadOnlySpan<float> v) { x = v[0]; y = v[1]; z = v[2]; }
            public override string ToString() { return $"({x},{y},{z})"; }
            public static implicit operator Vector3f(vec3f v) { return new Vector3f(v.x, v.y, v.z); }
            public static implicit operator Vector3d(vec3f v) { return new Vector3d(v.x, v.y, v.z); }
        }
        public struct vec3d
        {
            public double x, y, z;
            public vec3d(double xx = 0, double yy = 0, double zz = 0) { x = xx; y = yy; z = zz; }
            public vec3d(in real_list16 l) { x = l[0]; y = l[1]; z = l[2]; }
            public vec3d(ReadOnlySpan<double> v) { x = v[0]; y = v[1]; z = v[2]; }
            public override string ToString() { return $"({x},{y},{z})"; }
            public static implicit operator Vector3d(vec3d v) { return new Vector3d(v.x, v.y, v.z); }
        }

        public struct vec4f
        {
            public float x, y, z, w;
            public vec4f(float xx = 0, float yy = 0, float zz = 0, float ww = 0) { x = xx; y = yy; z = zz; w = ww; }
            public vec4f(in real_list16 l) { x = (float)l[0]; y = (float)l[1]; z = (float)l[2]; w = (float)l[3]; }
            public vec4f(ReadOnlySpan<float> v) { x = v[0]; y = v[1]; z = v[2]; w = v[3]; }
            public override string ToString() { return $"({x},{y},{z},{w})"; }
        }
        public struct vec4d
        {
            public double x, y, z, w;
            public vec4d(double xx = 0, double yy = 0, double zz = 0, double ww = 0) { x = xx; y = yy; z = zz; w = ww; }
            public vec4d(in real_list16 l) { x = l[0]; y = l[1]; z = l[2]; w = l[3]; }
            public vec4d(ReadOnlySpan<double> v) { x = v[0]; y = v[1]; z = v[2]; w = v[3]; }
            public override string ToString() { return $"({x},{y},{z},{w})"; }
            public static implicit operator Vector4d(vec4d v) { return new Vector4d(v.x, v.y, v.z, v.w); }
        }

        public struct matrix2d
        {
            public vec2d row0;
            public vec2d row1;
            public matrix2d() { }
            public matrix2d(ReadOnlySpan<double> m)
            {
                row0 = new vec2d(m[0], m[1]);
                row1 = new vec2d(m[2], m[3]);
            }
            public override string ToString() { return $"({row0},{row1})"; }
        }
        public struct matrix3d
        {
            public vec3d row0;
            public vec3d row1;
            public vec3d row2;
            public matrix3d() { }
            public matrix3d(ReadOnlySpan<double> m) {
                row0 = new vec3d(m[0], m[1], m[2]);
                row1 = new vec3d(m[3], m[4], m[5]);
                row2 = new vec3d(m[6], m[7], m[8]);
            }
            public override string ToString() { return $"({row0},{row1},{row2})"; }
        }
        public struct matrix4d
        {
            public vec4d row0;
            public vec4d row1;
            public vec4d row2;
            public vec4d row3;
            public matrix4d() { }
            public matrix4d(ReadOnlySpan<double> m) {
                row0 = new vec4d(m[0], m[1], m[2], m[3]);
                row1 = new vec4d(m[4], m[5], m[6], m[7]);
                row2 = new vec4d(m[8], m[9], m[10], m[11]);
                row3 = new vec4d(m[12], m[13], m[14], m[15]);
            }
            public override string ToString() { return $"({row0},{row1},{row2},{row3})"; }
        }

        public struct quat4f
        {
            public float w, x, y, z;
            public quat4f(float ww = 0, float xx = 0, float yy = 0, float zz = 0) { w = ww; x = xx; y = yy; z = zz; }
            public quat4f(in real_list16 l) { w = (float)l[0]; x = (float)l[1]; y = (float)l[2]; z = (float)l[3]; }
            public quat4f(ReadOnlySpan<float> v) { w = v[0]; x = v[1]; y = v[2]; z = v[3]; }
            public override string ToString() { return $"({w},{x},{y},{z})"; }
        }
        public struct quat4d
        {
            public double w, x, y, z;
            public quat4d(double ww = 0, double xx = 0, double yy = 0, double zz = 0) { w = ww; x = xx; y = yy; z = zz; }
            public quat4d(in real_list16 l) { w = l[0]; x = l[1]; y = l[2]; z = l[3]; }
            public quat4d(ReadOnlySpan<double> v) { w = v[0]; x = v[1]; y = v[2]; z = v[3]; }
            public override string ToString() { return $"({w},{x},{y},{z})"; }
        }


        public struct USDLayerOffset
        {
            public double offset;
            public double scale;
        }


        public struct USDListOpHeader
        {
            public byte data = 0;

            public bool IsExplicit => (data & 0b00000001) != 0;
            public bool HasExplicitItems => (data & 0b00000010) != 0;
            public bool HasAddedItems => (data & 0b00000100) != 0;
            public bool HasDeletedItems => (data & 0b00001000) != 0;
            public bool HasOrderedItems => (data & 0b00010000) != 0;
            public bool HasPrependedItems => (data & 0b00100000) != 0;
            public bool HasAppendedItems => (data & 0b01000000) != 0;
            public USDListOpHeader() {}
        }


        public struct USDReference
        {
            public string assetPath;
            public USDPath primPath;
            public USDLayerOffset layerOffset;
            public object? customData;      // this is a dictionary, if it is non-null
        }

        public class USDReferenceListOp
        {
            public USDListOpHeader header;
            public USDReference[] references = [];
        }





        public static void ExpandReferences(USDScene scene, ReadOptions options)
        {
            expand_references(scene.Root, options);
        }
        static void expand_references(USDPrim prim, ReadOptions options)
        {
            foreach (USDAttrib attrib in prim.Attribs) {
                if (attrib.USDType != EUSDType.ReferenceListOp || !(attrib.Data is USDReferenceListOp))
                    continue;
                USDReferenceListOp listOp = (USDReferenceListOp)attrib.Data;
                Util.gDevAssert(listOp.header.HasDeletedItems == false);
                // what to do w/ other list ops??
                Util.gDevAssert(listOp.references.Length == 1);
                USDReference reference = listOp.references[0];

                // TODO should cache this somehow, dumb to read multiple times...
                // can be async
                // should be done in a way that handles usda too
                // ideally want to forward warningEvent

                string filePath = System.IO.Path.Combine(options.BaseFilePath, reference.assetPath);
                if (System.IO.File.Exists(filePath)) {
                    USDCReader reader = new USDCReader();
                    IOReadResult result = reader.Read(filePath, options, out USDScene childScene);
                    if (result.code == IOCode.Ok) {
                        prim.Children.Add(childScene.Root);
                    }
                }
            }

            foreach (USDPrim child in prim.Children)
                expand_references(child, options);
        }
    }
}
