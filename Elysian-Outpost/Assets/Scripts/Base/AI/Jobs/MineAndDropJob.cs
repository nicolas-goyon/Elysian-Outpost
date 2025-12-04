using System;
using Base.InGameConsole;
using Base.Terrain;
using Libs.VoxelMeshOptimizer;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;


namespace Base.AI.Jobs
{
    [Obsolete("MineAndDropJob is deprecated testing only.")]
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(CitizenEntity))]
    public class MineAndDropJob : MonoBehaviour
    {
        private NavMeshAgent _navMeshAgent => GetComponent<NavMeshAgent>();
        private TerrainHolder _terrainHolder;
        private Voxel _voxel;
        private bool started = false;
        
        private enum JobState
        {
            MovingToMine,
            Mining,
            MovingToDropOff,
            DroppingOff
        }
        
        private  JobState _jobState;

        public void Begin(TerrainHolder terrainHolder)
        {
            _terrainHolder = terrainHolder;
            // SetMovingToMineState();
            started = true;
        }
        
        public void Update()
        {
            if (!started) return;
            // switch (_jobState)
            // {
            //     case JobState.MovingToMine:
            //         if (!_navMeshAgent.pathPending && _navMeshAgent.remainingDistance <= _navMeshAgent.stoppingDistance)
            //         {
            //             SetMiningState();
            //         }
            //         break;
            //     case JobState.Mining:
            //         // Mining is handled in SetMiningState
            //         break;
            //     case JobState.MovingToDropOff:
            //         if (!_navMeshAgent.pathPending && _navMeshAgent.remainingDistance <= _navMeshAgent.stoppingDistance)
            //         {
            //             SetDroppingOffState();
            //         }
            //         break;
            //     case JobState.DroppingOff:
            //         // Dropping off is handled in SetDroppingOffState
            //         break;
            //     default:
            //         throw new ArgumentOutOfRangeException();
            // }
            
            
        }

        private void DebugWandering()
        {
            // Default wandering behavior for testing
            if (!_navMeshAgent.pathPending && _navMeshAgent.remainingDistance <= _navMeshAgent.stoppingDistance)
            {
                Vector3 randomDirection = UnityEngine.Random.insideUnitSphere * 10f;
                randomDirection += transform.position;
                NavMeshHit navHit;
                NavMesh.SamplePosition(randomDirection, out navHit, 10f, NavMesh.AllAreas);
                _navMeshAgent.SetDestination(navHit.position);
            }
        }
        
        // private void SetMovingToMineState()
        // {
        //     _jobState = JobState.MovingToMine;
        //     // Get random mine location
        //     var mineLocation = new Vector3(Random.Range(-10, 10), 0, Random.Range(-10, 10));
        //     Debug.Log($"Moving to mine location: {mineLocation}");
        //     // Set NavMeshAgent destination to mine location
        //     _navMeshAgent.SetDestination(mineLocation);
        // }
        
        // private void SetMiningState()
        // {
        //     _jobState = JobState.Mining;
        //
        //     int3 worldPos = (int3)math.floor(_navMeshAgent.transform.position);
        //     _voxel = _terrainHolder.PickupVoxel(worldPos);
        //     
        //     Debug.Log($"Mined voxel at {worldPos}: {_voxel.ID}");
        //     
        //     // After a random delay, set state to moving to drop off
        //     // SetMovingToDropOffState();
        //     // StartCoroutine(DelayedSetMovingToDropOffState());
        //     StartCoroutine(DelayedSetMovingToDropOffState());
        // }
        //
        // private void SetMovingToDropOffState()
        // {
        //     _jobState = JobState.MovingToDropOff;
        //     // Get random drop off location
        //     var dropOffLocation = new Vector3(Random.Range(-10, 10), 0, Random.Range(-10, 10));
        //     Debug.Log($"Moving to drop off location: {dropOffLocation}");
        //     // Set NavMeshAgent destination to drop off location
        //     _navMeshAgent.SetDestination(dropOffLocation);
        // }
        //
        // private void SetDroppingOffState()
        // {
        //     _jobState = JobState.DroppingOff;
        //     // Simulate dropping off with a delay
        //     Debug.Log($"Dropped off voxel: {_voxel.ID}");
        //     // _voxel = default;
        //     // After dropping off, set state to moving to mine
        //     SetMovingToMineState();
        // }
        //
        // private System.Collections.IEnumerator DelayedSetMovingToDropOffState()
        // {
        //     yield return new WaitForSeconds(Random.Range(1f, 3f));
        //     SetMovingToDropOffState();
        // }
    }
}