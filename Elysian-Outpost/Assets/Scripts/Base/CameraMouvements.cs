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


        // Update is called once per frame
        void Update()
        {

            Vector2 viewDelta = gameInputs.GetViewDelta() * sensitivity;
            transform.Rotate(Vector3.up, viewDelta.x);
            transform.Rotate(Vector3.right, -viewDelta.y);

            // Clamp the camera rotation
            Vector3 angles = transform.eulerAngles;
            angles.z = 0;
            if (angles.x > 180) angles.x -= 360;
            angles.x = Mathf.Clamp(angles.x, minYAngle, maxYAngle);
            
            transform.eulerAngles = angles;
            


            // Reset the mouse position to the center of the screen
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            //gameInputs.ResetMousePosition();

        
            Vector3 vector3 = gameInputs.GetCameraMovementVector();

            if (gameInputs.IsSpeeding()) {
                vector3 *= speedMultiplier;
            }

            transform.Translate(vector3 * (speed * Time.deltaTime));
        }
    }
}