using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace g3
{
    public class StandardMeshWriter : IDisposable
    {
        public void Dispose()
        {
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

            try {
                return writeFunc(sFilename, vMeshes, options);
            } catch (Exception e) {
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
