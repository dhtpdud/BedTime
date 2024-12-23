using Cysharp.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;

public partial struct DestroySystem : ISystem
{

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GameManagerSingletonComponent>();
    }
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        new LifeTimerJob { time = SystemAPI.Time }.ScheduleParallel(state.Dependency).Complete();

        var gameManagerCache = SystemAPI.GetSingleton<GameManagerSingletonComponent>();
        new TimeLimitedJob { parallelWriter = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(), dragingEntity = gameManagerCache.dragingEntityInfo.entity }.ScheduleParallel();

        var bodyPartLookup = SystemAPI.GetComponentLookup<BodyPartComponent>(true);
        new TimeLimitedPlayerJob { bodyPartLookup = bodyPartLookup, parallelWriter = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(), dragingEntity = gameManagerCache.dragingEntityInfo.entity }.ScheduleParallel();

        new DestroyJob { parallelWriter = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter() }.ScheduleParallel();
    }

    [BurstCompile]
    partial struct DestroyJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter parallelWriter;
        public void Execute([ChunkIndexInQuery] int chunkIndex, in Entity entity, in DestroyMark mark)
        {
            parallelWriter.DestroyEntity(chunkIndex, entity);
        }
    }
    [BurstCompile]
    partial struct LifeTimerJob : IJobEntity
    {
        [ReadOnly] public TimeData time;
        public void Execute(ref TimeLimitedLifeComponent timeLimitedLifeComponent)
        {
            timeLimitedLifeComponent.lifeTime -= time.DeltaTime;
        }
    }
    [BurstCompile]
    [WithNone(typeof(PlayerComponent))]
    partial struct TimeLimitedJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter parallelWriter;
        [ReadOnly] public Entity dragingEntity;
        public void Execute([ChunkIndexInQuery] int chunkIndex, in Entity entity, in TimeLimitedLifeComponent timeLimitedLifeComponent)
        {
            if (timeLimitedLifeComponent.lifeTime <= 0 && (dragingEntity != entity))
                parallelWriter.AddComponent(chunkIndex, entity, new DestroyMark());
        }
    }
    partial struct TimeLimitedPlayerJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter parallelWriter;
        [ReadOnly] public ComponentLookup<BodyPartComponent> bodyPartLookup;
        [ReadOnly] public Entity dragingEntity;
        public void Execute([ChunkIndexInQuery] int chunkIndex, in Entity entity, ref TimeLimitedLifeComponent timeLimitedLifeComponent, in PlayerComponent playerComponent)
        {
            if (timeLimitedLifeComponent.lifeTime <= 0)
            {
                if (bodyPartLookup.HasComponent(dragingEntity) && (bodyPartLookup[dragingEntity].ownerEntity == entity))
                    return;
                timeLimitedLifeComponent.lifeTime = 99;
                UniTask.RunOnThreadPool(async () =>
                {
                    lock (GameManager.instance)
                        GameManager.instance.playerCount--;
                    await UniTask.SwitchToMainThread();
                    GameManager.instance.UpdatePlayerCount();
                }, true, GameManager.instance.destroyCancellationToken).Forget();
                GameManager.instance.AddChat($"<color=yellow><b>{playerComponent.userName}</b> left the game</color>");
                parallelWriter.AddComponent(chunkIndex, entity, new DestroyMark());
            }
        }
    }
}
