using System;
using UnityEngine;

namespace Base
{
    public class GameInputs : MonoBehaviour
    {
        private CameraInputs _inputActions;
        
        // Event handlers
        public delegate void OnOpenMenuEventHandler();
        public event OnOpenMenuEventHandler OnOpenMenuEvent;
        private bool _menuOpenKeyWasPressed = false;
        
        
        // Start is called before the first frame update
        void Awake()
        {
            _inputActions = new CameraInputs();
            _inputActions.CameraMovements.Enable();
            _inputActions.PlayerMenuControls.Enable();
            // _inputActions.PlayerMenuControls.Disable();
        
        }

        private void Update()
        {
            if (IsOpenMenu() && !_menuOpenKeyWasPressed)
            {
                OnOpenMenuEvent?.Invoke();
                _menuOpenKeyWasPressed = true;
            }
            else if (!IsOpenMenu())
            {
                _menuOpenKeyWasPressed = false;
            }
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
        
        public bool IsOpenMenu() {
            return _inputActions.PlayerMenuControls.OpenCloseMenu.ReadValue<float>() > 0;
        }
        
        
        
        
        
        
        
        
        
    }
}