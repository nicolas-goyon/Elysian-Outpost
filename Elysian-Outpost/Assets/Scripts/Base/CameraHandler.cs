using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Base;
using ScriptableObjectsDefinition;
using Unity.Mathematics;
using UnityEngine;

public class CameraHandler : MonoBehaviour
{
    [SerializeField] private GameInputs gameInputs;
    [SerializeField] private GameObject spherePrefab;
    private GameObject _sphere;

    private TerrainHolder _terrainHolder; // TODO : Link to existing TerrainHolder

    
    [SerializeField] private Canvas _canvas;
    [SerializeField] private CameraMovements _camera;

    private bool isClicked = false;
    
    
    // Update is called once per frame
    private void Start()
    {
        gameInputs.OnMenuEvent += OnOpenMenu;
    }

    private void OnOpenMenu()
    {
        // If open menu input is detected, toggle the canvas visibility
        _canvas.enabled = !_canvas.enabled;
        _camera.Set(_canvas.enabled ? CameraMovements.CameraState.OnMenu : CameraMovements.CameraState.FreeFly);
    }

    private void OnLeftClick()
    {
        // Raycast and add a sphere at the hit point on left click
        if (gameInputs.IsLeftClick())
        {
            // Center of the screen
            Ray ray = Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0));
            if (Physics.Raycast(ray, out RaycastHit hitInfo))
            {
                // Debug.Log("Hit " + hitInfo.collider.gameObject.name);
                Vector3 hitPoint = hitInfo.point;
                if (_sphere == null)
                {
                    _sphere = Instantiate(spherePrefab, hitPoint, Quaternion.identity);
                }
                _sphere.transform.position = hitPoint - hitInfo.normal * 0.1f;
        
                if (isClicked)
                {
                    return;
                }
                isClicked = true;
                
                // (ExampleChunk chunk, uint3 voxelPosition) = _terrainHolder.GetHitChunkAndVoxelPositionAtRaycast(hitInfo);
                // if (chunk == null)
                // {
                //     // Debug.Log($"No chunk at {hitInfo}");
                //     return;
                // }
                // Debug.Log($"Removing voxel at {voxelPosition} in chunk at {chunk.WorldPosition}");
                // chunk.RemoveVoxel(voxelPosition);
                // _terrainHolder.ReloadChunk(chunk);
            }
        }
        else
        {
            isClicked = false;
        }
    }
}