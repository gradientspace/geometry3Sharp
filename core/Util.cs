using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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




        static public string ToSecMilli(TimeSpan t)
        {
#if G3_USING_UNITY
            return string.Format("{0}", t.TotalSeconds);
#else
            return t.ToString("ss\\.ffff");
#endif
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
