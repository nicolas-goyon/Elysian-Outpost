using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.Serialization;

namespace Base
{
    [RequireComponent(typeof(NavMeshSurface))]
    public class NavMeshHotReload : MonoBehaviour
    {
        [SerializeField] private GameInputs _gameInputs;
        private NavMeshSurface _navMeshSurface;
    
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            _gameInputs.OnDebugEvent += HotReload;
            _navMeshSurface = GetComponent<NavMeshSurface>();
        }
    
        private void HotReload()
        {
            // If debug input is detected, reload the NavMesh
            Debug.Log("Hot Reloading NavMesh...");
            _navMeshSurface.BuildNavMesh();
            Debug.Log("NavMesh Hot Reloaded.");
        }

    }
}
