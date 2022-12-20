using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace g3
{

    public class BlockTimer
    {
        public Stopwatch Watch;
        public string Label;
        public TimeSpan Accumulated;

        public BlockTimer(string label, bool bStart)
        {
            Label = label;
            Watch = new Stopwatch();
            if (bStart)
                Watch.Start();
            Accumulated = TimeSpan.Zero;
        }
        public void Start()
        {
            Watch.Start();
        }
        public void Stop()
        {
            Watch.Stop();
        }
        public bool Running
        {
            get { return Watch.IsRunning; }
        }

        public void Accumulate(bool bReset = false)
        {
            Watch.Stop();
            Accumulated += Watch.Elapsed;
            if (bReset)
                Watch.Reset();
        }
        public void Reset()
        {
            Watch.Stop();
            Watch.Reset();
            Watch.Start();
        }

        public string AccumulatedString
        {
            get { return string.Format(TimeFormatString(Accumulated), Accumulated); }
        }
        public override string ToString()
        {
            TimeSpan t = Watch.Elapsed;
            return string.Format(TimeFormatString(Accumulated), Watch.Elapsed);
        }

        public static string TimeFormatString(TimeSpan span)
        {
            if (span.Minutes > 0)
                return minute_format;
            else
                return second_format;
        }
        const string minute_format = "{0:mm}:{0:ss}.{0:fffffff}";
        const string second_format = "{0:ss}.{0:fffffff}";
    }



    public class LocalProfiler : IDisposable
    {
        Dictionary<string, BlockTimer> Timers = new Dictionary<string, BlockTimer>();
        List<string> Order = new List<string>();

        public BlockTimer Start(string label)
        {
            if (Timers.ContainsKey(label)) {
                Timers[label].Reset();
            } else {
                Timers[label] = new BlockTimer(label, true);
                Order.Add(label);
            }
            return Timers[label];
        }


        public BlockTimer StopAllAndStartNew(string label)
        {
            StopAll();
            return Start(label);
        }

        public BlockTimer Get(string label)
        {
            return Timers[label];
        }


        public void Stop(string label)
        {
            Timers[label].Stop();
        }

        public void StopAll()
        {
            foreach (BlockTimer t in Timers.Values)
                if ( t.Running )
                    t.Stop();
        }


        public void StopAndAccumulate(string label, bool bReset = false)
        {
            Timers[label].Accumulate(bReset);
        }

        public void Reset(string label)
        {
            Timers[label].Reset();
        }

        public void ResetAccumulated(string label)
        {
            Timers[label].Accumulated = TimeSpan.Zero;
        }

        public void ResetAllAccumulated(string label)
        {
            foreach (BlockTimer t in Timers.Values)
                t.Accumulated = TimeSpan.Zero;
        }

        public void DivideAllAccumulated(int div)
        {
            foreach (BlockTimer t in Timers.Values) {
                t.Accumulated = new TimeSpan(t.Accumulated.Ticks / div);
            }
        }


        public string Elapsed(string label)
        {
            return Timers[label].ToString();
        }
        public string Accumulated(string label)
        {
            TimeSpan accum = Timers[label].Accumulated;
            return string.Format(BlockTimer.TimeFormatString(accum), accum);
        }

        public string AllTicks(string prefix = "Times:")
        {
            StringBuilder b = new StringBuilder();
            b.Append(prefix + " ");
            foreach ( string label in Order ) {
                b.Append(label + ": " + Timers[label].ToString() + " ");
            }
            return b.ToString();
        }

        public string AllAccumulatedTicks(string prefix = "Times:")
        {
            StringBuilder b = new StringBuilder();
            b.Append(prefix + " ");
            foreach ( string label in Order ) {
                b.Append(label + ": " + Accumulated(label) + " ");
            }
            return b.ToString();
        }



        public string AllTimes(string prefix = "Times:", string separator = " ")
        {
            StringBuilder b = new StringBuilder();
            b.Append(prefix + " ");
            foreach ( string label in Order ) {
                TimeSpan span = Timers[label].Watch.Elapsed;
                b.Append(label + ": " + string.Format(BlockTimer.TimeFormatString(span), span) + separator);
            }
            return b.ToString();
        }

        public string AllAccumulatedTimes(string prefix = "Times:", string separator = " ")
        {
            StringBuilder b = new StringBuilder();
            b.Append(prefix + " ");
            foreach ( string label in Order ) {
                TimeSpan span = Timers[label].Accumulated;
                b.Append(label + ": " + string.Format(BlockTimer.TimeFormatString(span), span) + separator);
            }
            return b.ToString();
        }



        public void Dispose()
        {
            foreach (var timer in Timers.Values)
                timer.Stop();
            Timers.Clear();
        }
    }




}
