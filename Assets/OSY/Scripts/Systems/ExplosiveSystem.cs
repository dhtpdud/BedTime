using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Aspects;
using Unity.Rendering;
using Unity.Transforms;
using Random = UnityEngine.Random;

[BurstCompile]
public sealed partial class ExplosiveSystem : SystemBase
{
    [BurstCompile]
    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<EntityStoreComponent>();
    }
    [BurstCompile]
    protected override void OnUpdate()
    {
        float dt = SystemAPI.Time.DeltaTime;
        var ecbMain = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged);
        Dependency = new TimerJob { deltaTime = dt, one = one }.ScheduleParallel(Dependency);
        Dependency.Complete();

        var store = SystemAPI.GetSingleton<EntityStoreComponent>();
        int explosionCount = AudioManager.instance.explosions.Count;

        foreach (var (ex, trans, entity) in SystemAPI.Query<RefRW<ExplosiveComponent>, RefRO<LocalTransform>>().WithEntityAccess())
        {
            ref var data = ref ex.ValueRW;
            if (data.isEnable && data.time <= 0)
            {
                data.isEnable = false;
                if (GameManager.instance.particleCount < GameManager.instance.MaxParticleCount)
                {
                    var pe = ecbMain.Instantiate(store.particleExplosionWhite);
                    var t = trans.ValueRO; t.Rotation = quaternion.identity;
                    ecbMain.SetComponent(pe, t);
                    lock (GameManager.instance) GameManager.instance.particleCount++;
                    UniTask.RunOnThreadPool(async () =>
                    {
                        await UniTask.Delay(TimeSpan.FromSeconds(4));
                        lock (GameManager.instance) GameManager.instance.particleCount--;
                    }).Forget();
                }
                GameManager.instance.mainCam.DOComplete();
                GameManager.instance.mainCam.DOShakePosition(1f, 4f, 10, 10);
                AudioManager.instance.audioSource
                    .PlayOneShot(AudioManager.instance.explosions[Random.Range(0, explosionCount)]);

                var job = new ExplosionJob
                {
                    exData = data,
                    center = trans.ValueRO.Position,
                    self = entity,
                    ecb = ecbMain.AsParallelWriter()
                };
                Dependency = job.ScheduleParallel(Dependency);
                Dependency.Complete();
            }
        }
    }
    float3 one = new float3(1, 1, 1);

    [BurstCompile]
    partial struct TimerJob : IJobEntity
    {
        [ReadOnly] public float deltaTime;
        [ReadOnly] public float3 one;
        public void Execute(ref ExplosiveComponent ex, ref HDRPMaterialPropertyEmissiveColor emissive)
        {
            if (ex.isEnable)
            {
                ex.time -= deltaTime;
                if (ex.time < 0) { ex.time = 0; emissive.Value = float3.zero; }
                else
                {
                    float s = math.abs(math.sin((ex.maxTime - ex.time) * 5));
                    emissive.Value = s > 0.5f ? one * 100f : float3.zero;
                }
            }
        }
    }

    [BurstCompile]
    partial struct ExplosionJob : IJobEntity
    {
        [ReadOnly] public ExplosiveComponent exData;
        [ReadOnly] public float3 center;
        [ReadOnly] public Entity self;
        public EntityCommandBuffer.ParallelWriter ecb;
        public void Execute([ChunkIndexInQuery] int chunk, in Entity e, RigidBodyAspect rb)
        {
            if (e == self)
            {
                ecb.AddComponent<DestroyMark>(chunk, e);
                return;
            }
            float3 force = rb.Position - center;
            float dist = math.length(force);
            if (dist <= exData.range)
                rb.LinearVelocity += math.normalize(force) * (math.max(exData.power / dist, exData.power) / rb.Mass);
        }
    }
}
