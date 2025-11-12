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
        
        public CameraControl ToogleCameraControl { get; set; } = CameraControl.FLYMODE;

        void Start()
        {
            Set(ToogleCameraControl);
        }

        // Update is called once per frame
        void Update()
        {
            if (ToogleCameraControl != CameraControl.FLYMODE) return;

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
        
        public enum CameraControl{
            FLYMODE,
            MENU
        }
        
        public void Set(CameraControl control)
        {
            switch (control)
            {
                case CameraControl.FLYMODE:
                    ToogleCameraControl = CameraControl.FLYMODE;
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                    break;
                case CameraControl.MENU:
                    ToogleCameraControl = CameraControl.MENU;
                    Cursor.lockState = CursorLockMode.Confined;
                    Cursor.visible = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(control), control, null);
            }
        }
    }
}