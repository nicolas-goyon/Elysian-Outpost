using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public partial struct RotateCubeSystem : ISystem
{

    public void OnCreate(ref SystemState state) {
        state.RequireForUpdate<RotateSpeed>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state) {
        RotatingCubeJob rotatingCubeJob = new RotatingCubeJob { DeltaTime = SystemAPI.Time.DeltaTime };
        rotatingCubeJob.ScheduleParallel();
    }

    public partial struct RotatingCubeJob : IJobEntity {

        public float DeltaTime;
        public void Execute(ref LocalTransform localTransform, in RotateSpeed rotateSpeed) {
            localTransform = localTransform.RotateY(rotateSpeed.value * DeltaTime);
        }
    }
}
