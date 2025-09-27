// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Distributed under the Boost Software License, Version 1.0. http://www.boost.org/LICENSE_1_0.txt
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using static g3.USDFile;

#nullable enable

namespace g3
{
    public partial class USDCReader
    {
        // connect to this to get warning messages
        public event ParsingMessagesHandler? warningEvent;

        
        public bool ExpandReferences = true;
        public bool ApplyOvers = true;


        public IOReadResult Read(string filename, ReadOptions options, out USDScene Scene)
        {
            using (FileStream stream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                using (BinaryReader reader = new BinaryReader(stream)) {
                    return Read(reader, options, out Scene);
                }
            }
        }

        public IOReadResult Read(BinaryReader reader, ReadOptions options, out USDScene Scene)
        {
            // default empty scene in case we get an error
            Scene = new USDScene() { Root = new USDPrim() { PrimType = EDefType.PsuedoRoot } };

            // CrateFile::_ReadStructuralSections

            // _ReadBootStrap

            int header_size = 88;       
            Util.gDevAssert(header_size == Marshal.SizeOf<USDC_Bootstrap>());
            Span<byte> headerBytes = reader.ReadBytes(header_size);
            USDC_Bootstrap Bootstrap = MemoryMarshal.AsRef<USDC_Bootstrap>(headerBytes);
            if (string.Compare(Bootstrap.Header, USDC_Header) != 0)
                return new IOReadResult(IOCode.FileParsingError, $"file header is ${Bootstrap.Header}, should be {USDC_Header}");
            if (Bootstrap.MajorVersion == 0 && Bootstrap.MinorVersion < 4)
                return new IOReadResult(IOCode.FileParsingError, "Version less than 0.4.0 unsupported");

            // _ReadTOC

            reader.BaseStream.Position = (long)Bootstrap.TOCOffset;
            ulong Count = reader.ReadUInt64();
            int section_size = 32;
            Util.gDevAssert(section_size == Marshal.SizeOf<USDC_Section>());

            TableOfContents = new();
            for (ulong i = 0; i < Count; ++i ) 
            {
                Span<byte> sectionBytes = reader.ReadBytes(section_size);
                USDC_Section section = MemoryMarshal.AsRef<USDC_Section>(sectionBytes);
                TableOfContents.Sections.Add(section);
            }

            // read sections

            foreach (USDC_Section section in TableOfContents.Sections) {
                if (section.TokenString == "TOKENS") {
                    Tokens = ReadSection_Tokens(reader, section);
                } else if (section.TokenString == "STRINGS") {
                    StringIndices = ReadSection_Strings(reader, section);
                } else if (section.TokenString == "FIELDS") {
                    Fields = ReadSection_Fields(reader, section);
                } else if (section.TokenString == "FIELDSETS") {
                    FieldSets = ReadSection_FieldSets(reader, section);
                } else if (section.TokenString == "PATHS") {
                    Paths = ReadSection_Paths(reader, section);
                } else if (section.TokenString == "SPECS") {
                    Specs = ReadSection_Specs(reader, section);
                }
            }

            Scene = BuildScene();
            //debug_print("", scene.Root);

            if (ExpandReferences) 
                ExpandReferences(Scene, options);
            if (ApplyOvers)
                ApplyOvers(Scene);
            
            return IOReadResult.Ok;
        }


        protected USDC_TableOfContents TableOfContents;
        protected List<string>? Tokens = null;
        protected List<int>? StringIndices = null;
        protected List<USDCField>? Fields = null;
        protected List<int>? FieldSets = null;
        protected USDPath[] Paths = [];
        protected USDCSpec[] Specs = [];

        protected string USDC_Header = "PXR-USDC";

        protected static Dictionary<string, EUSDType> typeNameToType = build_typeName_dictionary();
        static Dictionary<string, EUSDType> build_typeName_dictionary()
        {
            Dictionary<string, EUSDType> dict = new();
            for (int i = (int)EUSDType.Unknown; i < (int)EUSDType.LastType; ++i)
                dict.Add(FieldTypeTokens[i], (EUSDType)i);
            return dict;
        }
        EUSDType find_type_from_string(string typeString)
        {
            if ( typeNameToType.TryGetValue(typeString, out EUSDType type))
                return type;
            return EUSDType.Unknown;
        }


        USDCSpec? find_spec_by_path(string Path)
        {
            return Array.Find(Specs, (spec) => { return spec.Path.FullPath == Path; });
        }
        IEnumerable<USDCSpec> enumerate_children(USDCSpec parent)
        {
            foreach (USDCSpec spec in Specs) {
                if (spec.Path.parent_index == parent.Path.index)
                    yield return spec;
            }
        }


        protected void debug_print(string indent, USDPrim prim)
        {
            Debug.WriteLine(indent + "PRIM " + prim.ToString());
            string child_indent = indent + " ";

            foreach (USDAttrib attrib in prim.Attribs) {
                Debug.WriteLine(child_indent + attrib.ToString());
            }
            Debug.WriteLine(" ");
            child_indent += "  ";

            foreach (USDPrim childPrim in prim.Children)
                debug_print(child_indent, childPrim);
        }


        USDScene BuildScene()
        {
            USDCSpec? Root = find_spec_by_path("/");
            USDPrim RootPrim = new USDPrim() {
                Path = Root!.Path,
                PrimType = EDefType.PsuedoRoot,
                SpecifierType = EUSDSpecifierType.Def
            };

            USDScene scene = new USDScene() { Root = RootPrim };
            build_prim(Root, RootPrim, scene);

            // could clear internal data structures here to save memory

            return scene;
        }
        void build_prim(USDCSpec parent, USDPrim parentPrim, USDScene scene)
        {
            List<USDAttrib> attribs = new();

            // default specifier is Over, according to tinyusdz in USDCReader::Impl::ReconstructPrimNode()
            EUSDSpecifierType specifierType = (parent.SpecType == ESpecType.PsuedoRoot) ? EUSDSpecifierType.Def : EUSDSpecifierType.Over;
            foreach (USDCField field in parent.Fields) {

                if (field.FieldType == USDCDataType.Specifier) {
                    specifierType = (EUSDSpecifierType)field.data!;
                } else if ( field.Name == "typeName") {
                    string defname = (field.data as string) ?? "(no name)";
                    int idx = Array.FindIndex(USDFile.DefTypeTokens, (str) => { return str == defname; });
                    if ( idx >= 0) {
                        parentPrim.PrimType = (EDefType)idx;
                    } else {
                        parentPrim.PrimType = EDefType.Unknown;
                        parentPrim.CustomPrimTypeName = defname;
                    }
                } else {
                    attribs.Add(field_to_attrib(field));
                }
            }
            parentPrim.SpecifierType = specifierType;

            List<USDCSpec> childSpecs = new();
            foreach (USDCSpec child in enumerate_children(parent)) 
            {
                if (child.SpecType == ESpecType.Attribute) {
                    attribs.Add(make_attribute(parentPrim, child));
                }
                else if (child.SpecType == ESpecType.Prim) 
                {
                    childSpecs.Add(child);
                }
                else { 
                    warningEvent?.Invoke($"unhandled spec type {child.SpecType} in build_scene_children", null);
                }
            }

            parentPrim.Attribs = attribs;  

            for ( int i = 0; i < childSpecs.Count; ++i ) { 
                USDCSpec child = childSpecs[i];
                USDPrim childPrim = new USDPrim() {
                    Path = child.Path
                };
                build_prim(child, childPrim, scene);

                parentPrim.Children.Add(childPrim);
            }
        }

        // convert internal USDCField to general USDAttrib
        USDAttrib field_to_attrib(USDCField field)
        {
            USDAttrib attrib = new();
            attrib.Name = field.Name;
            if (field.FieldType == USDCDataType.TokenVector) 
            {
                attrib.Value.TypeInfo.bIsArray = true;
                attrib.Value.TypeInfo.USDType = EUSDType.Token;
                attrib.Value.data = field.data;
            } 
            else if (field.FieldType == USDCDataType.ReferenceListOp) 
            {
                attrib.Value.TypeInfo.USDType = usdc_to_usdtype(field.FieldType);

                if (field.data is ValueTuple<USDListOpHeader, USDCReference[]> ) {
                    (USDListOpHeader header, USDCReference[] references) = (ValueTuple<USDListOpHeader, USDCReference[]>)field.data;
                    USDReferenceListOp refListOp = new USDReferenceListOp();
                    refListOp.header = header;
                    refListOp.references = new USDReference[references.Length];
                    for (int i = 0; i < references.Length; ++i) {
                        USDCReference reference = references[i];
                        refListOp.references[i] = new USDReference();
                        refListOp.references[i].assetPath = Tokens![StringIndices![reference.assetPathStringIndex]];
                        refListOp.references[i].primPath = Specs[reference.primPathIndex].Path.Duplicate();
                        refListOp.references[i].layerOffset = reference.layerOffset;
                        refListOp.references[i].customData = dictionary_field_to_attrib(reference.customData);
                    }
                    attrib.Value.data = refListOp;
                }

            } else if (field.FieldType == USDCDataType.Dictionary) {
                attrib.Value.TypeInfo.USDType = EUSDType.Dictionary;
                attrib.Value.data = dictionary_field_to_attrib(field.data);

            } else {
                attrib.Value.TypeInfo.USDType = usdc_to_usdtype(field.FieldType);
                attrib.Value.data = field.data;
            }
            return attrib;
        }

        Dictionary<string, USDAttrib> dictionary_field_to_attrib(object? dictionaryObj)
        {
            Dictionary<string, USDAttrib> attribDict = new();
            if (dictionaryObj is Dictionary<string, USDCField> dict) {
                foreach (var pair in dict) {
                    USDAttrib attrib = field_to_attrib(pair.Value);
                    attribDict.Add(pair.Key, attrib);   
                }
            }
            return attribDict;
        }




        USDAttrib make_attribute(USDPrim prim, USDCSpec attribSpec)
        {
            USDAttrib attrib = new();
            attrib.Name = attribSpec.Path.ShortName;

            USDCField[] fields = attribSpec.Fields;

            // TODO: unclear how we should interpret fields...in many cases there
            // is 'typeName' and 'default' field that are the main type/data. And then
            // there might be other fields - do these correspond to stuff in the postfix brackets () section
            // in usda? 

            // find typename field first, if it exists
            USDCField? typeNameField = Array.Find(fields, (f) => { return f.Name == "typeName"; });
            if (typeNameField != null) {
                Util.gDevAssert(typeNameField.FieldType == USDCDataType.Token);
                string typeString = typeNameField.data as string ?? "";
                if (typeString.EndsWith("[]")) {
                    attrib.Value.TypeInfo.bIsArray = true;
                    typeString = typeString.Substring(0, typeString.Length-2);
                }
                EUSDType type = find_type_from_string(typeString);
                attrib.Value.TypeInfo.USDType = type;
            }

            // then find default field if it exists, as it contains the data?
            USDCField? defaultField = Array.Find(fields, (f) => { return f.Name == "default"; });
            if (defaultField != null) {
                EUSDType type = usdc_to_usdtype(defaultField.FieldType);
                Util.gDevAssert(IsTypeCompatible(type, attrib.Value.TypeInfo.USDType));
                attrib.Value.data = defaultField.data;
            }

            // process rest of fields.... ??
            foreach (USDCField field in fields) {
                if (field.Name == "typeName" || field.Name == "default")  {
                    continue;
                } else {
                    warningEvent?.Invoke($"unhandled attribute field {field.Name}", null);
                }
            }   


            return attrib;
        }



        // struct _TableOfContents in pxr\usd\sdf\crateFile.h
        protected struct USDC_TableOfContents
        {
            public List<USDC_Section> Sections = new();

            public USDC_TableOfContents() { }
        }

        [InlineArray(16)]
        protected struct USDC_String16
        {
            private byte _element;
            public readonly string AsString => read_usd_string(this);
        }


        // struct _Section in pxr\usd\sdf\crateFile.h
        // size = 32
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        protected struct USDC_Section
        {
            public USDC_String16 Token;
            public ulong Offset;
            public ulong Size;

            public readonly string TokenString => Token.AsString;
        }


        // struct _BootStrap in pxr\usd\sdf\crateFile.h
        // note that fields may not inspect property in VS2022 debugger...
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        [InlineArray(88)]
        protected struct USDC_Bootstrap
        {
            private byte _element;

            public readonly string Header {
                get {
                    ReadOnlySpan<byte> data = this;
                    return read_usd_string(data.Slice(0, 8));
                }
            }
            public readonly byte MajorVersion => this[8];
            public readonly byte MinorVersion => this[9];
            public readonly byte PatchVersion => this[10];

            public readonly ulong TOCOffset {
                get {
                    ReadOnlySpan<byte> data = this;
                    return MemoryMarshal.AsRef<ulong>(data.Slice(16, 8));
                }
            }
        }



        // crateDataTypes.h
        protected enum USDCDataType
        {
            UnknownInvalid = 0,
            Bool = 1,
            UChar = 2,
            Int = 3,
            UInt = 4,
            Int64 = 5,
            UInt64 = 6,
            Half = 7,
            Float = 8,
            Double = 9,
            String = 10,
            Token = 11,
            AssetPath = 12,

            Matrix2d = 13,
            Matrix3d = 14,
            Matrix4d = 15,

            Quatd = 16,
            Quatf = 17,
            Quath = 18,

            Vec2d = 19,
            Vec2f = 20,
            Vec2h = 21,
            Vec2i = 22,

            Vec3d = 23,
            Vec3f = 24,
            Vec3h = 25,
            Vec3i = 26,

            Vec4d = 27,
            Vec4f = 28,
            Vec4h = 29,
            Vec4i = 30,

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
            AnimationBlock = 60
        }
        protected EUSDType usdc_to_usdtype(USDCDataType dataType) {
            return (EUSDType)(int)dataType;
        }






        protected struct USDCValueRep
        {
            const ulong IsArrayMask = (ulong)1 << 63;
            const ulong IsInlinedMask = (ulong)1 << 62;
            const ulong IsCompressedMask = (ulong)1 << 61;
            const ulong IsArrayEditMask = (ulong)1 << 60;
            const ulong PayloadMask = (((ulong)1 << 48) - (ulong)1);

            public ulong Value = 0;
            public USDCValueRep() { }

            public bool IsArray => (Value & IsArrayMask) != 0;
            public bool IsInlined => (Value & IsInlinedMask) != 0;
            public bool IsCompressed => (Value & IsCompressedMask) != 0;
            public bool IsArrayEdit => (Value & IsArrayEditMask) != 0;

            public int FieldTypeInt => (int)((Value >> 48) & 0xFF);
            public USDCDataType FieldType => (USDCDataType)FieldTypeInt;

            public ulong PayloadData => (Value & PayloadMask);
        }

        protected class USDCField
        {
            public string Name = "";
            public USDCValueRep ValueRep;

            public bool IsArray => ValueRep.IsArray;
            public USDCDataType FieldType => ValueRep.FieldType;

            public object? data = null;
            public bool IsValid => data != null;

            public override string ToString() {
                return Name + " " + FieldType + (IsArray ? "[]" : "") + " = " + (data != null ? data.ToString() : "null");
            }
        }


        protected struct USDCReference
        {
            public int assetPathStringIndex;
            public int primPathIndex;
            public USDLayerOffset layerOffset;
            public object? customData;
        }






        protected List<string> ReadSection_Tokens(BinaryReader reader, USDC_Section section)
        {
            reader.BaseStream.Position = (long)section.Offset;
            ulong token_count = reader.ReadUInt64();
            ulong num_uncompressed_bytes = reader.ReadUInt64();
            ulong num_compressed_bytes = reader.ReadUInt64();
            Util.gDevAssert( (3*sizeof(ulong) + num_compressed_bytes) == section.Size);

            byte[] compressed_data = reader.ReadBytes((int)num_compressed_bytes);
            byte[]? data = try_decompress_data(compressed_data, num_uncompressed_bytes);
            if (data == null) { 
                warningEvent?.Invoke("error decompressing TOKENS section", null);
                return new();
            }

            List<string> strings = new();
            int cur_idx = 0;
            int start_idx = cur_idx;
            while (cur_idx < data.Length) {
                if (data[cur_idx] == 0) {
                    string s = Encoding.ASCII.GetString(data, start_idx, cur_idx - start_idx);
                    strings.Add(s);
                    start_idx = cur_idx + 1;
                }
                cur_idx++;  
            }

            return strings;
        }


        protected List<int> ReadSection_Strings(BinaryReader reader, USDC_Section section)
        {
            reader.BaseStream.Position = (long)section.Offset;
            ulong index_count = reader.ReadUInt64();
            Util.gDevAssert( (index_count*sizeof(int)) + sizeof(ulong) == section.Size);
            List<int> indices = new();
            for ( int i = 0; i < (int)index_count; ++i ) {
                indices.Add(reader.ReadInt32());
            }
            return indices;
        }





        protected List<USDCField>? ReadSection_Fields(BinaryReader reader, USDC_Section section)
        {
            List<USDCField> Fields = new();

            reader.BaseStream.Position = (long)section.Offset;
            ulong field_count = reader.ReadUInt64();

            // int index for each field, Tokens[index] is string name of field
            List<int>? indices = read_compressed_indices(reader, field_count);
            if (indices == null || indices.Count != (int)field_count ) {
                warningEvent?.Invoke("error decompressing FIELDS index section", null);
                return new();
            }

            // 8-byte element for each field, corresponds to struct ValueRep in crateFile.h
            ulong values_bytes = reader.ReadUInt64();
            byte[] compressed_values = reader.ReadBytes((int)values_bytes);
            byte[]? values_buffer = try_decompress_data(compressed_values, field_count*8);
            if (values_buffer == null) {
                warningEvent?.Invoke("error decompressing FIELDS value section", null);
                return Fields;
            }


            int cur_idx = 0;
            for (int i = 0; i < (int)field_count; i++) 
            {
                USDCValueRep value = MemoryMarshal.AsRef<USDCValueRep>(values_buffer.AsSpan().Slice(cur_idx, 8));
                cur_idx += 8;

                string Name = Tokens![indices[i]];

                Fields.Add(new USDCField { Name = Name, ValueRep = value });
            }

            foreach (USDCField field in Fields)
                parse_field(reader, field);

            return Fields;
        }


        protected List<int>? ReadSection_FieldSets(BinaryReader reader, USDC_Section section)
        {
            reader.BaseStream.Position = (long)section.Offset;
            ulong fieldset_count = reader.ReadUInt64();

            List<int>? FieldSets = read_compressed_indices(reader, fieldset_count);
            if (FieldSets == null || FieldSets.Count != (int)fieldset_count) { 
                warningEvent?.Invoke("error decompressing FIELDSETS section", null);
                return new();
            }
            return FieldSets;
        }



        protected USDPath[] ReadSection_Paths(BinaryReader reader, USDC_Section section)
        {
            reader.BaseStream.Position = (long)section.Offset;
            ulong initial_path_count = reader.ReadUInt64();
            ulong path_count = reader.ReadUInt64();
            //Util.gDevAssert(path_count == initial_path_count);   // actually not always the same...

            List<int>? path_indices = read_compressed_indices(reader, path_count);
            if (path_indices == null || path_indices.Count != (int)path_count) {
                warningEvent?.Invoke("error decompressing PATHS path_indices section", null);
                return Array.Empty<USDPath>();
            }

            List<int>? element_token_indices = read_compressed_indices(reader, path_count);
            if (element_token_indices == null || element_token_indices.Count != (int)path_count) {
                warningEvent?.Invoke("error decompressing PATHS element_token_indices section", null);
                return Array.Empty<USDPath>();
            }

            List<int>? jump_indices = read_compressed_indices(reader, path_count);
            if (jump_indices == null || jump_indices.Count != (int)path_count) {
                warningEvent?.Invoke("error decompressing PATHS jump_indices section", null);
                return Array.Empty<USDPath>();
            }

            USDPath[] PathSet = new USDPath[initial_path_count];
            USDPath RootPath = new USDPath("");
            AssembleChildPaths(0, RootPath, path_indices, element_token_indices, jump_indices, PathSet);
            return PathSet;
        }



        protected void AssembleChildPaths(
            int parent_indices_index,      // index into _indices lists
            USDPath ParentPath,
            List<int> path_indices,
            List<int> token_indices,
            List<int> jump_indices,
            USDPath[] PathSet )
        {
            bool bContinue = false;
            do {
                int cur_index = parent_indices_index;
                parent_indices_index++;
                int pathset_index = path_indices[cur_index];

                if (ParentPath.IsEmpty) {
                    ParentPath = new USDPath("/");
                    PathSet[pathset_index] = ParentPath;
                    PathSet[pathset_index].index = pathset_index;
                } else {
                    int token_index = token_indices[cur_index];
                    string token = Tokens![Math.Abs(token_index)];
                    if (token_index < 0) {
                        PathSet[pathset_index] = USDPath.CombineProperty(ParentPath, token);
                    } else {
                        PathSet[pathset_index] = USDPath.CombineElement(ParentPath, token);
                    }
                    PathSet[pathset_index].index = pathset_index;
                }

                bool bHasChild = (jump_indices[cur_index] > 0) || (jump_indices[cur_index] == -1);
                bool bHasSibling = (jump_indices[cur_index] >= 0);
                bContinue = (bHasChild || bHasSibling);

                // if there is a child and a sibling, this code recurses into the sibling and then continues down the child.
                // if there is a only child, it descends to the child
                // if there is only a sibling, it continues to the sibling
                // (the logic is that the trees tend to be wider than deep, and the sibling recursion can be done in parallel...)
                if (bHasChild) {
                    if (bHasSibling) {
                        int sibling_index = cur_index + jump_indices[cur_index];
                        AssembleChildPaths(sibling_index, ParentPath, path_indices, token_indices, jump_indices, PathSet);
                    }
                    // descend to child
                    ParentPath = PathSet[pathset_index];
                }

            } while (bContinue);
        }



        public enum ESpecType
        {
            Unknown = 0,

            Attribute = 1,
            Connection = 2,
            Expression = 3,  // pixar internal
            Mapper = 4,      // pixar internal
            Arg = 5,         // pixar internal
            Prim = 6,
            PsuedoRoot = 7,
            Relationship = 8,
            RelationshipTarget = 9,
            Variant = 10,
            VariantSet = 11
        }

        protected class USDCSpec
        {
            public ESpecType SpecType;
            public USDPath Path;
            public USDCField[] Fields;

            public USDCSpec(ESpecType specType, USDPath path, USDCField[] fields)
            {
                SpecType=specType;
                Path=path;
                Fields=fields;
            }

            public override string ToString() {
                return Path.ToString();
            }
        }


        protected USDCSpec[] ReadSection_Specs(BinaryReader reader, USDC_Section section)
        {
            reader.BaseStream.Position = (long)section.Offset;
            ulong spec_count = reader.ReadUInt64();

            List<int>? path_indices = read_compressed_indices(reader, spec_count);
            if (path_indices == null || path_indices.Count != (int)spec_count) {
                warningEvent?.Invoke("error decompressing SPECS path_indices section", null);
                return Array.Empty<USDCSpec>();
            }

            List<int>? field_set_indices = read_compressed_indices(reader, spec_count);
            if (field_set_indices == null || field_set_indices.Count != (int)spec_count) {
                warningEvent?.Invoke("error decompressing SPECS field_set_indices section", null);
                return Array.Empty<USDCSpec>();
            }

            List<int>? spec_types = read_compressed_indices(reader, spec_count);
            if (spec_types == null || spec_types.Count != (int)spec_count) {
                warningEvent?.Invoke("error decompressing SPECS spec_types section", null);
                return Array.Empty<USDCSpec>();
            }

            USDCSpec[] Specs = new USDCSpec[spec_count];
            for (int i = 0; i < (int)spec_count; ++i) {

                USDPath path = Paths[path_indices[i]];

                int fieldset_idx = field_set_indices[i];
                int field_count = 0;
                while (FieldSets![fieldset_idx++] != -1)
                    field_count++;
                USDCField[] fields = new USDCField[field_count];
                fieldset_idx = field_set_indices[i];
                for (int k = 0; k < field_count; ++k) {
                    int field_idx = FieldSets![fieldset_idx + k];
                    fields[k] = Fields![field_idx];
                }

                ESpecType specType = (ESpecType)spec_types[i];

                Specs[i] = new USDCSpec(specType, path, fields);
            }

            return Specs;
        }




        private void parse_field(BinaryReader reader, USDCField field)
        {
            // inline array is always an empty array
            if (field.ValueRep.IsArray && field.ValueRep.IsInlined) {
                Util.gDevAssert(field.ValueRep.PayloadData == 0);
                field.data = null;
                return; 
            }

            switch (field.FieldType) {
                case USDCDataType.Specifier:
                    field.data = (EUSDSpecifierType)field.ValueRep.PayloadData;
                    break;


                case USDCDataType.StringVector:
                    throw new NotImplementedException();
                case USDCDataType.String: {
                        Util.gDevAssert(field.IsArray == false);
                        int str_idx = (int)field.ValueRep.PayloadData;
                        int token_idx = StringIndices![str_idx];
                        field.data = Tokens![token_idx];
                    } break;
                case USDCDataType.PathVector:
                case USDCDataType.AssetPath:
                case USDCDataType.Token:
                case USDCDataType.TokenVector:
                    parse_field_token(reader, field);
                    break;

                case USDCDataType.Half:
                case USDCDataType.Vec2h:
                case USDCDataType.Vec3h:
                case USDCDataType.Vec4h:
                case USDCDataType.Quath:
                    // todo need half-float implementation
                    throw new NotImplementedException();

                case USDCDataType.Bool:
                    parse_field_bool(reader, field);
                    break;
                case USDCDataType.UChar:
                    parse_field_uchar(reader, field);
                    break;
                case USDCDataType.Int:
                    parse_field_int(reader, field);
                    break;
                case USDCDataType.UInt:
                    parse_field_uint(reader, field);
                    break;
                case USDCDataType.Int64:
                    parse_field_long(reader, field);
                    break;
                case USDCDataType.UInt64:
                    parse_field_ulong(reader, field);
                    break;
                case USDCDataType.Float:
                    parse_field_float(reader, field);
                    break;
                case USDCDataType.DoubleVector:
                case USDCDataType.Double:
                    parse_field_float(reader, field);
                    break;

                case USDCDataType.Vec2f:
                    parse_field_vec2f(reader, field);
                    break;
                case USDCDataType.Vec2d:
                    parse_field_vec2d(reader, field);
                    break;
                case USDCDataType.Vec3f:
                    parse_field_vec3f(reader, field);
                    break;
                case USDCDataType.Vec3d:
                    parse_field_vec3d(reader, field);
                    break;
                case USDCDataType.Vec4f:
                    parse_field_vec4f(reader, field);
                    break;
                case USDCDataType.Vec4d:
                    parse_field_vec4d(reader, field);
                    break;

                case USDCDataType.Quatf:
                    parse_field_quatf(reader, field);
                    break;
                case USDCDataType.Quatd:
                    parse_field_quatd(reader, field);
                    break;

                case USDCDataType.Matrix2d:
                    parse_field_matrix2d(reader, field);
                    break;
                case USDCDataType.Matrix3d:
                    parse_field_matrix3d(reader, field);
                    break;
                case USDCDataType.Matrix4d:
                    parse_field_matrix4d(reader, field);
                    break;


                case USDCDataType.Dictionary: 
                    parse_field_dictionary(reader, field);
                    break;


                case USDCDataType.ReferenceListOp:
                    parse_field_referenceListOp(reader, field);
                    break;

                default:
                    warningEvent?.Invoke($"unhandled field type {field.FieldType} in parse_field", null);
                    break;
            }
        }
    }



}
