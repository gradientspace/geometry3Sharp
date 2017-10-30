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
        InvalidFilenameError = 4,

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

        // format readers will inevitably have their own settings, we
        // can use this to pass arguments to them
        public CommandArgumentSet CustomFlags = new CommandArgumentSet();

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
        public IOReadResult(IOCode r, string s) : this()
        {
            code = r;
            message = s;
            if (message == "")
                message = "(no message)";
        }

		public static readonly IOReadResult Ok = new IOReadResult(IOCode.Ok, "");	
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
        public IOWriteResult( IOCode r, string s ) : this()
        {
            code = r;
            message = s;
            if (message == "")
                message = "(no message)";
        }

		public static readonly IOWriteResult Ok = new IOWriteResult(IOCode.Ok, "");
    }

    public struct WriteOptions
    {
		public bool bWriteBinary;        	// write binary format if supported (STL)

		public bool bPerVertexNormals;		// write per-vertex normals (OBJ)
		public bool bPerVertexColors;		// write per-vertex colors (OBJ)
		public bool bPerVertexUVs;			// write per-vertex UVs
											// can be overridden by per-mesh UVs in WriteMesh
		public bool bWriteGroups;			// write face groups (OBJ)

        public bool bCombineMeshes;     	// combine all input meshes into a single output mesh
											// some STL readers do not handle multiple solids...

		public int RealPrecisionDigits;		// number of digits of float precision (after decimal)

		public bool bWriteMaterials;		// for OBJ, indicates that .mtl file should be written
		public string MaterialFilePath;		// only used if bWriteMaterialFile = true

        public string groupNamePrefix;        // prefix for group names in OBJ files (default is "mmGroup")
        public Func<int, string> GroupNameF;  // if non-null, you can use this to generate your own group names


        public Action<int, int> ProgressFunc;	// progress monitoring callback

        public Func<string> AsciiHeaderFunc;    // if you define this, returned string will be written as header start of ascii formats

        public static readonly WriteOptions Defaults = new WriteOptions() {
            bWriteBinary = false,
            bPerVertexNormals = false,
            bPerVertexColors = false,
            bWriteGroups = false,
            bPerVertexUVs = false,
            bCombineMeshes = false,
			bWriteMaterials = false,
            ProgressFunc = null,

            RealPrecisionDigits = 15       // double
            //RealPrecisionDigits = 7        // float
        };
    }


    public struct WriteMesh
    {
        public IMesh Mesh;
        public string Name;         // supported by some formats

		public List<GenericMaterial> Materials;		// set of materials (possibly) used in this mesh
		public IIndexMap TriToMaterialMap;			// triangle index -> Materials list index

		public DenseUVMesh UVs;  // separate UV layer (just one for now)
								 // assumption is that # of triangles in this UV mesh is same as in Mesh

        public WriteMesh(IMesh mesh, string name = "") {
            Mesh = mesh;
            Name = name;
            UVs = null;
			Materials = null;
			TriToMaterialMap = null;
        }
    }

    
    public interface IMeshWriter
    {
        IOWriteResult Write(TextWriter writer, List<WriteMesh> vMeshes, WriteOptions options);
        IOWriteResult Write(BinaryWriter writer, List<WriteMesh> vMeshes, WriteOptions options);
    }


}
