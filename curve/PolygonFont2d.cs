using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace g3
{
    /// <summary>
    /// This class represents an outline font, where the outline is composed of polygons.
    /// Each font is a list of GeneralPolygon2D objects, so each outline may have 1 or more holes.
    /// (In fact, the mapping is [string,list_of_gpolygons], so you can actually keep entire strings together if desired)
    /// </summary>
    public class PolygonFont2d
    {
        public class CharacterInfo
        {
            public GeneralPolygon2d[] Polygons;
            public AxisAlignedBox2d Bounds;
        }

        public Dictionary<string, CharacterInfo> Characters;
        public AxisAlignedBox2d MaxBounds;


        public PolygonFont2d()
        {
            Characters = new Dictionary<string, CharacterInfo>();
            MaxBounds = AxisAlignedBox2d.Empty;
        }


        public void AddCharacter(string s, GeneralPolygon2d[] polygons)
        {
            CharacterInfo info = new CharacterInfo();
            info.Polygons = polygons;
            info.Bounds = polygons[0].Bounds;
            for (int i = 1; i < polygons.Length; ++i)
                info.Bounds.Contain(polygons[i].Bounds);
            Characters.Add(s, info);

            MaxBounds.Contain(info.Bounds);
        }


        public List<GeneralPolygon2d> GetCharacter(char c)
        {
            string s = c.ToString();
            if (!Characters.ContainsKey(s))
                throw new Exception("PolygonFont2d.GetCharacterBounds: character " + c + " not available!");
            return new List<GeneralPolygon2d>(Characters[s].Polygons);
        }
        public List<GeneralPolygon2d> GetCharacter(string s)
        {
            if (!Characters.ContainsKey(s))
                throw new Exception("PolygonFont2d.GetCharacterBounds: character " + s + " not available!");
            return new List<GeneralPolygon2d>(Characters[s].Polygons);
        }

        public AxisAlignedBox2d GetCharacterBounds(char c)
        {
            string s = c.ToString();
            if (!Characters.ContainsKey(s))
                throw new Exception("PolygonFont2d.GetCharacterBounds: character " + c + " not available!");
            return Characters[s].Bounds;
        }

        public bool HasCharacter(char c) {
            string s = c.ToString();
            return Characters.ContainsKey(s);
        }






        /*
         * Serialization
         */



        private const int SerializerVersion = 3;


        public static void Store(PolygonFont2d font, BinaryWriter writer)
        {
            writer.Write(SerializerVersion);   // version number

            int nc = font.Characters.Count;
            writer.Write((int)nc);

            foreach (var pair in font.Characters) {
                byte[] stringbuf = Encoding.Unicode.GetBytes(pair.Key);
                writer.Write((int)stringbuf.Length);
                writer.Write(stringbuf);
                CharacterInfo ci = pair.Value;
                writer.Write((int)ci.Polygons.Length);
                for (int k = 0; k < ci.Polygons.Length; ++k)
                    gSerialization.Store(ci.Polygons[k], writer);
            }
        }


        public static PolygonFont2d ReadFont(string filename)
        {
            using (FileStream file_stream = File.Open(filename, FileMode.Open)) {
                BinaryReader binReader = new BinaryReader(file_stream);
                PolygonFont2d newfont = new PolygonFont2d();
                PolygonFont2d.Restore(newfont, binReader);
                return newfont;
            }
        }
        public static PolygonFont2d ReadFont(Stream s)
        {
            BinaryReader binReader = new BinaryReader(s);
            PolygonFont2d newfont = new PolygonFont2d();
            PolygonFont2d.Restore(newfont, binReader);
            return newfont;
        }


        public static void Restore(PolygonFont2d font, BinaryReader reader)
        {
            int version = reader.ReadInt32();
            if (version != SerializerVersion)
                throw new Exception("PolygonFont2d.Restore: invalid version!");

            int nc = reader.ReadInt32();
            for ( int ci = 0; ci < nc; ++ci) {
                int buflen = reader.ReadInt32();
                byte[] stringbuf = reader.ReadBytes(buflen);
                string s = Encoding.Unicode.GetString(stringbuf);

                int numpolys = reader.ReadInt32();
                GeneralPolygon2d[] polys = new GeneralPolygon2d[numpolys];
                for (int k = 0; k < numpolys; ++k) {
                    polys[k] = new GeneralPolygon2d();
                    gSerialization.Restore(polys[k], reader);
                }

                font.AddCharacter(s, polys);
            }
        }


    }
}
