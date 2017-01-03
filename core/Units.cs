using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace g3
{
    static public class Units
    {
        public enum Angular
        {
            Degrees,
            Radians
        }

        public enum Linear
        {
            // numbers are 20 + power-of-ten  
            Nanometers = 11,        // 10^-9
            Microns = 14,           // 10^-6
            Millimeters = 17,       // 10^-3
            Centimeters = 18,       // 10^-2
            Meters = 20,            // 10^0
            Kilometers = 23,        // 10^3

            // metric values must be < 50

            Inches = 105,
            Feet = 109,
            Yards = 110,
            Miles = 115
        }



        public static bool IsMetric(Linear t)
        {
            return (int)t < 50;     // based on enum above
        }


        public static double GetMetricPower(Linear t)
        {
            if ((int)t < 50)
                return (double)t - 20.0;        // based on enum values above
            throw new Exception("Units.GetMetricPower: input unit is not metric!");
        }


        public static double ToMeters(Linear t)
        {
            if ( (int)t < 50 ) {
                double d = GetMetricPower(t);
                return Math.Pow(10, d);
            }
            switch(t) {
                case Linear.Inches:
                    return 0.0254;
                case Linear.Feet:
                    return 0.3048;
                case Linear.Yards:
                    return 0.9144;
                case Linear.Miles:
                    return 1609.34;
            }
            throw new Exception("Units.ToMeters: input unit is not handled!");
        }


        public static double MetersTo(Linear t)
        {
            if ( (int)t < 50 ) {
                double d = GetMetricPower(t);
                return Math.Pow(10, -d);
            }
            switch(t) {
                case Linear.Inches:
                    return 1.0 / 0.0254;
                case Linear.Feet:
                    return 1.0 / 0.3048;
                case Linear.Yards:
                    return 1.0 / 0.9144;
                case Linear.Miles:
                    return 1.0 / 1609.34;
            }
            throw new Exception("Units.ToMeters: input unit is not handled!");
        }


        public static double Convert(Linear from, Linear to)
        {
            if (from == to)
                return 1.0;
            if ( IsMetric(from) && IsMetric(to) ) {
                double pfrom = GetMetricPower(from);
                double pto = GetMetricPower(to);
                double d = pto - pfrom;
                return Math.Pow(10, d);
            }

            return ToMeters(from) * MetersTo(to);
        }



    }
}
