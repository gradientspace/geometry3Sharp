using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace g3
{
    public class StandardMeshReader
    {


        static public Tuple<ReadResult, string> ReadFile(string sFilename, ReadOptions options, IMeshBuilder builder)
        {
            StandardMeshReader reader = new StandardMeshReader();
            return reader.Read(sFilename, options, builder);
        }


        public Tuple<ReadResult, string> Read(string sFilename, ReadOptions options, IMeshBuilder builder)
        {
            string sExtension = Path.GetExtension(sFilename).ToUpper();

            if (sExtension == ".OBJ") {
                try {
                    return Read_OBJ(sFilename, options, builder);
                } catch (Exception) {
                    return Tuple.Create(ReadResult.GenericReaderError, "Unknown error");
                }

            } else
                return Tuple.Create(ReadResult.UnknownFormatError, "cannot read file format " + sExtension);

        }




        Tuple<ReadResult, string> Read_OBJ(string sFilename, ReadOptions options, IMeshBuilder builder)
        {
            StreamReader stream = new StreamReader(sFilename);
            if (stream.BaseStream == null)
                return Tuple.Create(ReadResult.FileAccessError, "Could not open file " + sFilename + " for writing");

            OBJReader reader = new OBJReader();
            var result = reader.Read(stream, options, builder);
            stream.Close();
            return result;
        }


    }


}
