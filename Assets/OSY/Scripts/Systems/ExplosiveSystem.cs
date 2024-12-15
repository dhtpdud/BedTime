using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Aspects;

partial struct ExplosiveSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
    [BurstCompile]
    partial struct GetExplosiveJob : IJobEntity
    {
        public NativeArray<ExplosiveComponent> explosiveComponent;
        public void Execute(ref RigidBodyAspect rigidBody)
        {
        }
    }
    [BurstCompile]
    partial struct ExplosionJob : IJobEntity
    {
        [ReadOnly] public float range;
        [ReadOnly] public float power;
        [ReadOnly] public float3 explosionPoint;
        public void Execute(ref RigidBodyAspect rigidBody)
        {
            float3 forceDir = rigidBody.Position - explosionPoint;
            if (math.length(forceDir) <= range)
            {
                rigidBody.LinearVelocity += forceDir / rigidBody.Mass * power;
            }
        }
    }
}
