using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace g3
{
    public class StandardMeshWriter
    {

        static public Tuple<WriteResult, string> WriteFile(string sFilename, List<IMesh> vMeshes, WriteOptions options)
        {
            StandardMeshWriter writer = new StandardMeshWriter();
            return writer.Write(sFilename, vMeshes, options);
        }


        public Tuple<WriteResult, string> Write(string sFilename, List<IMesh> vMeshes, WriteOptions options)
        {
            string sExtension = Path.GetExtension(sFilename).ToUpper();

            if (sExtension == ".OBJ") {
                try {
                    return Write_OBJ(sFilename, vMeshes, options);
                } catch (Exception) {
                    return Tuple.Create(WriteResult.WriterError, "Unknown error");
                }

            } else
                return Tuple.Create(WriteResult.UnknownFormatError, "cannot write file format " + sExtension);

        }




        Tuple<WriteResult, string> Write_OBJ(string sFilename, List<IMesh> vMeshes, WriteOptions options)
        {
            StreamWriter w = new StreamWriter(sFilename);
            if (w.BaseStream == null)
                return Tuple.Create(WriteResult.FileAccessError, "Could not open file " + sFilename + " for writing");

            OBJWriter writer = new OBJWriter();
            var result = writer.Write(w, vMeshes, options);
            w.Close();
            return result;
        }


    }


}
