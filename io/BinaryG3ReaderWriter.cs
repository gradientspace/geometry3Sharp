using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading;

namespace g3
{

    public class BinaryG3Writer : IMeshWriter
    {
        public IOWriteResult Write(BinaryWriter writer, List<WriteMesh> vMeshes, WriteOptions options)
        {
            int nMeshes = vMeshes.Count;
            writer.Write(nMeshes);
            for (int k = 0; k < vMeshes.Count; ++k) {
                DMesh3 mesh = vMeshes[k].Mesh as DMesh3;
                if (mesh == null)
                    throw new NotImplementedException("BinaryG3Writer.Write: can only write DMesh3 meshes");
                gSerialization.Store(mesh, writer);
            }

            return new IOWriteResult(IOCode.Ok, "");
        }

        public IOWriteResult Write(TextWriter writer, List<WriteMesh> vMeshes, WriteOptions options)
        {
            throw new NotSupportedException("BinaryG3 Writer does not support ascii mode");
        }

    }



    public class BinaryG3Reader : IMeshReader
    {
        public IOReadResult Read(BinaryReader reader, ReadOptions options, IMeshBuilder builder)
        {
            int nMeshes = reader.ReadInt32();
            for (int k = 0; k < nMeshes; ++k) {
                DMesh3 m = new DMesh3();
                gSerialization.Restore(m, reader);
                builder.AppendNewMesh(m);
            }

            return new IOReadResult(IOCode.Ok, "");
        }

        public IOReadResult Read(TextReader reader, ReadOptions options, IMeshBuilder builder)
        {
            throw new NotSupportedException("BinaryG3Reader Writer does not support ascii mode");
        }

    }



}
