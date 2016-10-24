using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace g3
{
    public class StandardMeshReader
    {


        static public IOReadResult ReadFile(string sFilename, ReadOptions options, IMeshBuilder builder)
        {
            StandardMeshReader reader = new StandardMeshReader();
            return reader.Read(sFilename, options, builder);
        }


        public IOReadResult Read(string sFilename, ReadOptions options, IMeshBuilder builder)
        {
            string sExtension = Path.GetExtension(sFilename).ToUpper();

            if (sExtension == ".OBJ") {
                try {
                    return Read_OBJ(sFilename, options, builder);
                } catch (Exception) {
                    return new IOReadResult(ReadResult.GenericReaderError, "Unknown error");
                }

            } else
                return new IOReadResult(ReadResult.UnknownFormatError, "cannot read file format " + sExtension);

        }




        IOReadResult Read_OBJ(string sFilename, ReadOptions options, IMeshBuilder builder)
        {
            StreamReader stream = new StreamReader(sFilename);
            if (stream.BaseStream == null)
                return new IOReadResult(ReadResult.FileAccessError, "Could not open file " + sFilename + " for writing");

            OBJReader reader = new OBJReader();
            var result = reader.Read(stream, options, builder);
            stream.Close();
            return result;
        }


    }


}
