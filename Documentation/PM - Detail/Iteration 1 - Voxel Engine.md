# General
**Work time** : 2 to 3 weeks

**Kanban tasks** :
- Research voxel engines and their implementation in Unity.
- Choose a data structure for storing voxel data (e.g., octree, sparse voxel octree).
- Set up a basic Unity project with the chosen data structure.
- Implement a basic voxel renderer to visualize the voxel data.
- Implement voxel modification tools (add, remove, modify voxels).
- Test the voxel engine to ensure it works correctly.



# Detail 


**Existing Voxel Implementations in Unity**:

1. **Unity's Experimental DOTS-based Voxel System**: Unity has been developing a voxel system based on the Data-Oriented Technology Stack (DOTS). It's still in the experimental phase but offers high performance due to its use of Burst Compiler, Entity Component System (ECS), and C# Job System.
2. **Voxel Play**: Voxel Play is a free, open-source voxel engine for Unity. It supports LOD, mesh optimization, and multithreading. However, it's no longer actively maintained.
3. **Cubiquity for Unity**: Cubiquity is a commercial voxel engine that supports Unity integration. It offers features like LOD, culling, and multithreading, but it's not free.
4. **MagicaVoxel**: MagicaVoxel is a free voxel art editor and interactive path tracing renderer. While it's not a voxel engine for Unity, you can export your voxel models as .obj files and import them into Unity. 
   TODO : Is MagicaVoxel's ".obj" are the same as other's Voxel editors objects ? like can someone using MagicaVoxel and another using another editor can create assets that could be used in the same project ? 

**Early Optimizations and Key Considerations**:

1. **Data Structure**: Choose an appropriate data structure for storing voxel data, such as an octree or sparse voxel octree. These data structures can help minimize memory usage and improve performance by only storing relevant data.
2. **Chunking**: Divide the world into smaller chunks to manage memory usage and rendering performance. Only load and render chunks near the player, and unload/cull chunks that are far away.
3. **Mesh Generation**: Optimize mesh generation by merging adjacent voxels with the same material and reducing the number of vertices and triangles.
4. **Level of Detail (LOD)**: Implement LOD techniques to reduce the complexity of meshes as they move further away from the camera. This can significantly improve rendering performance.
5. **Occlusion Culling**: Use occlusion culling to avoid rendering voxels that are hidden behind other voxels or objects. This can help improve rendering performance, especially in dense environments.
6. **Multithreading**: Use multithreading to distribute processing tasks across multiple CPU cores. This can help improve performance for tasks like mesh generation, noise generation, and pathfinding.
7. **Batching**: Use Unity's batching features to reduce draw calls and improve rendering performance. This includes static batching, dynamic batching, and GPU instancing.
8. **Memory Management**: Be mindful of memory allocation and deallocation. Use object pooling and memory pooling to avoid frequent allocations and reduce garbage collection overhead.

# Choices

## General 

Each objects and things should be handled the most as some microservices, they should have the least dependencies from one to the others. MUST APPLY SOLID PRINCIPLES. For each interaction, an interface should be used and cleared declared methods and specifications should be done.

## Voxel data storage

TODO : I don't know what to store in the voxel, because i would like to store what kind of block is it (not only the color) and the level pheromone but i think it should be stored in another data structure because its not linked to the voxel display and engine.

## Engine 
TODO
Should I implement the voxel system for the players and little assets ? like having everything made by voxel of different scale ? Or should i use assets made by MagicaVoxel or things like this to have ".obj" assets and then doing things to them ?

TODO 
Should I implement a physics engine inside the Voxel engine or should i put them appart and having it like micro-services that interact but not coupled ? Isn't it too hard to make ?  

TODO 
Is it the job to the Voxel engine to know what block is displayed ? or is it the core that only send to the engine what block should be displayed ? Like does the core send every blocs and the engine does whatever or is it the engine to filter things ? or both ?

## Storage

### Map spec 
- Needs to be stored at least with chunks so the procedural generation will be working easily, by just passing the area to generate.
- Each chunk should be small enough to be generated easily but not too small to generate a good amount each time.
- A 16 by 16 blocs by chunk seems to be reasonable, this means for a height of 128 this means having : 32,768 block per chunk. One way to helping the generation, we could check the generation for each 16 x 16 x 16 1st chunk layer if its needed. Like, most of time we won't need to generate first couple of 1st chunk layer at the bottom of the map.
- TODO : The map storage and generation should be scalable, if the height change, we should be able to scale the model without the need to redo everything.


### **Map storage**
Changed areas are stored, TODO : needs to find a way to store it, should i store only changed on destroyed items for each for space economy ? or should i store the entire area  for algorithmic simplicity ? Maybe not storing only the changes because it can have issues if the model changes, the existing building could be under the floor or above it, instead, having chunk "glitches" would be easier to the player to manage.


### **On memory storage** 
An Octree seems to be a good way to store chunks of the map. I mostly think a tree matching like this could be nice : 

- A chunk (8 of 1st chunk layer blocks stacked vertically)
- 1st chunk layer (16 x 16 x 16)
- 2dn chunk layer (8 x 8 x 8)
- 3rd chunk layer (4 x 4 x 4)
- 4rth chunk layer (2 x 2 x 2)
- A cube (leaf)


## Level of Detail 
TODO : 
I think blocks should have only one color, assets are blocks with 1/16 or 1/32 ratio in my opinion. but still don't know if i should use some assets(.obj) and prefab or should i use voxels for assets ?




# Modeling

Here is a Drawio of the current modeling : 
- https://drive.google.com/file/d/1G88aJrY81_tYl7XVz7-ToLaw_Sz5Yqzn/view?usp=sharing


## Detail 

### World Simulation 

#### Simulation -> World Generation 
- Generate block (Position)
- Generate chunk (Position)

#### Simulation -> Entity Engine
- Display entities

#### Simulation -> Voxel Engine
- Display voxels

#### Simulation -> Block

This is needed to create voxels for the Voxel Engine.

- GetPosition
- GetType
- Get Pheromon Level

### Player 

#### Player -> World Simulation

Incomplete !

- Place template to world
- Select Entities
- Pause game
- Command Entity

### Entity 

#### Entity -> PathFinding

- GetPathToObjective