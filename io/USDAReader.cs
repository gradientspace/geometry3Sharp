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
using static g3.USDFile;

#nullable enable

namespace g3
{
    public partial class USDAReader
    {
        // connect to this to get warning messages
        public event ParsingMessagesHandler? warningEvent;

        public IOReadResult Read(TextReader reader, ReadOptions options, out USDScene Scene)
        {
            // default empty scene in case we get an error
            Scene = new USDScene() { Root = new USDPrim() { PrimType = EDefType.PsuedoRoot } };

            // note: possible that USDA is guaranteed to be line-based? this could change some things...

            string? firstline = reader.ReadLine();
            if (firstline == null || firstline.StartsWith("#usda", StringComparison.OrdinalIgnoreCase) == false)
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
            List<USDAField>? HeaderFields = null;
            if (alltext[first_char_idx] == '(') {
                bool bHeaderOK = extract_block(alltext, 0, '(', ')',
                    out string header_prefix, out string header_block, out next_idx);
                bool bHeaderFieldsOK = extract_fields(header_block, out HeaderFields);
            }

            List<USDADef>? topLevelDefs = null;
            bool bTopLevelDefsOK = extract_defs(alltext, next_idx, out topLevelDefs);

            for (int i = 0; i < topLevelDefs.Count; ++i) {
                USDADef cur_def = topLevelDefs[i];
                parse_def(cur_def);
            }

            USDPrim Root = new USDPrim() { PrimType = EDefType.PsuedoRoot };
            append_fields(Root, HeaderFields ?? []);

            for ( int i = 0; i < topLevelDefs.Count; ++i ) {
                USDPrim newChild = build_child_prim(topLevelDefs[i], Root.Path);
                Root.Children.Add(newChild);
            }

            Scene = new USDScene() { Root = Root };

            // could clear internal data structures here to save memory

            return IOReadResult.Ok;
        }


        USDPrim build_child_prim(USDADef def, USDPath parentPath)
        {
            USDPrim prim = new USDPrim();
            prim.Path = USDPath.CombineElement(parentPath, def.DefIdentifier);
            prim.PrimType = def.DefType;
            append_fields(prim, enumerate_fields(def));
            
            int NumChildren = (def.ChildDefs != null) ? def.ChildDefs.Count : 0;
            for ( int i = 0; i < NumChildren; ++i ) {
                USDPrim newChild = build_child_prim(def.ChildDefs![i], prim.Path);
                prim.Children.Add(newChild);
            }

            return prim;
        }

        void append_fields(USDPrim prim, IEnumerable<USDAField> fields)
        {
            int Count = fields.Count();
            foreach (USDAField field in fields) {
                USDAttrib attrib = new USDAttrib();
                attrib.Name = field.Name;
                attrib.Value = field.Value;
                prim.AddAttrib(attrib);
            }
        }

        IEnumerable<USDAField> enumerate_fields(USDADef def)
        {
            foreach (USDAField field in def.ContentFields ?? [])
                yield return field;
            foreach (USDAField field in def.HeaderFields ?? [])
                yield return field;
        }

        protected void debug_print(string indent, USDADef def)
        {
            Debug.WriteLine(indent + "DEF " + def.DefType.ToString() + " " + def.DefIdentifier);
            string child_indent = indent + " ";

            foreach (USDAField field in def.ContentFields ?? []) {
                string valString = field.Value.ToString();
                Debug.WriteLine(child_indent + "FIELD [" + field.TypeInfo.ToString() + "] [" + field.Name + "] = [ " + valString + " ]");
            }

            foreach (USDADef childDef in def.ChildDefs ?? [])
                debug_print(child_indent, childDef);
        }



        // Fields are structured like [type = value], or just [type] in some cases.
        // Called properties in USD docs
        protected class USDAField
        {
            // original '(prefix) = (value)' string text
            public string prefix = "";
            public string block = "";

            // parsed data
            public string Name = "";
            public USDTypeInfo TypeInfo;
            public USDValue Value = new();

            public List<USDAField>? Subfields = null;

            public override string ToString() {
                return $"|{prefix}| = |{block}|";
            }
        }

        // USDADef represents a "Prim" in USD file
        protected class USDADef
        {
            // original 'def prefix(header_block) { content_block }' string text
            public string prefix = "";
            public string header_block = "";
            public string content_block = "";

            // parsed data
            public EDefType DefType = EDefType.Unknown;
            public string? CustomDefType = null;
            public string DefIdentifier = "";

            public List<USDADef>? ChildDefs = null;

            public List<USDAField>? HeaderFields = null;
            public List<USDAField>? ContentFields = null;
        }




        protected bool parse_def(USDADef def)
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
                    if ( extract_defs( subblock!, 0, out List<USDADef> childDefs ) ) {
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
                else if (string.Compare(start_token, "over", true) == 0) 
                {
                    warningEvent?.Invoke("ignoring over block", null);
                }
                else if (string.Compare(start_token, "class", true) == 0) 
                {
                    warningEvent?.Invoke("ignoring class block", null);
                }
                else {
                    warningEvent?.Invoke($"unknown def subblock with token {start_token}", null);
                }

            }

            // extract child defs
            //extract_defs(def.content_block, start_of_subdefs, out def.ChildDefs);

            // recursively parse each child def
            if (def.ChildDefs != null) {
                foreach (USDADef childDef in def.ChildDefs)
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


        protected bool extract_defs(string buffer, int start_index, out List<USDADef> defs)
        {
            defs = new List<USDADef>();

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

                USDADef def = new USDADef();
                def.prefix = def_prefix;
                def.header_block = def_header_block;
                def.content_block = def_content_block;
                defs.Add(def);
            }

            return (defs.Count > 0);
        }





        protected bool extract_fields(string block, out List<USDAField> fields)
        {
            // assume each field is on it's own line?
            // note this was perhaps a dumb decision...complicates a lot of nested-scope handling...revisit?
            string[] lines = block.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return extract_fields(lines, out fields);
        }

        protected bool extract_fields(Span<string> lines, out List<USDAField> fields)
        {
            fields = new List<USDAField>();

            int line_idx = 0;
            while (line_idx != lines.Length) {

                if (lines[line_idx].StartsWith('#')) {
                    line_idx++;
                    continue;
                }

                USDAField field = new USDAField();

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

        void process_field_options(USDAField field, Span<string> lines, ref int line_idx) 
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



        protected bool parse_field(USDAField field)
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

            parse_field_value(field);

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
