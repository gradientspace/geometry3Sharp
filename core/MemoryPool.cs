using System;
using System.Collections.Generic;

namespace g3
{
	/// <summary>
	/// Very basic object pool class. 
	/// </summary>
	public class MemoryPool<T> where T : class, new()
	{
        DVector<T> Allocated;
        DVector<T> Free;

		public MemoryPool()
		{
			Allocated = new DVector<T>();
			Free = new DVector<T>();
        }

		public T Allocate()
		{
			if ( Free.size > 0 ) {
				T allocated = Free[Free.size - 1];
                Free.pop_back();
				return allocated;
			} else {
				T newval = new T();
                Allocated.Add(newval);
				return newval;
			}
		}

		public void Return(T obj) {
			Free.Add(obj);
		}


        public void ReturnAll()
        {
            Free = new DVector<T>(Allocated);
        }


        public void FreeAll()
        {
            Allocated = new DVector<T>();
            Free = new DVector<T>();
        }

	}
}
