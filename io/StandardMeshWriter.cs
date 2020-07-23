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

        /// <summary>
        /// Converts the given Meshes into a storage (file) format and writes into the given Stream. Does not close the stream in the end.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="vMeshes"></param>
        /// <param name="options"></param>
        /// <param name="format">desired file format</param>
        /// <returns></returns>
        public IOWriteResult Write(Stream stream, List<WriteMesh> vMeshes, WriteOptions options, MeshFileFormats format)
        {
            Func<Stream, List<WriteMesh>, WriteOptions, IOWriteResult> writeFunc = null;

            switch (format)
            {
                case MeshFileFormats.Obj: writeFunc = Write_OBJ; break;
                case MeshFileFormats.Stl: writeFunc = Write_STL; break;
                case MeshFileFormats.Off: writeFunc = Write_OFF; break;
                case MeshFileFormats.G3mesh: writeFunc = Write_G3Mesh; break;
                default: return new IOWriteResult(IOCode.UnknownFormatError, $"format {format} is not supported");
            }

            // save current culture
            var current_culture = Thread.CurrentThread.CurrentCulture;

            try
            {
                // push invariant culture for write
                if (WriteInvariantCulture)
                    Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

                var result = writeFunc(stream, vMeshes, options);

                // restore culture
                if (WriteInvariantCulture)
                    Thread.CurrentThread.CurrentCulture = current_culture;

                return result;

            }
            catch (Exception e)
            {
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

        IOWriteResult Write_OBJ(Stream stream, List<WriteMesh> vMeshes, WriteOptions options)
        {
            StreamWriter w = new StreamWriter(stream); // dont dispose the StreamWriter explicitly: it will dispose underlying Stream
            OBJWriter writer = new OBJWriter()
            {
                OpenStreamF = (fn) => { throw new NotSupportedException("File operations are not supported in stream mode"); },
                CloseStreamF = (s) => { },  // do nothing, because the stream should be managed outside, by its creator
            };
            var result = writer.Write(w, vMeshes, options);
            w.Flush();
            return result;
        }

        IOWriteResult Write_OFF(string sFilename, List<WriteMesh> vMeshes, WriteOptions options)
        {
            Stream stream = OpenStreamF(sFilename);
            if (stream == null)
                return new IOWriteResult(IOCode.FileAccessError, "Could not open file " + sFilename + " for writing");

            try {
                return Write_OFF(stream, vMeshes, options);
            } finally {
                CloseStreamF(stream);
            }
        }

        IOWriteResult Write_OFF(Stream stream, List<WriteMesh> vMeshes, WriteOptions options)
        {
            StreamWriter w = new StreamWriter(stream); // dont dispose the StreamWriter explicitly: it will dispose underlying Stream
            OFFWriter writer = new OFFWriter();
            var result = writer.Write(w, vMeshes, options);
            w.Flush();
            return result;
        }

        IOWriteResult Write_STL(string sFilename, List<WriteMesh> vMeshes, WriteOptions options)
        {
            Stream stream = OpenStreamF(sFilename);
            if (stream == null)
                return new IOWriteResult(IOCode.FileAccessError, "Could not open file " + sFilename + " for writing");

            try {
                return Write_STL(stream, vMeshes, options);
            } finally {
                CloseStreamF(stream);
            }
        }

        IOWriteResult Write_STL(Stream stream, List<WriteMesh> vMeshes, WriteOptions options)
        {
            if (options.bWriteBinary)
            {
                BinaryWriter w = new BinaryWriter(stream); // dont dispose the BinaryWriter explicitly: it will dispose underlying Stream
                STLWriter writer = new STLWriter();
                var result = writer.Write(w, vMeshes, options);
                w.Flush();
                return result;
            }
            else
            {
                StreamWriter w = new StreamWriter(stream); // dont dispose the StreamWriter explicitly: it will dispose underlying Stream
                STLWriter writer = new STLWriter();
                var result = writer.Write(w, vMeshes, options);
                w.Flush();
                return result;
            }
        }

        IOWriteResult Write_G3Mesh(string sFilename, List<WriteMesh> vMeshes, WriteOptions options)
        {
            Stream stream = OpenStreamF(sFilename);
            if (stream == null)
                return new IOWriteResult(IOCode.FileAccessError, "Could not open file " + sFilename + " for writing");

            try {
                return Write_G3Mesh(stream, vMeshes, options);
            } finally {
                CloseStreamF(stream);
            }
        }

        IOWriteResult Write_G3Mesh(Stream stream, List<WriteMesh> vMeshes, WriteOptions options)
        {
            BinaryWriter w = new BinaryWriter(stream); // dont dispose the BinaryWriter explicitly: it will dispose underlying Stream
            BinaryG3Writer writer = new BinaryG3Writer();
            var result = writer.Write(w, vMeshes, options);
            w.Flush();
            return result;
        }


        public enum MeshFileFormats
        {
            Obj = 1,
            Stl = 2,
            Off = 4,
            G3mesh = 8
        }
    }
}
