using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

#if !G3_USING_UNITY
using System.Threading.Tasks;
#endif

namespace g3
{
    public class gParallel
    {

        public static void ForEach<T>( IEnumerable<T> source, Action<T> body )
        {
#if G3_USING_UNITY
            for_each<T>(source, body);
#else
            Parallel.ForEach<T>(source, body);
#endif
        }


        // parallel for-each that will work on .net 3.5 (maybe?)
        // adapted from https://www.microsoft.com/en-us/download/details.aspx?id=19222
        public static void for_each<T>( IEnumerable<T> source, Action<T> body )
        {
            int numProcs = Environment.ProcessorCount;
            int remainingWorkItems = numProcs;
            using (var enumerator = source.GetEnumerator()) {
                using (ManualResetEvent mre = new ManualResetEvent(false)) {
                    // Create each of the work items.
                    for (int p = 0; p < numProcs; p++) {
                        ThreadPool.QueueUserWorkItem(delegate {
                            // Iterate until there's no more work.
                            while (true) {
                                // Get the next item under a lock,
                                // then process that item.
                                T nextItem;
                                lock (enumerator) {
                                    if (!enumerator.MoveNext())
                                        break;
                                    nextItem = enumerator.Current;
                                }
                                body(nextItem);
                            }
                            if (Interlocked.Decrement(ref remainingWorkItems) == 0)
                                mre.Set();
                        });
                    }
                    // Wait for all threads to complete.
                    mre.WaitOne();
                }
            }
        }


    }
}
