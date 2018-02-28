using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.IO;

namespace g3
{

    public enum FailMode { DebugAssert, gDevAssert, Throw, ReturnOnly }


    public static class Util
    {

		static public void gBreakToDebugger() {
			if ( System.Diagnostics.Debugger.IsAttached)
				System.Diagnostics.Debugger.Break();
		}

        static public bool DebugBreakOnDevAssert = true;

        [Conditional("DEBUG")] 
        static public void gDevAssert(bool bValue, string message = "gDevAssert") {
            if (bValue == false) {
                if (DebugBreakOnDevAssert)
                    System.Diagnostics.Debugger.Break();
                else
                    throw new Exception(message);
            }
        }
	
	
        // useful in some cases
        public static bool IsRunningOnMono ()
        {
            return Type.GetType ("Mono.Runtime") != null;
        }


        static public bool IsBitSet(byte b, int pos)
        {
            return (b & (1 << pos)) != 0;
        }
        static public bool IsBitSet(int n, int pos)
        {
            return (n & (1 << pos)) != 0;
        }


        // have not tested this extensively, but internet says it is reasonable...
        static public bool IsTextString(byte[] array)
        {
            foreach ( byte b in array ) {
                if (b > 127)
                    return false;
                if ( b < 32 && b != 9 && b != 10 && b != 13 )
                    return false;
            }
            return true;
        }


        /// <summary>
        /// check if file contains bytes that correspond to ascii control characters,
        /// which would not occur in a plain text file, but are likely in a binary file.
        /// (note: this is not conclusive! for example if binary file was all FF's, this would return true)
        /// </summary>
        static public bool IsBinaryFile(string path, int max_search_len = -1)
        {
            using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                return IsBinaryStream(stream, max_search_len);
            }
        }

        /// <summary>
        /// See IsBinaryFile()
        /// </summary>
        static public bool IsBinaryStream(Stream streamIn, int max_search_len = -1)
        {
            int i = 0, ch = 0;
            int sequential_null = 0;
            StreamReader stream = new StreamReader(streamIn);
            bool is_binary = false;
            while ((ch = stream.Read()) != -1) {
                if (i++ >= max_search_len) 
                    goto decided_no;

                if (IsASCIIControlChar(ch))
                    goto decided_yes;

                if (ch == 0) {
                    sequential_null++;
                    if (sequential_null >= 2) 
                        goto decided_yes;
                } else
                    sequential_null = 0;
            }
        decided_yes:
            is_binary = true;
        decided_no:
            streamIn.Seek(0, SeekOrigin.Begin); // reset stream
            return is_binary;
        }


        /// <summary>
        /// test if character is ascii control character, which (presumably?) won't
        /// occur in unicode files?
        /// </summary>
        static public bool IsASCIIControlChar(int ch) {
            const char NUL = (char)0; // Null char
            const char BS = (char)8; // Back Space
            const char CR = (char)13; // Carriage Return
            const char SUB = (char)26; // Substitute
            return (ch > NUL && ch <= BS) || (ch > CR && ch <= SUB);
        }


        static public string ToHexString(byte[] bytes, bool upperCase = false)
        {
            StringBuilder result = new StringBuilder(bytes.Length*2);
            for (int i = 0; i < bytes.Length; i++)
                result.Append(bytes[i].ToString(upperCase ? "X2" : "x2"));
            return result.ToString();
        }

        
        static public float ParseInt(string s, int nDefault) {
            try {
                return int.Parse(s);
            } catch {
                return nDefault;
            }
        }
        static public float ParseFloat(string s, float fDefault) {
            try {
                return float.Parse(s);
            } catch {
                return fDefault;
            }
        }
        static public double ParseDouble(string s, double fDefault) {
            try {
                return double.Parse(s);
            } catch {
                return fDefault;
            }
        }



        static public float[] BufferCopy(float[] from, float[] to)
        {
            if (from == null)
                return null;
            if (to.Length != from.Length)
                to = new float[from.Length];
            Buffer.BlockCopy(from, 0, to, 0, from.Length * sizeof(float));
            return to;
        }
        static public int[] BufferCopy(int[] from, int[] to)
        {
            if (from == null)
                return null;
            if (to.Length != from.Length)
                to = new int[from.Length];
            Buffer.BlockCopy(from, 0, to, 0, from.Length * sizeof(int));
            return to;
        }



        public static string MakeFloatFormatString(int i, int nPrecision)
        {
            return string.Format("{{{0}:F{1}}}", i, nPrecision);
        }
        public static string MakeVec3FormatString(int i0, int i1, int i2, int nPrecision)
        {
            return string.Format("{{{0}:F{3}}} {{{1}:F{3}}} {{{2}:F{3}}}", i0, i1, i2, nPrecision);
        }



        static public string ToSecMilli(TimeSpan t)
        {
#if G3_USING_UNITY
            return string.Format("{0}", t.TotalSeconds);
#else
            return string.Format("{0:F5}", t.TotalSeconds);
#endif
        }



        static public T[] AppendArrays<T>(params object[] args)
        {
            int count = args.Length;
            int N = 0;
            for (int i = 0; i < count; ++i)
                N += (args[i] as T[]).Length;

            T[] result = new T[N];
            int cur_offset = 0;
            for ( int i = 0; i < count; ++i ) {
                T[] c = args[i] as T[];
                Array.Copy(c, 0, result, cur_offset, c.Length);
                cur_offset += c.Length;
            }

            return result;
        }




        // conversion to/from bytes
        public static byte[] StructureToByteArray(object obj)
        {
            int len = Marshal.SizeOf(obj);
            byte[] arr = new byte[len];
            IntPtr ptr = Marshal.AllocHGlobal(len);
            Marshal.StructureToPtr(obj, ptr, true);
            Marshal.Copy(ptr, arr, 0, len);
            Marshal.FreeHGlobal(ptr);
            return arr;
        }

        public static void ByteArrayToStructure(byte[] bytearray, ref object obj)
        {
            int len = Marshal.SizeOf(obj);
            IntPtr i = Marshal.AllocHGlobal(len);
            Marshal.Copy(bytearray, 0, i, len);
            obj = Marshal.PtrToStructure(i, obj.GetType());
            Marshal.FreeHGlobal(i);
        }



        public static void WriteDebugMesh(IMesh mesh, string sPath)
        {
            WriteOptions options = WriteOptions.Defaults;
            options.bWriteGroups = true;
            options.bPerVertexColors = true;
            options.bPerVertexNormals = true;
            options.bPerVertexUVs = true;
            StandardMeshWriter.WriteFile(sPath, new List<WriteMesh>() { new WriteMesh(mesh) }, options);
        }

        public static void WriteDebugMeshAndMarkers(IMesh mesh, List<Vector3d> Markers, string sPath)
        {
            WriteOptions options = WriteOptions.Defaults;
            options.bWriteGroups = true;
            List<WriteMesh> meshes = new List<WriteMesh>() { new WriteMesh(mesh) };
            double size = BoundsUtil.Bounds(mesh).Diagonal.Length * 0.01f;
            foreach ( Vector3d v in Markers ) {
                TrivialBox3Generator boxgen = new TrivialBox3Generator();
                boxgen.Box = new Box3d(v, size * Vector3d.One);
                boxgen.Generate();
                DMesh3 m = new DMesh3();
                boxgen.MakeMesh(m);
                meshes.Add(new WriteMesh(m));
            }

            StandardMeshWriter.WriteFile(sPath, meshes, options);
        }


    }



    public class gException : Exception
    {
        public gException(string sMessage) 
            : base(sMessage)
        { 
        }
        public gException(string text, object arg0)
            : base(string.Format(text, arg0))
        {
        }
        public gException(string text, object arg0, object arg1)
            : base(string.Format(text, arg0, arg1))
        {
        }
        public gException(string text, params object[] args)
            : base(string.Format(text, args))
        {
        }
    }


}
