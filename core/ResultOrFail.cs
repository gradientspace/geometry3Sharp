using System;

namespace g3
{
    /**
     * ResultOrFail is a specialized Pair<Value, bool> that can be used 
     * to indicate whether a computed value is valid/invalid
     */
    public struct ResultOrFail<T>
    {
        T value;
        bool isValid;
        string[] messages = null;

        public T Value { get { return value; } }
        public bool IsValid { get { return isValid; } }

        public ResultOrFail(T valueIn)
        {
            value = valueIn;
            isValid = true;
        }

		public ResultOrFail()
		{
			isValid = false;
		}

        public ResultOrFail(bool b)
        {
            isValid = false;
        }

        public ResultOrFail(bool b, string[] errors)
        {
            isValid = false;
            messages = errors;
        }
    }
}
