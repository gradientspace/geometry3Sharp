using System;


namespace g3
{
    /// <summary>
    /// Construct hash of multiple values using FNV hash (ish)
    /// http://www.isthe.com/chongo/tech/comp/fnv/
    /// 
    /// (should probably be using uint? but standard GetHashCode() returns int...)
    /// </summary>
    public struct HashBuilder
    {
        public int Hash;

        public HashBuilder(int init = unchecked((int)2166136261) )
        {
            Hash = init;
        }


        public void Add(int i) {
            unchecked {
                Hash = (Hash * 16777619) ^ i.GetHashCode();
            }
        }

        public void Add(double d) {
            unchecked {
                Hash = (Hash * 16777619) ^ d.GetHashCode();
            }
        }

        public void Add(float f) {
            unchecked {
                Hash = (Hash * 16777619) ^ f.GetHashCode();
            }
        }


        public void Add(Vector2f v) {
            unchecked {
                Hash = (Hash * 16777619) ^ v.x.GetHashCode();
                Hash = (Hash * 16777619) ^ v.y.GetHashCode();
            }
        }

        public void Add(Vector2d v)
        {
            unchecked {
                Hash = (Hash * 16777619) ^ v.x.GetHashCode();
                Hash = (Hash * 16777619) ^ v.y.GetHashCode();
            }
        }

        public void Add(Vector3f v) {
            unchecked {
                Hash = (Hash * 16777619) ^ v.x.GetHashCode();
                Hash = (Hash * 16777619) ^ v.y.GetHashCode();
                Hash = (Hash * 16777619) ^ v.z.GetHashCode();
            }
        }

        public void Add(Vector3d v)
        {
            unchecked {
                Hash = (Hash * 16777619) ^ v.x.GetHashCode();
                Hash = (Hash * 16777619) ^ v.y.GetHashCode();
                Hash = (Hash * 16777619) ^ v.z.GetHashCode();
            }
        }

        public void Add(Frame3f f)
        {
            unchecked {
                Hash = (Hash * 16777619) ^ f.Origin.x.GetHashCode();
                Hash = (Hash * 16777619) ^ f.Origin.y.GetHashCode();
                Hash = (Hash * 16777619) ^ f.Origin.z.GetHashCode();
                Hash = (Hash * 16777619) ^ f.Rotation.x.GetHashCode();
                Hash = (Hash * 16777619) ^ f.Rotation.y.GetHashCode();
                Hash = (Hash * 16777619) ^ f.Rotation.z.GetHashCode();
                Hash = (Hash * 16777619) ^ f.Rotation.w.GetHashCode();
            }
        }

        public void Add(Index3i v) {
            unchecked {
                Hash = (Hash * 16777619) ^ v.a.GetHashCode();
                Hash = (Hash * 16777619) ^ v.b.GetHashCode();
                Hash = (Hash * 16777619) ^ v.c.GetHashCode();
            }
        }

        public void Add(Index2i v) {
            unchecked {
                Hash = (Hash * 16777619) ^ v.a.GetHashCode();
                Hash = (Hash * 16777619) ^ v.b.GetHashCode();
            }
        }

    }


}
