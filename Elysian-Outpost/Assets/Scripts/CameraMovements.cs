using UnityEngine;

public class CameraMovements : MonoBehaviour
{
    [SerializeField] private float speed = 5f;
    [SerializeField] private float speedMultiplier = 2f;
    [SerializeField] private float sensitivity = .5f;

    [SerializeField] private GameInputs gameInputs;
    // Start is called before the first frame update
    void Start()
    {
    }


    // Update is called once per frame
    void Update()
    {

        Vector2 viewDelta = gameInputs.GetViewDelta() * sensitivity;
        transform.Rotate(Vector3.up, viewDelta.x);
        transform.Rotate(Vector3.right, -viewDelta.y);

        // Clamp the camera rotation
        Vector3 angles = transform.eulerAngles;
        angles.z = 0;
        transform.eulerAngles = angles;


        // Reset the mouse position to the center of the screen
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        //gameInputs.ResetMousePosition();

        
        Vector3 vector3 = gameInputs.GetCameraMovementVector();

        if (gameInputs.IsSpeeding()) {
            vector3 *= speedMultiplier;
        }

        transform.Translate(vector3 * speed * Time.deltaTime);
    }
}
