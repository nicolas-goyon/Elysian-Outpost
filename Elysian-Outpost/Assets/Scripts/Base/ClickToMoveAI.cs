using Base;
using UnityEngine;
using UnityEngine.AI;

public class ClickToMoveAI : MonoBehaviour
{
    [SerializeField] private NavMeshAgent _agent;
    [SerializeField] private Camera _mainCamera;
    
    [SerializeField] private GameInputs _gameInputs;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _gameInputs._leftClickEvent += OnMouseClick;
    }

    private void OnMouseClick()
    {
        Ray ray = _mainCamera.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0));
        if (Physics.Raycast(ray, out RaycastHit hitInfo))
        {
            Vector3 hitPoint = hitInfo.point;
            _agent.SetDestination(hitPoint);
        }
        else
        {
            Debug.Log($"No hit detected from raycast.");
        }
    }
}
