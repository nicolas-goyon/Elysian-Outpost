using System;
using UnityEngine;

namespace Base
{
    public class CameraMovements : MonoBehaviour
    {
        [SerializeField] private float speed = 20f;
        [SerializeField] private float speedMultiplier = 2f;
        [SerializeField] private float sensitivity = .125f;

        [SerializeField] private GameInputs gameInputs;

        [SerializeField] private float maxYAngle = 80f;
        [SerializeField] private float minYAngle = -80f;
        
        private CameraState _toogleCameraControl = CameraState.OnMenu;

        void Start()
        {
            Set(_toogleCameraControl);
        }

        // Update is called once per frame
        void Update()
        {
            if (_toogleCameraControl != CameraState.FreeFly) return;

            Vector2 viewDelta = gameInputs.GetViewDelta() * sensitivity;
            transform.Rotate(Vector3.up, viewDelta.x);
            transform.Rotate(Vector3.right, -viewDelta.y);

            // Clamp the camera rotation
            Vector3 angles = transform.eulerAngles;
            angles.z = 0;
            if (angles.x > 180) angles.x -= 360;
            angles.x = Mathf.Clamp(angles.x, minYAngle, maxYAngle);
            
            transform.eulerAngles = angles;

            Vector3 vector3 = gameInputs.GetCameraMovementVector();

            if (gameInputs.IsSpeeding()) {
                vector3 *= speedMultiplier;
            }

            transform.Translate(vector3 * (speed * Time.deltaTime));
        }
        public void Set(CameraState state)
        {
            switch (state)
            {
                case CameraState.FreeFly:
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                    _toogleCameraControl = CameraState.FreeFly;
                    break;
                case CameraState.OnMenu:
                    Cursor.lockState = CursorLockMode.Confined;
                    Cursor.visible = true;
                    _toogleCameraControl = CameraState.OnMenu;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }
        
        public enum CameraState
        {
            OnMenu,
            FreeFly
        }
        
    }
}