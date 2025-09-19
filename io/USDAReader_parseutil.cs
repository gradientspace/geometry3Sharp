// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Distributed under the Boost Software License, Version 1.0. http://www.boost.org/LICENSE_1_0.txt
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using static g3.USDFile;

#nullable enable


namespace g3
{
    public partial class USDAReader : IMeshReader
    {
        // parsing utilities
        //   find_end_of_scope() / find_start_of_scope() - search through buffer from
        //          one end of a scope to the other (eg from { to }, ( to ), etc).
        //          Handles nesting of the scope characters
        //
        //   parse_field_value(USDAField) - set the Value.data member of the field
        //          by parsing the contents of the .block string. 
        //
        //   parse_xyz() - implementations of all the different parsers for parse_field_value()
        //  

        static int find_end_of_scope(string buffer, int start_index, char open_scope, char close_scope)
        {
            Util.gDevAssert(buffer[start_index] == open_scope);
            int cur_idx = start_index+1;
            int depth = 1;
            while (depth != 0 && cur_idx < buffer.Length) {
                if (buffer[cur_idx] == open_scope)
                    depth++;
                else if (buffer[cur_idx] == close_scope)
                    depth--;
                cur_idx++;
            }
            return (depth == 0) ? cur_idx : -1;
        }

        static int find_start_of_scope(string buffer, int end_index, char open_scope, char close_scope)
        {
            Util.gDevAssert(buffer[end_index] == close_scope);
            int cur_idx = end_index;
            int depth = 1;
            while (depth != 0) {
                cur_idx--;
                if (buffer[cur_idx] == close_scope)
                    depth++;
                else if (buffer[cur_idx] == open_scope)
                    depth--;
            }
            return cur_idx;
        }



        protected void parse_field_value(USDAField field)
        {
            bool bIsArray = field.TypeInfo.bIsArray;
            switch (field.TypeInfo.USDType) 
            {
                case EUSDType.Unknown:
                case EUSDType.Opaque:
                    field.Value.data = field.block;   // just leave as string
                    break;

                case EUSDType.Asset:
                case EUSDType.Rel:
                    field.Value.data = field.block;     // do we need array handling here? is it possible?
                    break;

                case EUSDType.String:
                case EUSDType.Token:
                    field.Value.data = (bIsArray) ? parse_array_string(field.block) : field.block.Substring(1, field.block.Length-2);  // strip off quotes
                    break;

                case EUSDType.Float3:
                case EUSDType.Half3:
                case EUSDType.Point3f:
                case EUSDType.Point3h:
                case EUSDType.Color3f:
                case EUSDType.Color3h:
                case EUSDType.Vector3f:
                case EUSDType.Vector3h:
                case EUSDType.Normal3f:
                case EUSDType.Normal3h:
                case EUSDType.TexCoord3f:
                case EUSDType.TexCoord3h:
                    field.Value.data = (bIsArray) ? parse_array_vec3f(field.block) : parse_vec3f(field.block);
                    break;
                case EUSDType.Float2:
                case EUSDType.Half2:
                case EUSDType.TexCoord2f:
                case EUSDType.TexCoord2h:
                    field.Value.data = (bIsArray) ? parse_array_vec2f(field.block) : parse_vec2f(field.block);
                    break;
                case EUSDType.Float4:
                case EUSDType.Half4:
                case EUSDType.Color4f:
                case EUSDType.Color4h:
                    field.Value.data = (bIsArray) ? parse_array_vec4f(field.block) : parse_vec4f(field.block);
                    break;
                case EUSDType.Double3:
                case EUSDType.Vector3d:
                case EUSDType.Normal3d:
                case EUSDType.Point3d:
                case EUSDType.Color3d:
                case EUSDType.TexCoord3d:
                    field.Value.data = (bIsArray) ? parse_array_vec3d(field.block) : parse_vec3d(field.block);
                    break;
                case EUSDType.Double2:
                case EUSDType.TexCoord2d:
                    field.Value.data = (bIsArray) ? parse_array_vec2d(field.block) : parse_vec2d(field.block);
                    break;
                case EUSDType.Double4:
                case EUSDType.Color4d:
                    field.Value.data = (bIsArray) ? parse_array_vec4d(field.block) : parse_vec4d(field.block);
                    break;
                case EUSDType.Quatf:
                case EUSDType.Quath:
                    field.Value.data = (bIsArray) ? parse_array_quat4f(field.block) : parse_quat4f(field.block);
                    break;
                case EUSDType.Quatd:
                    field.Value.data = (bIsArray) ? parse_array_quat4d(field.block) : parse_quat4d(field.block);
                    break;
                case EUSDType.Matrix2d:
                    field.Value.data = (bIsArray) ? parse_array_matrix<matrix2d>(field.block, parse_matrix2d) : parse_matrix2d(field.block);
                    break;
                case EUSDType.Matrix3d:
                    field.Value.data = (bIsArray) ? parse_array_matrix<matrix3d>(field.block, parse_matrix3d) : parse_matrix3d(field.block);
                    break;
                case EUSDType.Matrix4d:
                case EUSDType.Frame4d:
                    field.Value.data = (bIsArray) ? parse_array_matrix<matrix4d>(field.block, parse_matrix4d) : parse_matrix4d(field.block);
                    break;
                case EUSDType.Float:
                case EUSDType.Half:
                    field.Value.data = (bIsArray) ? parse_array_float(field.block) : (float.TryParse(field.block, out float floatval) ? floatval : null);
                    break;
                case EUSDType.Double:
                case EUSDType.Timecode:
                    field.Value.data = (bIsArray) ? parse_array_double(field.block) : (double.TryParse(field.block, out double dblval) ? dblval : null);
                    break;
                case EUSDType.Bool:
                    field.Value.data = (bIsArray) ? parse_array_bool(field.block) : (int.TryParse(field.block, out int boolval) ? (bool)(boolval!=0) : null);
                    break;
                case EUSDType.UChar:
                    field.Value.data = (bIsArray) ? parse_array_byte(field.block) : (byte.TryParse(field.block, out byte ucharval) ? ucharval : null);
                    break;
                case EUSDType.Int:
                    field.Value.data = (bIsArray) ? parse_array_int(field.block) : (int.TryParse(field.block, out int intval) ? intval : null);
                    break;
                case EUSDType.UInt:
                    field.Value.data = (bIsArray) ? parse_array_uint(field.block) : (uint.TryParse(field.block, out uint uintval) ? uintval : null);
                    break;
                case EUSDType.Int64:
                    field.Value.data = (bIsArray) ? parse_array_int64(field.block) : (long.TryParse(field.block, out long int64val) ? int64val : null);
                    break;
                case EUSDType.UInt64:
                    field.Value.data = (bIsArray) ? parse_array_uint64(field.block) : (ulong.TryParse(field.block, out ulong uint64val) ? uint64val  : null);
                    break;
                case EUSDType.Int2:
                    field.Value.data = (bIsArray) ? parse_array_vec2i(field.block) : parse_vec2i(field.block);
                    break;
                case EUSDType.Int3:
                    field.Value.data = (bIsArray) ? parse_array_vec3i(field.block) : parse_vec3i(field.block);
                    break;
                case EUSDType.Int4:
                    field.Value.data = (bIsArray) ? parse_array_vec4i(field.block) : parse_vec4i(field.block);
                    break;

                default:
                    warningEvent?.Invoke("cannot parse field value for USDType {field.TypeInfo.USDType}", null);
                    break;
            }
        }




        protected static real_list16 parse_real_vec(string value, int count, bool bParseAs32Bit)
        {
            real_list16 result = new();
            string tokensString = value.StartsWith('(') ? value.Substring(1, value.Length-2) : value;       // strip off () brackets
            string[] numberStrings = tokensString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (numberStrings.Length != count)
                throw new Exception($"parse_real_vec: incorrect number of elements {numberStrings.Length} in parsing {count}-element string {tokensString}");
            bool bAllOK = true;
            for (int i = 0; i < count; ++i) {
                if (bParseAs32Bit) {
                    bAllOK &= float.TryParse(numberStrings[i], out float f);
                    if (bAllOK ) result[i] = f;
                } else 
                    bAllOK &= double.TryParse(numberStrings[i], out result[i]);
            }
            if (!bAllOK)
                throw new Exception($"parse_real_vec: failed parsing {tokensString}");
            return result;
        }

        // these are inefficient and could be done with FindNext and Span into stackalloc arrays...
        protected static vec2f parse_vec2f(string value) {
            return new vec2f(parse_real_vec(value, 2, true));
        }
        protected static vec2d parse_vec2d(string value) {
            return new vec2d(parse_real_vec(value, 2, false));
        }
        protected static vec3f parse_vec3f(string value) {
            return new vec3f(parse_real_vec(value, 3, true));
        }
        protected static vec3d parse_vec3d(string value) {
            return new vec3d(parse_real_vec(value, 3, false));
        }
        protected static vec4f parse_vec4f(string value) {
            return new vec4f(parse_real_vec(value, 4, true));
        }
        protected static vec4d parse_vec4d(string value) {
            return new vec4d(parse_real_vec(value, 4, false));
        }
        protected static quat4f parse_quat4f(string value) {
            return new quat4f(parse_real_vec(value, 4, true));
        }
        protected static quat4d parse_quat4d(string value) {
            return new quat4d(parse_real_vec(value, 4, false));
        }
        protected static matrix2d parse_matrix2d(string value)
        {
            string tokensString = value.Substring(1, value.Length-2);       // strip off outermost () brackets
            tokensString = tokensString.Replace(" ", string.Empty);         // remove any whitespace, so that inner values are ),(
            string[] numberStrings = tokensString.Split("),(", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (numberStrings.Length != 2)
                throw new Exception($"parse_matrix2d: incorrect number of elements {numberStrings.Length} in parsing matrix2d string {tokensString}");
            vec2f r0 = parse_vec2f(numberStrings[0].Substring(1));
            vec2f r1 = parse_vec2f(numberStrings[1].Substring(0, numberStrings[1].Length-1));
            return new matrix2d() { row0 = r0, row1 = r1 };
        }
        protected static matrix3d parse_matrix3d(string value)
        {
            string tokensString = value.Substring(1, value.Length-2);       // strip off outermost () brackets
            tokensString = tokensString.Replace(" ", string.Empty);         // remove any whitespace, so that inner values are ),(
            string[] numberStrings = tokensString.Split("),(", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (numberStrings.Length != 3)
                throw new Exception($"parse_matrix4d: incorrect number of elements {numberStrings.Length} in parsing matrix3d string {tokensString}");
            vec3f r0 = parse_vec3f(numberStrings[0].Substring(1));
            vec3f r1 = parse_vec3f(numberStrings[1]);
            vec3f r2 = parse_vec3f(numberStrings[2].Substring(0, numberStrings[2].Length-1));
            return new matrix3d() { row0 = r0, row1 = r1, row2 = r2 };
        }
        protected static matrix4d parse_matrix4d(string value)
        {
            string tokensString = value.Substring(1, value.Length-2);       // strip off outermost () brackets
            tokensString = tokensString.Replace(" ", string.Empty);         // remove any whitespace, so that inner values are ),(
            string[] numberStrings = tokensString.Split("),(", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (numberStrings.Length != 4)
                throw new Exception($"parse_matrix4d: incorrect number of elements {numberStrings.Length} in parsing matrix4d string {tokensString}");
            vec4f r0 = parse_vec4f(numberStrings[0].Substring(1));
            vec4f r1 = parse_vec4f(numberStrings[1]);
            vec4f r2 = parse_vec4f(numberStrings[2]);
            vec4f r3 = parse_vec4f(numberStrings[3].Substring(0, numberStrings[3].Length-1));
            return new matrix4d() { row0 = r0, row1 = r1, row2 = r2, row3 = r3 };
        }


        protected static int64_list8 parse_int_vec(string value, int count)
        {
            int64_list8 result = new();
            string tokensString = value.Substring(1, value.Length-2);       // strip off () brackets
            string[] numberStrings = tokensString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (numberStrings.Length != count)
                throw new Exception($"parse_int_vec: incorrect number of elements {numberStrings.Length} in parsing {count}-element string {tokensString}");
            bool bAllOK = true;
            for (int i = 0; i < count; ++i) {
                bAllOK &= long.TryParse(numberStrings[i], out result[i]);
            }
            if (!bAllOK)
                throw new Exception($"parse_int_vec: failed parsing {tokensString}");
            return result;
        }
        protected static vec2i parse_vec2i(string value) {
            return new vec2i(parse_int_vec(value, 2));
        }
        protected static vec3i parse_vec3i(string value)
        {
            return new vec3i(parse_int_vec(value, 3));
        }
        protected static vec4i parse_vec4i(string value)
        {
            return new vec4i(parse_int_vec(value,4));
        }

        protected static vec2f[]? parse_array_vec2f(string value)
        {
            List<vec2f> values = new List<vec2f>();
            bool bOK = parse_array_realN(value, 2, (real_list16 val) => { values.Add(new vec2f(val)); }, true);
            return (bOK && values.Count > 0) ? values.ToArray() : null;
        }
        protected static vec3f[]? parse_array_vec3f(string value)
        {
            List<vec3f> values = new List<vec3f>();
            bool bOK = parse_array_realN(value, 3, (real_list16 val) => { values.Add(new vec3f(val)); }, true);
            return (bOK && values.Count > 0) ? values.ToArray() : null;
        }
        protected static vec4f[]? parse_array_vec4f(string value)
        {
            List<vec4f> values = new List<vec4f>();
            bool bOK = parse_array_realN(value, 4, (real_list16 val) => { values.Add(new vec4f(val)); }, true);
            return (bOK && values.Count > 0) ? values.ToArray() : null;
        }
        protected static vec2d[]? parse_array_vec2d(string value)
        {
            List<vec2d> values = new List<vec2d>();
            bool bOK = parse_array_realN(value, 2, (real_list16 val) => { values.Add(new vec2d(val)); }, false);
            return (bOK && values.Count > 0) ? values.ToArray() : null;
        }
        protected static vec3d[]? parse_array_vec3d(string value)
        {
            List<vec3d> values = new List<vec3d>();
            bool bOK = parse_array_realN(value, 3, (real_list16 val) => { values.Add(new vec3d(val)); }, false);
            return (bOK && values.Count > 0) ? values.ToArray() : null;
        }
        protected static vec4d[]? parse_array_vec4d(string value)
        {
            List<vec4d> values = new List<vec4d>();
            bool bOK = parse_array_realN(value, 4, (real_list16 val) => { values.Add(new vec4d(val)); }, false);
            return (bOK && values.Count > 0) ? values.ToArray() : null;
        }
        protected static quat4f[]? parse_array_quat4f(string value)
        {
            List<quat4f> values = new List<quat4f>();
            bool bOK = parse_array_realN(value, 4, (real_list16 val) => { values.Add(new quat4f(val)); }, true);
            return (bOK && values.Count > 0) ? values.ToArray() : null;
        }
        protected static quat4d[]? parse_array_quat4d(string value)
        {
            List<quat4d> values = new List<quat4d>();
            bool bOK = parse_array_realN(value, 4, (real_list16 val) => { values.Add(new quat4d(val)); }, false);
            return (bOK && values.Count > 0) ? values.ToArray() : null;
        }
        protected static bool parse_array_realN(string value, int numFloatsPerElement, Action<real_list16> onElementF, bool bParseAs32Bit = false)
        {
            if (value.StartsWith("[") == false || value.EndsWith("]") == false)
                return false;
            real_list16 parsedFloats = new();
            int start_idx = 1;

            int cur_idx = start_idx;
            bool bDone = false;
            while (!bDone) {
                int start_bracket_idx = value.IndexOf('(', cur_idx);
                if (start_bracket_idx < 0)
                    break;
                int end_bracket_idx = value.IndexOf(')', start_bracket_idx+1);
                cur_idx = end_bracket_idx+1;
                start_bracket_idx++;
                string tokensString = value.Substring(start_bracket_idx, end_bracket_idx-start_bracket_idx);
                string[] numberStrings = tokensString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (numberStrings.Length != numFloatsPerElement)
                    throw new NotImplementedException($"parse_array_realN: incorrect number of elements {numberStrings.Length} in parsing vec{numFloatsPerElement}...??");
                bool bOK = true; 
                for ( int i = 0; i < numFloatsPerElement && bOK; i++ ) {
                    if ( bParseAs32Bit ) {
                        bOK &= float.TryParse(numberStrings[i], out float parsedFloat);
                        parsedFloats[i] = (double)parsedFloat;
                    } else
                        bOK &= double.TryParse(numberStrings[i], out parsedFloats[i]);
                }
                if (bOK == false)
                    throw new NotImplementedException($"failed parsing vec{numFloatsPerElement} elements...??");
                onElementF(parsedFloats);
            }
            return true;
        }

        protected static vec2i[]? parse_array_vec2i(string value)
        {
            List<vec2i> values = new List<vec2i>();
            bool bOK = parse_array_integerN(value, 2, (int64_list8 val) => { values.Add(new vec2i(val)); });
            return (bOK && values.Count > 0) ? values.ToArray() : null;
        }
        protected static vec3i[]? parse_array_vec3i(string value)
        {
            List<vec3i> values = new List<vec3i>();
            bool bOK = parse_array_integerN(value, 3, (int64_list8 val) => { values.Add(new vec3i(val)); });
            return (bOK && values.Count > 0) ? values.ToArray() : null;
        }
        protected static vec4i[]? parse_array_vec4i(string value)
        {
            List<vec4i> values = new List<vec4i>();
            bool bOK = parse_array_integerN(value, 4, (int64_list8 val) => { values.Add(new vec4i(val)); });
            return (bOK && values.Count > 0) ? values.ToArray() : null;
        }
        protected static bool parse_array_integerN(string value, int numIntsPerElem, Action<int64_list8> onElementF)
        {
            if (value.StartsWith("[") == false || value.EndsWith("]") == false)
                return false;
            int64_list8 parsedInts = new();
            int start_idx = 1;

            int cur_idx = start_idx;
            bool bDone = false;
            while (!bDone) {
                int start_bracket_idx = value.IndexOf('(', cur_idx);
                if (start_bracket_idx < 0)
                    break;
                int end_bracket_idx = value.IndexOf(')', start_bracket_idx+1);
                cur_idx = end_bracket_idx+1;
                start_bracket_idx++;
                string tokensString = value.Substring(start_bracket_idx, end_bracket_idx-start_bracket_idx);
                string[] numberStrings = tokensString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (numberStrings.Length != numIntsPerElem)
                    throw new NotImplementedException($"parse_array_integerN: incorrect number of elements {numberStrings.Length} in parsing vec{numIntsPerElem}i...??");
                bool bOK = true;
                for (int i = 0; i < numIntsPerElem && bOK; i++) {
                    bOK &= long.TryParse(numberStrings[i], out parsedInts[i]);
                }
                if (bOK == false)
                    throw new NotImplementedException($"failed parsing vec{numIntsPerElem} elements...??");
                onElementF(parsedInts);
            }
            return true;
        }


        protected static T[]? parse_array_matrix<T>(string value, Func<string, T> parse_matrix_func) where T : struct
        {
            if (value.StartsWith("[") == false || value.EndsWith("]") == false)
                return null;
            int start_index = 1;

            List<T> values = new List<T>();
            start_index = value.IndexOf('(', start_index);

            while (start_index > 0) {
                int end_index = find_end_of_scope(value, start_index, '(', ')');
                string substr = value.Substring(start_index, end_index-start_index);
                T mat = parse_matrix_func(substr);
                values.Add(mat);
                start_index = value.IndexOf('(', end_index);
            }
            return (values.Count > 0) ? values.ToArray() : null;
        }



        protected static string[]? parse_array_string(string valueString)
        {
            List<string> values = new List<string>();
            bool bOK = parse_array_string(valueString, (string val) => {
                values.Add(val);
            });
            return (bOK && values.Count > 0) ? values.ToArray() : null;
        }
        protected static bool parse_array_string(string valueString, Action<string> onElementF)
        {
            if (valueString.StartsWith("[") == false || valueString.EndsWith("]") == false)
                return false;
            int start_idx = 1;

            int cur_idx = start_idx;
            bool bDone = false;
            while (!bDone) {
                // todo: do we need to handle quoting inside string??   (yes!!)
                int start_quote_idx = valueString.IndexOf('"', cur_idx);
                if (start_quote_idx < 0)
                    break;
                int end_quote_idx = valueString.IndexOf('"', start_quote_idx+1);
                cur_idx = end_quote_idx+1;
                start_quote_idx++;
                string tokenString = valueString.Substring(start_quote_idx, end_quote_idx-start_quote_idx);
                if (tokenString.Length > 0)
                    onElementF(tokenString);
            }
            return true;
        }



        protected static float[]? parse_array_float(string valueString)
        {
            List<float> values = new List<float>();
            bool bOK = parse_array_real(valueString, (double val) => { values.Add((float)val); }, true);
            return (bOK) ? values.ToArray() : null;
        }
        protected static double[]? parse_array_double(string valueString)
        {
            List<double> values = new List<double>();
            bool bOK = parse_array_real(valueString, (double val) => { values.Add(val); });
            return (bOK) ? values.ToArray() : null;
        }
        protected static int[]? parse_array_int(string valueString)
        {
            List<int> values = new List<int>();
            bool bOK = parse_array_integer(valueString, (long val) => { values.Add((int)val); });
            return (bOK) ? values.ToArray() : null;
        }
        protected static uint[]? parse_array_uint(string valueString)
        {
            List<uint> values = new List<uint>();
            bool bOK = parse_array_integer(valueString, (long val) => { values.Add((uint)val); });
            return (bOK) ? values.ToArray() : null;
        }
        protected static long[]? parse_array_int64(string valueString)
        {
            List<long> values = new List<long>();
            bool bOK = parse_array_integer(valueString, (long val) => { values.Add(val); });
            return (bOK) ? values.ToArray() : null;
        }
        protected static ulong[]? parse_array_uint64(string valueString)
        {
            List<ulong> values = new List<ulong>();
            bool bOK = parse_array_integer(valueString, (long val) => { values.Add((ulong)val); });
            return (bOK) ? values.ToArray() : null;
        }
        protected static byte[]? parse_array_byte(string valueString)
        {
            List<byte> values = new List<byte>();
            bool bOK = parse_array_integer(valueString, (long val) => { values.Add((byte)val); });
            return (bOK) ? values.ToArray() : null;
        }
        protected static bool[]? parse_array_bool(string valueString)
        {   
            List<bool> values = new List<bool>();
            bool bOK = parse_array_integer(valueString, (long val) => {
                values.Add( (val == 1) ? true : false );
            });
            return (bOK) ? values.ToArray() : null;
        }
        protected static bool parse_array_real(string valueString, Action<double> onElementF, bool bReadAs32bit = false)
        {
            if (valueString.StartsWith("[") == false || valueString.EndsWith("]") == false)
                return false;
            int start_idx = 1;

            int cur_idx = start_idx;
            bool bDone = false;
            while (!bDone) {
                int next_comma_idx = valueString.IndexOf(',', cur_idx);
                if (next_comma_idx == -1) {
                    next_comma_idx = valueString.Length-1;      // end-of-string
                    bDone = true;
                }
                string tokenString = valueString.Substring(cur_idx, next_comma_idx-cur_idx).Trim();
                if (tokenString.Length == 0)        // handle string ending in  ',]'
                    continue;
                cur_idx = next_comma_idx+1;
                double parsedValue = 0; bool bOK = false;
                if (bReadAs32bit) {
                    bOK = float.TryParse(tokenString, out float parsedFloat);
                    parsedValue = parsedFloat;
                } else {
                    bOK = double.TryParse(tokenString, out parsedValue);
                }
                if (bOK == false)
                    throw new NotImplementedException($"failed parsing float from '{tokenString}'...??");
                onElementF(parsedValue);
            }
            return true;
        }
        protected static bool parse_array_integer(string valueString, Action<long> onElementF)
        {
            if (valueString.StartsWith("[") == false || valueString.EndsWith("]") == false)
                return false;
            int start_idx = 1;

            int cur_idx = start_idx;
            bool bDone = false;
            while (!bDone) {
                int next_comma_idx = valueString.IndexOf(',', cur_idx);
                if (next_comma_idx == -1) {
                    next_comma_idx = valueString.Length-1;      // end-of-string
                    bDone = true;
                }
                string tokenString = valueString.Substring(cur_idx, next_comma_idx-cur_idx).Trim();
                if (tokenString.Length == 0)        // handle string ending in  ',]'
                    continue;
                cur_idx = next_comma_idx+1;
                bool bOK = long.TryParse(tokenString, out long parsedValue);
                if (bOK == false)
                    throw new NotImplementedException($"failed parsing long from '{tokenString}'...??");
                onElementF(parsedValue);
            }
            return true;
        }


    }
}
