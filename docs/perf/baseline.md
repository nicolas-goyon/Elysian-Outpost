# Performance baseline

The results of the core library before the first optimization of M2 (#43). The optimization pull requests of M2 compare their results with these values.

## Code and computer

| Item | Value |
|---|---|
| Commit | `0f2cf1d` (`develop` after the release v0.1.0) |
| Date | 2026-11-07 |
| Computer | A virtual machine of the cloud (the session of Claude Code) |
| CPU | Intel Xeon Processor, 2 virtual cores, AVX-512 |
| RAM | 8 GB |
| Operating system | Ubuntu 24.04.5 LTS, Linux 6.18 |
| .NET | .NET 10.0.14, X64 RyuJIT, concurrent workstation GC |
| BenchmarkDotNet | 0.15.2 |

This computer is not the reference computer of the low-end target. #44 measures the reference computer (the laptop of the owner). Compare only the results of the same computer.

## Chunk benchmarks

Default job of BenchmarkDotNet. One chunk has 16 × 64 × 16 voxels.

| Method | Mean | Error | StdDev | Median | Gen0 | Gen1 | Gen2 | Allocated |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| GenerateChunk | 360.0 µs | 4.67 µs | 4.14 µs | 358.1 µs | - | - | - | 32.05 KB |
| MeshTerrainChunk | 1,404.7 µs | 30.23 µs | 87.70 µs | 1,393.2 µs | 41.0156 | 41.0156 | 41.0156 | 1,161.71 KB |
| MeshCheckerboardChunk | 20,675.0 µs | 1,144.15 µs | 3,301.14 µs | 19,197.4 µs | 1000.0000 | 968.7500 | 906.2500 | 30,937.79 KB |

## Full default world

1024 chunks of the sample scene (seed 123), generated and meshed on one thread. Job: 1 launch, 1 warm-up, 5 measurements.

| Method | Mean | Error | StdDev | Gen0 | Gen1 | Gen2 | Allocated |
|---|---:|---:|---:|---:|---:|---:|---:|
| BuildDefaultWorld | 2.155 s | 0.1842 s | 0.0478 s | 29000 | 28000 | 26000 | 1.14 GB |

## Managed memory of the full default world

After a full garbage collection (`dotnet run -c Release --project tests/ElysianOutpost.Benchmarks -- memory`):

| Item | Value |
|---|---:|
| Chunks | 1024 |
| Quads | 24,522 |
| World builder and all the meshes | 35.4 MiB |
| World builder only (the voxel world) | 32.2 MiB |
| Vertex and index data of the meshes | 3.2 MiB |

## Findings

- **The mesher allocates much memory.** One terrain chunk allocates 1.1 MB, and the checkerboard chunk allocates 30 MB. The full world allocates 1.14 GB, for 35 MiB of data that stays. The Gen2 collections show that many of these objects live long enough to leave Gen0 and Gen1. #5 and #45 remove these allocations.
- **The mesh takes most of the time.** The mesh of a terrain chunk takes about 4 times the time of its generation (1.40 ms against 0.36 ms).
- **The voxel world uses 32 MiB.** Since #40, the app keeps the block types of each chunk in a `ushort[,,]` array: 2 bytes for each voxel. The memory problem of the legacy project (about 9 GB in Unity) came from the `Voxel` objects of the mesher input. In the app, these objects exist only during the mesh of one chunk. Thus they show in the allocations, not in the memory that stays.
