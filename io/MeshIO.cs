using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace g3
{

    public enum ReadResult
    {
        Ok = 0,
        FileAccessError = 1,
        UnknownFormatError = 2,
        FileParsingError = 3,
        GarbageDataError = 4,
        GenericReaderError = 5,
        GenericReaderWarning = 6
    }

    public class ReadOptions
    {
		public bool ReadMaterials;

        public ReadOptions()
        {
			ReadMaterials = false;
        }
    }

    public struct IOReadResult
    {
        public ReadResult result { get; set; }
        public string info { get; set; }
        public IOReadResult(ReadResult r, string s) { result = r; info = s; if (info == "") info = "(no message)"; }
    }



    public interface IMeshReader
    {
        IOReadResult Read(TextReader reader, ReadOptions options, IMeshBuilder builder);
        IOReadResult Read(BinaryReader reader, ReadOptions options, IMeshBuilder builder);
    }




    public enum WriteResult
    {
        Ok = 0,
        FileAccessError = 1,
        UnknownFormatError = 2,
        WriterError = 3
    }

    public struct IOWriteResult
    {
        public WriteResult result { get; set; }
        public string info { get; set; }
        public IOWriteResult( WriteResult r, string s ) { result = r;  info = s; if (info == "") info = "(no message)"; }
    }

    public class WriteOptions
    {
        //bool bWriteBinary;        // currently unused

        public bool bPerVertexNormals;
        public bool bPerVertexColors;

        public WriteOptions()
        {
            //bWriteBinary = false;

            bPerVertexNormals = false;
            bPerVertexColors = false;
        }
    }

    
    public interface IMeshWriter
    {
        IOWriteResult Write(TextWriter writer, List<IMesh> vMeshes, WriteOptions options);
        IOWriteResult Write(BinaryWriter writer, List<IMesh> vMeshes, WriteOptions options);
    }


}
