using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace g3
{
    public class StandardMeshReader
    {

        // connect to this to get warning status messages
        public event ErrorEventHandler warningEvent;

        public IMeshBuilder MeshBuilder { get; set; }


        static public IOReadResult ReadFile(string sFilename, ReadOptions options, IMeshBuilder builder)
        {
            StandardMeshReader reader = new StandardMeshReader();
            reader.MeshBuilder = builder;
            return reader.Read(sFilename, options);
        }


        public IOReadResult Read(string sFilename, ReadOptions options)
        {
            string sExtension = Path.GetExtension(sFilename).ToUpper();

            if (sExtension == ".OBJ") {
                try {
                    return Read_OBJ(sFilename, options);
                } catch (Exception e) {
                    return new IOReadResult(ReadResult.GenericReaderError, "caught exception : " + e.Message);
                }

            } else
                return new IOReadResult(ReadResult.UnknownFormatError, "cannot read file format " + sExtension);

        }







        IOReadResult Read_OBJ(string sFilename, ReadOptions options)
        {
            StreamReader stream = new StreamReader(sFilename);
            if (stream.BaseStream == null)
                return new IOReadResult(ReadResult.FileAccessError, "Could not open file " + sFilename + " for writing");

            OBJReader reader = new OBJReader();
            if (options.ReadMaterials)
                reader.MTLFileSearchPaths.Add(Path.GetDirectoryName(sFilename));
            reader.warningEvent += on_warning;

            var result = reader.Read(stream, options, MeshBuilder);
            stream.Close();
            return result;
        }


        private void on_warning(object sender, ErrorEventArgs e)
        {
            if (warningEvent != null)
                warningEvent(sender, e);
        }


    }


}
