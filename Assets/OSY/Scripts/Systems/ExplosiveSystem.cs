using DG.Tweening;
using OSY;
using Unity.Burst;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Aspects;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

[BurstCompile]
public partial class ExplosiveSystem : SystemBase
{
    float3 float3One;
    EntityStoreComponent store;

    [BurstCompile]
    protected override void OnCreate()
    {
        float3One = new float3(1, 1, 1);
        CheckedStateRef.RequireForUpdate<EntityStoreComponent>();
    }

    [BurstCompile]
    protected override void OnStartRunning()
    {
        store = SystemAPI.GetSingleton<EntityStoreComponent>();
    }

    [BurstCompile]
    protected override void OnStopRunning()
    {
    }

    [BurstCompile]
    protected override void OnUpdate()
    {

        new TimerJob { time = SystemAPI.Time, float3One = this.float3One }.ScheduleParallel(CheckedStateRef.Dependency).Complete();

        EntityCommandBuffer ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(CheckedStateRef.WorldUnmanaged);
        foreach (var (explosiveRef, localTransformRef, entity) in SystemAPI.Query<RefRW<ExplosiveComponent>, RefRO<LocalTransform>>().WithEntityAccess())
        {
            ref var explosive = ref explosiveRef.ValueRW;
            if (explosive.isEnable && explosive.time <= 0)
            {
                explosive.isEnable = false;

                var particleEntity = ecb.Instantiate(store.particleExplosionWhite);
                ecb.SetComponent(particleEntity, localTransformRef.ValueRO);
                GameManager.instance.mainCam.DOComplete();
                GameManager.instance.mainCam.DOShakePosition(1f, 4f, 10, 10).SetEase(Ease.OutExpo);
                //GameManager.instance.mainCam.DOShakeRotation(2f, 10, 10, 90).SetEase(Ease.OutExpo);

                new ExplosionJob { explosiveComponent = explosive, explosionPoint = localTransformRef.ValueRO.Position, self = entity, parallelWriter = ecb.AsParallelWriter() }.ScheduleParallel(CheckedStateRef.Dependency).Complete();
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
