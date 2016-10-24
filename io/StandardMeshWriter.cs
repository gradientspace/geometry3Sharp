using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace g3
{
    public class StandardMeshWriter
    {

        static public IOWriteResult WriteFile(string sFilename, List<IMesh> vMeshes, WriteOptions options)
        {
            StandardMeshWriter writer = new StandardMeshWriter();
            return writer.Write(sFilename, vMeshes, options);
        }


        public IOWriteResult Write(string sFilename, List<IMesh> vMeshes, WriteOptions options)
        {
            string sExtension = Path.GetExtension(sFilename).ToUpper();

            if (sExtension == ".OBJ") {
                try {
                    return Write_OBJ(sFilename, vMeshes, options);
                } catch (Exception) {
                    return new IOWriteResult(WriteResult.WriterError, "Unknown error");
                }

            } else
                return new IOWriteResult(WriteResult.UnknownFormatError, "cannot write file format " + sExtension);

        }




        IOWriteResult Write_OBJ(string sFilename, List<IMesh> vMeshes, WriteOptions options)
        {
            StreamWriter w = new StreamWriter(sFilename);
            if (w.BaseStream == null)
                return new IOWriteResult(WriteResult.FileAccessError, "Could not open file " + sFilename + " for writing");

            OBJWriter writer = new OBJWriter();
            var result = writer.Write(w, vMeshes, options);
            w.Close();
            return result;
        }


    }


}
