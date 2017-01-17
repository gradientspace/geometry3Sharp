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
             Func<string, ReadOptions, IOReadResult> readFunc = null;

            string sExtension = Path.GetExtension(sFilename);

            if (sExtension.Equals(".obj", StringComparison.OrdinalIgnoreCase))
                readFunc = Read_OBJ;
            else if (sExtension.Equals(".stl", StringComparison.OrdinalIgnoreCase))
                readFunc = Read_STL;

            if ( readFunc == null )
                return new IOReadResult(IOCode.UnknownFormatError, "format " + sExtension + " is not supported");

            try {
                return readFunc(sFilename, options);
            } catch (Exception e) {
                return new IOReadResult(IOCode.GenericReaderError, "Unknown error : exception : " + e.Message);
            }

        }



        IOReadResult Read_STL(string sFilename, ReadOptions options)
        {
            // detect binary STL
            FileStream stream = null;
            try {
                stream = File.Open(sFilename, FileMode.Open);
            } catch (Exception e) {
                return new IOReadResult(IOCode.FileAccessError, "Could not open file " + sFilename + " for reading : " + e.Message);
            }

            BinaryReader binReader = new BinaryReader(stream);
            byte[] header = binReader.ReadBytes(80);
            bool bIsBinary = false;

            // if first 80 bytes contain non-text chars, probably a binary file
            if (Util.IsTextString(header) == false)
                bIsBinary = true;
            // if we don't see "solid" string in first 80 chars, probably binary
            if ( bIsBinary == false ) {
                string sText = System.Text.Encoding.ASCII.GetString(header);
                if (sText.Contains("solid") == false)
                    bIsBinary = true;
            }

            stream.Seek(0, SeekOrigin.Begin); // reset stream

            STLReader reader = new STLReader();
            reader.warningEvent += on_warning;
            IOReadResult result = (bIsBinary) ?
                reader.Read(new BinaryReader(stream), options, MeshBuilder) :
                reader.Read(new StreamReader(stream), options, MeshBuilder);

            stream.Close();
            return result;
        }



        IOReadResult Read_OBJ(string sFilename, ReadOptions options)
        {
            StreamReader stream = new StreamReader(sFilename);
            if (stream.BaseStream == null)
                return new IOReadResult(IOCode.FileAccessError, "Could not open file " + sFilename + " for reading");

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
