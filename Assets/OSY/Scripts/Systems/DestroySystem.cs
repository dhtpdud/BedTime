using Unity.Burst;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

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
        new TimeLimitedJob { parallelWriter = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(), time = SystemAPI.Time, gameManager = SystemAPI.GetSingleton<GameManagerSingletonComponent>() }.ScheduleParallel();
        //new TimeLimitedPeepoJob { time = SystemAPI.Time, parallelWriter = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(), gameManager = SystemAPI.GetSingleton<GameManagerSingletonComponent>() }.ScheduleParallel();
        new DestroyJob { parallelWriter = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter() }.ScheduleParallel();
    }

    [BurstCompile]
    partial struct DestroyJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter parallelWriter;
        public void Execute([ChunkIndexInQuery] int chunkIndex, Entity entity, DestroyMark mark)
        {
            parallelWriter.DestroyEntity(chunkIndex, entity);
        }
    }
    [BurstCompile]
    [WithNone(typeof(SteveComponent))]
    partial struct TimeLimitedJob : IJobEntity
    {
        [ReadOnly] public TimeData time;
        public EntityCommandBuffer.ParallelWriter parallelWriter;
        [ReadOnly] public GameManagerSingletonComponent gameManager;
        public void Execute([ChunkIndexInQuery] int chunkIndex, Entity entity, ref TimeLimitedLifeComponent timeLimitedLifeComponent)
        {
            timeLimitedLifeComponent.lifeTime -= time.DeltaTime;
            if (timeLimitedLifeComponent.lifeTime <= 0 && (gameManager.dragingEntityInfo.entity != entity))
                parallelWriter.AddComponent(chunkIndex, entity, new DestroyMark());
        }
    }
    /*partial struct TimeLimitedPeepoJob : IJobEntity
    {
        [ReadOnly] public TimeData time;
        public EntityCommandBuffer.ParallelWriter parallelWriter;
        [ReadOnly] public GameManagerSingletonComponent gameManager;
        public void Execute([ChunkIndexInQuery] int chunkIndex, Entity entity, ref TimeLimitedLifeComponent timeLimitedLifeComponent, in SteveComponent peepoComponent, ref LocalTransform localTransform, in HashIDComponent hash)
        {
            SteveConfig peepoConfig = default;
            localTransform.Scale = math.clamp(math.lerp(localTransform.Scale, timeLimitedLifeComponent.lifeTime / peepoConfig.DefalutLifeTime * peepoConfig.DefaultSize, time.DeltaTime), peepoConfig.MinSize, peepoConfig.MaxSize);
            timeLimitedLifeComponent.lifeTime -= time.DeltaTime;
            if (timeLimitedLifeComponent.lifeTime <= 0 && (gameManager.dragingEntityInfo.entity != entity))
            {
                //Debug.Log($"»èÁ¦: {peepoComponent.hashID}");
                GameManager.instance.viewerInfos[hash.ID].OnDestroy();
                parallelWriter.AddComponent(chunkIndex, entity, new DestroyMark());
            }
        }
    }*/
}
