// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Distributed under the Boost Software License, Version 1.0. http://www.boost.org/LICENSE_1_0.txt
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;


namespace g3
{
#nullable enable

    // GLTF 2.0 spec: https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.html

    public class GLTFFile
    {

        public struct Asset
        {
            public string? copyright { get; set; } = null;
            public string? generator { get; set; } = null;
            public string version { get; set; } = "2.0";

            public Asset() { }
        }


        public enum EComponentType
        {
            SignedByte = 5120,          // 8-bit signed
            UnsignedByte = 5121,        // 8-bit unsigned
            SignedShort = 5122,         // 16-bit signed
            UnsignedShort = 5123,       // 16-bit unsigned
            UnsignedInt = 5125,         // 32-bit unsigned
            Float = 5126                // 32-bit float
        }
        public readonly static int[] EComponentTypeByteCount = [1, 1, 2, 2, 4, 4];

        public enum EElementType
        {
            Scalar = 0,                 // 1 component
            Vec2 = 1,                   // 2 components
            Vec3 = 2,                   // 3 components
            Vec4 = 3,                   // 4 components
            Mat2 = 4,                   // 4 components
            Mat3 = 5,                   // 8 components
            Mat4 = 6                    // 16 components
        }
        public readonly static string[] EElementTypeStrings =
            ["SCALAR", "VEC2", "VEC3", "VEC4", "MAT2", "MAT3", "MAT4"];
        public readonly static int[] EElementTypeElemCount = [1, 2, 3, 4, 4, 8, 16];
        public static string GetElementTypeString(EElementType type) { return EElementTypeStrings[(int)type]; }

        public enum EMeshAttribute
        {
            Position = 0,
            Normal = 1,
            Tangent = 2,
            TexCoord = 3,
            Color = 4,
            Joints = 5,
            Weights = 6
        }
        public const string MeshAttribute_Position = "POSITION";
        public const string MeshAttribute_Normal = "NORMAL";
        public const string MeshAttribute_Tangent = "TANGENT";
        public const string MeshAttribute_UVPrefix = "TEXCOORD_";
        public readonly static string[] EMeshAttributePrefixStrings =
            [MeshAttribute_Position, MeshAttribute_Normal, MeshAttribute_Tangent, "TEXCOORD_", "COLOR_", "JOINTS_", "WEIGHTS_"];




        public enum EPrimitiveMode
        {
            Points = 0,
            Lines = 1,
            LineLoop = 2,
            LineStrip = 3,
            Triangles = 4,
            TriangleStrip = 5,
            TriangleFan = 6
        }
        public readonly static string[] EPrimitiveModeStrings =
            ["POINTS", "LINES", "LINE_LOOP", "LINE_STRIP", "TRIANGLES", "TRIANGLE_STRIP", "TRIANGLE_FAN"];



        public struct SparseAccessorInfo
        {
            public int count { get; set; } = 0;

            public SparseAccessorInfo() { }
        }

        public struct Accessor
        {
            [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
            public int bufferView { get; set; } = 0;
            
            [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
            public int byteOffset { get; set; } = 0;

            [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
            public int componentType { get; set; } = (int)EComponentType.Float;
            
            [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
            public int count { get; set; } = 0;
            
            [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
            public string type { get; set; } = "VEC3";

            // min/max could be int or float...
            public Decimal[]? min { get; set; } = null;
            public Decimal[]? max { get; set; } = null;

            public SparseAccessorInfo? sparse { get; set; } = null;

            [JsonIgnore]
            public EComponentType ComponentTypeEnum {
                get { return (EComponentType)componentType; }
                set { componentType = (int)value; }
            }
            
            public readonly int GetComponentByteCount()
            {
                switch ((EComponentType)componentType) {
                    case EComponentType.SignedByte:
                    case EComponentType.UnsignedByte:
                        return 1;
                    case EComponentType.SignedShort:
                    case EComponentType.UnsignedShort:
                        return 2;
                    case EComponentType.UnsignedInt:
                    case EComponentType.Float:
                        return 4;
                }
                throw new KeyNotFoundException($"unknown Accessor.componentType field {componentType}");
            }

            public readonly int GetElementByteCount()
            {
                EElementType elemType = GetElementType();
                int elemCount = EElementTypeElemCount[(int)elemType] * GetComponentByteCount();
                return elemCount;
            }

            public readonly EElementType GetElementType()
            {
                for (int i = 0; i < EElementTypeStrings.Length; ++i)
                    if (string.Equals(EElementTypeStrings[i], type, StringComparison.OrdinalIgnoreCase))
                        return (EElementType)i;
                throw new KeyNotFoundException($"unknown Accessor.type field {type}");
            }


            public Accessor() { }

        }


        public struct BufferView
        {
            public string? name { get; set; } = null;
            
            [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
            public int buffer { get; set; } = 0;

            [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
            public int byteLength { get; set; } = 0;

            [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
            public int byteOffset { get; set; } = 0;

            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
            public int byteStride { get; set; } = 0;

            public int? target { get; set; } = null;


            public BufferView() { }
        }


        public struct Buffer
        {
            public string? name { get; set; } = null;
            public string? uri { get; set; } = null;

            [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
            public int byteLength { get; set; } = 0;

            public Buffer() { }
        }


        public struct Image
        {
            public string? name { get; set; } = null;

            // only one of these can be defined
            public int? bufferView { get; set; } = null;
            public string? uri { get; set; } = null;

            // must be defined if bufferView is defined
            public string? mimeType { get; set; } = null;

            public Image() { }
        }


        public struct Texture
        {
            public string? name { get; set; } = null;
            public int? source { get; set; } = null;
            public int? sampler { get; set; } = null;      // default undefined

            public Texture() { }
        }


        public enum ESamplerMagFilter { NEAREST = 9728, LINEAR = 9729 };
        public enum ESamplerMinFilter { 
            NEAREST = 9728, LINEAR = 9729, 
            NEAREST_MIPMAP_NEAREST = 9984, LINEAR_MIPMAP_NEAREST = 9985,
            NEAREST_MIPMAP_LINEAR = 9986, LINEAR_MIPMAP_LINEAR = 9987
        };
        public enum EWrapMode { CLAMP_TO_EDGE = 33071, MIRRORED_REPEAT = 33648, REPEAT = 10497 };
        public struct Sampler
        {
            public string? name { get; set; } = null;

            public int? magFilter { get; set; } = null;     // ESamplerMagFilter
            public int? minFilter { get; set; } = null;     // ESamplerMinFilter

            public int? wrapS { get; set; } = null;     // EWrapMode, REPEAT if undefined
            public int? wrapT { get; set; } = null;     // EWrapMode, REPEAT if undefined

            public Sampler() { }
        }



        public struct TextureInfo
        {
            [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
            public int index { get; set; } = -1;        // must be defined

            public int texCoord { get; set; } = 0;      // defaults to 0

            public TextureInfo() { }
        }
        public struct NormalTextureInfo
        {
            [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
            public int index { get; set; } = -1;        // must be defined

            public int texCoord { get; set; } = 0;      // defaults to 0
            public Decimal? scale { get; set; } = null; // defaults to 1

            public NormalTextureInfo() { }
        }
        public struct OcclusionTextureInfo
        {
            [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
            public int index { get; set; } = -1;        // must be defined

            public int texCoord { get; set; } = 0;          // defaults to 0
            public Decimal? strength { get; set; } = null;  // defaults to 1

            public OcclusionTextureInfo() { }
        }
        public struct PBRMetallicRoughness
        {
            public Decimal[]? baseColorFactor { get; set; } = null;
            public TextureInfo? baseColorTexture { get; set; } = null;

            public Decimal? metallicFactor { get; set; } = null;    // default = 1
            public Decimal? roughnessFactor { get; set; } = null;    // default = 1

            public TextureInfo? metallicRoughnessTexture { get; set; } = null;

            public PBRMetallicRoughness() { }
        }
        public struct Material
        {
            // https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.html#reference-material
            public string? name { get; set; } = null;

            public PBRMetallicRoughness? pbrMetallicRoughness { get; set; } = null;
            public NormalTextureInfo? normalTexture { get; set; } = null;
            public OcclusionTextureInfo? occlusionTexture { get; set; } = null;
            public Decimal[]? emissiveFactor { get; set; } = null;
            public string? alphaMode { get; set; } = null;       // valid values are "OPAQUE", "MASK", "BLEND"
            public Decimal? alphaCutoff { get; set; } = null;    // default = 0.5
            public bool doubleSided { get; set; } = false;

            public Material() { }
        }


        public struct Primitive
        {

            public int? indices { get; set; } = null;

            public int? material { get; set; } = null;

            [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
            public int mode { get; set; } = 4;

            public Dictionary<string, int>? attributes { get; set; } = null;

            public Primitive() { }
        }


        public struct Mesh
        {
            public string? name { get; set; } = null;
            public Primitive[]? primitives { get; set; } = null;

            public Mesh() { }
        }



        public struct Node
        {
            public string? name { get; set; } = null;

            public int? mesh { get; set; } = null;

            public float[]? matrix { get; set; } = null;

            // order is T * R * S
            public float[]? translation { get; set; } = null;
            public float[]? rotation { get; set; } = null;
            public float[]? scale { get; set; } = null;

            public int[]? children { get; set; } = null;

            public Node() { }

            public readonly Matrix4d GetTransformMatrix()
            {
                if (matrix != null)
                    return new Matrix4d(matrix).Transpose();    // gltf is column-major but we use row-major...

                if (translation == null && rotation == null && scale == null)
                    return Matrix4d.Identity;

                // order is T * R * S ...
                Vector3d Scale = (scale != null) ? new Vector3d(scale[0], scale[1], scale[2]) : Vector3d.One;
                Quaterniond Rotation = (rotation != null) ? new Quaterniond(rotation) : Quaterniond.Identity;
                Matrix3d RS = Rotation.ToRotationMatrix() * new Matrix3d(Scale);
                Vector3d T = (translation != null) ? new Vector3d(translation[0], translation[1], translation[2]) : Vector3d.Zero;
                //return Matrix4d.Affine(in RS) * Matrix4d.Translation(T);
                return new Matrix4d(in RS, in T);
            }
        }


        public struct Scene
        {
            public string? name { get; set; } = null;
            public int[]? nodes { get; set; } = null;

            public Scene() { }
        }


        public class Root
        {
            // todo: materials, samplers, skins, animations, cameras, 

            public string[]? extensionsUsed { get; set; } = null;
            public string[]? extensionsRequired { get; set; } = null;

            public Asset asset { get; set; }

            public Buffer[]? buffers { get; set; } = null;
            public BufferView[]? bufferViews { get; set; } = null;
            public Accessor[]? accessors { get; set; } = null;

            public Mesh[]? meshes { get; set; } = null;

            public Image[]? images { get; set; } = null;
            public Texture[]? textures { get; set; } = null;
            public Sampler[]? samplers { get; set; } = null;
            public Material[]? materials { get; set; } = null;

            public Node[]? nodes { get; set; } = null;
            public Scene[]? scenes { get; set; } = null;
            public int? scene { get; set; } = null;          // index of default scene (defaults to 0)

            public object? extensions { get; set; } = null;
            public object? extras { get; set; } = null;


            // these are helper fields, not from json file

            public string RootPath = "";

            public Root() { }
        }


        public struct GLBHeader
        {
            public uint magic;
            public uint version;
            public uint length;

            public const uint MagicNumber = 0x46546C67;      // "glTF" in ASCII
        }
        public const uint GLB_ChunkType_JSON = 0x4E4F534A;
        public const uint GLB_ChunkType_BIN = 0x004E4942;


        public static ResultOrFail<Root> ParseFile(Stream utf8Stream)
        {
            Root? Result = JsonSerializer.Deserialize<Root>(utf8Stream);
            if (Result == null) 
                return new(false, ["parsing failed"]);
            return new ResultOrFail<Root>( Result! );
        }


    }


    public class GLTFBuffer
    {
        byte[] data;        // could this be a Span<byte> ?
        
        public GLTFBuffer(string binFileName)
        {
            try {
                data = File.ReadAllBytes(binFileName);
            } catch (Exception) {
                data = [];
            }
        }
        public GLTFBuffer(byte[] ExternalData)
        {
            data = ExternalData;
        }

        public bool IsValid {
            get { return data != null && data.Length > 0; }
        }

        public Span<byte> GetBytes(GLTFFile.BufferView bufferView, int BytesPerElem)
        {
            if (bufferView.byteStride != 0 && bufferView.byteStride != BytesPerElem)
                throw new NotImplementedException();

            int byteLen = bufferView.byteLength;
            int byteOffset = bufferView.byteOffset;
            return new Span<byte>(data, byteOffset, byteLen);
        }

        public Span<uint> GetUIntBuffer(GLTFFile.Accessor accessor, GLTFFile.BufferView bufferView)
        {
            Util.gDevAssert(accessor.ComponentTypeEnum == GLTFFile.EComponentType.UnsignedInt);
            int BytesPerElem = accessor.GetElementByteCount();
            Span<byte> accessorSpan = GetBytes(bufferView, BytesPerElem).Slice(accessor.byteOffset, accessor.count*BytesPerElem);
            return MemoryMarshal.Cast<byte, uint>(accessorSpan);
        }
        public Span<ushort> GetUShortBuffer(GLTFFile.Accessor accessor, GLTFFile.BufferView bufferView)
        {
            Util.gDevAssert(accessor.ComponentTypeEnum == GLTFFile.EComponentType.UnsignedShort);
            int BytesPerElem = accessor.GetElementByteCount();
            Span<byte> accessorSpan = GetBytes(bufferView, BytesPerElem).Slice(accessor.byteOffset, accessor.count*BytesPerElem);
            return MemoryMarshal.Cast<byte, ushort>(accessorSpan);
        }
        public Span<short> GetShortBuffer(GLTFFile.Accessor accessor, GLTFFile.BufferView bufferView)
        {
            Util.gDevAssert(accessor.ComponentTypeEnum == GLTFFile.EComponentType.SignedShort);
            int BytesPerElem = accessor.GetElementByteCount();
            Span<byte> accessorSpan = GetBytes(bufferView, BytesPerElem).Slice(accessor.byteOffset, accessor.count*BytesPerElem);
            return MemoryMarshal.Cast<byte, short>(accessorSpan);
        }
        public Span<byte> GetByteBuffer(GLTFFile.Accessor accessor, GLTFFile.BufferView bufferView)
        {
            Util.gDevAssert(accessor.ComponentTypeEnum == GLTFFile.EComponentType.UnsignedByte);
            int BytesPerElem = accessor.GetElementByteCount();
            Span<byte> accessorSpan = GetBytes(bufferView, BytesPerElem).Slice(accessor.byteOffset, accessor.count*BytesPerElem);
            return accessorSpan;
        }
        public Span<sbyte> GetSignedByteBuffer(GLTFFile.Accessor accessor, GLTFFile.BufferView bufferView)
        {
            Util.gDevAssert(accessor.ComponentTypeEnum == GLTFFile.EComponentType.SignedByte);
            int BytesPerElem = accessor.GetElementByteCount();
            Span<byte> accessorSpan = GetBytes(bufferView, BytesPerElem).Slice(accessor.byteOffset, accessor.count*BytesPerElem);
            return MemoryMarshal.Cast<byte, sbyte>(accessorSpan);
        }
        public Span<float> GetFloatBuffer(GLTFFile.Accessor accessor, GLTFFile.BufferView bufferView)
        {
            Util.gDevAssert(accessor.ComponentTypeEnum == GLTFFile.EComponentType.Float);
            int BytesPerElem = accessor.GetElementByteCount();
            Span<byte> accessorSpan = GetBytes(bufferView, BytesPerElem).Slice(accessor.byteOffset, accessor.count*BytesPerElem);
            return MemoryMarshal.Cast<byte, float>(accessorSpan);
        }


    }
    public class GLTFBufferSet
    {
        public List<GLTFBuffer> Buffers { get; init; } = new List<GLTFBuffer>();

        public GLTFBufferSet()
        {
        }

        public void AddBuffer(GLTFBuffer buffer)
        {
            Buffers.Add(buffer);
        }

        public Span<uint> GetUIntBuffer(GLTFFile.Root root, GLTFFile.Accessor accessor)
        {
            GLTFFile.BufferView bufferView = root.bufferViews![accessor.bufferView];
            GLTFBuffer useBuffer = Buffers[bufferView.buffer];
            return useBuffer.GetUIntBuffer(accessor, bufferView);
        }
        public Span<ushort> GetUShortBuffer(GLTFFile.Root root, GLTFFile.Accessor accessor)
        {
            GLTFFile.BufferView bufferView = root.bufferViews![accessor.bufferView];
            GLTFBuffer useBuffer = Buffers[bufferView.buffer];
            return useBuffer.GetUShortBuffer(accessor, bufferView);
        }


        public Span<float> GetFloatBuffer(GLTFFile.Root root, GLTFFile.Accessor accessor)
        {
            GLTFFile.BufferView bufferView = root.bufferViews![accessor.bufferView];
            GLTFBuffer useBuffer = Buffers[bufferView.buffer];
            return useBuffer.GetFloatBuffer(accessor, bufferView);
        }

    }



#nullable disable
}
