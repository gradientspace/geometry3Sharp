using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Globalization;

namespace g3
{
    public class StandardMeshWriter : IDisposable
    {
        /// <summary>
        /// If the mesh format we are writing is text, then the OS will write in the number style
        /// of the current language. So in Germany, numbers are written 1,00 instead of 1.00, for example.
        /// If this flag is true, we override this to always write in a consistent way.
        /// </summary>
        public bool WriteInvariantCulture = true;


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
            StreamWriter w = new StreamWriter(sFilename);
            if (w.BaseStream == null)
                return new IOWriteResult(IOCode.FileAccessError, "Could not open file " + sFilename + " for writing");

            OBJWriter writer = new OBJWriter();
            var result = writer.Write(w, vMeshes, options);
            w.Close();
            return result;
        }


        IOWriteResult Write_OFF(string sFilename, List<WriteMesh> vMeshes, WriteOptions options)
        {
            StreamWriter w = new StreamWriter(sFilename);
            if (w.BaseStream == null)
                return new IOWriteResult(IOCode.FileAccessError, "Could not open file " + sFilename + " for writing");

            OFFWriter writer = new OFFWriter();
            var result = writer.Write(w, vMeshes, options);
            w.Close();
            return result;
        }


        IOWriteResult Write_STL(string sFilename, List<WriteMesh> vMeshes, WriteOptions options)
        {
            if (options.bWriteBinary) {
                FileStream file_stream = File.Open(sFilename, FileMode.Create);
                BinaryWriter w = new BinaryWriter(file_stream);
                if (w.BaseStream == null)
                    return new IOWriteResult(IOCode.FileAccessError, "Could not open file " + sFilename + " for writing");
                STLWriter writer = new STLWriter();
                var result = writer.Write(w, vMeshes, options);
                w.Close();
                return result;

            } else {
                StreamWriter w = new StreamWriter(sFilename);
                if (w.BaseStream == null)
                    return new IOWriteResult(IOCode.FileAccessError, "Could not open file " + sFilename + " for writing");
                STLWriter writer = new STLWriter();
                var result = writer.Write(w, vMeshes, options);
                w.Close();
                return result;
            }
        }


        IOWriteResult Write_G3Mesh(string sFilename, List<WriteMesh> vMeshes, WriteOptions options)
        {
            FileStream file_stream = File.Open(sFilename, FileMode.Create);
            BinaryWriter w = new BinaryWriter(file_stream);
            if (w.BaseStream == null)
                return new IOWriteResult(IOCode.FileAccessError, "Could not open file " + sFilename + " for writing");
            BinaryG3Writer writer = new BinaryG3Writer();
            var result = writer.Write(w, vMeshes, options);
            w.Close();
            return result;
        }



    }


}
