using System;
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
    public class USDCReader : IMeshReader
    {
        // connect to this to get warning messages
        public event ParsingMessagesHandler? warningEvent;

        public IOReadResult Read(TextReader reader, ReadOptions options, IMeshBuilder builder)
        {
            throw new NotImplementedException();
        }





        public IOReadResult Read(BinaryReader reader, ReadOptions options, IMeshBuilder builder)
        {
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

            Debug.WriteLine($"USD version is {Bootstrap.MajorVersion}.{Bootstrap.MinorVersion}.{Bootstrap.PatchVersion}");

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
                }
            }

            return new IOReadResult(IOCode.FileParsingError, $"boo");

        }


        protected USDC_TableOfContents TableOfContents;
        protected List<string>? Tokens = null;
        protected List<int>? StringIndices = null;
        protected List<USDCField>? Fields = null;
        protected List<int>? FieldSets = null;
        protected USDPath[] Paths = [];


        protected string USDC_Header = "PXR-USDC";


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
            Uint64 = 6,
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





        protected struct USDCValue
        {
            const ulong IsArrayMask = (ulong)1 << 63;
            const ulong IsInlinedMask = (ulong)1 << 62;
            const ulong IsCompressedMask = (ulong)1 << 61;
            const ulong IsArrayEditMask = (ulong)1 << 60;
            const ulong PayloadMask = (((ulong)1 << 48) - (ulong)1);

            public ulong Value = 0;
            public USDCValue() { }

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
            public USDCValue ValueRep;

            public bool IsArray => ValueRep.IsArray;
            public USDCDataType FieldType => ValueRep.FieldType;

            public object? data = null;
            public bool IsValid => data != null;
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
            //ulong indices_bytes = reader.ReadUInt64();
            //byte[] compressed_indices = reader.ReadBytes((int)indices_bytes);
            ////ulong max_uncompressed_size = (field_count > 0) ? (sizeof(int)) + ((field_count * 2 + 7) / 8) + (field_count * sizeof(int)) : 0;
            //byte[]? indices_buffer = try_decompress_data(compressed_indices, /* verify buffersize calc? */0);
            //if (indices_buffer == null) {
            //    warningEvent?.Invoke("error decompressing FIELDS index section", null);
            //    return Fields;
            //}
            //List<int> indices = decode_packed_integers(indices_buffer, field_count);
            //Util.gDevAssert(indices.Count == (int)field_count);

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
                USDCValue value = MemoryMarshal.AsRef<USDCValue>(values_buffer.AsSpan().Slice(cur_idx, 8));
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
            ulong path_count = reader.ReadUInt64();
            ulong path_count_2 = reader.ReadUInt64();
            Util.gDevAssert(path_count == path_count_2);   // ???

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

            USDPath[] PathSet = new USDPath[path_count];
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
                } else {
                    int token_index = token_indices[cur_index];
                    string token = Tokens![Math.Abs(token_index)];
                    if (token_index < 0) {
                        PathSet[pathset_index] = USDPath.CombineProperty(ParentPath, token);
                    } else {
                        PathSet[pathset_index] = USDPath.CombineElement(ParentPath, token);
                    }
                }

                bool bHasChild = (jump_indices[cur_index] > 0) || (jump_indices[cur_index] == -1);
                bool bHasSibling = (jump_indices[cur_index] >= 0);
                bContinue = (bHasChild || bHasSibling);

                if (bHasChild) {
                    if (bHasSibling) {
                        // recurse down sibling path
                        int sibling_index = cur_index + jump_indices[cur_index];
                        AssembleChildPaths(sibling_index, ParentPath, path_indices, token_indices, jump_indices, PathSet);
                    }
                    // descend to child
                    ParentPath = PathSet[pathset_index];
                }

            } while (bContinue);
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
                case USDCDataType.String: {
                        Util.gDevAssert(field.IsArray == false);
                        int str_idx = (int)field.ValueRep.PayloadData;
                        field.data = StringIndices![str_idx];
                    } break;
                case USDCDataType.Token:
                    parse_field_token(reader, field);
                    break;

                case USDCDataType.Int:
                    parse_field_int(reader, field);
                    break;

                case USDCDataType.Float:
                    parse_field_float(reader, field);
                    break;

                default:
                    warningEvent?.Invoke($"unhandled field type {field.FieldType} in parse_field", null);
                    break;
            }
        }


        private List<int>? read_compressed_indices(BinaryReader reader, ulong index_count)
        {
            ulong indices_bytes = reader.ReadUInt64();
            byte[] compressed_indices = reader.ReadBytes((int)indices_bytes);
            byte[]? indices_buffer = try_decompress_data(compressed_indices, /* verify buffersize calc? */0);
            if (indices_buffer == null)
                return null;
            return decode_packed_integers(indices_buffer, index_count);
        }


        private T[] read_uncompressed_array<T>(BinaryReader reader, USDCField field) where T : struct
        {
            ulong offset = field.ValueRep.PayloadData;
            reader.BaseStream.Position = (long)offset;
            ulong num_values = reader.ReadUInt64();
            byte[] bytes = reader.ReadBytes((int)num_values * 4);
            T[] values = MemoryMarshal.Cast<byte, T>(bytes).ToArray();
            return values;
        }
        private string[] map_indices(int[] indices, List<string> strings)
        {
            string[] values = new string[indices.Length];
            for (int i = 0; i < indices.Length; ++i)
                values[i] = strings[indices[i]];
            return values;
        }

        private void parse_field_token(BinaryReader reader, USDCField field)
        {
            if (field.IsArray) {
                // if it's compressed we need to figure out if the indices are also compressed...
                Util.gDevAssert(field.ValueRep.IsCompressed == false);

                int[] indices = read_uncompressed_array<int>(reader, field);
                field.data = map_indices(indices, Tokens!);
            } else {
                int token_idx = (int)field.ValueRep.PayloadData;
                field.data = Tokens![token_idx];
            }
        }


        private void parse_field_float(BinaryReader reader, USDCField field)
        {
            if (field.IsArray) {
                ulong offset = field.ValueRep.PayloadData;
                if (field.ValueRep.IsCompressed) {
                    reader.BaseStream.Position = (long)offset;
                    ulong num_floats = reader.ReadUInt64();
                    ulong compressed_bytes = reader.ReadUInt64();
                    byte[] compressed = reader.ReadBytes((int)compressed_bytes);
                    byte[]? uncompressed = try_decompress_data(compressed);
                    float[] values = MemoryMarshal.Cast<byte, float>(uncompressed!).ToArray();
                    field.data = values;
                } else
                    field.data = read_uncompressed_array<float>(reader, field);
            } else {
                // todo this can maybe become a generic function...
                if (field.ValueRep.IsInlined) {
                    ulong value = field.ValueRep.PayloadData;
                    field.data = MemoryMarshal.AsRef<float>(
                        MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref value, 1)) );
                } else {
                    ulong offset = field.ValueRep.PayloadData;
                    reader.BaseStream.Position = (long)offset;
                    field.data = reader.ReadSingle();
                }
            }
        }


        private void parse_field_int(BinaryReader reader, USDCField field)
        {
            if (field.IsArray) {
                ulong offset = field.ValueRep.PayloadData;
                if (field.ValueRep.IsCompressed) {
                    reader.BaseStream.Position = (long)offset;
                    ulong num_integers = reader.ReadUInt64();
                    ulong compressed_bytes = reader.ReadUInt64();
                    byte[] compressed = reader.ReadBytes((int)compressed_bytes);
                    byte[]? uncompressed = try_decompress_data(compressed);
                    int[] values = decode_packed_integers(uncompressed!, num_integers).ToArray();
                    field.data = values;
                } else 
                    field.data = read_uncompressed_array<int>(reader, field);
            } else {
                if (field.ValueRep.IsInlined) {
                    field.data = (int)field.ValueRep.PayloadData;
                } else {
                    ulong offset = field.ValueRep.PayloadData;
                    reader.BaseStream.Position = (long)offset;
                    field.data = reader.ReadInt32();
                }
            }
        }




        // decode block of integers with an encoding specialized for indices.
        // scheme is variable-bit-length delta-encoding, where a 2-byte 'code' is
        // stored for each integer. The code specifies how many bytes used to store the delta (1/2/4).
        // code = 0 means use the special 'common' value. So eg if the list was
        // sequential numbers, the common value could be 1 and all the codes would be 0.
        // (in practice it seems like the common value is usually 0...)
        // (see _DecodeIntegers() in integerCoding.cpp)
        // todo since we have num_integers we could do array here instead of list??
        public static List<int> decode_packed_integers(byte[] buffer, ulong num_integers)
        {
            int commonValue = BitConverter.ToInt32(buffer, 0);
            int offset = 4;
            int code_bytes = (int)((2*num_integers + 7) / 8);  // 2 bits per integer

            ReadOnlySpan<byte> codes_buffer = buffer.AsSpan(offset, code_bytes); 
            offset += code_bytes;
            ReadOnlySpan<byte> deltas_buffer = buffer.AsSpan(offset, buffer.Length-offset);

            List<int> results = new List<int>();

            int deltas_idx = 0, codes_idx = 0;
            int cur_value = 0;
            int remaining = (int)num_integers;
            while (remaining > 0) {
                int count = Math.Min(remaining, 4);      // bytecount codes are packed into groups of 4, except last
                byte code_byte = codes_buffer[codes_idx++];
                for (int i = 0; i < count; i++) {
                    int shift = 2*i;
                    //int x = (code_byte & (0b11 << shift)) >> shift;     // extract i'th packed 2-bit integer
                    int x = (code_byte >> shift) & 0b11;
                    if (x == 0) {
                        cur_value += commonValue;
                    } else if (x == 1) {
                        //cur_value += unchecked((sbyte)deltas_buffer[deltas_idx]);
                        cur_value += MemoryMarshal.AsRef<sbyte>(deltas_buffer.Slice(deltas_idx, 1));
                        deltas_idx += 1;
                    } else if (x == 2) {
                        cur_value += MemoryMarshal.AsRef<short>(deltas_buffer.Slice(deltas_idx, 2));
                        deltas_idx += 2;
                    } else if (x == 3) {
                        cur_value += MemoryMarshal.AsRef<int>(deltas_buffer.Slice(deltas_idx, 4));
                        deltas_idx += 4;
                    }
                    results.Add(cur_value);
                }
                remaining -= count;
            }

            return results;
        }






        protected static string read_usd_string(BinaryReader reader, int length)
        {
            byte[] bytes = reader.ReadBytes(length);
            return Encoding.ASCII.GetString(bytes).TrimEnd('\0');
        }
        protected static string read_usd_string(ReadOnlySpan<byte> bytes)
        {
            return Encoding.ASCII.GetString(bytes).TrimEnd('\0');
        }


        protected byte[]? try_decompress_data(byte[] compressedData, ulong uncompressedBytes = 0)
        {
            try {
                byte numBlocks = compressedData[0];
                int cur_offset = 1;
                if (numBlocks == 0) {
                    Span<byte> block_data = compressedData.AsSpan(cur_offset, compressedData.Length - 1);
                    if (uncompressedBytes == 0)
                        return LZ4Compression.DecompressLZ4BlockFormat(block_data);
                    else
                        return LZ4Compression.DecompressLZ4BlockFormat(block_data, (uint)uncompressedBytes);
                } else {
                    throw new NotImplementedException();        // TODO (maybe?)
                    //for (int k = 0; k < numBlocks; ++k) {
                    //    int next_block_size = MemoryMarshal.AsRef<int>(compressedData.AsSpan(cur_offset, 4));
                    //    cur_offset += 4;
                    //    Span<byte> compressed_block_data = compressedData.AsSpan(cur_offset, next_block_size);
                    //    byte[] block_data = LZ4Compression.DecompressLZ4BlockFormat(compressed_block_data, (uint)uncompressedBytes);

                    //    cur_offset += next_block_size;
                    //}
                }
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine("error decompressing data: " + ex.Message);
                Debugger.Break();
                return null;
            }
        }


    }
}
