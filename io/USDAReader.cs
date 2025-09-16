// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Distributed under the Boost Software License, Version 1.0. http://www.boost.org/LICENSE_1_0.txt
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using static g3.Units;
using static g3.USDFile;

#nullable enable

namespace g3
{
    public class USDAReader : IMeshReader
    {
        // connect to this to get warning messages
        public event ParsingMessagesHandler? warningEvent;

        public IOReadResult Read(BinaryReader reader, ReadOptions options, IMeshBuilder builder) {
            throw new NotImplementedException();
        }

        public IOReadResult Read(TextReader reader, ReadOptions options, IMeshBuilder builder)
        {
            Debug.WriteLine("[FILENAME]" + options.BaseFileName);

            // note: possible that USDA is guaranteed to be line-based? this could change some things...

            string? firstline = reader.ReadLine();
            if (firstline == null || firstline.StartsWith("#usda",StringComparison.OrdinalIgnoreCase) == false)
                return IOReadResult.Error(IOCode.UnknownFormatError, "file does not start with #usda");

            // todo do we need to care about version? 

            // read rest of text
            string alltext = reader.ReadToEnd();

            // [TODO] need to strip out all comment lines... currently only doing it properly for fields...

            int first_char_idx = 0;
            while (first_char_idx < alltext.Length && Char.IsWhiteSpace(alltext[first_char_idx]))
                first_char_idx++;
            if (first_char_idx == alltext.Length)
                return IOReadResult.Error(IOCode.FileParsingError, "usda file has no content");

            int next_idx = first_char_idx;

            // if the first character we encounter is an open-bracket, we have the header block
            List<USDField>? HeaderFields = null;
            if (alltext[first_char_idx] == '(') {
                bool bHeaderOK = extract_block(alltext, 0, '(', ')',
                    out string header_prefix, out string header_block, out next_idx);
                bool bHeaderFieldsOK = extract_fields(header_block, out HeaderFields);
            }

            List<USDDef>? topLevelDefs = null;
            bool bTopLevelDefsOK = extract_defs(alltext, next_idx, out topLevelDefs);

            for (int i = 0; i < topLevelDefs.Count; ++i) {
                USDDef cur_def = topLevelDefs[i];
                parse_def(cur_def);

                debug_print("", cur_def);
            }

            return IOReadResult.Ok;

        }


        protected void debug_print(string indent, USDDef def)
        {
            Debug.WriteLine(indent + "DEF " + def.DefType.ToString() + " " + def.DefIdentifier);
            string child_indent = indent + " ";

            foreach (USDField field in def.ContentFields ?? []) {
                string valString = field.Value.ToString();
                Debug.WriteLine(child_indent + "FIELD [" + field.TypeInfo.ToString() + "] [" + field.Name + "] = [ " + valString + " ]");
            }

            foreach (USDDef childDef in def.ChildDefs ?? [])
                debug_print(child_indent, childDef);
        }



        protected class USDDef
        {
            public string prefix = "";
            public string header_block = "";
            public string content_block = "";

            public USDFile.EDefType DefType = USDFile.EDefType.Unknown;
            public string? CustomDefType = null;
            public string DefIdentifier = "";

            public List<USDDef>? ChildDefs = null;

            public List<USDField>? HeaderFields = null;
            public List<USDField>? ContentFields = null;
        }


        protected bool parse_def(USDDef def)
        {
            string[] prefix_tokens = def.prefix.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.RemoveEmptyEntries);

            if (prefix_tokens[1].StartsWith('"')) 
            {
                def.DefIdentifier = prefix_tokens[1].Trim('\"');
                def.DefType = EDefType.NoType;     // anonymous def?
            } 
            else 
            {
                string type_string = prefix_tokens[1];
                int type_index = Array.FindIndex(USDFile.DefTypeTokens, (str) => { return string.Compare(str, type_string, true) == 0; });
                if (type_index >= 0) {
                    def.DefType = (USDFile.EDefType)type_index;
                } else {
                    def.DefType = EDefType.Unknown;
                    def.CustomDefType = type_string;
                    warningEvent?.Invoke($"Def type {type_string} unrecognized", def.prefix);
                }

                def.DefIdentifier = prefix_tokens[2].Trim('\"');
            }

            // todo parse header...

            // def may contain a list of type = value fields, and then one or more variantSet, def, etc. 
            // These appear to always be separated by an empty line?
            // can anything else appear besides def and variantSet? documentation would be aces...

            int start_of_subdefs = def.content_block.IndexOf("def ");
            int start_of_variants = def.content_block.IndexOf("variantSet ");
            int end_of_fields = Math.Min(
                (start_of_subdefs < 0) ? def.content_block.Length : start_of_subdefs,
                (start_of_variants < 0) ? def.content_block.Length : start_of_variants);

            // no subdefs, only fields
            if (end_of_fields == def.content_block.Length) 
            {
                extract_fields(def.content_block, out def.ContentFields);
                return true;
            }

            // extract_fields could take a span...
            string contentfields_block = def.content_block.Substring(0, end_of_fields).Trim();
            if (contentfields_block.Length > 0 ) {
                extract_fields(contentfields_block, out def.ContentFields);
            }

            int cur_index = end_of_fields;
            while (extract_next_subblock(def.content_block, cur_index, out string? subblock, out string? start_token, out int new_cur_index)) 
            {
                cur_index = new_cur_index;

                if ( string.Compare(start_token, "def", true) == 0 ) 
                {
                    if ( extract_defs( subblock!, 0, out List<USDDef> childDefs ) ) {
                        if (def.ChildDefs == null)
                            def.ChildDefs = childDefs;
                        else
                            def.ChildDefs.AddRange(childDefs);
                    }
                }
                else if (string.Compare(start_token, "variantSet", true) == 0) 
                {
                    warningEvent?.Invoke("ignoring variantSet block", null);
                }
                else {
                    warningEvent?.Invoke($"unknown def subblock with token {start_token}", null);
                }

            }

            // extract child defs
            //extract_defs(def.content_block, start_of_subdefs, out def.ChildDefs);

            // recursively parse each child def
            if (def.ChildDefs != null) {
                foreach (USDDef childDef in def.ChildDefs)
                    parse_def(childDef);
            }

            return true;
        }


        protected bool extract_next_subblock(string block, int start_index, out string? subblock, out string? start_token, out int new_start_index)
        {
            subblock = null; start_token = null;  new_start_index = -1;
            while (start_index < block.Length && Char.IsWhiteSpace(block[start_index]))
                start_index++;
            if (start_index == block.Length)
                return false;

            int next_space_idx = block.IndexOf(' ', start_index);
            if (next_space_idx < 0)
                return false;

            start_token = block.Substring(start_index, next_space_idx-start_index);

            int cur_idx = start_index;

            // def may or may not start with a () scope before the {} scope
            int first_brace_idx = block.IndexOf('{', cur_idx);
            int first_bracket_idx = block.IndexOf('(', cur_idx);
            if ( first_bracket_idx > cur_idx && first_bracket_idx < first_brace_idx ) {
                cur_idx = find_end_of_scope(block, first_bracket_idx, '(', ')');
            }

            cur_idx = block.IndexOf('{', cur_idx);
            if (cur_idx < 0) {
                warningEvent?.Invoke($"unexpected def section starting with {start_token} - ignoring rest of def contents", null);
                return false;
            }

            Util.gDevAssert(cur_idx > 0);       // can we have () scope without {} ??
            cur_idx = find_end_of_scope(block, cur_idx, '{', '}');

            new_start_index = cur_idx;
            subblock = block.Substring(start_index, new_start_index-start_index);
            return true;
        }

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



        // this just drops the variantSet for now...
        protected bool extract_variantSet(string buffer, int start_index, out int end_index)
        {
            int cur_idx = start_index;
            while (buffer[cur_idx] != '{')
                cur_idx++;
            cur_idx++;
            int depth = 1;
            while (depth != 0) {
                if (buffer[cur_idx] == '{')
                    depth++;
                else if (buffer[cur_idx] == '}')
                    depth--;
            }
            end_index = cur_idx+1;
            return true;
        }


        protected bool extract_defs(string buffer, int start_index, out List<USDDef> defs)
        {
            defs = new List<USDDef>();

            int next_idx = start_index;

            while (next_idx >= 0 && next_idx <= buffer.Length) {
                int next_def_idx = buffer.IndexOf("def", next_idx);
                if (next_def_idx < 0)
                    break;      // done?

                string def_prefix = "";
                string def_header_block = "";
                string def_content_block = "";

                int next_open_brace = buffer.IndexOf('(', next_def_idx);
                int next_open_curly = buffer.IndexOf('{', next_def_idx);
                if (next_open_brace >= 0 && next_open_brace < next_open_curly) {
                    int end_header_idx = -1, end_content_idx = -1;
                    bool bDefHeaderOK = extract_block(buffer, next_def_idx, '(', ')',
                        out def_prefix, out def_header_block, out end_header_idx);
                    bool bDefContentOK = extract_block(buffer, end_header_idx, '{', '}',
                        out string empty_prefix, out def_content_block, out end_content_idx);
                    next_idx = end_content_idx;
                } else if (next_open_curly > 0) {
                    bool bDefContentOK = extract_block(buffer, next_def_idx, '{', '}',
                        out def_prefix, out def_content_block, out next_idx);
                } else {
                    Debugger.Break();
                    // what to do now??
                }

                USDDef def = new USDDef();
                def.prefix = def_prefix;
                def.header_block = def_header_block;
                def.content_block = def_content_block;
                defs.Add(def);
            }

            return (defs.Count > 0);
        }



        protected class USDField
        {
            public string prefix = "";
            public string block = "";

            public string Name = "";
            public USDFile.USDTypeInfo TypeInfo;
            public USDValue Value = new();

            public List<USDField>? Subfields = null;

            public override string ToString() {
                return $"|{prefix}| = |{block}|";
            }
        }

        protected bool extract_fields(string block, out List<USDField> fields)
        {
            // assume each field is on it's own line?
            // note this was perhaps a dumb decision...complicates a lot of nested-scope handling...revisit?
            string[] lines = block.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return extract_fields(lines, out fields);
        }

        protected bool extract_fields(Span<string> lines, out List<USDField> fields)
        {
            fields = new List<USDField>();

            int line_idx = 0;
            while (line_idx != lines.Length) {

                if (lines[line_idx].StartsWith('#')) {
                    line_idx++;
                    continue;
                }

                USDField field = new USDField();

                int equal_idx = lines[line_idx].IndexOf('=');

                // some rows don't have an = ...
                if (equal_idx == -1) {
                    field.block = lines[line_idx];
                    line_idx++;

                    process_field_options(field, lines, ref line_idx);

                    field.prefix = field.block;
                    field.block = "";

                    fields.Add(field);
                    continue;
                }

                field.prefix = lines[line_idx].Substring(0, equal_idx-1).Trim();
                field.block = lines[line_idx].Substring(equal_idx+1).Trim();
                fields.Add(field);
                line_idx++;

                if (field.prefix.StartsWith("variantSet"))
                    Debugger.Break();

                // field value may be an array/list that spans multiple lines
                // in that case the scope will not end on this line
                bool bHaveOpenBraceScope =
                    field.block.StartsWith('{') && (find_end_of_scope(field.block, 0, '{', '}') == -1);
                bool bHaveOpenSquareScope =
                    field.block.StartsWith('[') && (find_end_of_scope(field.block, 0, '[', ']') == -1);
                bool bHaveOpenScope = (bHaveOpenBraceScope || bHaveOpenSquareScope);

                // if we don't have an open scope, 
                // field may end with a single-line (...) or multiline (\n...) options block
                // want to extract that data and strip it the field value
                if (bHaveOpenScope == false)
                    process_field_options(field, lines, ref line_idx);

                // if we have an open scope, append lines to field value until we find end of scope
                // (todo: these two branches are identical except {} vs []
                if (bHaveOpenBraceScope) 
                {
                    int depth = 1;
                    while (depth > 0) {
                        string line = lines[line_idx];
                        for (int k = 0; k < line.Length; ++k) {
                            if (line[k] == '{')
                                depth++;
                            else if (line[k] == '}')
                                depth--;
                        }
                        field.block += ' ' + line;
                        line_idx++;
                    }
                } 
                else if (bHaveOpenSquareScope) 
                {
                    int depth = 1;
                    while (depth > 0) {
                        string line = lines[line_idx];
                        for (int k = 0; k < line.Length; ++k) {
                            if (line[k] == '[')
                                depth++;
                            else if (line[k] == ']')
                                depth--;
                        }
                        field.block += ' ' + line;
                        line_idx++;
                    }
                }

                // can have options block after we have dealt with multi-line [] / {}
                process_field_options(field, lines, ref line_idx);

            }

            // parse the fields
            foreach (var field in fields)
                parse_field(field);

            return true;
        }

        void process_field_options(USDField field, Span<string> lines, ref int line_idx) 
        {
            if (field.block.EndsWith(')')) 
            {
                // field might end with a single-line ( ... ) options block
                // however we have to differentiate between that and vector/matrix value (a,b,c,...)
                int open_brace_idx = find_start_of_scope(field.block, field.block.Length-1, '(', ')');
                if (open_brace_idx != 0) {      // or it's a vector...
                    string options_block = field.block.Substring(open_brace_idx+1, field.block.Length-open_brace_idx-2).Trim();
                    extract_fields(options_block, out field.Subfields);
                    field.block = field.block.Remove(open_brace_idx).Trim();
                }
            } 
            else if (field.block.EndsWith('(')) 
            {
                // field might end with a single (, which begins a multi-line options block that terminates on a later line
                field.block = field.block.Remove(field.block.Length-1).Trim();   // strip off the (
                int first_line = line_idx;
                while (lines[line_idx].StartsWith(')') == false && lines[line_idx].EndsWith(')') == false)
                    line_idx++;
                int last_line = line_idx;
                extract_fields(lines.Slice(first_line, last_line-first_line), out field.Subfields);
                line_idx++;     // skip the final one
            }
            else if (line_idx < lines.Length && lines[line_idx].StartsWith('(')) 
            {
                // next line (line_idx is next line currently) might open with a (, which begins an options block
                // search for end of options block...
                int first_line = line_idx;
                while ( lines[line_idx].EndsWith(')') == false ) {
                    line_idx++;
                }
                // argh dumb
                string combined = string.Join("\r\n", lines.Slice(first_line, line_idx-first_line+1).ToArray());
                combined = combined.TrimStart('(').TrimEnd(')');
                extract_fields(combined, out field.Subfields);
                line_idx++;
            }

        }



        protected bool parse_field(USDField field)
        {
            string[] tokens = field.prefix.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            int N = tokens.Length;

            field.Name = tokens[N-1];
            if (N > 1) {
                string type_token = tokens[N-2];

                // strip off array
                if (type_token.EndsWith("[]")) {
                    field.TypeInfo.bIsArray = true;
                    type_token = type_token.Substring(0, type_token.Length-2);
                }

                int type_index = Array.FindIndex(USDFile.FieldTypeTokens, (token) => { return string.Compare(type_token, token, StringComparison.OrdinalIgnoreCase) == 0; });
                if (type_index >= 0)
                    field.TypeInfo.USDType = (USDFile.EUSDType)type_index;
                else
                    warningEvent?.Invoke($"unknown field type {tokens[N-2]}", null);

                // handle prefixes
                for ( int i = 0; i < N-2; ++i ) {
                    if (string.Compare(tokens[i], "uniform") == 0)
                        field.TypeInfo.bUniform = true;
                    else if (string.Compare(tokens[i], "custom") == 0)
                        field.TypeInfo.bCustom = true;
                }
            }

            field.Value.TypeInfo = field.TypeInfo;

            if (field.block == null || field.block.Length == 0)
                return true;

            // does name always end with .connect in this case? should we strip <> ?
            if (field.block.StartsWith('<') && field.block.EndsWith('>')) {
                field.Value.bIsConnect = true;
                field.Value.data = field.block;
                return true;
            }

            // do something about this?
            if (field.block.StartsWith('{')) {
                if (field.Value.TypeInfo.bIsArray)
                    field.Value.data = new string[] { "(TIMESAMPLES/MULTI-ARRAYS NOT SUPPORTED YET)" };
                else 
                    field.Value.data = "(TIMESAMPLES/MULTI-ARRAYS NOT SUPPORTED YET)";
                return true;
            }

            if (field.TypeInfo.bIsArray) 
            {
                if ( field.TypeInfo.USDType == EUSDType.Float3 
                    || field.TypeInfo.USDType == EUSDType.Color3f
                    || field.TypeInfo.USDType == EUSDType.Point3f
                    || field.TypeInfo.USDType == EUSDType.Vector3f
                    || field.TypeInfo.USDType == EUSDType.Normal3f
                    || field.TypeInfo.USDType == EUSDType.Half3 ) 
                {
                    field.Value.data = parse_array_vec3f(field.block);
                }
                else if ( field.TypeInfo.USDType == EUSDType.Float2 ||
                          field.TypeInfo.USDType == EUSDType.TexCoord2f ) 
                {
                    field.Value.data = parse_array_vec2f(field.block);
                }
                else if ( field.TypeInfo.USDType == EUSDType.Float4 ) 
                {
                    field.Value.data = parse_array_vec4f(field.block);
                }
                else if ( field.TypeInfo.USDType == EUSDType.Double3
                    || field.TypeInfo.USDType == EUSDType.Vector3d ) 
                {
                    field.Value.data = parse_array_vec3d(field.block);
                }
                else if ( field.TypeInfo.USDType == EUSDType.Quatf ) 
                {
                    field.Value.data = parse_array_quat4f(field.block);
                }
                else if ( field.TypeInfo.USDType == EUSDType.Float
                    || field.TypeInfo.USDType == EUSDType.Half) 
                {
                    field.Value.data = parse_array_float(field.block);
                }
                else if ( field.TypeInfo.USDType == EUSDType.Double ) 
                {
                    field.Value.data = parse_array_double(field.block);
                }
                else if ( field.TypeInfo.USDType == EUSDType.Int ) 
                {
                    field.Value.data = parse_array_int(field.block);
                }
                else if ( field.TypeInfo.USDType == EUSDType.Bool ) 
                {
                    field.Value.data = parse_array_bool(field.block);
                } 
                else if ( field.TypeInfo.USDType == EUSDType.String
                    || field.TypeInfo.USDType == EUSDType.Token
                    || field.TypeInfo.USDType == EUSDType.Asset) 
                {
                    field.Value.data = parse_array_string(field.block);
                }
            } 
            else 
            {
                if (field.TypeInfo.USDType == EUSDType.Rel
                    || field.TypeInfo.USDType == EUSDType.Asset ) 
                {
                    field.Value.data = field.block;
                }
                if (field.TypeInfo.USDType == EUSDType.String
                    || field.TypeInfo.USDType == EUSDType.Token)
                {
                    field.Value.data = field.block.Substring(1, field.block.Length-2);  // strip off quotes
                }
                else if ( field.TypeInfo.USDType == EUSDType.Float
                    || field.TypeInfo.USDType == EUSDType.Half ) 
                {
                    bool bOK = float.TryParse(field.block, out float f);
                    field.Value.data = (bOK) ? f : null;
                }
                else if ( field.TypeInfo.USDType == EUSDType.Double ) 
                {
                    bool bOK = double.TryParse(field.block, out double f);
                    field.Value.data = (bOK) ? f : null;
                }
                else if ( field.TypeInfo.USDType == EUSDType.Int ) 
                {
                    bool bOK = int.TryParse(field.block, out int i);
                    field.Value.data = (bOK) ? i : null;
                }
                else if ( field.TypeInfo.USDType == EUSDType.Bool ) 
                {
                    bool bOK = int.TryParse(field.block, out int i);
                    field.Value.data = (bOK) ? ( (i==0) ? false : true ) : null;
                } 
                if ( field.TypeInfo.USDType == EUSDType.Float3 
                    || field.TypeInfo.USDType == EUSDType.Color3f
                    || field.TypeInfo.USDType == EUSDType.Point3f
                    || field.TypeInfo.USDType == EUSDType.Vector3f
                    || field.TypeInfo.USDType == EUSDType.Normal3f ) 
                {
                    field.Value.data = parse_vec3f(field.block);
                }
                else if ( field.TypeInfo.USDType == EUSDType.Float2 ||
                          field.TypeInfo.USDType == EUSDType.TexCoord2f ) 
                {
                    field.Value.data = parse_vec2f(field.block);
                }
                else if ( field.TypeInfo.USDType == EUSDType.Float4 ) 
                {
                    field.Value.data = parse_vec4f(field.block);
                }
                else if ( field.TypeInfo.USDType == EUSDType.Double3
                    || field.TypeInfo.USDType == EUSDType.Vector3d ) 
                {
                    field.Value.data = parse_vec3d(field.block);
                }
                else if ( field.TypeInfo.USDType == EUSDType.Quatf ) 
                {
                    vec4f v = parse_vec4f(field.block);
                    field.Value.data = new quat4f() { w = v.x, x = v.y, y = v.z, z = v.w };
                }
                else if ( field.TypeInfo.USDType == EUSDType.Matrix4d ) 
                {
                    field.Value.data = parse_matrix4d(field.block);
                }
            }

            return true;
        }


        protected static vec2f parse_vec2f(string value)
        {
            string tokensString = value.Substring(1, value.Length-2);       // strip off () brackets
            string[] numberStrings = tokensString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (numberStrings.Length != 2)
                throw new Exception($"parse_vec2f: incorrect number of elements {numberStrings.Length} in parsing vec2f string {tokensString}");
            bool bx = float.TryParse(numberStrings[0], out float fx);
            bool by = float.TryParse(numberStrings[1], out float fy);
            if (bx == false || by == false)
                throw new Exception($"parse_vec2f: failed parsing {tokensString}");
            return new vec2f() { u = fx, v = fy };
        }
        protected static vec3f parse_vec3f(string value)
        {
            string tokensString = value.Substring(1, value.Length-2);       // strip off () brackets
            string[] numberStrings = tokensString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (numberStrings.Length != 3)
                throw new Exception($"parse_vec3f: incorrect number of elements {numberStrings.Length} in parsing vec3f string {tokensString}");
            bool bx = float.TryParse(numberStrings[0], out float fx);
            bool by = float.TryParse(numberStrings[1], out float fy);
            bool bz = float.TryParse(numberStrings[2], out float fz);
            if (bx == false || by == false || bz == false)
                throw new Exception($"parse_vec3f: failed parsing {tokensString}");
            return new vec3f() { x = fx, y = fy, z = fz };
        }
        protected static vec3d parse_vec3d(string value)
        {
            string tokensString = value.Substring(1, value.Length-2);       // strip off () brackets
            string[] numberStrings = tokensString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (numberStrings.Length != 3)
                throw new Exception($"parse_vec3f: incorrect number of elements {numberStrings.Length} in parsing vec3f string {tokensString}");
            bool bx = double.TryParse(numberStrings[0], out double fx);
            bool by = double.TryParse(numberStrings[1], out double fy);
            bool bz = double.TryParse(numberStrings[2], out double fz);
            if (bx == false || by == false || bz == false)
                throw new Exception($"parse_vec3d: failed parsing {tokensString}");
            return new vec3d() { x = fx, y = fy, z = fz };
        }
        protected static vec4f parse_vec4f(string value)
        {
            string tokensString = value;
            if ( tokensString.StartsWith('('))
                tokensString = tokensString.Substring(1, tokensString.Length-2);       // strip off () brackets
            string[] numberStrings = tokensString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (numberStrings.Length != 4)
                throw new Exception($"parse_vec4f: incorrect number of elements {numberStrings.Length} in parsing vec3f string {tokensString}");
            bool bx = float.TryParse(numberStrings[0], out float fx);
            bool by = float.TryParse(numberStrings[1], out float fy);
            bool bz = float.TryParse(numberStrings[2], out float fz);
            bool bw = float.TryParse(numberStrings[3], out float fw);
            if (bx == false || by == false || bz == false || bw == false)
                throw new Exception($"parse_vec4f: failed parsing {tokensString}");
            return new vec4f() { x = fx, y = fy, z = fz, w = fw };
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

        protected static vec2f[]? parse_array_vec2f(string value)
        {
            List<vec2f> values = new List<vec2f>();
            bool bOK = parse_array_realN(value, 2, (double[] val) => {
                values.Add(new vec2f() { u = (float)val[0], v = (float)val[1] });
            }, true);
            return (bOK && values.Count > 0) ? values.ToArray() : null;
        }
        protected static vec3f[]? parse_array_vec3f(string value)
        {
            List<vec3f> values = new List<vec3f>();
            bool bOK = parse_array_realN(value, 3, (double[] val) => {
                values.Add(new vec3f() { x = (float)val[0], y = (float)val[1], z = (float)val[2] });
            }, true);
            return (bOK && values.Count > 0) ? values.ToArray() : null;
        }
        protected static vec4f[]? parse_array_vec4f(string value)
        {
            List<vec4f> values = new List<vec4f>();
            bool bOK = parse_array_realN(value, 4, (double[] val) => {
                values.Add(new vec4f() { x = (float)val[0], y = (float)val[1], z = (float)val[2], w = (float)val[3] });
            }, true);
            return (bOK && values.Count > 0) ? values.ToArray() : null;
        }
        protected static vec3d[]? parse_array_vec3d(string value)
        {
            List<vec3d> values = new List<vec3d>();
            bool bOK = parse_array_realN(value, 3, (double[] val) => {
                values.Add(new vec3d() { x = val[0], y = val[1], z = val[2] });
            });
            return (bOK && values.Count > 0) ? values.ToArray() : null;
        }
        protected static quat4f[]? parse_array_quat4f(string value)
        {
            List<quat4f> values = new List<quat4f>();
            bool bOK = parse_array_realN(value, 4, (double[] val) => {
                values.Add(new quat4f() { w = (float)val[0], x = (float)val[1], y = (float)val[2], z = (float)val[3] });
            }, true);
            return (bOK && values.Count > 0) ? values.ToArray() : null;
        }
        protected static bool parse_array_realN(string value, int numFloatsPerElement, Action<double[]> onElementF, bool bParseAs32Bit = false)
        {
            if (value.StartsWith("[") == false || value.EndsWith("]") == false)
                return false;
            double[] parsedFloats = new double[numFloatsPerElement];
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
                    throw new NotImplementedException($"parse_array_float: incorrect number of elements {numberStrings.Length} in parsing vec{numFloatsPerElement}...??");
                bool bOK = true; 
                for ( int i = 0; i <  numFloatsPerElement && bOK; i++ ) {
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
                // todo: do we need to handle quoting inside string??
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
            bool bOK = parse_array_real(valueString, (double val) => {
                values.Add((float)val);
            }, true);
            return (bOK && values.Count > 0) ? values.ToArray() : null;
        }
        protected static double[]? parse_array_double(string valueString)
        {
            List<double> values = new List<double>();
            bool bOK = parse_array_real(valueString, (double val) => {
                values.Add(val);
            });
            return (bOK && values.Count > 0) ? values.ToArray() : null;
        }
        protected static int[]? parse_array_int(string valueString)
        {   // kinda cheating to use float here...but will work in range we should ever see in 3D geometry...
            List<int> values = new List<int>();
            bool bOK = parse_array_real(valueString, (double val) => {
                values.Add((int)val);
            });
            return (bOK && values.Count > 0) ? values.ToArray() : null;
        }
        protected static bool[]? parse_array_bool(string valueString)
        {   
            List<bool> values = new List<bool>();
            bool bOK = parse_array_real(valueString, (double val) => {
                values.Add( (val == 1) ? true : false );
            });
            return (bOK && values.Count > 0) ? values.ToArray() : null;
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



        protected bool extract_block(string buffer, int begin_index, char startchar, char endchar,
            out string prefix, out string block, out int next_idx)
        {
            prefix = block = "";
            next_idx = -1;

            int startchar_idx = begin_index;
            while (buffer[startchar_idx] != startchar && startchar_idx < buffer.Length) {
                startchar_idx++;
            }
            if (startchar_idx == buffer.Length) 
                return false;
            prefix = buffer.Substring(begin_index, startchar_idx-begin_index).Trim();

            int cur_idx = startchar_idx+1;
            int endchar_idx = -1;
            int depth = 0;
            while (endchar_idx == -1 && cur_idx != buffer.Length) {
                if (buffer[cur_idx] == startchar) {
                    depth++;
                } else if (buffer[cur_idx] == endchar) {
                    if ( depth == 0) {
                        endchar_idx = cur_idx;
                    } else {
                        depth--;
                    }
                }
                cur_idx++;
            }
            if (endchar_idx == -1)
                return false;

            next_idx = endchar_idx+1;

            if (endchar_idx == startchar_idx+1) {       // empty block
                block = "";
                return true;
            }
            startchar_idx++; endchar_idx--;
            block = buffer.Substring(startchar_idx, endchar_idx-startchar_idx).Trim();
            return true;
        }

    }
}
