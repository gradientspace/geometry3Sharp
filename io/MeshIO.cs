using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace g3
{

    public enum IOCode
    {
        Ok = 0,

        FileAccessError = 1,
        UnknownFormatError = 2,
        FormatNotSupportedError = 3,

        // read errors
        FileParsingError = 100,
        GarbageDataError = 101,
        GenericReaderError = 102,
        GenericReaderWarning = 103,

        // write errors
        WriterError = 200,


        // other status
        ComputingInWorkerThread = 1000
    }

    public class ReadOptions
    {
		public bool ReadMaterials;

        public ReadOptions()
        {
			ReadMaterials = false;
        }

        public static readonly ReadOptions Defaults = new ReadOptions() {
            ReadMaterials = false
        };
    }

    public struct IOReadResult
    {
        public IOCode code { get; set; }
        public string message { get; set; }
        public IOReadResult(IOCode r, string s) { code = r; message = s; if (message == "") message = "(no message)"; }
    }



    public interface IMeshReader
    {
        IOReadResult Read(TextReader reader, ReadOptions options, IMeshBuilder builder);
        IOReadResult Read(BinaryReader reader, ReadOptions options, IMeshBuilder builder);
    }



    public struct IOWriteResult
    {
        public IOCode code { get; set; }
        public string message { get; set; }
        public IOWriteResult( IOCode r, string s ) { code = r;  message = s; if (message == "") message = "(no message)"; }
    }

    public struct WriteOptions
    {
        public bool bWriteBinary;        

        public bool bPerVertexNormals;
        public bool bPerVertexColors;
        public bool bWriteGroups;
        public bool bWriteUVs;

        public bool bCombineMeshes;     // some STL readers do not handle multiple solids...

        public int RealPrecisionDigits;

        public Action<int, int> ProgressFunc;

        public Func<string> AsciiHeaderFunc;    // if you define this, returned string will be written as header start of ascii formats

        public static readonly WriteOptions Defaults = new WriteOptions() {
            bWriteBinary = false,
            bPerVertexNormals = false,
            bPerVertexColors = false,
            bWriteGroups = false,
            bWriteUVs = false,
            bCombineMeshes = false,
            ProgressFunc = null,

            RealPrecisionDigits = 15       // double
            //RealPrecisionDigits = 7        // float
        };
    }


    public struct WriteMesh
    {
        public IMesh Mesh;
        public string Name;         // supported by some formats

        public DenseMeshUVSet UVs;  // for supporting a separate UV layer (just one for now?)

        public WriteMesh(IMesh mesh, string name = "") {
            Mesh = mesh;
            Name = name;
            UVs = null;
        }
    }

    
    public interface IMeshWriter
    {
        IOWriteResult Write(TextWriter writer, List<WriteMesh> vMeshes, WriteOptions options);
        IOWriteResult Write(BinaryWriter writer, List<WriteMesh> vMeshes, WriteOptions options);
    }


}
