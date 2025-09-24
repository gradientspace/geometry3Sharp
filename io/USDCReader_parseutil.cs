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

        protected static string read_usd_string(BinaryReader reader, int length)
        {
            byte[] bytes = reader.ReadBytes(length);
            return Encoding.ASCII.GetString(bytes).TrimEnd('\0');
        }
        protected static string read_usd_string(ReadOnlySpan<byte> bytes)
        {
            return Encoding.ASCII.GetString(bytes).TrimEnd('\0');
        }




        // decode block of integers with an encoding specialized for indices.
        // scheme is variable-bit-length delta-encoding, where a 2-byte 'code' is
        // stored for each integer. The code specifies how many bytes used to store the delta (1/2/4).
        // code = 0 means use the special 'common' value. So eg if the list was
        // sequential numbers, the common value could be 1 and all the codes would be 0.
        // (in practice it seems like the common value is usually 0...)
        // (see _DecodeIntegers() in integerCoding.cpp)
        // todo since we have num_integers we could do array here instead of list??
        protected static List<int> decode_packed_integers(byte[] buffer, ulong num_integers)
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



        protected static byte[]? try_decompress_data(byte[] compressedData, ulong uncompressedBytes = 0)
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



        protected static List<int>? read_compressed_indices(BinaryReader reader, ulong index_count)
        {
            ulong indices_bytes = reader.ReadUInt64();
            byte[] compressed_indices = reader.ReadBytes((int)indices_bytes);
            byte[]? indices_buffer = try_decompress_data(compressed_indices, /* verify buffersize calc? */0);
            if (indices_buffer == null)
                return null;
            return decode_packed_integers(indices_buffer, index_count);
        }


        protected static T[] read_uncompressed_array<T>(BinaryReader reader, USDCField field, int bytesPerElem, int numElemsPerValue) where T : struct
        {
            ulong offset = field.ValueRep.PayloadData;
            reader.BaseStream.Position = (long)offset;
            ulong num_values = reader.ReadUInt64();
            byte[] bytes = reader.ReadBytes((int)num_values * bytesPerElem * numElemsPerValue);
            T[] values = MemoryMarshal.Cast<byte, T>(bytes).ToArray();
            return values;
        }
        protected static string[] map_indices(int[] indices, List<string> strings)
        {
            string[] values = new string[indices.Length];
            for (int i = 0; i < indices.Length; ++i)
                values[i] = strings[indices[i]];
            return values;
        }


        protected void parse_field_token(BinaryReader reader, USDCField field)
        {
            if (field.IsArray || field.FieldType == USDCDataType.TokenVector) {
                // if it's compressed we need to figure out if the indices are also compressed...
                Util.gDevAssert(field.ValueRep.IsCompressed == false);

                int[] indices = read_uncompressed_array<int>(reader, field, sizeof(int), 1);
                field.data = map_indices(indices, Tokens!);
            } else {
                int token_idx = (int)field.ValueRep.PayloadData;
                field.data = Tokens![token_idx];
            }
        }

        protected static T[] read_array_value<T>(BinaryReader reader, USDCField field, int bytesPerElem, int numElemsPerValue) where T : struct
        {
            ulong offset = field.ValueRep.PayloadData;
            T[] values = Array.Empty<T>();
            if (field.ValueRep.IsCompressed) {
                reader.BaseStream.Position = (long)offset;
                ulong num_values = reader.ReadUInt64();
                ulong compressed_bytes = reader.ReadUInt64();
                byte[] compressed = reader.ReadBytes((int)compressed_bytes);
                byte[]? uncompressed = try_decompress_data(compressed, /*todo est size from num_values*/0);
                values = MemoryMarshal.Cast<byte, T>(uncompressed!).ToArray();
            } else
                values = read_uncompressed_array<T>(reader, field, bytesPerElem, numElemsPerValue);
            return values;
        }

        protected static void parse_field_float(BinaryReader reader, USDCField field)
        {
            if (field.IsArray) {
                field.data = read_array_value<float>(reader, field, sizeof(float), 1);
            } else {
                if (field.ValueRep.IsInlined) {
                    ulong value = field.ValueRep.PayloadData;
                    field.data = MemoryMarshal.AsRef<float>(
                        MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref value, 1)) );
                } else {
                    reader.BaseStream.Position = (long)field.ValueRep.PayloadData;
                    field.data = reader.ReadSingle();
                }
            }
        }
        protected static void parse_field_double(BinaryReader reader, USDCField field)
        {
            if (field.IsArray) {
                field.data = read_array_value<double>(reader, field, sizeof(double), 1);
            } else {
                if (field.ValueRep.IsInlined) {
                    ulong value = field.ValueRep.PayloadData;
                    field.data = MemoryMarshal.AsRef<double>(
                        MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref value, 1)));
                } else {
                    reader.BaseStream.Position = (long)field.ValueRep.PayloadData;
                    field.data = reader.ReadDouble();
                }
            }
        }


        protected static void parse_field_bool(BinaryReader reader, USDCField field)
        {
            if (field.IsArray) {
                // todo need to figure out how this would be stored
                throw new NotImplementedException("array of bool not implemented");
            } else {
                Util.gDevAssert(field.ValueRep.IsInlined == true);
                field.data = (field.ValueRep.PayloadData != 0) ? true : false;
            }
        }

        protected static void parse_field_int(BinaryReader reader, USDCField field)
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
                    field.data = read_uncompressed_array<int>(reader, field, sizeof(int), 1);
            } else {
                if (field.ValueRep.IsInlined) {
                    field.data = (int)field.ValueRep.PayloadData;
                } else {
                    reader.BaseStream.Position = (long)field.ValueRep.PayloadData;
                    field.data = reader.ReadInt32();
                }
            }
        }

        protected static Span<T> read_N_values<T>(BinaryReader reader, int N, int elemsize) where T : struct
        {
            Span<byte> data = reader.ReadBytes(N * elemsize).AsSpan();
            return MemoryMarshal.Cast<byte, T>(data);
        }

        protected static vec4i extract_packed_values(ulong payload)
        {
            sbyte a = unchecked((sbyte)(payload & 0xFF));
            sbyte b = unchecked((sbyte)((payload >> 8) & 0xFF));
            sbyte c = unchecked((sbyte)((payload >> 16) & 0xFF));
            sbyte d = unchecked((sbyte)((payload >> 24) & 0xFF));
            return new vec4i() {x = a, y = b, z = c, w = d};
        }


        protected static void parse_field_vec2f(BinaryReader reader, USDCField field)
        {
            if (field.IsArray) {
                float[] values = read_array_value<float>(reader, field, sizeof(float), 2);
                vec2f[] vectors = new vec2f[values.Length / 2];
                for (int i = 0; i < vectors.Length; ++i)
                    vectors[i] = new vec2f(values.AsSpan(2*i));
                field.data = vectors;
            } else {
                if (field.ValueRep.IsInlined) {
                    vec4i vec = extract_packed_values(field.ValueRep.PayloadData);
                    field.data = new vec2f((float)vec.x, (float)vec.y);
                } else {
                    reader.BaseStream.Position = (long)field.ValueRep.PayloadData;
                    field.data = new vec2f(reader.ReadSingle(), reader.ReadSingle());
                }
            }
        }
        protected static void parse_field_vec2d(BinaryReader reader, USDCField field)
        {
            if (field.IsArray) {
                double[] values = read_array_value<double>(reader, field, sizeof(double), 2);
                vec2d[] vectors = new vec2d[values.Length / 2];
                for (int i = 0; i < vectors.Length; ++i)
                    vectors[i] = new vec2d(values.AsSpan(2*i));
                field.data = vectors;
            } else {
                if (field.ValueRep.IsInlined) {
                    vec4i vec = extract_packed_values(field.ValueRep.PayloadData);
                    field.data = new vec2f((float)vec.x, (float)vec.y);
                } else {
                    reader.BaseStream.Position = (long)field.ValueRep.PayloadData;
                    field.data = new vec2f(reader.ReadSingle(), reader.ReadSingle());
                }
            }
        }

        protected static void parse_field_vec3f(BinaryReader reader, USDCField field)
        {
            if (field.IsArray) {
                float[] values = read_array_value<float>(reader, field, sizeof(float), 3);
                vec3f[] vectors = new vec3f[values.Length / 3];
                for (int i = 0; i < vectors.Length; ++i)
                    vectors[i] = new vec3f(values.AsSpan(3*i));
                field.data = vectors;
            } else {
                if (field.ValueRep.IsInlined) {
                    vec4i vec = extract_packed_values(field.ValueRep.PayloadData);
                    field.data = new vec3f((float)vec.x, (float)vec.y, (float)vec.z);
                } else {
                    reader.BaseStream.Position = (long)field.ValueRep.PayloadData;
                    field.data = new vec3f(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                }
            }
        }
        protected static void parse_field_vec3d(BinaryReader reader, USDCField field)
        {
            if (field.IsArray) {
                double[] values = read_array_value<double>(reader, field, sizeof(double), 3);
                vec3d[] vectors = new vec3d[values.Length / 3];
                for (int i = 0; i < vectors.Length; ++i)
                    vectors[i] = new vec3d(values.AsSpan(3*i));
                field.data = vectors;
            } else {
                if (field.ValueRep.IsInlined) {
                    vec4i vec = extract_packed_values(field.ValueRep.PayloadData);
                    field.data = new vec3d((double)vec.x, (double)vec.y, (double)vec.z);
                } else {
                    reader.BaseStream.Position = (long)field.ValueRep.PayloadData;
                    field.data = new vec3d(reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble());
                }
            }
        }


        protected static void parse_field_vec4f(BinaryReader reader, USDCField field)
        {
            if (field.IsArray) {
                float[] values = read_array_value<float>(reader, field, sizeof(float), 4);
                vec4f[] vectors = new vec4f[values.Length / 4];
                for (int i = 0; i < vectors.Length; ++i)
                    vectors[i] = new vec4f(values.AsSpan(4*i));
                field.data = vectors;
            } else {
                if (field.ValueRep.IsInlined) {
                    vec4i vec = extract_packed_values(field.ValueRep.PayloadData);
                    field.data = new vec4f((float)vec.x, (float)vec.y, (float)vec.z, (float)vec.w);
                } else {
                    reader.BaseStream.Position = (long)field.ValueRep.PayloadData;
                    field.data = new vec4f(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                }
            }
        }
        protected static void parse_field_vec4d(BinaryReader reader, USDCField field)
        {
            if (field.IsArray) {
                double[] values = read_array_value<double>(reader, field, sizeof(double), 4);
                vec4d[] vectors = new vec4d[values.Length / 4];
                for (int i = 0; i < vectors.Length; ++i)
                    vectors[i] = new vec4d(values.AsSpan(4*i));
                field.data = vectors;
            } else {
                if (field.ValueRep.IsInlined) {
                    vec4i vec = extract_packed_values(field.ValueRep.PayloadData);
                    field.data = new vec4d((double)vec.x, (double)vec.y, (double)vec.z, (double)vec.w);
                } else {
                    reader.BaseStream.Position = (long)field.ValueRep.PayloadData;
                    field.data = new vec4d(reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble());
                }
            }
        }


        protected static void parse_field_quatf(BinaryReader reader, USDCField field)
        {
            if (field.IsArray) {
                float[] values = read_array_value<float>(reader, field, sizeof(float), 4);
                quat4f[] vectors = new quat4f[values.Length / 4];
                for (int i = 0; i < vectors.Length; ++i)
                    vectors[i] = new quat4f(values.AsSpan(4*i));
                field.data = vectors;
            } else {
                if (field.ValueRep.IsInlined) {
                    Util.gDevAssert(false);     // todo need to validate ordering
                    vec4i vec = extract_packed_values(field.ValueRep.PayloadData);
                    field.data = new quat4f((float)vec.x, (float)vec.y, (float)vec.z, (float)vec.w);
                } else {
                    reader.BaseStream.Position = (long)field.ValueRep.PayloadData;
                    field.data = new quat4f(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                }
            }
        }
        protected static void parse_field_quatd(BinaryReader reader, USDCField field)
        {
            if (field.IsArray) {
                double[] values = read_array_value<double>(reader, field, sizeof(double), 4);
                quat4d[] vectors = new quat4d[values.Length / 4];
                for (int i = 0; i < vectors.Length; ++i)
                    vectors[i] = new quat4d(values.AsSpan(4*i));
                field.data = vectors;
            } else {
                if (field.ValueRep.IsInlined) {
                    Util.gDevAssert(false);     // todo need to validate ordering
                    vec4i vec = extract_packed_values(field.ValueRep.PayloadData);
                    field.data = new quat4d((double)vec.x, (double)vec.y, (double)vec.z, (double)vec.w);
                } else {
                    reader.BaseStream.Position = (long)field.ValueRep.PayloadData;
                    field.data = new quat4d(reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble());
                }
            }
        }


        protected static void parse_field_matrix2d(BinaryReader reader, USDCField field)
        {
            if (field.IsArray) {
                double[] values = read_array_value<double>(reader, field, sizeof(double), 4);
                matrix2d[] matrices = new matrix2d[values.Length / 4];
                for (int i = 0; i < matrices.Length; ++i)
                    matrices[i] = new matrix2d(values.AsSpan(4*i));
                field.data = matrices;
            } else {
                if (field.ValueRep.IsInlined) {
                    vec4i diag = extract_packed_values(field.ValueRep.PayloadData);
                    field.data = new matrix2d() {   // todo is this the right indexing??
                        row0 = new vec2d() { u = diag.x },
                        row1 = new vec2d() { v = diag.y }
                    };
                } else {
                    reader.BaseStream.Position = (long)field.ValueRep.PayloadData;
                    double[] values = read_N_values<double>(reader, 4, sizeof(double)).ToArray();
                    field.data = new matrix2d(values);
                }
            }
        }
        protected static void parse_field_matrix3d(BinaryReader reader, USDCField field)
        {
            if (field.IsArray) {
                double[] values = read_array_value<double>(reader, field, sizeof(double), 9);
                matrix3d[] matrices = new matrix3d[values.Length / 9];
                for (int i = 0; i < matrices.Length; ++i)
                    matrices[i] = new matrix3d(values.AsSpan(9*i));
                field.data = matrices;
            } else {
                if (field.ValueRep.IsInlined) {
                    vec4i diag = extract_packed_values(field.ValueRep.PayloadData);
                    field.data = new matrix3d() {   // todo is this the right indexing??
                        row0 = new vec3d() { x = diag.x },
                        row1 = new vec3d() { y = diag.y },
                        row2 = new vec3d() { z = diag.z }
                    };
                } else {
                    reader.BaseStream.Position = (long)field.ValueRep.PayloadData;
                    double[] values = read_N_values<double>(reader, 9, sizeof(double)).ToArray();
                    field.data = new matrix3d(values);
                }
            }
        }
        protected static void parse_field_matrix4d(BinaryReader reader, USDCField field)
        {
            if (field.IsArray) {
                double[] values = read_array_value<double>(reader, field, sizeof(double), 16);
                matrix4d[] matrices = new matrix4d[values.Length / 16];
                for (int i = 0; i < matrices.Length; ++i)
                    matrices[i] = new matrix4d(values.AsSpan(16*i));
                field.data = matrices;
            } else {
                if (field.ValueRep.IsInlined) {
                    vec4i diag = extract_packed_values(field.ValueRep.PayloadData);
                    field.data = new matrix4d() { row0 = new vec4d() { x = diag.x },
                                                 row1 = new vec4d() { y = diag.y },
                                                 row2 = new vec4d() { z = diag.z },
                                                 row3 = new vec4d() { w = diag.w } };
                } else {
                    ulong offset = field.ValueRep.PayloadData;
                    reader.BaseStream.Position = (long)offset;
                    double[] values = read_N_values<double>(reader, 16, sizeof(double)).ToArray();
                    field.data = new matrix4d( values );
                }
            }
        }


    }
}