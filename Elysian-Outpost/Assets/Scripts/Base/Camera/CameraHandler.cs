using UnityEngine;

namespace Base.Camera
{
    public class CameraHandler : MonoBehaviour
    {
        [SerializeField] private GameInputs _gameInputs;
        [SerializeField] private Canvas _canvas;
        [SerializeField] private CameraMovements _camera;
        
        [SerializeField] private WanderingEntity _wanderingEntityPrefab;

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
            if (_canvas.enabled) return;
            Ray ray = UnityEngine.Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0));
            if (Physics.Raycast(ray, out RaycastHit hitInfo))
            {
                Vector3 spawnPosition = hitInfo.point + hitInfo.normal * 1.5f;
                Instantiate(_wanderingEntityPrefab, spawnPosition, Quaternion.identity);
            }
        }
    }
}