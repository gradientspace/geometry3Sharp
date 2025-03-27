using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace g3
{
	/**
	 * OptionalValue<T> is a simple wrapper struct that allows a value to be stored along with
	 * an 'is valid' flag which defines whether or not the value is defined. The main use case
	 * is as method arguments, eg given MyStruct one can define a method like
	 * 
	 *    public void MyMethod( OptionalValue<MyStruct> StructValue = default )
	 *    
	 * and then the caller can either call MyMethod() and the calling code should ignore the value
	 * (as the IsValid flag will default to false) or call MyMethod( new MyStruct(...) ) and 
	 * the IsValid flag will be true.
	 * 
	 * Note an implicit cast is provided which means the ugly 'new OptionalValue<T>( new MyStruct(...) )' can be avoided
	 */
	public struct OptionalValue<T>
	{
		T value;
		bool isValid;

		public T Value { get { return value; } }
		public bool IsValid { get { return isValid; } }

		public OptionalValue(T valueIn)
		{
			value = valueIn;
			isValid = true;
		}

		public OptionalValue()
		{
			isValid = false;
		}

		public static implicit operator OptionalValue<T>(T value)
		{
			return new OptionalValue<T>(value);
		}
	}
}
