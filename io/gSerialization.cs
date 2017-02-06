using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace g3
{
    public static class gSerialization
    {
        public static void Store(Polygon2d polygon, BinaryWriter writer)
        {
            writer.Write(polygon.VertexCount);
            for (int i = 0; i < polygon.VertexCount; ++i) {
                writer.Write(polygon[i].x);
                writer.Write(polygon[i].y);
            }
        }

        public static void Restore(Polygon2d polygon, BinaryReader reader)
        {
            int count = reader.ReadInt32();
            for ( int i = 0; i < count; ++i ) {
                double x = reader.ReadDouble();
                double y = reader.ReadDouble();
                polygon.AppendVertex(new Vector2d(x, y));
            }
        }




        public static void Store(GeneralPolygon2d polygon, BinaryWriter writer)
        {
            Store(polygon.Outer, writer);
            writer.Write(polygon.Holes.Count);
            for ( int i = 0; i < polygon.Holes.Count; ++i )
                Store(polygon.Holes[i], writer);
        }

        public static void Restore(GeneralPolygon2d polygon, BinaryReader reader)
        {
            Polygon2d outer = new Polygon2d();
            Restore(outer, reader);
            polygon.Outer = outer;

            int hole_count = reader.ReadInt32();
            for ( int i = 0; i < hole_count; ++i ) {
                Polygon2d holepoly = new Polygon2d();
                Restore(holepoly, reader);
                polygon.AddHole(holepoly, false);
            }
        }



    }
}
