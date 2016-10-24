using System;


namespace g3
{
    // Very hard to abstract material definitions from different formats.
    // basically we just have a generic top-level class and then completely different subclasses...

    public abstract class GenericMaterial
    {
        public static readonly Vector3f Invalid = new Vector3f(-1, -1, -1);

        public string name;
        public int id;

        public enum KnownMaterialTypes
        {
            OBJ_MTL_Format
        }
        public KnownMaterialTypes Type { get; set; }
    }



    // details: http://www.fileformat.info/format/material/
    // Note: if value is initialized to Invalid vector, -1, or NaN, it was not defined in material file
    public class OBJMaterial : GenericMaterial
    {
        public Vector3f Ka;     // rgb ambient reflectivity
        public Vector3f Kd;     // rgb diffuse reflectivity 
        public Vector3f Ks;     // rgb specular reflectivity
        public Vector3f Tf;        // rgb transmission filter
        public int illum;          // illumination model 0-10
        public float d;            // dissolve
        public float Ns;           // specular exponent
        public float sharpness;    // reflection sharpness
        public float Ni;            // index of refraction / optical density

        // [TODO] texture materials


        public OBJMaterial()
        {
            Type = KnownMaterialTypes.OBJ_MTL_Format;
            id = -1;
            name = "///INVALID_NAME";
            Ka = Kd = Ks = Tf = Invalid;
            illum = -1;
            d = Ns = sharpness = Ni = Single.NaN;
        }
    }
}
