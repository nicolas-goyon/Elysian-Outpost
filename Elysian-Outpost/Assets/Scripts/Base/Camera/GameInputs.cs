using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Base
{
    public class GameInputs : MonoBehaviour
    {
        private CameraInputs _inputActions;
        
        // Event handlers
        public KeyPressInputEvent _menuOpenEvent;
        public KeyPressInputEvent _leftClickEvent;
        public KeyPressInputEvent _debugEvent;
        
        // Start is called before the first frame update
        void Awake()
        {
            _inputActions = new CameraInputs();
            _inputActions.CameraMovements.Enable();
            _inputActions.PlayerMenuControls.Enable();
            
            _menuOpenEvent = new KeyPressInputEvent("OpenMenu", _inputActions.PlayerMenuControls.OpenCloseMenu);;
            _leftClickEvent = new KeyPressInputEvent("LeftClick", _inputActions.CameraMovements.LeftClick);
            _debugEvent = new KeyPressInputEvent("Debug", _inputActions.PlayerMenuControls.DebugInput);
        }

        private void Update()
        {
            _menuOpenEvent.Update();
            _leftClickEvent.Update();
            _debugEvent.Update();
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

        public class KeyPressInputEvent
        {
            public string Name;
            public delegate void InputEventHandler();
            public event InputEventHandler OnInputEvent;
            
            private bool _wasPressed = false;
            private readonly InputAction _inputAction;
            
            public KeyPressInputEvent(string name, InputAction inputAction)
            {
                Name = name;
                _inputAction = inputAction;
                
            }
            
            public void Update()
            {
                if (_inputAction.ReadValue<float>() > 0 && !_wasPressed)
                {
                    Debug.Log($"Input Event Triggered: {Name}");
                    OnInputEvent?.Invoke();
                    _wasPressed = true;
                }
                else if (_inputAction.ReadValue<float>() == 0)
                {
                    _wasPressed = false;
                }
            }
            
            public static KeyPressInputEvent operator +(KeyPressInputEvent inputEvent, InputEventHandler handler)
            {
                Debug.Log($"Subscribing to Input Event: {inputEvent.Name} with handler {handler.Method.Name}");
                inputEvent.OnInputEvent += handler;
                return inputEvent;
            }


        }
        
    }
}