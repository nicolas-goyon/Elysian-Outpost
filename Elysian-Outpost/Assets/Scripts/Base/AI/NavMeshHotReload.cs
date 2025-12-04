using System;
using System.Collections.Generic;
using System.Diagnostics;
using Base.InGameConsole;
using Base.Terrain;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using Debug = UnityEngine.Debug;

namespace Base.AI
{
    [RequireComponent(typeof(NavMeshSurface))]
    public class NavMeshHotReload : MonoBehaviour
    {
        private NavMeshSurface _navMeshSurface;
        private Bounds _bounds;
    
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        private void Start()
        {
            _navMeshSurface = GetComponent<NavMeshSurface>();
        }

        public void Init(FixedSizeTerrain fixedSizeTerrain)
        {
            _bounds = new Bounds(
                center: fixedSizeTerrain.GetWorldCenterPosition(),
                size: fixedSizeTerrain.GetWorldSize()
            );
            DebuggerConsole.ConsoleCommand cmd = new("navmesh_bake", "Rebuild the navmesh", (args) =>
            {
                _navMeshSurface.BuildNavMesh();
                DebuggerConsole.Log("NavMesh rebuilt.");
            });

            DebuggerConsole.AddCommand(cmd);
        }
    
        public void HotReload(TerrainHolder terrainHolder)
        {
            NavMeshBuilder.UpdateNavMeshData(
                data: _navMeshSurface.navMeshData,
                buildSettings: _navMeshSurface.GetBuildSettings(),
                sources: terrainHolder.GetAllInstanciatedChunks()
                    .ConvertAll(chunk => chunk.GetNavMeshBuildSource()
                    ),
                localBounds: _bounds
            );
        }

    }
}
