// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Distributed under the Boost Software License, Version 1.0. http://www.boost.org/LICENSE_1_0.txt
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;


namespace g3
{
#nullable enable
    public class GLBReader : IMeshReader
    {
        // https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.html#glb-file-format-specification

        // connect to this to get warning messages
        public event ParsingMessagesHandler? warningEvent;

        public GLTFProcessingOptions ProcessingOptions = default;


        public IOReadResult Read(TextReader reader, ReadOptions options, IMeshBuilder builder)
        {
            throw new NotImplementedException();
        }

        public IOReadResult Read(BinaryReader reader, ReadOptions options, IMeshBuilder builder)
        {
            byte[] headerBytes = reader.ReadBytes(12);
            GLTFFile.GLBHeader header = MemoryMarshal.Cast<byte, GLTFFile.GLBHeader>(headerBytes)[0];
            if (header.magic != GLTFFile.GLBHeader.MagicNumber) {
                return new IOReadResult(IOCode.GarbageDataError, $"GLB header magic number is not {GLTFFile.GLBHeader.MagicNumber} (glTF in ASCII)");
            }
            if (header.version != 2) {
                return new IOReadResult(IOCode.GarbageDataError, $"GLB header version number is not 2");
            }

            uint chunk0Length = reader.ReadUInt32();
            uint chunk0Type = reader.ReadUInt32();
            if (chunk0Type != GLTFFile.GLB_ChunkType_JSON)
                return new IOReadResult(IOCode.GarbageDataError, $"GLB chunk0 type is not {GLTFFile.GLB_ChunkType_JSON} (JSON)");
            byte[] chunk0 = reader.ReadBytes((int)chunk0Length);

            // TODO handle case where chunk1 is undefined??

            uint chunk1Length = reader.ReadUInt32();
            uint chunk1Type = reader.ReadUInt32();
            if (chunk1Type != GLTFFile.GLB_ChunkType_BIN)
                return new IOReadResult(IOCode.GarbageDataError, $"GLB chunk0 type is not {GLTFFile.GLB_ChunkType_BIN} (BIN)");
            byte[] chunk1 = reader.ReadBytes((int)chunk1Length);

            string jsonString = Encoding.UTF8.GetString(chunk0);
            GLTFReader gltfReader = new GLTFReader();
            gltfReader.GLBBinaryBuffer = chunk1;
            gltfReader.warningEvent += this.warningEvent;
            gltfReader.ProcessingOptions = this.ProcessingOptions;

            using (StringReader stringReader = new StringReader(jsonString)) 
            {
                return gltfReader.Read(stringReader, options, builder);
            }
        }




    }
}
