using System.Collections.Generic;
using Base.InGameConsole;
using Base.Terrain;
using Libs.VoxelMeshOptimizer;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Base.Camera
{
    public class CameraHandler : MonoBehaviour
    {
        [SerializeField] private GameInputs _gameInputs;
        [SerializeField] private GameObject _canvas;
        [SerializeField] private CameraMovements _camera;

        // Update is called once per frame
        private void Start()
        {
            _gameInputs.OnMenuEvent += OnOpenMenu;
            _gameInputs.OnClickEvent += OnLeftClick;
        }

        private void OnOpenMenu()
        {
            // If open menu input is detected, toggle the canvas visibility
            _canvas.SetActive(!_canvas.activeSelf);
            _camera.Set(_canvas.activeSelf ? CameraMovements.CameraState.OnMenu : CameraMovements.CameraState.FreeFly);
        }

        private void OnLeftClick()
        {
            SpawnEntityAtCursor();
            // OnPickUpDropVoxel();
        }

        #region WanderingEntitySpawn

        [SerializeField] private WanderingEntity _wanderingEntityPrefab;

        private void SpawnEntityAtCursor()
        {
            if (_canvas.activeSelf) return;
            Ray ray = UnityEngine.Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0));
            if (Physics.Raycast(ray, out RaycastHit hitInfo))
            {
                Vector3 spawnPosition = hitInfo.point + hitInfo.normal * 1.5f;
                Instantiate(_wanderingEntityPrefab, spawnPosition, Quaternion.identity);
            }
        }

        #endregion

        #region PickUpDropVoxel

        private short _voxelInventory = -1;
        [SerializeField] private FixedSizeTerrain _terrain;

        private void OnPickUpDropVoxel()
        {
            if (_canvas.activeSelf) return;
            Ray ray = UnityEngine.Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0));
            if (!Physics.Raycast(ray, out RaycastHit hitInfo)) return;


            (List<int3> solidVoxels, List<int3> nonSolidVoxels) = PossibleNearbyVoxelPositions(hitInfo.point, 3);
            if (solidVoxels.Count == 0)
            {
                DebuggerConsole.Log($"Solid voxel list is empty, cannot pick up or drop voxel.");
                return;
            }

            if (nonSolidVoxels.Count == 0)
            {
                DebuggerConsole.Log($"Non-solid voxel list is empty, cannot pick up or drop voxel.");
                return;
            }


            if (_voxelInventory >= 0)
            {
                // Try to pick up a block
                int randomIndex = Random.Range(0, nonSolidVoxels.Count);
                int3 selectedVoxelPosition = nonSolidVoxels[randomIndex];

                Chunk chunk = _terrain.TerrainHolder.GetChunkAtWorldPosition(new int3(
                    (int)selectedVoxelPosition.x,
                    (int)selectedVoxelPosition.y,
                    (int)selectedVoxelPosition.z
                ));
                if (chunk == null)
                {
                    DebuggerConsole.LogError(
                        $"Should not happen: no chunk found at {selectedVoxelPosition} for {gameObject.name}");
                    return;
                }

                Voxel voxelData = chunk.GetAtWorldPosition(selectedVoxelPosition);
                if (voxelData == null || voxelData.IsSolid)
                {
                    DebuggerConsole.LogError(
                        $"Should not happen: hit a solid voxel at {selectedVoxelPosition} in chunk at {chunk.WorldPosition}");
                    return;
                }

                // Place the voxel
                chunk.SetAtWorldPosition(selectedVoxelPosition,
                    new Voxel((ushort)_voxelInventory));
                _terrain.TerrainHolder.ReloadChunk(chunk);
                _voxelInventory = -1;
            }
            else
            {
                // Try to pick up a block
                int randomIndex = Random.Range(0, solidVoxels.Count);
                int3 selectedVoxelPosition = solidVoxels[randomIndex];

                Chunk chunk = _terrain.TerrainHolder.GetChunkAtWorldPosition(new int3(
                    (int)selectedVoxelPosition.x,
                    (int)selectedVoxelPosition.y,
                    (int)selectedVoxelPosition.z
                ));
                if (chunk == null)
                {
                    DebuggerConsole.LogError(
                        $"Should not happen: no chunk found at {selectedVoxelPosition} for {gameObject.name}");
                    return;
                }

                Voxel voxelData = chunk.GetAtWorldPosition(selectedVoxelPosition);
                if (voxelData == null || !voxelData.IsSolid)
                {
                    DebuggerConsole.LogError(
                        $"Should not happen: hit a non-solid voxel at {selectedVoxelPosition} in chunk at {chunk.WorldPosition}");
                    return;
                }

                // Pick up the voxel
                _voxelInventory = (short)voxelData.ID;
                chunk.SetAtWorldPosition(selectedVoxelPosition, null);
                _terrain.TerrainHolder.ReloadChunk(chunk);
                
            }
        }

        private (List<int3> solid, List<int3> nonSolid) PossibleNearbyVoxelPositions(Vector3 transform, int radius)
        {
            List<int3> solidPositions = new();
            List<int3> nonSolidPositions = new();
            int3 centerPos = new int3(
                Mathf.FloorToInt(transform.x),
                Mathf.FloorToInt(transform.y),
                Mathf.FloorToInt(transform.z)
            );

            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    for (int z = -radius; z <= radius; z++)
                    {
                        int3 worldPos = new int3(
                            centerPos.x + x,
                            centerPos.y + y,
                            centerPos.z + z
                        );
                        Voxel voxel = _terrain.TerrainHolder.GetVoxelAtWorldPosition(worldPos);
                        if (voxel == null) continue; // Out of bounds

                        if (voxel.IsSolid)
                        {
                            solidPositions.Add(worldPos);
                        }
                        else
                        {
                            nonSolidPositions.Add(worldPos);
                        }
                    }
                }
            }

            return (solidPositions, nonSolidPositions);
        }

        #endregion
    }
}