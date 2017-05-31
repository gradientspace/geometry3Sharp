using System;
using System.Collections.Generic;
using System.Linq;

namespace g3
{

	/// <summary>
	/// Collection of utility functions for one-line deep copies of lists
	/// </summary>
	public static class DeepCopy
	{

		public static List<T> List<T>(IEnumerable<T> Input) where T: IDuplicatable<T> {
			List<T> result = new List<T>();
			foreach ( T val in Input ) {
				result.Add(val.Duplicate());
			}
			return result;
		}


		public static T[] Array<T>(IEnumerable<T> Input) where T: IDuplicatable<T> {
			int count = Input.Count();
			T[] a = new T[count];
			int i = 0;
			foreach (T val in Input)
				a[i++] = val.Duplicate();
			return a;
		}

	}
}
