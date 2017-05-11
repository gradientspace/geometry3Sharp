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
        public BlockTimer(string label, bool bStart)
        {
            Label = label;
            Watch = new Stopwatch();
            if (bStart)
                Watch.Start();
        }
        public virtual void Start()
        {
            Watch.Start();
        }
        public virtual void Stop()
        {
            Watch.Stop();
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

        public void Reset(string label)
        {
            Timers[label].Reset();
        }

        public string Elapsed(string label)
        {
            return Timers[label].ToString();
        }


        public string AllTimes(string prefix)
        {
            StringBuilder b = new StringBuilder();
            b.Append(prefix + " ");
            foreach ( string label in Order ) {
                b.Append(label + ": " + Timers[label].ToString() + " ");
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
