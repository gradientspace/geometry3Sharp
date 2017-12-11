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

        public static void ForEach_Sequential<T>(IEnumerable<T> source, Action<T> body)
        {
            foreach (T v in source)
                body(v);
        }
        public static void ForEach<T>( IEnumerable<T> source, Action<T> body )
        {
#if G3_USING_UNITY
            for_each<T>(source, body);
#else
            Parallel.ForEach<T>(source, body);
#endif
        }


        /// <summary>
        /// Evaluate input actions in parallel
        /// </summary>
        public static void Evaluate(params Action[] funcs)
        {
            int N = funcs.Length;
            gParallel.ForEach(Interval1i.Range(N), (i) => {
                funcs[i]();
            });
        }



        /// <summary>
        /// Process indices [iStart,iEnd], inclusive, by passing sub-intervals [start,end] to blockF.
        /// Blocksize is automatically determind unless you specify one.
        /// </summary>
        public static void BlockStartEnd(int iStart, int iEnd, Action<int,int> blockF, int iBlockSize = -1, bool bDisableParallel = false )
        {
            if (iBlockSize == -1)
                iBlockSize = 100;  // seems to work
            int N = (iEnd - iStart + 1);
            int num_blocks = N / iBlockSize;
            // process main blocks in parallel
            if (bDisableParallel) {
                ForEach_Sequential(Interval1i.Range(num_blocks), (bi) => {
                    int k = iStart + iBlockSize * bi;
                    blockF(k, k + iBlockSize - 1);
                });
            } else {
                ForEach(Interval1i.Range(num_blocks), (bi) => {
                    int k = iStart + iBlockSize * bi;
                    blockF(k, k + iBlockSize - 1);
                });
            }
            // process leftover elements
            int remaining = N - (num_blocks * iBlockSize);
            if (remaining > 0) {
                int k = iStart + num_blocks * iBlockSize;
                blockF(k, k+remaining-1);
            }
        }



        // parallel for-each that will work on .net 3.5 (maybe?)
        // adapted from https://www.microsoft.com/en-us/download/details.aspx?id=19222
        static void for_each<T>( IEnumerable<T> source, Action<T> body )
        {
            int numProcs = Environment.ProcessorCount;
            int remainingWorkItems = numProcs;
            Exception last_exception = null;
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

                                // [RMS] added try/catch here because if body() throws an exception
                                // then one thread breaks out of loop, and somehow we end up hung forever.
                                // (Maybe the WaitOne() on other threads never finishes?)
                                // Anyway, we will just hold onto the exception and throw it at the end.
                                try {
                                    body(nextItem);
                                } catch (Exception e) {
                                    last_exception = e;
                                    break;
                                }
                            }
                            if (Interlocked.Decrement(ref remainingWorkItems) == 0)
                                mre.Set();
                        });
                    }
                    // Wait for all threads to complete.
                    mre.WaitOne();
                }
            }

            // pass on last exception thrown by enumerables
            if (last_exception != null)
                throw last_exception;
        }


    }








    // the idea for this class is that it provides a clean way to
    // process data through a stream of operations, where the ordering
    // of data members must be maintained. You provide a Producer and Consumer
    // as lambdas, and then provide an enumerator to define the order.
    // 
    // The idea is that between Producer and Consumer can be N intermediate
    // stages (Operators). However this is not implemented yet.
    //
    // If the compute steps are too short, it seems like this approach is not effective.
    // I think this is because the memory overhead of the Queue gets too high, as it
    // can get quite large, eg if the consumer is slower than the producer, or even
    // just can block (eg like writing to disk).
    // Also locking overhead will have some effect.
    //
    // Perhaps an alternative would be to use a fixed buffer of T?
    // However, if T is a class (eg by-reference), then this doesn't help as they still have to be allocated....
    public class ParallelStream<V, T>
    {
        public Func<V, T> ProducerF = null;
        //public List<Action<T>> Operators = new List<Action<T>>();
        public Action<T> ConsumerF = null;

        LockingQueue<T> store0 = new LockingQueue<T>();
        IEnumerable<V> source = null;


        // this is the non-threaded variant. useful for comparing/etc.
        public void Run_NoThreads( IEnumerable<V> sourceIn )
        {
            foreach ( V v in sourceIn ) {
                T product = ProducerF(v);
                //foreach (var op in Operators)
                //    op(product);
                ConsumerF(product);
            }
        }


        bool producer_done = false;
        AutoResetEvent consumer_done_event;

        //int max_queue_size = 0;

        public void Run( IEnumerable<V> sourceIn )
        {
            source = sourceIn;
            producer_done = false;
            consumer_done_event = new AutoResetEvent(false);

            Thread producer = new Thread(ProducerThreadFunc);
            producer.Name = "ParallelStream_producer";
            producer.Start();
            Thread consumer = new Thread(ConsumerThreadFunc);
            consumer.Name = "ParallelStream_consumer";
            consumer.Start();

            // wait for threads to finish
            consumer_done_event.WaitOne();

            //System.Console.WriteLine("MAX QUEUE SIZE " + max_queue_size);       
        }



        void ProducerThreadFunc() {
            foreach ( V v in source ) {
                T product = ProducerF(v);
                store0.Add(product);
            }
            producer_done = true;
        }


        void ConsumerThreadFunc()
        {
            // this just spins...is that a good idea??

            T next = default(T);
            while ( producer_done == false || store0.Count > 0 ) {
                //max_queue_size = Math.Max(max_queue_size, store0.Count);
                bool ok = store0.Remove(ref next);
                if (ok)
                    ConsumerF(next);
            }

            consumer_done_event.Set();
        }

    }










    // locking queue - provides thread-safe sequential add/remove/count to Queue<T>
    public class LockingQueue<T>
    {
        Queue<T> queue;
        object queue_lock;

        public LockingQueue()
        {
            queue = new Queue<T>();
            queue_lock = new object();
        }

        public bool Remove(ref T val)
        {
            lock (queue_lock) {
                if (queue.Count > 0) {
                    val = queue.Dequeue();
                    return true;
                } else {
                    return false;
                }
            }
        }

        public void Add(T obj)
        {
            lock (queue_lock) {
                queue.Enqueue(obj);
            }
        }

        public int Count {
            get {
                lock (queue_lock) {
                    return queue.Count;
                }
            }
        }
    }




#if G3_USING_UNITY && (NET_2_0 || NET_2_0_SUBSET)

    /*
     * .NET 3.5 (default in Unity) does not have SpinLock object, which we
     * are using in a few places. So provide a wrapper around Monitor.
     * Note that this is class and SpinLock is a struct, so this may cause
     * disasters, but at least things build...
     */
    public class SpinLock
    {
        object o;
        public SpinLock()
        {
            o = new object();
        }

        public void Enter(ref bool entered)
        {
            Monitor.Enter(o);
            entered = true;
        }

        public void Exit()
        {
            Monitor.Exit(o);
        }

    }


#endif


}
