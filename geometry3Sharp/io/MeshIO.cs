using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        public ReadOptions()
        {
        }
    }


    public interface IMeshReader
    {
        Tuple<ReadResult, string> Read(TextReader reader, ReadOptions options, IMeshBuilder builder);
        Tuple<ReadResult, string> Read(BinaryReader reader, ReadOptions options, IMeshBuilder builder);
    }




    public enum WriteResult
    {
        Ok = 0,
        FileAccessError = 1,
        UnknownFormatError = 2,
        WriterError = 3
    }


    public class WriteOptions
    {
        bool bWriteBinary;

        public bool bPerVertexNormals;
        public bool bPerVertexColors;

        public WriteOptions()
        {
            bWriteBinary = false;

            bPerVertexNormals = true;
            bPerVertexColors = false;
        }
    }

    
    public interface IMeshWriter
    {
        Tuple<WriteResult, string> Write(TextWriter writer, List<IMesh> vMeshes, WriteOptions options);
        Tuple<WriteResult, string> Write(BinaryWriter writer, List<IMesh> vMeshes, WriteOptions options);
    }


}
