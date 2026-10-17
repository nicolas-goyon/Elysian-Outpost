# 0001. Move from Unity to BRUTAL

- Status: Accepted
- Date: 2026-10-16
- Issue: #25

## Context

Elysian Outpost is a voxel colony game, like Stonehearth. The game has two main goals:

1. A pathfinding that keeps a memory of earlier paths, like a pheromone trail.
2. Good performance on low-end computers, with Intel UHD graphics at most.

The legacy project uses Unity 6.3 beta (6000.3.0b10) and the Universal Render Pipeline. A review of the legacy code in October 2026 found these facts:

- The mesher and the terrain generation are plain C# code. They use few Unity APIs.
- Each chunk is a GameObject with a MeshCollider and a NavMeshSurface. A voxel edit destroys the GameObject of the chunk and creates a new one.
- The citizens use the Unity NavMesh. The pheromone pathfinding does not exist yet.
- The main performance defects are in our code, not in Unity (#5, #47). A move to another engine does not solve them.
- The legacy code generates the chunks on one plain thread.

In Unity, a fast voxel world probably needs its own data outside the GameObject model. It probably also needs the C# Job System and Burst for the heavy work.

Unity runs C# scripts on Mono, or on IL2CPP in builds. Unity 6.7 LTS is the last version on Mono. Unity plans a CoreCLR runtime for Unity 7, and the main performance work for the LTS version after Unity 7. Creative Bloq (July 2026) expects the first beta of Unity 7 in December 2026, and the release in the first quarter of 2027.

The owner wants full control of the data, the threads and the main loop. The owner also wants less dependency on the decisions of one engine company.

## Options

### Option 1: Stay on Unity

Advantages:

- Unity has an editor, many tools and a large community.
- The legacy project runs today.
- Unity released Unity 6.3 LTS in December 2025. The project can leave the beta version.
- Unity 7 brings the CoreCLR runtime, probably in 2027.

Disadvantages:

- A fast voxel world probably needs code outside the GameObject model of the engine.
- Until Unity 7, the plain C# code runs on Mono.
- The project depends on the decisions of Unity Technologies.

### Option 2: Godot 4 with C#

Advantages:

- Godot is open source (MIT License), and it has an editor.
- Godot runs C# on modern .NET.
- Godot has a Compatibility renderer on OpenGL for old GPUs.
- A voxel module exists: Voxel Tools, by Zylann.

Disadvantages:

- We must learn a new engine and port all the game code to the node model of Godot.
- The voxel module is C++ code. Our mesher and our terrain generation are C# code.

### Option 3: A custom engine in C++ or Rust

Advantages:

- A custom engine gives full control and native performance.
- Rust gives memory safety.

Disadvantages:

- We must rewrite all the code in a new language.
- The work on the window, the GPU code and the tools probably takes months before new gameplay.
- An example of the risk: the Hytale team stopped a new engine, which was a full rewrite. For the early access of January 2026, the team returned to the original version of the game.

### Option 4: BRUTAL

BRUTAL is a C# framework from Ahwoo and RocketWerkz, the companies of Dean Hall. It is not an engine. It gives bindings to native libraries, for example Vulkan, OpenGL, GLFW, SDL, Dear ImGui, Jolt and FMOD. It also gives libraries for memory, collections, strings, jobs, logging and localization.

Advantages:

- BRUTAL uses C# on .NET 10. The mesher and the terrain generation move with few changes (#31, #33).
- We get full control of the memory, the threads and the main loop.
- Simulation games use BRUTAL: Kitten Space Agency (RocketWerkz), and Waylith, the new engine of RimWorld (Ludeon Studios).
- Ludeon tests RimWorld 1.7 on Waylith in a public beta. Ludeon reports 2 to 4 times the performance of the Unity version, in its own tests.
- BRUTAL is free, with no royalties.

Disadvantages:

- BRUTAL has no editor, no scene graph and no renderer. We write the renderer, the UI and the tools.
- BRUTAL is a young public project (October 2026). The documentation is short, and the repository has no build files yet.
- BRUTAL supports Windows and Linux only.
- The BRUTAL License has conditions for each product: credits, a logo and a notice file.

## Decision

We move the game from Unity to BRUTAL. We keep C#. We use Vulkan 1.3 for the renderer.

The work follows this order:

1. M1: port the mesher and the terrain generation to the core library of a .NET 10 solution.
2. M1: write a first renderer on BRUTAL.
3. M2: fix and optimize the core library.
4. M3: write the pheromone pathfinding.
5. M4 and later: write the systems of the game.

Reasons:

- The mesher and the terrain generation are C# code, so the port is probably short.
- The game needs control of the data and of the threads more than an editor.
- RimWorld 1.7 probably shows that a simulation game can move from Unity to an engine on BRUTAL. This move is in a public beta now.

## Consequences

Constraints:

- Developers need the .NET 10 SDK. Players need a GPU with Vulkan 1.3 and current drivers.
- The game runs on Windows and Linux only.
- BRUTAL has no editor, no scene graph and no renderer. We write the renderer (#39), the debug UI (#36) and the game UI (#83).
- The BRUTAL License v1.0 gives three conditions for each product (sections 2.1 to 2.3). #42 applies them:
  1. The credits screen contains the BRUTAL copyright notice.
  2. The BRUTAL logo shows before the gameplay starts.
  3. Each copy of the product contains the file `NOTICE.txt`.
- On 2026-10-17, the owner selected MPL-2.0 for the code, not GPL-3.0 (#26). The reason:
  - Sections 2.2 (logo) and 6.0 (New Zealand law and courts) of the BRUTAL License are probably further restrictions under GPL-3.0 sections 7 and 10.
  - The BRUTAL README offers to waive the logo for non-profit projects. This waiver does not change section 6.0.

Advantages:

- The core library does not depend on an engine. The CI can test it without a GPU (#16).
- The game code runs on the current .NET runtime.

Risks:

- BRUTAL is young and can change. To decrease this risk, the core library uses only the base packages of BRUTAL, for example numerics and logging (#16, #31). Thus a move of the core library to another framework stays possible.
- We write more code: the renderer, the UI and the tools. To decrease this risk, we use small milestones and Dear ImGui for the debug tools.
- An old integrated GPU can lack Vulkan 1.3. The hardware report (#81) records the Vulkan version. Check the reference computer first (#44). BRUTAL also has OpenGL bindings, so an OpenGL renderer is a possible fallback.

## References

- BRUTAL: https://github.com/ahwootech/BRUTAL
- BRUTAL License v1.0: https://github.com/ahwootech/BRUTAL/blob/main/LICENSE.txt
- DayZ creator presents BRUTAL (80.lv): https://80.lv/articles/dayz-creator-presents-brutal-a-free-open-source-development-framework
- RimWorld moves to the Waylith engine (PC Gamer, 13 October 2026): https://www.pcgamer.com/gaming-industry/game-development/rimworld-drops-unity-for-its-own-in-house-engine-says-your-saves-still-work-so-do-most-mods-and-the-whole-thing-runs-twice-as-fast/
- Unity: CoreCLR, scripting and serialization update (June 2026): https://discussions.unity.com/t/coreclr-scripting-and-serialization-update-june-2026/1723299
- Unity 7 schedule (Creative Bloq, July 2026): https://creativebloq.com/3d/video-game-design/unity-7s-most-surprising-advance-isnt-a-feature-upgrade
- Unity 6.3 LTS (Unity, December 2025): https://unity.com/blog/unity-6-3-lts-is-now-available
- Voxel Tools for Godot: https://github.com/Zylann/godot_voxel
- Hytale early access (Hytale, November 2025): https://hytale.com/news/2025/11/hytale-early-access-january-13-2026
