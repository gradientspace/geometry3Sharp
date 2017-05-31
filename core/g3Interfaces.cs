using System;

namespace g3
{
	
	/// <summary>
	/// Deep-copy cloning interface. Duplicate() *must* return
	/// a full deep copy of object, including all internal data structures.
	/// </summary>
	public interface IDuplicatable<T>
	{
		T Duplicate();
	}


}
