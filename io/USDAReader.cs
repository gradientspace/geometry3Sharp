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

            Matrix4d BaseTransform = Matrix4d.Identity;
            foreach (USDDef def in topLevelDefs)
                build_meshes(def, options, builder, BaseTransform);

            return IOReadResult.Ok;

        }


        protected void build_meshes(USDDef def, ReadOptions options, IMeshBuilder builder, Matrix4d ParentTransform)
        {
            Matrix4d CurTransform = ParentTransform;
            if (def.DefType == EDefType.Mesh && def.ContentFields != null) 
            {
                // find vertices
                USDField? points = def.ContentFields.Find((USDField f) => { return f.Name == "points"; });

                USDField? faceVertexCounts = def.ContentFields.Find((USDField f) => { return f.Name == "faceVertexCounts"; });
                USDField? faceVertexIndices = def.ContentFields.Find((USDField f) => { return f.Name == "faceVertexIndices"; });

                USDField? uvs = def.ContentFields.Find((USDField f) => { return f.Name == "primvars:st"; });
                USDField? normals = def.ContentFields.Find((USDField f) => { return f.Name == "normals"; });

                bool bHaveRequiredFields = true;
                if (points == null) { warningEvent?.Invoke($"Mesh {def.DefIdentifier} is missing points field", null); bHaveRequiredFields = false; }
                if (faceVertexCounts == null) { warningEvent?.Invoke($"Mesh {def.DefIdentifier} is missing faceVertexCounts field", null); bHaveRequiredFields = false; }
                if (faceVertexIndices == null) { warningEvent?.Invoke($"Mesh {def.DefIdentifier} is missing faceVertexIndices field", null); bHaveRequiredFields = false; }

                if (bHaveRequiredFields) {
                    DMesh3 Mesh = new DMesh3();
                    AppendAsVertices(Mesh, def, points!, CurTransform);
                    AppendAsTriangles(Mesh, def, faceVertexCounts!, faceVertexIndices!, normals, uvs, CurTransform);
                    Mesh.CheckValidity();
                    builder.AppendNewMesh(Mesh);
                }
            }

            foreach (USDDef childDef in def.ChildDefs ?? [])
                build_meshes(childDef, options, builder, CurTransform);
        }

        protected static Vector3d ToVector3d(vec3f v) { return new Vector3d(v.x, v.y, v.z); }
        protected static Vector3f ToVector3f(vec3f v) { return new Vector3f(v.x, v.y, v.z); }
        protected static Vector2f ToVector2f(vec2f v) { return new Vector2f(v.u, v.v); }


        protected bool AppendAsVertices(DMesh3 mesh, USDDef def, USDField points, Matrix4d Transform)
        {
            if (points.TypeInfo.USDType != EUSDType.Point3f || points.TypeInfo.bIsArray == false ) {
                warningEvent?.Invoke($"Mesh field {def.DefIdentifier}.points has incorrect type {points.TypeInfo.USDType}", null);
                return false;
            }
            if ( points.Value.data is vec3f[] vectorList && vectorList.Length > 0) {
                for (int i = 0; i < vectorList.Length; ++i) {
                    Vector3d v = ToVector3d(vectorList[i]);
                    v = Transform.TransformPointAffine(v);
                    mesh.AppendVertex(v);
                }
                return true;
            } else {
                warningEvent?.Invoke($"Mesh field {def.DefIdentifier}.points data is invalid or 0-length", null);
                return false;
            }
        }


        protected bool AppendAsTriangles(DMesh3 mesh, USDDef def, USDField vertexCounts, USDField vertexIndices,
            USDField? normals, USDField? uvs, Matrix4d Transform)
        {
            if (vertexCounts.TypeInfo.USDType != EUSDType.Int || vertexCounts.TypeInfo.bIsArray == false ) {
                warningEvent?.Invoke($"Mesh field {def.DefIdentifier}.faceVertexCounts has incorrect type {vertexCounts.TypeInfo.USDType}", null);
                return false;
            }
            if (vertexIndices.TypeInfo.USDType != EUSDType.Int || vertexIndices.TypeInfo.bIsArray == false) {
                warningEvent?.Invoke($"Mesh field {def.DefIdentifier}.faceVertexIndices has incorrect type {vertexIndices.TypeInfo.USDType}", null);
                return false;
            }

            int[]? countsList = vertexCounts.Value.data as int[];
            int[]? indexList = vertexIndices.Value.data as int[];
            if (countsList == null || countsList.Length == 0 ||
                indexList == null || indexList.Length == 0) 
            {
                warningEvent?.Invoke($"Mesh field {def.DefIdentifier}.faceVertexCounts or .faceVertexIndices data is invalid or 0-length", null);
                return false;
            }

            Transform.GetAffineNormalTransform(out Matrix3d NormalTransform);
            vec3f[]? normalsList = normals?.Value.data as vec3f[] ?? null;
            bool bHaveNormals = (normalsList != null && normalsList.Length > 0 && normalsList.Length == indexList.Length);
            if (bHaveNormals)
                mesh.Attribs.EnableTriNormals();
            TriNormalsGeoAttribute? NormalsAttrib = (bHaveNormals) ? mesh.Attribs.TriNormals : null;

            vec2f[]? uvList = uvs?.Value.data as vec2f[] ?? null;
            bool bHaveUVs = (uvList != null && uvList.Length > 0 && uvList.Length == indexList.Length);
            if (bHaveUVs)
                mesh.Attribs.EnableTriUVs(1);
            TriUVsGeoAttribute? UVsAttrib = (bHaveUVs) ? mesh.Attribs.TriUVChannel(0) : null;

            int cur_idx = 0;
            for (int i = 0; i < countsList.Length; ++i) {
                int count = countsList[i];

                int start_idx = cur_idx;
                int a = indexList[cur_idx];
                int b = indexList[cur_idx + 1];

                Vector3f na = Vector3f.UnitZ, nb = Vector3f.UnitZ, nc = Vector3f.UnitZ;
                if ( bHaveNormals ) {
                    na = NormalTransform * ToVector3f(normalsList![cur_idx]);
                    nb = NormalTransform * ToVector3f(normalsList[cur_idx+1]);
                }

                Vector2f uva = Vector2f.Zero, uvb = Vector2f.Zero, uvc = Vector2f.Zero;
                if (bHaveUVs) {
                    uva = ToVector2f(uvList![cur_idx]);
                    uvb = ToVector2f(uvList[cur_idx+1]);
                }

                for (int k = 2; k < count; ++k) {
                    int c = indexList[cur_idx + k];
                    int tid = mesh.AppendTriangle(new Index3i(a, b, c), i);
                    if (tid < 0) {
                        string errType = (tid == DMesh3.NonManifoldID) ? "nonmanifold" : "invalid";
                        warningEvent?.Invoke($"triangle ({a},{b},{c}) is {errType} - skipping", null);
                    }
                    b = c;

                    if (tid >= 0 && bHaveNormals) {
                        nc = NormalTransform * ToVector3f(normalsList![cur_idx+k]);
                        NormalsAttrib!.SetValue(tid, new TriNormals(na, nb, nc));
                        nb = nc;
                    }
                    if (tid >= 0 && bHaveUVs) {
                        uvc = ToVector2f(uvList![cur_idx+k]);
                        UVsAttrib!.SetValue(tid, new TriUVs(uva, uvb, uvc));
                        uvb = uvc;
                    }
                }

                cur_idx += count;
            }

            return true;
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


            if (def.header_block.Length > 0 ) {
                extract_fields(def.header_block, out def.HeaderFields);
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
                switch (field.TypeInfo.USDType) {
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
                        field.Value.data = parse_array_vec3f(field.block);
                        break;
                    case EUSDType.Float2:
                    case EUSDType.Half2:
                    case EUSDType.TexCoord2f:
                    case EUSDType.TexCoord2h:
                        field.Value.data = parse_array_vec2f(field.block);
                        break;
                    case EUSDType.Float4:
                    case EUSDType.Half4:
                    case EUSDType.Color4f:
                    case EUSDType.Color4h:
                        field.Value.data = parse_array_vec4f(field.block);
                        break;
                    case EUSDType.Double3:
                    case EUSDType.Vector3d:
                    case EUSDType.Normal3d:
                    case EUSDType.Point3d:
                    case EUSDType.Color3d:
                    case EUSDType.TexCoord3d:
                        field.Value.data = parse_array_vec3d(field.block);
                        break;
                    case EUSDType.Double2:
                    case EUSDType.TexCoord2d:
                        field.Value.data = parse_array_vec2d(field.block);
                        break;
                    case EUSDType.Double4:
                    case EUSDType.Color4d:
                        field.Value.data = parse_array_vec4d(field.block);
                        break;
                    case EUSDType.Quatf:
                    case EUSDType.Quath:
                        field.Value.data = parse_array_quat4f(field.block);
                        break;
                    case EUSDType.Quatd:
                        field.Value.data = parse_array_quat4d(field.block);
                        break;
                    case EUSDType.Matrix2d:
                        field.Value.data = parse_array_matrix<matrix2d>(field.block, parse_matrix2d);
                        break;
                    case EUSDType.Matrix3d:
                        field.Value.data = parse_array_matrix<matrix3d>(field.block, parse_matrix3d);
                        break;
                    case EUSDType.Matrix4d:
                    case EUSDType.Frame4d:
                        field.Value.data = parse_array_matrix<matrix4d>(field.block, parse_matrix4d);
                        break;
                    case EUSDType.Float:
                    case EUSDType.Half:
                        field.Value.data = parse_array_float(field.block);
                        break;
                    case EUSDType.Double:
                    case EUSDType.Timecode:
                        field.Value.data = parse_array_double(field.block);
                        break;
                    case EUSDType.Bool:
                        field.Value.data = parse_array_bool(field.block);
                        break;
                    case EUSDType.UChar:
                        field.Value.data = parse_array_byte(field.block);
                        break;
                    case EUSDType.Int:
                        field.Value.data = parse_array_int(field.block);
                        break;
                    case EUSDType.UInt:
                        field.Value.data = parse_array_uint(field.block);
                        break;
                    case EUSDType.Int64:
                        field.Value.data = parse_array_int64(field.block);
                        break;
                    case EUSDType.UInt64:
                        field.Value.data = parse_array_uint64(field.block);
                        break;
                    case EUSDType.Int2:
                        field.Value.data = parse_array_vec2i(field.block);
                        break;
                    case EUSDType.Int3:
                        field.Value.data = parse_array_vec3i(field.block);
                        break;
                    case EUSDType.Int4:
                        field.Value.data = parse_array_vec4i(field.block);
                        break;
                    case EUSDType.String:
                    case EUSDType.Token:
                    case EUSDType.Asset:
                        field.Value.data = parse_array_string(field.block);
                        break;
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
                    field.Value.data = float.TryParse(field.block, out float f) ? f : null;
                }
                else if ( field.TypeInfo.USDType == EUSDType.Double
                    || field.TypeInfo.USDType == EUSDType.Timecode) 
                {
                    field.Value.data = double.TryParse(field.block, out double f) ? f : null;
                }
                else if ( field.TypeInfo.USDType == EUSDType.Int ) 
                {
                    field.Value.data = int.TryParse(field.block, out int i) ? i : null;
                }
                else if ( field.TypeInfo.USDType == EUSDType.UInt ) 
                {
                    field.Value.data = uint.TryParse(field.block, out uint i) ? i : null;
                }
                else if ( field.TypeInfo.USDType == EUSDType.Int64 ) 
                {
                    field.Value.data = int.TryParse(field.block, out int i) ? i : null;
                }
                else if ( field.TypeInfo.USDType == EUSDType.UInt64 ) 
                {
                    field.Value.data = uint.TryParse(field.block, out uint i) ? i : null;
                }
                else if ( field.TypeInfo.USDType == EUSDType.UChar ) 
                {
                    field.Value.data = byte.TryParse(field.block, out byte i) ? i : null;
                }
                else if ( field.TypeInfo.USDType == EUSDType.Bool ) 
                {
                    field.Value.data = int.TryParse(field.block, out int i) ? (bool)(i!=0) : null;
                } 
                else if ( field.TypeInfo.USDType == EUSDType.Float3 
                    || field.TypeInfo.USDType == EUSDType.Half3
                    || field.TypeInfo.USDType == EUSDType.Point3f
                    || field.TypeInfo.USDType == EUSDType.Point3h
                    || field.TypeInfo.USDType == EUSDType.Color3f
                    || field.TypeInfo.USDType == EUSDType.Color3h
                    || field.TypeInfo.USDType == EUSDType.Vector3f
                    || field.TypeInfo.USDType == EUSDType.Vector3h
                    || field.TypeInfo.USDType == EUSDType.Normal3f
                    || field.TypeInfo.USDType == EUSDType.Normal3h
                    || field.TypeInfo.USDType == EUSDType.TexCoord3f
                    || field.TypeInfo.USDType == EUSDType.TexCoord3h ) 
                {
                    field.Value.data = parse_vec3f(field.block);
                }
                else if ( field.TypeInfo.USDType == EUSDType.Float2
                    || field.TypeInfo.USDType == EUSDType.Half2
                    || field.TypeInfo.USDType == EUSDType.TexCoord2f
                    || field.TypeInfo.USDType == EUSDType.TexCoord2h ) 
                {
                    field.Value.data = parse_vec2f(field.block);
                }
                else if ( field.TypeInfo.USDType == EUSDType.Float4
                    || field.TypeInfo.USDType == EUSDType.Half4
                    || field.TypeInfo.USDType == EUSDType.Color4f 
                    || field.TypeInfo.USDType == EUSDType.Color4h ) 
                {
                    field.Value.data = parse_vec4f(field.block);
                }
                else if ( field.TypeInfo.USDType == EUSDType.Double3
                    || field.TypeInfo.USDType == EUSDType.Vector3d
                    || field.TypeInfo.USDType == EUSDType.Normal3d
                    || field.TypeInfo.USDType == EUSDType.Point3d
                    || field.TypeInfo.USDType == EUSDType.Color3d
                    || field.TypeInfo.USDType == EUSDType.TexCoord3d ) 
                {
                    field.Value.data = parse_vec3d(field.block);
                }
                else if ( field.TypeInfo.USDType == EUSDType.Double2
                    || field.TypeInfo.USDType == EUSDType.TexCoord2d ) 
                {
                    field.Value.data = parse_vec2d(field.block);
                }
                else if ( field.TypeInfo.USDType == EUSDType.Double4
                    || field.TypeInfo.USDType == EUSDType.Color4d ) 
                {
                    field.Value.data = parse_vec4d(field.block);
                }
                else if ( field.TypeInfo.USDType == EUSDType.Quatf
                    || field.TypeInfo.USDType == EUSDType.Quath) 
                {
                    field.Value.data = parse_quat4f(field.block);
                }
                else if ( field.TypeInfo.USDType == EUSDType.Quatd) 
                {
                    field.Value.data = parse_quat4d(field.block);
                }
                else if ( field.TypeInfo.USDType == EUSDType.Matrix2d ) 
                {
                    field.Value.data = parse_matrix2d(field.block);
                }
                else if ( field.TypeInfo.USDType == EUSDType.Matrix3d ) 
                {
                    field.Value.data = parse_matrix3d(field.block);
                }
                else if ( field.TypeInfo.USDType == EUSDType.Matrix4d
                    || field.TypeInfo.USDType == EUSDType.Frame4d ) 
                {
                    field.Value.data = parse_matrix4d(field.block);
                }
                else if ( field.TypeInfo.USDType == EUSDType.Int2) {
                    field.Value.data = parse_vec2i(field.block);
                } else if (field.TypeInfo.USDType == EUSDType.Int3) {
                    field.Value.data = parse_vec3i(field.block);
                } else if (field.TypeInfo.USDType == EUSDType.Int4) {
                    field.Value.data = parse_vec4i(field.block);
                }
            }

            return true;
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
