using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace g3
{

    public delegate void ParsingMessagesHandler(string message, object extra_data);


    public interface MeshFormatReader
    {
        List<string> SupportedExtensions { get; }
        IOReadResult ReadFile(string sFilename, IMeshBuilder builder, ReadOptions options, ParsingMessagesHandler warnings);
    }


    public class StandardMeshReader
    {

        // connect to this to get warning status messages
        public event ParsingMessagesHandler warningEvent;

        /// <summary>
        /// The various format handlers will use this IMeshBuilder to construct meshes
        /// based on the file data and read options.
        /// Default is initialized to DMesh3Builder
        /// </summary>
        public IMeshBuilder MeshBuilder { get; set; }

        /// <summary>
        /// Set of format handlers
        /// </summary>
        List<MeshFormatReader> Readers = new List<MeshFormatReader>();


        /// <summary>
        /// Construct a MeshReader, optionally with default format handlers
        /// Initializes MeshBuilder to a DMesh3Builder
        /// </summary>
        public StandardMeshReader(bool bIncludeDefaultReaders = true)
        {
            Readers = new List<MeshFormatReader>();
            MeshBuilder = new DMesh3Builder();

            if ( bIncludeDefaultReaders ) {
                Readers.Add(new OBJFormatReader());
                Readers.Add(new STLFormatReader());
                Readers.Add(new OFFFormatReader());
            }
        }


        /// <summary>
        /// Check if extension type is supported
        /// </summary>
        public bool SupportsFormat(string sExtension)
        {
            foreach (var reader in Readers)
                foreach (string ext in reader.SupportedExtensions)
                    if (ext.Equals(sExtension, StringComparison.OrdinalIgnoreCase))
                        return true;
            return false;
        }


        /// <summary>
        /// Add a handler for a given formta
        /// </summary>
        public void AddFormatHandler(MeshFormatReader reader)
        {
            List<string> formats = reader.SupportedExtensions;
            foreach (string s in formats)
                if (SupportsFormat(s))
                    throw new Exception("StandardMeshReader.AddFormatHandler: format " + s + " is already registered!");

            Readers.Add(reader);
        }


        /// <summary>
        /// Read mesh file at path, with given Options. Result is stored
        /// in MeshBuilder parameter
        /// </summary>
        public IOReadResult Read(string sFilename, ReadOptions options)
        {
            if (MeshBuilder == null)
                return new IOReadResult(IOCode.GenericReaderError, "MeshBuilder is null!");

            string sExtension = Path.GetExtension(sFilename).Substring(1);

            MeshFormatReader useReader = null;
            foreach (var reader in Readers) {
                foreach (string ext in reader.SupportedExtensions) {
                    if (ext.Equals(sExtension, StringComparison.OrdinalIgnoreCase))
                        useReader = reader;
                }
                if (useReader != null)
                    break;
            }
            if ( useReader == null ) 
                return new IOReadResult(IOCode.UnknownFormatError, "format " + sExtension + " is not supported");

            try {
                return useReader.ReadFile(sFilename, MeshBuilder, options, on_warning);
            } catch (Exception e) {
                return new IOReadResult(IOCode.GenericReaderError, "Unknown error : exception : " + e.Message);
            }

        }


        /// <summary>
        /// Read mesh file using options and builder. You must provide our own Builder
        /// here because the reader is not returned
        /// </summary>
        static public IOReadResult ReadFile(string sFilename, ReadOptions options, IMeshBuilder builder)
        {
            StandardMeshReader reader = new StandardMeshReader();
            reader.MeshBuilder = builder;
            return reader.Read(sFilename, options);
        }


        /// <summary>
        /// This is basically a utility function, returns first mesh in file, with default options.
        /// </summary>
        static public DMesh3 ReadMesh(string sFilename)
        {
            DMesh3Builder builder = new DMesh3Builder();
            IOReadResult result = ReadFile(sFilename, ReadOptions.Defaults, builder);
            return (result.code == IOCode.Ok) ? builder.Meshes[0] : null;
        }


        private void on_warning(string message, object extra_data)
        {
            if (warningEvent != null)
                warningEvent(message, extra_data);
        }
    }



    // MeshFormatReader impl for OBJ
    public class OBJFormatReader : MeshFormatReader
    {
        public List<string> SupportedExtensions { get {
                return new List<string>() { "obj" };
            }
        }


        public IOReadResult ReadFile(string sFilename, IMeshBuilder builder, ReadOptions options, ParsingMessagesHandler messages)
        {
            StreamReader stream = new StreamReader(sFilename);
            if (stream.BaseStream == null)
                return new IOReadResult(IOCode.FileAccessError, "Could not open file " + sFilename + " for reading");

            OBJReader reader = new OBJReader();
            if (options.ReadMaterials)
                reader.MTLFileSearchPaths.Add(Path.GetDirectoryName(sFilename));
            reader.warningEvent += messages;

            var result = reader.Read(stream, options, builder);
            stream.Close();
            return result;
        }
    }



    // MeshFormatReader impl for STL
    public class STLFormatReader : MeshFormatReader
    {
        public List<string> SupportedExtensions { get {
                return new List<string>() { "stl" };
            }
        }


        public IOReadResult ReadFile(string sFilename, IMeshBuilder builder, ReadOptions options, ParsingMessagesHandler messages)
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
            reader.warningEvent += messages;
            IOReadResult result = (bIsBinary) ?
                reader.Read(new BinaryReader(stream), options, builder) :
                reader.Read(new StreamReader(stream), options, builder);

            stream.Close();
            return result;
        }
    }




    // MeshFormatReader impl for OFF
    public class OFFFormatReader : MeshFormatReader
    {
        public List<string> SupportedExtensions { get {
                return new List<string>() { "off" };
            }
        }


        public IOReadResult ReadFile(string sFilename, IMeshBuilder builder, ReadOptions options, ParsingMessagesHandler messages)
        {
            StreamReader stream = new StreamReader(sFilename);
            if (stream.BaseStream == null)
                return new IOReadResult(IOCode.FileAccessError, "Could not open file " + sFilename + " for reading");

            OFFReader reader = new OFFReader();
            reader.warningEvent += messages;

            var result = reader.Read(stream, options, builder);
            stream.Close();
            return result;
        }
    }



}
