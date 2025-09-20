// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Distributed under the Boost Software License, Version 1.0. http://www.boost.org/LICENSE_1_0.txt
using System;
using System.Runtime.InteropServices;

namespace g3
{
    public static class LZ4Compression
    {
        public static byte[] DecompressLZ4BlockFormat(Span<byte> compressedData, uint UncompressedSize)
        {
            // https://github.com/lz4/lz4/blob/dev/doc/lz4_Block_format.md

            byte[] Uncompressed = new byte[UncompressedSize];
            int out_offset = 0;

            int offset = 0;
            while (offset != compressedData.Length) 
            {
                byte token = compressedData[offset++];
                int tok_literal_length = (token & 0b11110000) >> 4;
                int tok_match_length = (token & 0b00001111);

                // figure out number of literals
                int num_literals = 0;
                if (tok_literal_length < 15) {
                    num_literals = tok_literal_length;
                } else {
                    num_literals = 15;
                    byte next_add_literals = 0;
                    do {
                        next_add_literals = compressedData[offset++];
                        num_literals += next_add_literals;
                    } while (next_add_literals == 255);
                }

                for ( int i = 0; i < num_literals; ++i ) 
                    Uncompressed[out_offset++] = compressedData[offset++];

                // we are done
                if (offset == compressedData.Length) {
                    break;
                }

                Span<byte> matchcopy_offset_bytes = compressedData.Slice(offset, 2); offset += 2;
                int matchcopy_offset = (int)MemoryMarshal.AsRef<ushort>(matchcopy_offset_bytes);
                if (matchcopy_offset == 0)
                    throw new Exception("this is invalid?");

                int matchlength = 0;
                if (tok_match_length < 15) {
                    matchlength = tok_match_length + 4;
                } else {
                    matchlength = 15 + 4;
                    byte next_add_matchlength = 0;
                    do {
                        next_add_matchlength = compressedData[offset++];
                        matchlength += next_add_matchlength;
                    } while (next_add_matchlength == 255);
                }

                int start_copy = out_offset - matchcopy_offset;
                for ( int i = 0; i < matchlength; ++i )
                    Uncompressed[out_offset++] = Uncompressed[start_copy+i];
            }

            return Uncompressed;
        }



        /// <summary>
        /// if uncompressed size is not known, we can decompress into a dynamically-growing buffer.
        /// Implemented using DVector so growing does not involve memory-copies
        /// </summary>
        public static byte[] DecompressLZ4BlockFormat(Span<byte> compressedData)
        {
            // https://github.com/lz4/lz4/blob/dev/doc/lz4_Block_format.md

            DVector<byte> Uncompressed = new();

            int offset = 0;
            while (offset != compressedData.Length) 
            {
                byte token = compressedData[offset++];
                int tok_literal_length = (token & 0b11110000) >> 4;
                int tok_match_length = (token & 0b00001111);

                // figure out number of literals
                int num_literals = 0;
                if (tok_literal_length < 15) {
                    num_literals = tok_literal_length;
                } else {
                    num_literals = 15;
                    byte next_add_literals = 0;
                    do {
                        next_add_literals = compressedData[offset++];
                        num_literals += next_add_literals;
                    } while (next_add_literals == 255);
                }

                for (int i = 0; i < num_literals; ++i)
                    Uncompressed.push_back(compressedData[offset++]);

                // we are done
                if (offset == compressedData.Length) {
                    break;
                }

                Span<byte> matchcopy_offset_bytes = compressedData.Slice(offset, 2); offset += 2;
                int matchcopy_offset = (int)MemoryMarshal.AsRef<ushort>(matchcopy_offset_bytes);
                if (matchcopy_offset == 0)
                    throw new Exception("this is invalid?");

                int matchlength = 0;
                if (tok_match_length < 15) {
                    matchlength = tok_match_length + 4;
                } else {
                    matchlength = 15 + 4;
                    byte next_add_matchlength = 0;
                    do {
                        next_add_matchlength = compressedData[offset++];
                        matchlength += next_add_matchlength;
                    } while (next_add_matchlength == 255);
                }

                int start_copy = Uncompressed.size - matchcopy_offset;
                for ( int i = 0; i < matchlength; ++i )
                    Uncompressed.push_back(Uncompressed[start_copy+i]);
            }

            return Uncompressed.ToArray();
        }



        // not finished yet
        private static void DecompressLZ4FrameFormat(Span<byte> compressedBuffer)
        {
            // https://github.com/lz4/lz4/blob/dev/doc/lz4_Frame_format.md

            uint totalDataSize = (uint)compressedBuffer.Length;

            int offset = 0;
            Span<byte> magicBytes = compressedBuffer.Slice(offset, 4);  offset += 4;
            uint magicNumber = MemoryMarshal.AsRef<uint>(magicBytes);
            if (magicNumber != 0x184D2204)
                throw new Exception("not lz4...");

            byte FLG = compressedBuffer[offset]; offset++;
            int FLG_Version =         (FLG & 0b11000000) >> 6;
            int FLG_BlockIndep =      (FLG & 0b00100000) >> 5;
            int FLG_BlockChecksum =   (FLG & 0b00010000) >> 4;
            int FLG_ContentSize  =    (FLG & 0b00001000) >> 3;
            int FLG_ContentChecksum = (FLG & 0b00000100) >> 2;
            int FLG_DictID =          (FLG & 0b00000001);
            if (FLG_Version != 0b01)
                throw new Exception("lz4 FLG_Version field is not 01");

            byte BD = compressedBuffer[offset]; offset++;
            int BD_MaxBlockSize =     (BD  & 0b01110000) >> 4;

            ulong ContentSize = 0;
            if (FLG_ContentSize > 0) {
                Span<byte> contentSizeBytes = compressedBuffer.Slice(offset, 8); offset += 8;
                ContentSize = MemoryMarshal.AsRef<ulong>(contentSizeBytes);
            }

            uint DictionaryID = 0;
            if (FLG_DictID > 0) {
                Span<byte> dictidBytes = compressedBuffer.Slice(offset, 4); offset += 4;
                DictionaryID = MemoryMarshal.AsRef<uint>(dictidBytes);
            }

            byte HeaderChecksumHC = compressedBuffer[offset]; offset++;
            // todo: check this... using xx32()

            // read stream blocks...
        }



    }

}
