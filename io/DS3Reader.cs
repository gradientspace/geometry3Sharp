using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace g3
{
    //
    // Parse 3DS mesh format
    // https://en.wikipedia.org/wiki/.3ds
    //
    // and https://web.archive.org/web/20090404091233/http://www.jalix.org/ressources/graphics/3DS/_unofficials/3ds-info.txt 
    //
    // using this method
    // https://social.msdn.microsoft.com/Forums/vstudio/en-US/db4945bb-c244-42df-832f-5d9fe37d0175/read-contents-of-a-binary-file-with-extension-3ds?forum=csharpgeneralhttps://en.wikipedia.org/wiki/OFF_(file_format)
    //
    // Currently - .3ds files can only be read - not written - and this driver does not take any account of the material.
    class DS3Reader : IMeshReader
    {
        // connect to this to get warning messages
		public event ParsingMessagesHandler warningEvent;

        //int nWarningLevel = 0;      // 0 == no diagnostics, 1 == basic, 2 == crazy
        Dictionary<string, int> warningCount = new Dictionary<string, int>();

        private String MeshName; // current object name - temporary store
        private bool hasMesh; // used to show that at least one mesh has been found
        private bool is3ds; // usd to shwo that the 4D4D magic number has been found


        public IOReadResult Read(BinaryReader reader, ReadOptions options, IMeshBuilder builder)
        {
            ushort ChunkID;
            String ChnkID = "";
            UInt32 Clength;
            MeshName = "";
            hasMesh = false;
            is3ds = false;

            // Process the file - fails very politely when there is no more data
            while (true) {

                //Get the Id of the next Chunk
                try {
                    ChunkID = reader.ReadUInt16();
                } catch {
                    break;
                }
                ChnkID = ChunkID.ToString("X");

                //Get the size of the next chunk in chars
                Clength = reader.ReadUInt32();

                //Process based on Chunk ID
                switch (ChnkID) {
                    case "4D4D":
                        //This is a new file header
                        is3ds = true;
                        reader.ReadChars(10);
                        break;
                    case "3D3D":
                        //This is a new Object Header
                        reader.ReadChars(10);
                        break;
                    case "4000":
                        //This is an object Block. Store the name temporarily in case it is a mesh
                        List<char> name = new List<char>();
                        while (true) {
                            char next = reader.ReadChar();
                            if (next == 0) {
                                break;
                            }
                            name.Add(next);
                        }
                        MeshName = new String(name.ToArray<char>());
                        break;

                    case "4100":
                        // This is a new Mesh. Retrieve the name and add if the builder supports Metadata
                        builder.AppendNewMesh(false, false, false, false);
                        if (builder.SupportsMetaData)
                        {
                            builder.AppendMetaData("name", MeshName);
                        }
                        break;

                    case "4110":
                        // List of Vertexes
                        ushort VertexCount = reader.ReadUInt16();
                        for (int x = 0; x < VertexCount; x++) {
                            double X = reader.ReadSingle();
                            double Y = reader.ReadSingle();
                            double Z = reader.ReadSingle();
                            builder.AppendVertex(X, Y, Z);
                        }
                        break;

                    case "4120":
                        // List of Triangles
                        ushort PolygonCount = reader.ReadUInt16();
                        for (int j = 0; j < PolygonCount; j++) {

                            int a = reader.ReadInt16();
                            int b = reader.ReadInt16();
                            int c = reader.ReadInt16();
                            int flags = reader.ReadUInt16();
                            builder.AppendTriangle(a, b, c);
                        }
                        break;
                    case "4130":
                        // Mapping from Vertex to Material - retrieved but not currently used
                        List<char> mname = new List<char>();
                        while (true) {
                            char next = reader.ReadChar();
                            if (next == 0) {
                                break;
                            }
                            mname.Add(next);
                        }
                        string MatName = new String(mname.ToArray<char>());
                        ushort entries = reader.ReadUInt16();
                        for (int i = 0; i < entries; i++) {
                            ushort face = reader.ReadUInt16();
                        }
                        break;

                    case "4140":
                        // List of UVs per vertex
                        ushort uvCount = reader.ReadUInt16();
                        for (ushort y = 0; y < uvCount; y++) {
                            Vector2f UV = new Vector2f(reader.ReadSingle(), reader.ReadSingle());
                            builder.SetVertexUV(y, UV);
                        }
                        break;
                    default:
                        // Any other chunk - retrieved and not used - held in dump temporarily for debug
                        char[] dump = reader.ReadChars((int)Clength - 6);
                        break;
                }
            }
            if (!is3ds)
            {
                return new IOReadResult(IOCode.FileAccessError, "File is not in .3DS format");
            } 
            else if (!hasMesh)
            {
                return new IOReadResult(IOCode.FileParsingError, "no mesh found in file");
            }
            else
            {
                return new IOReadResult(IOCode.Ok, "");
            }
        }

        public IOReadResult Read(TextReader reader, ReadOptions options, IMeshBuilder builder) {
            return new IOReadResult(IOCode.FormatNotSupportedError, "text read not supported for 3DS format");
        }

        private void emit_warning(string sMessage)
        {
            string sPrefix = sMessage.Substring(0, 15);
            int nCount = warningCount.ContainsKey(sPrefix) ? warningCount[sPrefix] : 0;
            nCount++; warningCount[sPrefix] = nCount;
            if (nCount > 10)
                return;
            else if (nCount == 10)
                sMessage += " (additional message surpressed)";

            var e = warningEvent;
            if (e != null)
                e(sMessage, null);
        }

    }
}
