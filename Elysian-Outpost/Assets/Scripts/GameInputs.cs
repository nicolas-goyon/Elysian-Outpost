using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameInputs : MonoBehaviour
{
    private CameraInputs inputActions;
    // Start is called before the first frame update
    void Awake()
    {
        inputActions = new CameraInputs();
        inputActions.CameraMovements.Enable();
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }


    public Vector3 GetCameraMovementVector() {
        return inputActions.CameraMovements.Movements.ReadValue<Vector3>();
    }

    public bool IsSpeeding() {
        return inputActions.CameraMovements.Speeding.ReadValue<float>() > 0;
    }

    public Vector2 GetViewDelta() {
        return inputActions.CameraMovements.View.ReadValue<Vector2>();
    }
}
