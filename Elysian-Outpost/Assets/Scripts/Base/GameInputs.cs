using UnityEngine;

namespace Base
{
    public class GameInputs : MonoBehaviour
    {
        private CameraInputs _inputActions;
        // Start is called before the first frame update
        void Awake()
        {
            _inputActions = new CameraInputs();
            _inputActions.CameraMovements.Enable();
        
        }

        public Vector3 GetCameraMovementVector() {
            return _inputActions.CameraMovements.Movements.ReadValue<Vector3>();
        }

        public bool IsSpeeding() {
            return _inputActions.CameraMovements.Speeding.ReadValue<float>() > 0;
        }

        public Vector2 GetViewDelta() {
            return _inputActions.CameraMovements.View.ReadValue<Vector2>();
        }
        
        public bool IsLeftClick() {
            return _inputActions.CameraMovements.LeftClick.ReadValue<float>() > 0;
        }
    }
}