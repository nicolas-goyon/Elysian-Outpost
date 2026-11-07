# Performance measurements

This folder contains the benchmarks procedure and the measured results of the core library (#43).
M2 changes the core library for speed and memory. Each optimization pull request gives the benchmark results before and after its change.

| File | Content |
|---|---|
| [baseline.md](baseline.md) | The results of the code before the first optimization of M2. |

## The benchmarks

The project `tests/ElysianOutpost.Benchmarks` uses [BenchmarkDotNet](https://benchmarkdotnet.org/). All the chunks have the size of the sample scene: 16 × 64 × 16 voxels.

| Benchmark | What it measures |
|---|---|
| `ChunkBenchmarks.GenerateChunk` | The terrain generation of one chunk (seed 123, the chunk at (0, 0, 0)). |
| `ChunkBenchmarks.MeshTerrainChunk` | The mesh of the same terrain chunk, with the conversion to the GPU vertex data. |
| `ChunkBenchmarks.MeshCheckerboardChunk` | The mesh of a checkerboard chunk: the worst case of the mesher. Each solid voxel has six visible faces, and no face can merge. |
| `WorldBenchmarks.BuildDefaultWorld` | The generation and the mesh of the 1024 chunks of the sample scene, on one thread, with the world builder. |
| `WorldBenchmarks.StreamDefaultWorld` | The same world with the chunk streamer (#47), as the app does at the start. The parameter `Workers` gives the number of worker threads. |

All the benchmarks use the memory diagnoser. The column `Allocated` gives the managed memory that one operation allocates.

The program also measures the managed memory of the full default world after a full garbage collection (`memory`).

## Procedure

1. Close the other applications. A browser or a build in the background changes the results.
2. Connect the power supply of a laptop, and select the high-performance power mode.
3. Run all the benchmarks and the memory measurement from the root of the repository:

   ```shell
   dotnet run -c Release --project tests/ElysianOutpost.Benchmarks
   ```

   The full run takes approximately 4 minutes on a computer with 2 CPU cores.

4. To run some benchmarks only, give a BenchmarkDotNet filter:

   ```shell
   dotnet run -c Release --project tests/ElysianOutpost.Benchmarks -- --filter "*Mesh*"
   ```

5. To measure only the memory of the full default world:

   ```shell
   dotnet run -c Release --project tests/ElysianOutpost.Benchmarks -- memory
   ```

6. BenchmarkDotNet writes the reports in `BenchmarkDotNet.Artifacts/results/`. Git ignores this folder.

## Rules for a comparison

- Compare only the results of the same computer. The file of the results gives the CPU, the RAM and the operating system.
- Run the benchmarks of the two versions one after the other, with no change of the computer.
- For a pull request, give the table of BenchmarkDotNet for the two versions, and the output of the memory measurement.
- A change of less than 5 % on a computer of the cloud is not significant. The other virtual machines on the same host change the speed.
