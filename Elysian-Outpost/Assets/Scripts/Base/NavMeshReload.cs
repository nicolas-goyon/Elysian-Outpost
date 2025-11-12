using Base;
using Unity.AI.Navigation;
using UnityEngine;

public class NavMeshReload : MonoBehaviour
{
    
    [SerializeField] private NavMeshSurface _navMeshSurface;
    [SerializeField] private GameInputs _gameInputs;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _gameInputs._debugEvent += HotReBake;
    }
    
    private void HotReBake()
    {
        Debug.Log("Rebaking NavMesh...");
        
        _navMeshSurface.BuildNavMesh();
    }
}
