using Unity.Burst;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
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

        //NativeList<FixedString128Bytes> destroyNames = new NativeList<FixedString128Bytes>(Allocator.TempJob);
        new TimeLimitedPlayerJob { /*destroyNames = destroyNames.AsParallelWriter(),*/ time = SystemAPI.Time, parallelWriter = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(), gameManager = SystemAPI.GetSingleton<GameManagerSingletonComponent>() }.ScheduleParallel();


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
    [WithNone(typeof(PlayerComponent))]
    partial struct TimeLimitedJob : IJobEntity
    {
        [ReadOnly] public TimeData time;
        public EntityCommandBuffer.ParallelWriter parallelWriter;
        [ReadOnly] public GameManagerSingletonComponent gameManager;
        public void Execute([ChunkIndexInQuery] int chunkIndex, in Entity entity, ref TimeLimitedLifeComponent timeLimitedLifeComponent)
        {
            timeLimitedLifeComponent.lifeTime -= time.DeltaTime;
            if (timeLimitedLifeComponent.lifeTime <= 0 && (gameManager.dragingEntityInfo.entity != entity))
                parallelWriter.AddComponent(chunkIndex, entity, new DestroyMark());
        }
    }
    partial struct TimeLimitedPlayerJob : IJobEntity
    {
        //public NativeList<FixedString128Bytes>.ParallelWriter destroyNames;
        [ReadOnly] public TimeData time;
        public EntityCommandBuffer.ParallelWriter parallelWriter;
        [ReadOnly] public GameManagerSingletonComponent gameManager;
        public void Execute([ChunkIndexInQuery] int chunkIndex, in Entity entity, ref TimeLimitedLifeComponent timeLimitedLifeComponent, in PlayerComponent playerComponent)
        {
            //SteveConfig peepoConfig = default;
            timeLimitedLifeComponent.lifeTime -= time.DeltaTime;
            if (timeLimitedLifeComponent.lifeTime <= 0 && (gameManager.dragingEntityInfo.entity != entity))
            {
                //Debug.Log($"»èÁ¦: {peepoComponent.hashID}");
                GameManager.instance.viewerInfos[playerComponent.userName].OnDestroy(playerComponent.userName);
                //destroyNames.AddNoResize(playerComponent.userName);
                parallelWriter.AddComponent(chunkIndex, entity, new DestroyMark());
            }
        }
    }
}
