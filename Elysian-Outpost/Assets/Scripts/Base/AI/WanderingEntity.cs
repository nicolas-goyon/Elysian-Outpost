using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class WanderingEntity : MonoBehaviour
{
    private NavMeshAgent _navMeshAgent;
    
    [SerializeField] private float _wanderRadius = 10f;
    [SerializeField] private float _changeDestinationInterval = 5f;
    private float _timeSinceLastChange = 0f;
    public WanderState _currentState { get; private set; } = WanderState.Idle;
    
    public enum WanderState
    {
        Idle,
        Wandering
    }
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _navMeshAgent = GetComponent<NavMeshAgent>();
    }
    
    private void SetRandomDestination()
    {
        Vector3 randomDirection = Random.insideUnitSphere * _wanderRadius;
        randomDirection += transform.position;
        if (NavMesh.SamplePosition(randomDirection, out NavMeshHit navHit, _wanderRadius, NavMesh.AllAreas))
        {
            _navMeshAgent.SetDestination(navHit.position);
        }
    }
    
    
    // Update is called once per frame
    void Update()
    {
        // _timeSinceLastChange += Time.deltaTime;
        // if (!(_timeSinceLastChange >= _changeDestinationInterval)) return;
        // SetRandomDestination();
        // _timeSinceLastChange = 0f;
        
        // Time count only after reaching destination
        // When reaching destination, become idle for a while before choosing a new destination
        if (_navMeshAgent.remainingDistance <= _navMeshAgent.stoppingDistance)
        {
            _currentState = WanderState.Idle;
        }
        else
        {
            _currentState = WanderState.Wandering;
        }
        
        if (_currentState == WanderState.Idle)
        {
            _timeSinceLastChange += Time.deltaTime;
            if (_timeSinceLastChange >= _changeDestinationInterval)
            {
                SetRandomDestination();
                _timeSinceLastChange = 0f;
            }
        }
        
    }



}
