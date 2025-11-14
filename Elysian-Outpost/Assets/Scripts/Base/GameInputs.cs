using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Base
{
    public class GameInputs : MonoBehaviour
    {
        private CameraInputs _inputActions;
        
        // Event handlers
        public KeyPressedEvent OnMenuEvent;
        public KeyPressedEvent OnClickEvent;
        public KeyPressedEvent OnDebugEvent;
        public KeyPressedEvent OnConsoleEvent;
        
        
        // Start is called before the first frame update
        void Awake()
        {
            _inputActions = new CameraInputs();
            _inputActions.CameraMovements.Enable();
            _inputActions.PlayerMenuControls.Enable();
            
            OnMenuEvent = new KeyPressedEvent("Menu", _inputActions.PlayerMenuControls.OpenCloseMenu);
            OnClickEvent = new KeyPressedEvent("Click", _inputActions.CameraMovements.LeftClick);
            OnDebugEvent = new KeyPressedEvent("Debug", _inputActions.PlayerMenuControls.Debug);
            OnConsoleEvent = new KeyPressedEvent("Console", _inputActions.PlayerMenuControls.ConsoleOpen);
            
        
        }

        private void Update()
        {
            OnMenuEvent.Update();
            OnClickEvent.Update();
            OnDebugEvent.Update();
            OnConsoleEvent.Update();
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
        
        
        public class KeyPressedEvent
        {
            public readonly string Name;
            public delegate void KeyPressedAction();
            public event KeyPressedAction OnKeyPressed;
            
            private bool _keyWasPressed = false;
            private readonly InputAction _inputAction;
            
            public KeyPressedEvent(string name, InputAction inputAction)
            {
                Name = name;
                _inputAction = inputAction;
            }
            
            public void Update()
            {
                if (_inputAction.ReadValue<float>() > 0 && !_keyWasPressed)
                {
                    OnKeyPressed?.Invoke();
                    _keyWasPressed = true;
                }
                else if (_inputAction.ReadValue<float>() == 0)
                {
                    _keyWasPressed = false;
                }
            }
            
            public static KeyPressedEvent operator +(KeyPressedEvent e, KeyPressedAction action)
            {
                e.OnKeyPressed += action;
                return e;
            }
            
        }
        
        
        
        
        
        
    }
}