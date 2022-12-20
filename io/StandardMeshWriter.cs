using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Globalization;

namespace g3
{
    /// <summary>
    /// Writes various mesh file formats. Format is determined from extension. Currently supports:
    ///   * .obj : Wavefront OBJ Format https://en.wikipedia.org/wiki/Wavefront_.obj_file
    ///   * .stl : ascii and binary STL formats https://en.wikipedia.org/wiki/STL_(file_format) 
    ///   * .off : OFF format https://en.wikipedia.org/wiki/OFF_(file_format)
    ///   * .g3mesh : internal binary format for packed DMesh3 objects
    ///   
    /// Each of these is implemented in a separate Writer class, eg OBJWriter, STLWriter, etc
    /// 
    /// </summary>
    public class StandardMeshWriter : IDisposable
    {

        /// <summary>
        /// If the mesh format we are writing is text, then the OS will write in the number style
        /// of the current language. So in Germany, numbers are written 1,00 instead of 1.00, for example.
        /// If this flag is true, we override this to always write in a consistent way.
        /// </summary>
        public bool WriteInvariantCulture = true;



        /// <summary>
        /// By default we write to files, but if you would like to write to some other
        /// Stream type (eg MemoryStream), you can replace this function.  
        /// We also pass this function down into the XYZWriter classes
        /// that need to write additional files (eg OBJ mesh)
        /// </summary>
        public Func<string, Stream> OpenStreamF = (sFilename) => {
            return File.Open(sFilename, FileMode.Create);
        };

        /// <summary>
        /// called on Streams returned by OpenStreamF when we are done with them.
        /// </summary>
        public Action<Stream> CloseStreamF = (stream) => {
            stream.Close();
            stream.Dispose();
        };




        public void Dispose()
        {
        }


        static public IOWriteResult WriteMeshes(string sFilename, List<DMesh3> vMeshes, WriteOptions options)
        {
            List<WriteMesh> meshes = new List<g3.WriteMesh>();
            foreach (var m in vMeshes)
                meshes.Add(new WriteMesh(m));
            StandardMeshWriter writer = new StandardMeshWriter();
            return writer.Write(sFilename, meshes, options);
        }
        static public IOWriteResult WriteFile(string sFilename, List<WriteMesh> vMeshes, WriteOptions options)
        {
            StandardMeshWriter writer = new StandardMeshWriter();
            return writer.Write(sFilename, vMeshes, options);
        }
        static public IOWriteResult WriteMesh(string sFilename, IMesh mesh, WriteOptions options)
        {
            StandardMeshWriter writer = new StandardMeshWriter();
            return writer.Write(sFilename, new List<WriteMesh>() { new WriteMesh(mesh) }, options);
        }


        public IOWriteResult Write(string sFilename, List<WriteMesh> vMeshes, WriteOptions options)
        {
            Func<string, List<WriteMesh>, WriteOptions, IOWriteResult> writeFunc = null;

            string sExtension = Path.GetExtension(sFilename);
            if (sExtension.Equals(".obj", StringComparison.OrdinalIgnoreCase))
                writeFunc = Write_OBJ;
            else if (sExtension.Equals(".stl", StringComparison.OrdinalIgnoreCase))
                writeFunc = Write_STL;
            else if (sExtension.Equals(".off", StringComparison.OrdinalIgnoreCase))
                writeFunc = Write_OFF;
            else if (sExtension.Equals(".g3mesh", StringComparison.OrdinalIgnoreCase))
                writeFunc = Write_G3Mesh;

            if (writeFunc == null)
                return new IOWriteResult(IOCode.UnknownFormatError, "format " + sExtension + " is not supported");

            // save current culture
            var current_culture = Thread.CurrentThread.CurrentCulture;

            try {
                // push invariant culture for write
                if (WriteInvariantCulture)
                    Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

                var result = writeFunc(sFilename, vMeshes, options);

                // restore culture
                if (WriteInvariantCulture)
                    Thread.CurrentThread.CurrentCulture = current_culture;

                return result;

            } catch (Exception e) {
                // restore culture
                if (WriteInvariantCulture)
                    Thread.CurrentThread.CurrentCulture = current_culture;

                return new IOWriteResult(IOCode.WriterError, "Unknown error : exception : " + e.Message);
            }
        }




        IOWriteResult Write_OBJ(string sFilename, List<WriteMesh> vMeshes, WriteOptions options)
        {
            Stream stream = OpenStreamF(sFilename);
            if ( stream == null )
                return new IOWriteResult(IOCode.FileAccessError, "Could not open file " + sFilename + " for writing");

            try {
                StreamWriter w = new StreamWriter(stream);
                OBJWriter writer = new OBJWriter() {
                    OpenStreamF = this.OpenStreamF,
                    CloseStreamF = this.CloseStreamF
                };
                var result = writer.Write(w, vMeshes, options);
                w.Flush();
                return result;
            } finally {
                CloseStreamF(stream);
            }
        }


        IOWriteResult Write_OFF(string sFilename, List<WriteMesh> vMeshes, WriteOptions options)
        {
            Stream stream = OpenStreamF(sFilename);
            if (stream == null)
                return new IOWriteResult(IOCode.FileAccessError, "Could not open file " + sFilename + " for writing");

            try {
                StreamWriter w = new StreamWriter(stream);
                OFFWriter writer = new OFFWriter();
                var result = writer.Write(w, vMeshes, options);
                w.Flush();
                return result;
            } finally {
                CloseStreamF(stream);
            }
        }


        IOWriteResult Write_STL(string sFilename, List<WriteMesh> vMeshes, WriteOptions options)
        {
            Stream stream = OpenStreamF(sFilename);
            if (stream == null)
                return new IOWriteResult(IOCode.FileAccessError, "Could not open file " + sFilename + " for writing");

            try { 
                if (options.bWriteBinary) {
                    BinaryWriter w = new BinaryWriter(stream);
                    STLWriter writer = new STLWriter();
                    var result = writer.Write(w, vMeshes, options);
                    w.Flush();
                    return result;
                } else {
                    StreamWriter w = new StreamWriter(stream);
                    STLWriter writer = new STLWriter();
                    var result = writer.Write(w, vMeshes, options);
                    w.Flush();
                    return result;
                }
            } finally {
                CloseStreamF(stream);
            }
        }


        IOWriteResult Write_G3Mesh(string sFilename, List<WriteMesh> vMeshes, WriteOptions options)
        {
            Stream stream = OpenStreamF(sFilename);
            if (stream == null)
                return new IOWriteResult(IOCode.FileAccessError, "Could not open file " + sFilename + " for writing");

            try {
                BinaryWriter w = new BinaryWriter(stream);
                BinaryG3Writer writer = new BinaryG3Writer();
                var result = writer.Write(w, vMeshes, options);
                w.Flush();
                return result;
            } finally {
                CloseStreamF(stream);
            }
        }



    }


}
