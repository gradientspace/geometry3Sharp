using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace g3.core;

/// <summary>
/// Modern .NET 8 implementation of gParallel using the Task Parallel Library (TPL).
/// </summary>
public class gParallelNew {
    public static void ForEach<T>(IEnumerable<T> source, Action<T> body) {
        Parallel.ForEach(source, body);
    }

    public static void Evaluate(params Action[] funcs) {
        Parallel.Invoke(funcs);
    }

    public static void BlockStartEnd(int iStart, int iEnd, Action<int, int> blockF, int iBlockSize = -1, bool bDisableParallel = false) {
        int N = (iEnd - iStart + 1);
        if (N <= 0) return;

        if (iBlockSize == -1)
            iBlockSize = 2048;

        int num_blocks = (N + iBlockSize - 1) / iBlockSize;

        if (bDisableParallel) {
            for (int bi = 0; bi < num_blocks; ++bi) {
                int start = iStart + bi * iBlockSize;
                int end = Math.Min(start + iBlockSize - 1, iEnd);
                blockF(start, end);
            }
        }
        else {
            Parallel.For(0, num_blocks, (bi) => {
                int start = iStart + bi * iBlockSize;
                int end = Math.Min(start + iBlockSize - 1, iEnd);
                blockF(start, end);
            });
        }
    }
}
