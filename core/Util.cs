using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace g3
{
    public class Util
    {

		static public void gBreakToDebugger() {
			if ( System.Diagnostics.Debugger.IsAttached)
				System.Diagnostics.Debugger.Break();
		}

        [Conditional("DEBUG")] 
        static public void gDevAssert(bool bValue) {
            if (bValue == false)
                throw new Exception("gDevAssert");
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




        public static string MakeVec3FormatString(int i0, int i1, int i2, int nPrecision)
        {
            return string.Format("{{{0}:F{3}}} {{{1}:F{3}}} {{{2}:F{3}}}", i0, i1, i2, nPrecision);
        }



        static public string ToSecMilli(TimeSpan t)
        {
#if G3_USING_UNITY
            return string.Format("{0}", t.TotalSeconds);
#else
            return t.ToString("ss\\.ffff");
#endif
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
