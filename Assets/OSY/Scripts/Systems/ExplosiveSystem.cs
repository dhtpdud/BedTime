using OSY;
using System.Security.Principal;
using Unity.Burst;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Aspects;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

partial struct ExplosiveSystem : ISystem
{
    float3 float3One;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        float3One = new float3(1, 1, 1);
    }
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        new TimerJob { time = SystemAPI.Time, float3One = this.float3One }.ScheduleParallel(state.Dependency).Complete();

        EntityCommandBuffer ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        foreach (var (explosiveRef, localTransformRef, entity) in SystemAPI.Query<RefRW<ExplosiveComponent>, RefRO<LocalTransform>>().WithEntityAccess())
        {
            ref var explosive = ref explosiveRef.ValueRW;
            if (explosive.isEnable && explosive.time <= 0)
            {
                explosive.isEnable = false;
                new ExplosionJob { explosiveComponent = explosive, explosionPoint = localTransformRef.ValueRO.Position, self = entity, parallelWriter = ecb.AsParallelWriter() }.ScheduleParallel(state.Dependency).Complete();
            }
        }
    }
    [BurstCompile]
    partial struct TimerJob : IJobEntity
    {
        [ReadOnly] public TimeData time;
        [ReadOnly] public float3 float3One;
        public void Execute(ref ExplosiveComponent explosive, ref HDRPMaterialPropertyEmissiveColor emissiveColor)
        {
            if (explosive.isEnable)
            {
                explosive.time -= time.DeltaTime;
                if (explosive.time < 0)
                {
                    explosive.time = 0;
                    emissiveColor.Value = float3.zero;
                }
                else
                {
                    if (math.abs(math.sin((explosive.maxTime - explosive.time) * 5)) > 0.5f)
                        emissiveColor.Value = float3One * 100;
                    else
                        emissiveColor.Value = float3.zero;
                }
            }
        }
    }
    [BurstCompile]
    partial struct ExplosionJob : IJobEntity
    {
        [ReadOnly] public ExplosiveComponent explosiveComponent;
        [ReadOnly] public float3 explosionPoint;
        [ReadOnly] public Entity self;

        public EntityCommandBuffer.ParallelWriter parallelWriter;
        public void Execute([ChunkIndexInQuery] int chunkIndex, in Entity entity, RigidBodyAspect rigidBody)
        {
            if (self == entity)
            {
                parallelWriter.AddComponent(chunkIndex, entity, new DestroyMark());
                return;
            }
            Vector3 force = rigidBody.Position - explosionPoint;
            float distance = math.length(force);
            if (distance <= explosiveComponent.range)
            {
                rigidBody.LinearVelocity += force.normalized.ToFloat3() * (math.max(explosiveComponent.power / distance, explosiveComponent.power) / rigidBody.Mass);
            }
        }
    }

    [BurstCompile]
    partial struct ExplosionChainJob : IJobEntity
    {
        [ReadOnly] public ExplosiveComponent otherExplosiveComponent;
        [ReadOnly] public float3 explosionPoint;
        public void Execute(ref ExplosiveComponent explosive, in LocalTransform localTransform)
        {
            float3 forceDir = localTransform.Position - explosionPoint;
            if (math.length(forceDir) <= otherExplosiveComponent.range)
            {
                explosive.isEnable = true;
            }
        }
    }
}
