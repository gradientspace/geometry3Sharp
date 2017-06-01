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
        public virtual void Start()
        {
            Watch.Start();
        }
        public virtual void Stop()
        {
            Watch.Stop();
        }
        public virtual void Accumulate()
        {
            Watch.Stop();
            Accumulated += Watch.Elapsed;
        }
        public virtual void Reset()
        {
            Watch.Stop();
            Watch.Reset();
            Watch.Start();
        }
        public override string ToString()
        {
            TimeSpan t = Watch.Elapsed;
            return string.Format("{0:fffffff}", Watch.Elapsed);
        }
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

        public void Stop(string label)
        {
            Timers[label].Stop();
        }

        public void StopAndAccumulate(string label)
        {
            Timers[label].Accumulate();
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


        public string Elapsed(string label)
        {
            return Timers[label].ToString();
        }
        public string Accumulated(string label)
        {
            return string.Format("{0:fffffff}", Timers[label].Accumulated);
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
                b.Append(label + ": " + string.Format("{0:ss}.{0:ffffff}", Timers[label].Watch.Elapsed) + separator);
            }
            return b.ToString();
        }

        public string AllAccumulatedTimes(string prefix = "Times:", string separator = " ")
        {
            StringBuilder b = new StringBuilder();
            b.Append(prefix + " ");
            foreach ( string label in Order ) {
                b.Append(label + ": " + string.Format("{0:ss}.{0:ffffff}", Timers[label].Accumulated) + separator);
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
