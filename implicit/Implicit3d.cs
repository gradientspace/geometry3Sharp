using System;
using System.Collections.Generic;


namespace g3
{

    public interface ImplicitFunction3d
    {
        double Value(ref Vector3d pt);
    }



    public class ImplicitSphere3d : ImplicitFunction3d
    {
        public double Value(ref Vector3d pt)
        {
            return pt.Length - 5.0f;
        }
    }


}
