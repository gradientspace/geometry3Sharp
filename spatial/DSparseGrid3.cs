using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{

    /// <summary>
    /// generic 3D grid interface (is this useful?)
    /// </summary>
    public interface IGrid3
    {
        AxisAlignedBox3i BoundsInclusive { get; }
    }



    /// <summary>
    /// this type can be used in a SparseGrid. 
    /// </summary>
    public interface IGridElement3
    {
        /// <summary>
        /// create new instance of this object w/ same size parameters, but
        /// don't copy data unless bCopy = true
        /// </summary>
        IGridElement3 CreateNewGridElement(bool bCopy);
    }



    /// <summary>
    /// Dynamic sparse 3D grid. Idea is that we have grid of some type of object
    /// and we don't want to pre-allocate full grid of them. So we allocate on-demand.
    /// This can be used to implement multi-grid schemes, eg for example the GridElement
    /// type could be Bitmap3 of a fixed dimension.
    /// </summary>
    public class DSparseGrid3<T> : IGrid3 where T : class, IGridElement3
    {
        T exemplar;

        Dictionary<Vector3i, T> elements;
        AxisAlignedBox3i bounds;

        /// <summary>
        /// Must provide a sample instance of the element type that we can Duplicate()
        /// to make additional copies. Should be no data in here
        /// </summary>
        public DSparseGrid3(T toDuplicate)
        {
            this.exemplar = toDuplicate;
            elements = new Dictionary<Vector3i, T>();
            bounds = AxisAlignedBox3i.Empty;
        }


        public bool Has(Vector3i index)
        {
            return elements.ContainsKey(index);
        }


        public T Get(Vector3i index, bool allocateIfMissing = true)
        {
            T result;
            bool found = elements.TryGetValue(index, out result);
            if (found)
                return result;
            if (allocateIfMissing)
                return allocate(index);
            return null;
        }


        public bool Free(Vector3i index)
        {
            if ( elements.ContainsKey(index) ) {
                elements.Remove(index);
                return true;
            }
            return false;
        }


        public void FreeAll()
        {
            while ( elements.Count > 0 ) 
                elements.Remove(elements.First().Key);
        }


        public int Count
        {
            get { return elements.Count; }
        }

        public double Density
        {
            get { return (double)elements.Count / (double)bounds.Volume; }
        }


        /// <summary>
        /// returns integer-aabb where indices range from [min,max] (inclusive)
        /// </summary>
        public AxisAlignedBox3i BoundsInclusive
        {
            get { return bounds; }
        }


        public Vector3i Dimensions
        {
            get { return bounds.Diagonal + Vector3i.One; }
        }


        public IEnumerable<Vector3i> AllocatedIndices()
        {
            foreach (var pair in elements)
                yield return pair.Key;
        }

        public IEnumerable<KeyValuePair<Vector3i,T>> Allocated()
        {
            return elements;
        }





        T allocate(Vector3i index)
        {
            T new_elem = exemplar.CreateNewGridElement(false) as T;
            elements.Add(index, new_elem);
            bounds.Contain(index);
            return new_elem;
        }



    }
}
