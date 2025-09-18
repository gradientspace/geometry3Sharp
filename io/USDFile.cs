// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Distributed under the Boost Software License, Version 1.0. http://www.boost.org/LICENSE_1_0.txt
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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


        // types to support:
        // matrix4d[]
        // uint2

        public enum EUSDType
        {
            Unknown,
            String,
            Token,
            Rel,
            Asset,
            Bool,
            Int,
            Float,
            Float2,
            Float3,
            Float4,
            Double,
            Double3,
            Half,
            Half3,
            Point3f,
            Vector3f,
            Vector3d,
            Normal3f,
            Color3f,
            TexCoord2f,
            Quatf,
            Matrix4d
        };
        public static readonly string[] FieldTypeTokens = [
            "unknown",
            "string",
            "token",
            "rel",
            "asset",
            "bool",
            "int",
            "float",
            "float2",
            "float3",
            "float4",
            "double",
            "double3",
            "half",
            "half3",
            "point3f",
            "vector3f",
            "vector3d",
            "normal3f",
            "color3f",
            "texcoord2f",
            "quatf",
            "matrix4d"
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
    }
}
