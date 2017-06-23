using System;


namespace g3
{
    // Very hard to abstract material definitions from different formats.
    // basically we just have a generic top-level class and then completely different subclasses...

    public abstract class GenericMaterial
    {
        public static readonly float Invalidf = float.MinValue;
        public static readonly Vector3f Invalid = new Vector3f(-1, -1, -1);

        public string name;
        public int id;


        abstract public Vector3f DiffuseColor { get; set; }
        abstract public float Alpha { get; set; }


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
        public Vector3f Ke;     // rgb emissive
        public Vector3f Tf;        // rgb transmission filter
        public int illum;          // illumination model 0-10
        public float d;            // dissolve (alpha)
        public float Ns;           // specular exponent (shininess)
        public float sharpness;    // reflection sharpness
        public float Ni;            // index of refraction / optical density

        public string map_Ka;
        public string map_Kd;
        public string map_Ks;
        public string map_Ke;
        public string map_d;
        public string map_Ns;

        public string bump;
        public string disp;
        public string decal;
        public string refl;

        // [TODO] texture materials


        public OBJMaterial()
        {
            Type = KnownMaterialTypes.OBJ_MTL_Format;
            id = -1;
            name = "///INVALID_NAME";
            Ka = Kd = Ks = Ke = Tf = Invalid;
            illum = -1;
			d = Ns = sharpness = Ni = Invalidf;
        }

        override public Vector3f DiffuseColor {
            get { return (Kd == Invalid) ? new Vector3f(1, 1, 1) : Kd; }
            set { Kd = value; }
        }
        override public float Alpha {
            get { return (d == Invalidf) ? 1.0f : d; }
            set { d = value; }
        }


    }
}
