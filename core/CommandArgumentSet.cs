using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace g3
{
    // this is a utility class for parsing command-line arguments, but can
    // also be used for other things (eg like constructing command-line arguments!)
    public class CommandArgumentSet
    {
        // expectation is that these arguments will appear as like
        //  -samples 7   or -output /some/kind/of/path
        public Dictionary<string, int> Integers = new Dictionary<string, int>();
        public Dictionary<string, float> Floats = new Dictionary<string, float>();

        // these arguments can only have one string as a value. Need more? extend this class.
        public Dictionary<string, string> Strings = new Dictionary<string, string>();

        // these are flags that don't have an argument, like -disable-something
        public Dictionary<string, bool> Flags = new Dictionary<string, bool>();

        // if we saw an argument string it gets added here, so you can check if
        // it has the default value w/o having to remember all the defaults
        public HashSet<string> SawArguments = new HashSet<string>();

        // assumption is that any string that isn't a flag or parameter is a filename
        public List<string> Filenames = new List<string>();


        public void Register(string argument, int defaultValue) {
            Integers.Add(argument, defaultValue);
        }
        public void Register(string argument, float defaultValue) {
            Floats.Add(argument, defaultValue);
        }
        public void Register(string argument, bool defaultValue) {
            Flags.Add(argument, defaultValue);
        }
        public void Register(string argument, string defaultValue) {
            Strings.Add(argument, defaultValue);
        }


        public bool Saw(string argument)
        {
            return SawArguments.Contains(argument);
        }


		public bool Validate(string sParam, params string[] values) {
			if ( Strings.ContainsKey(sParam) == false )
				throw new Exception("Argument set does not contain " + sParam);
			string s = Strings[sParam];
			for ( int i = 0; i < values.Length; ++i ) {
				if (s == values[i])
					return true;
			}
			return false;
		}


        public bool Parse(string[] arguments)
        {
            int N = arguments.Length;
            int i = 0;
            while ( i < N ) {
                string arg = arguments[i];
                i++;

                if (Integers.ContainsKey(arg)) {
                    SawArguments.Add(arg);
                    if (i == N) {
                        error_missing_argument(arg);
                        return false;
                    }
                    string value = arguments[i];
                    int nValue;
                    if (int.TryParse(value, out nValue) == false) {
                        error_invalid_value(arg, value);
                        return false;
                    }
                    Integers[arg] = nValue;
                    i++;

                } else if (Floats.ContainsKey(arg)) {
                    SawArguments.Add(arg);
                    if (i == N) {
                        error_missing_argument(arg);
                        return false;
                    }
                    string value = arguments[i];
                    float fValue;
                    if (float.TryParse(value, out fValue) == false) {
                        error_invalid_value(arg, value);
                        return false;
                    }
                    Floats[arg] = fValue;
                    i++;

                } else if (Strings.ContainsKey(arg)) {
                    SawArguments.Add(arg);
                    if (i == N) {
                        error_missing_argument(arg);
                        return false;
                    }
                    string value = arguments[i];
                    Strings[arg] = value;
                    i++;

                } else if (Flags.ContainsKey(arg)) {
                    SawArguments.Add(arg);
                    Flags[arg] = true;

                } else {

                    Filenames.Add(arg);
                }

            }

            return true;
        }



        virtual protected void error_missing_argument(string arg)
        {
            System.Console.WriteLine("argument {0} is missing value", arg);
        }
        virtual protected void error_invalid_value(string arg, string value)
        {
            System.Console.WriteLine("argument {0} has invalid value {1}", arg, value);
        }

    }
}
