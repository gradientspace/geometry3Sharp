// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Distributed under the Boost Software License, Version 1.0. http://www.boost.org/LICENSE_1_0.txt
using System;
using System.Text;


#nullable enable

namespace g3
{
    public class USDFile
    {

        // to support:
        // 

        public enum EDefType
        {
            Unknown, 
            NoType,
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
            "Unkown",
            "NoType",
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
            Unknown,
            Rel,

            // basic data types
            // https://openusd.org/dev/api/_usd__page__datatypes.html#Usd_Basic_Datatypes

            Bool,
            UChar,      // -8 bit unsigned integer
            Int,        // -32 bit signed integer
            UInt,       // -32 bit unsigned integer
            Int64,      // -64 bit signed integer
            UInt64,     // -64 bit unsigned integer
            Half,       // -16 bit floating point
            Float,      // -32 bit floating point
            Double,     // -64 bit floating point
            Timecode,   // double representing a resolvable time
            String,     // -string
            Token,      // -interned string with fast comparison and hashing
            Asset,      // -represents a resolvable path to another asset
            Opaque,     // represents a value that can't be serialized
            Matrix2d,   // 2x2 matrix of doubles
            Matrix3d,   // 3x3 matrix of doubles
            Matrix4d,   // 4x4 matrix of doubles
            Quatd,      // -double-precision quaternion
            Quatf,      // -single-precision quaternion
            Quath,      // -half-precision quaternion
            Double2,    // -vector of 2 doubles
            Float2,     // -vector of 2 floats
            Half2,      // -vector of 2 half's
            Int2,       // vector of 2 ints
            Double3,    // -vector of 3 doubles
            Float3,     // -vector of 3 floats
            Half3,      // -vector of 3 half's
            Int3,       // vector of 3 ints
            Double4,    // -vector of 4 doubles
            Float4,     // -vector of 4 floats
            Half4,      // -vector of 4 half's
            Int4,       // vector of 4 ints


            // role data types
            // https://openusd.org/dev/api/_usd__page__datatypes.html#Usd_Roles

            Point3d,    // -double3 Point   transform as a position
            Point3f,    // -float3  Point   transform as a position
            Point3h,    // -half3   Point   transform as a position
            Normal3d,   // -double Normal  transform as a normal
            Normal3f,   // -float3  Normal transform as a normal
            Normal3h,   // -half3   Normal transform as a normal
            Vector3d,   // -double3 Vector transform as a direction
            Vector3f,   // -float3  Vector transform as a direction
            Vector3h,   // -half3   Vector transform as a direction
            Color3d,    // -double3 Color energy-linear RGB
            Color3f,    // -float3  Color energy-linear RGB
            Color3h,    // -half3   Color energy-linear RGB
            Color4d,    // -double4 Color energy-linear RGBA, not pre-alpha multiplied
            Color4f,    // -float4  Color energy-linear RGBA, not pre-alpha multiplied
            Color4h,    // -half4   Color energy-linear RGBA, not pre-alpha multiplied
            Frame4d,    // matrix4d    Frame defines a coordinate frame
            TexCoord2d, // -double2 TextureCoordinate	2D uv texture coordinate
            TexCoord2f, // -float2 TextureCoordinate	2D uv texture coordinate
            TexCoord2h, // -half2 TextureCoordinate	2D uv texture coordinate
            TexCoord3d, // -double3 TextureCoordinate	3D uvw texture coordinate
            TexCoord3f, // -float3 TextureCoordinate	3D uvw texture coordinate
            TexCoord3h, // -half3 TextureCoordinate	3D uvw texture coordinate
            Group       // opaque Group   used as a grouping mechanism for namespaced properties
        };
        // string tokens for above type enum - order must stay the same!
        public static readonly string[] FieldTypeTokens = [
            "unknown",
            "rel",

            "bool",
            "uchar",
            "int",
            "uint",
            "int64",
            "uint64",
            "half",
            "float",
            "double",
            "timecode",
            "string",
            "token",
            "asset",
            "opaque",
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
            "group"
            ];

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
                string typeString = FieldTypeTokens[(int)TypeInfo.USDType];
                if (data == null) return "null";
                else if (TypeInfo.bIsArray) {
                    int NumElems = (data as Array)!.Length;
                    return $"{typeString}[{NumElems}]";
                } else {
                    return data.ToString() ?? "(unknown)";
                }
            }
        }


        public struct vec2f
        {
            public float u;
            public float v;
            public override string ToString() { return $"({u},{v})"; }
        }
        public struct vec2d
        {
            public double u;
            public double v;
            public override string ToString() { return $"({u},{v})"; }
        }

        public struct vec3f
        {
            public float x;
            public float y;
            public float z;
            public override string ToString() { return $"({x},{y},{z})"; }
        }
        public struct vec3d
        {
            public double x;
            public double y;
            public double z;
            public override string ToString() { return $"({x},{y},{z})"; }
        }

        public struct vec4f
        {
            public float x;
            public float y;
            public float z;
            public float w;
            public override string ToString() { return $"({x},{y},{z},{w})"; }
        }
        public struct vec4d
        {
            public double x;
            public double y;
            public double z;
            public double w;
            public override string ToString() { return $"({x},{y},{z},{w})"; }
        }

        public struct matrix4d
        {
            public vec4f row0;
            public vec4f row1;
            public vec4f row2;
            public vec4f row3;
            public override string ToString() { return $"({row0},{row1},{row2},{row3})"; }
        }

        public struct quat4f
        {
            public float w;
            public float x;
            public float y;
            public float z;
            public override string ToString() { return $"({w},{x},{y},{z})"; }
        }
        public struct quat4d
        {
            public double w;
            public double x;
            public double y;
            public double z;
            public override string ToString() { return $"({w},{x},{y},{z})"; }
        }
    }
}
