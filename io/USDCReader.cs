using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;
using System.Threading.Tasks;

namespace g3
{
    public class USDCReader : IMeshReader
    {
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

            USDC_TableOfContents toc = new();
            for (ulong i = 0; i < Count; ++i ) 
            {
                Span<byte> sectionBytes = reader.ReadBytes(section_size);
                USDC_Section section = MemoryMarshal.AsRef<USDC_Section>(sectionBytes);
                toc.Sections.Add(section);
            }

            return new IOReadResult(IOCode.FileParsingError, $"boo");

        }




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




        protected static string read_usd_string(BinaryReader reader, int length)
        {
            byte[] bytes = reader.ReadBytes(length);
            return Encoding.ASCII.GetString(bytes);
        }
        protected static string read_usd_string(ReadOnlySpan<byte> bytes)
        {
            return Encoding.ASCII.GetString(bytes);
        }

    }
}
