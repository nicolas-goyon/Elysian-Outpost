using UnityEngine;
using UnityEngine.AI;

namespace Base.AI
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class CitizenEntity : MonoBehaviour
    {
        public NavMeshAgent _navMeshAgent => GetComponent<NavMeshAgent>();
 
    }
}
