// SPDX-License-Identifier: MPL-2.0

using BenchmarkDotNet.Running;
using ElysianOutpost.Benchmarks;

// "memory": measure the managed memory of the full default world (#43). Other arguments go to BenchmarkDotNet.
// With no argument, the program runs all the benchmarks, then the memory measurement.
if (args is ["memory"])
{
    WorldMemory.Run();
    return;
}

BenchmarkSwitcher.FromAssembly(typeof(ChunkBenchmarks).Assembly).Run(args.Length == 0 ? ["--filter", "*"] : args);
if (args.Length == 0)
{
    WorldMemory.Run();
}
