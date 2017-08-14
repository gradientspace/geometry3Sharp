using System;
using System.Collections.Generic;

namespace g3
{
	/// <summary>
	/// Very basic object pool class. 
	/// </summary>
	public class MemoryPool<T> where T : class, new()
	{
		List<T> Allocated;
		List<T> Free;

		public MemoryPool(int nEstCapacity = 64)
		{
			Allocated = new List<T>(nEstCapacity);
			Free = new List<T>(16);
		}

		public T Allocate()
		{
			if ( Free.Count > 0 ) {
				T allocated = Free[Free.Count - 1];
				Free.RemoveAt(Free.Count - 1);
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


	}
}
