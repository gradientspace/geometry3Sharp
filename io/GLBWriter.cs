// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Distributed under the Boost Software License, Version 1.0. http://www.boost.org/LICENSE_1_0.txt
using System;
using System.Collections.Generic;
using System.IO;

#nullable enable

namespace g3
{

    public class GLBWriter : IMeshWriter
    {
        public IOWriteResult Write(BinaryWriter writer, List<WriteMesh> vMeshes, WriteOptions options)
        {
            List<MemoryStream> BinaryStreams = new();

            GLTFWriter gltfWriter = new GLTFWriter();
            gltfWriter.bWritingGLB = true;
            gltfWriter.OpenStreamF = (sFilename) => {
                MemoryStream memStream = new MemoryStream();
                BinaryStreams.Add(memStream);
                return memStream;
            };
            gltfWriter.CloseStreamF = (stream) => {
                //
            };

            MemoryStream jsonStream = new MemoryStream();
            using (StreamWriter jsonWriter = new StreamWriter(jsonStream)) {
                gltfWriter.Write(jsonWriter, vMeshes, options);
            }

            if (BinaryStreams.Count != 1)
                return new IOWriteResult(IOCode.WriterError, $"GLTFWriter did not write binary chunk data");

            byte[] jsonBytes = jsonStream.ToArray();
            uint jsonLength = (uint)jsonBytes.Length;
            uint jsonPadLength = jsonLength;
            while (jsonPadLength % 4 != 0)
                jsonPadLength++;

            MemoryStream dataStream = BinaryStreams[0];
            byte[] dataBytes = dataStream.ToArray();
            uint dataLength = (uint)dataBytes.Length;
            uint dataPadLength = dataLength;
            while (dataPadLength % 4 != 0) 
                dataPadLength++;

            uint TotalLength = jsonPadLength + dataPadLength + sizeof(uint)*(3+2+2); // file header is 3 bytes, chunk headers are 2 each

            // write header
            writer.Write(GLTFFile.GLBHeader.MagicNumber);
            writer.Write((uint)2);      // version
            writer.Write(TotalLength);

            // write json chunk0
            writer.Write(jsonPadLength);
            writer.Write(GLTFFile.GLB_ChunkType_JSON);
            writer.Write(jsonBytes);
            while (jsonLength++ != jsonPadLength)
                writer.Write((byte)' ');

            // write data chunk1
            writer.Write(dataPadLength);
            writer.Write(GLTFFile.GLB_ChunkType_BIN);
            writer.Write(dataBytes);
            while (dataLength++ != dataPadLength)
                writer.Write((byte)0);

            return IOWriteResult.Ok;
        }


        public IOWriteResult Write(TextWriter writer, List<WriteMesh> vMeshes, WriteOptions options)
        {
            throw new NotImplementedException();
        }

    }
}