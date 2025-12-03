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
        private Stopwatch _stopWatch;
        private Bounds _bounds;
    
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            _navMeshSurface = GetComponent<NavMeshSurface>();
            _stopWatch = new Stopwatch();
        }

        public void Init(FixedSizeTerrain fixedSizeTerrain)
        {
            _bounds = new Bounds(
                center: fixedSizeTerrain.GetWorldCenterPosition(),
                size: fixedSizeTerrain.GetWorldSize()
            );
            
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
