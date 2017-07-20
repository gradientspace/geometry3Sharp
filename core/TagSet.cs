using System;
using System.Collections.Generic;

namespace g3
{

    /// <summary>
    /// Basic object->integer mapping
    /// </summary>
    public class IntTagSet<T>
    {
        public const int InvalidTag = int.MaxValue;

        public IntTagSet()
        {
        }


        Dictionary<T, int> tags;
        void create() {
            if (tags == null)
                tags = new Dictionary<T, int>();
        }

        public void Add(T reference, int tag) {
            create();
            tags.Add(reference, tag);
        }
        public bool Has(T reference)
        {
            int tag = 0;
            if (tags != null && tags.TryGetValue(reference, out tag))
                return true;
            return false;
        }
        public int Get(T reference)
        {
            int tag = 0;
            if (tags != null && tags.TryGetValue(reference, out tag))
                return tag;
            return InvalidTag;
        }
    }




    /// <summary>
    /// Basic object->string mapping
    /// </summary>
    public class StringTagSet<T>
    {
        public const string InvalidTag = "";

        public StringTagSet()
        {
        }


        Dictionary<T, string> tags;
        void create() {
            if (tags == null)
                tags = new Dictionary<T, string>();
        }

        public void Add(T reference, string tag) {
            create();
            tags.Add(reference, tag);
        }
        public bool Has(T reference)
        {
            string tag = InvalidTag;
            if (tags != null && tags.TryGetValue(reference, out tag))
                return true;
            return false;
        }
        public string Get(T reference)
        {
            string tag = InvalidTag;
            if (tags != null && tags.TryGetValue(reference, out tag))
                return tag;
            return InvalidTag;
        }
    }

}
