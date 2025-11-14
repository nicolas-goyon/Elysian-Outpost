using System;
using System.Diagnostics;
using Unity.AI.Navigation;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Base.AI
{
    [RequireComponent(typeof(NavMeshSurface))]
    public class NavMeshHotReload : MonoBehaviour
    {
        private NavMeshSurface _navMeshSurface;
        private Stopwatch _stopWatch;
    
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            _navMeshSurface = GetComponent<NavMeshSurface>();
            _stopWatch = new Stopwatch();
        }
    
        public void HotReload()
        {
            _stopWatch.Restart();
            _navMeshSurface.BuildNavMesh();
            _stopWatch.Stop();
            // Get the elapsed time as a TimeSpan value.
            TimeSpan ts = _stopWatch.Elapsed;

            Debug.Log($"NavMesh hot reload completed in: { ts.TotalMilliseconds }ms");
        }

    }
}
