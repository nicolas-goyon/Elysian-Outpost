# Performance measurements

This folder contains the benchmarks procedure and the measured results of the core library (#43), and the format of the performance profiles of the app (#80).
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

## Performance profiles of the app

The app records the performance of each frame of a session in a CSV file (#80). The benchmarks measure the core library alone. A profile measures the full app: the window, the GPU and the world generation.

### Record a profile

- In the app, open the console (see the console key in the README) and enter `profile start`. Enter `profile stop` to stop the profile.
- Or start the app with the option `--profile`. The profile starts with the main loop and stops when the window closes. With `--exit-after 60`, the session takes 60 seconds:

  ```shell
  dotnet run -c Release --project src/ElysianOutpost.App -- --profile --exit-after 60
  ```

At the stop, the app writes the file `profiles/profile-<date>-<time>.csv` in the folder of the executable. The date and the time are the local time of the start, for example `profile-20261107-232926.csv`. The log gets a summary: the number of frames, the average, the median, the 1 % low and the maximum frame time, the average GPU time, the garbage collections, and the cost of the profiler.

The 1 % low is the mean frame time of the slowest 1 % of the frames, and of one frame or more.

### File format

The file has one header line, then one line for each frame. The separator is a comma, and the decimal separator is a period. All times are in milliseconds, with 3 decimals.

| Column | Value |
|---|---|
| `time_ms` | The start of the frame, from the start of the profile. |
| `frame_ms` | The time from the start of the frame to the start of the next frame. It contains the wait for the next frame. The last frame of a profile has no next frame, thus the file does not contain it. |
| `events_ms` | The window events (GLFW). |
| `update_ms` | The update of the user interface, the camera and the voxel editor. |
| `tick_ms` | The simulation ticks of the frame. |
| `ticks` | The number of simulation ticks of the frame. |
| `world_ms` | The chunk work of the main thread: the worker results and the GPU uploads (#47, #6). |
| `render_ms` | The renderer on the CPU: the ImGui frame, the wait for a free frame in flight, the commands and the presentation. |
| `gpu_ms` | The GPU time between the first and the last command of a frame, from Vulkan timestamps. It is the time of an earlier frame: the frame that used the same frame slot, 2 frames before. The field is empty when the time is not known, for example in the first frames or on a GPU queue without timestamps. |
| `managed_bytes` | The managed memory after the frame (`GC.GetTotalMemory(false)`). |
| `gc0`, `gc1`, `gc2` | The total number of garbage collections of each generation, since the start of the process. |
| `gpu_bytes` | The GPU memory of the VMA allocations. The app measures it two times in each second. |
| `chunk_queue` | The number of chunk meshes that wait for the GPU upload (#6). |

The profiler keeps the values in memory, and writes the file only at the stop. Its cost in each frame is in the summary of the log. On lavapipe with 2 CPU cores, it is approximately 0.006 ms.
