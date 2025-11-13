using Base.Terrain;
using Libs.VoxelMeshOptimizer;
using Unity.Mathematics;
using UnityEngine;

namespace Base.Camera
{
    public class CameraHandler : MonoBehaviour
    {
        [SerializeField] private GameInputs _gameInputs;
        [SerializeField] private Canvas _canvas;
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
            _canvas.enabled = !_canvas.enabled;
            _camera.Set(_canvas.enabled ? CameraMovements.CameraState.OnMenu : CameraMovements.CameraState.FreeFly);
        }

        private void OnLeftClick()
        {
            // SpawnEntityAtCursor();
            OnPickUpDropVoxel();
        }

        #region WanderingEntitySpawn


        [SerializeField] private WanderingEntity _wanderingEntityPrefab;

        private void SpawnEntityAtCursor()
        {
            if (_canvas.enabled) return;
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
            if (_canvas.enabled) return;
            Ray ray = UnityEngine.Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0));
            if (!Physics.Raycast(ray, out RaycastHit hitInfo)) return;
            
            
            
            if (_voxelInventory >= 0)
            {
                (Chunk chunk, uint3 voxelPosition) = _terrain.TerrainHolder.GetChunkAndVoxelPositionBeforeHitRaycast(hitInfo);
                Voxel voxelData = chunk.Get(voxelPosition.x, voxelPosition.y, voxelPosition.z);
                if (voxelData == null || voxelData.IsSolid)
                {
                    Debug.LogError(
                        $"Should not happen: hit a solid voxel at {voxelPosition} in chunk at {chunk.WorldPosition}");
                    return;
                }

                // Place the voxel
                Debug.Log($"Placed voxel ID {_voxelInventory} at {voxelPosition} in chunk at {chunk.WorldPosition}");
                chunk.Set(voxelPosition.x, voxelPosition.y, voxelPosition.z,
                    new Voxel((ushort)_voxelInventory));
                _terrain.TerrainHolder.ReloadChunk(chunk);
                _voxelInventory = -1;
            }
            else
            {
                (Chunk chunk, uint3 voxelPosition) =
                    _terrain.TerrainHolder.GetHitChunkAndVoxelPositionAtRaycast(hitInfo);
                Voxel voxelData = chunk.Get(voxelPosition.x, voxelPosition.y, voxelPosition.z);
                if (voxelData == null || !voxelData.IsSolid)
                {
                    Debug.LogError(
                        $"Should not happen: hit a non-solid voxel at {voxelPosition} in chunk at {chunk.WorldPosition}");
                    return;
                }

                // Pick up the voxel
                Debug.Log($"Picked up voxel ID {voxelData.ID} at {voxelPosition} in chunk at {chunk.WorldPosition}");
                _voxelInventory = (short)voxelData.ID;
                chunk.Set(voxelPosition.x, voxelPosition.y, voxelPosition.z, null);
                _terrain.TerrainHolder.ReloadChunk(chunk);
                
            }
        }

        #endregion
    }
}