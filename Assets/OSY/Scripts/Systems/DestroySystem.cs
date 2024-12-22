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
        new TimeLimitedJob { parallelWriter = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(), time = SystemAPI.Time, gameManager = SystemAPI.GetSingleton<GameManagerSingletonComponent>() }.ScheduleParallel();

        var bodyPartLookup = SystemAPI.GetComponentLookup<BodyPartComponent>(true);
        //NativeList<FixedString128Bytes> destroyNames = new NativeList<FixedString128Bytes>(Allocator.TempJob);
        new TimeLimitedPlayerJob { BodyPartLookup = bodyPartLookup, /*destroyNames = destroyNames.AsParallelWriter(),*/ time = SystemAPI.Time, parallelWriter = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(), gameManager = SystemAPI.GetSingleton<GameManagerSingletonComponent>() }.ScheduleParallel();


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
        [ReadOnly] public ComponentLookup<BodyPartComponent> BodyPartLookup;
        public void Execute([ChunkIndexInQuery] int chunkIndex, in Entity entity, ref TimeLimitedLifeComponent timeLimitedLifeComponent, in PlayerComponent playerComponent)
        {
            //SteveConfig peepoConfig = default;
            timeLimitedLifeComponent.lifeTime -= time.DeltaTime;
            if (timeLimitedLifeComponent.lifeTime <= 0)
            {
                if (BodyPartLookup.HasComponent(gameManager.dragingEntityInfo.entity) && BodyPartLookup[gameManager.dragingEntityInfo.entity].ownerEntity == entity)
                    return;
                timeLimitedLifeComponent.lifeTime = 99;
                //Debug.Log($"»èÁ¦: {peepoComponent.hashID}");
                //GameManager.instance.viewerInfos[playerComponent.userName].OnDestroy(playerComponent.userName);
                UniTask.RunOnThreadPool(async () =>
                {
                    lock (GameManager.instance)
                        GameManager.instance.playerCount--;
                    await UniTask.SwitchToMainThread();
                    GameManager.instance.UpdatePlayerCount();
                }, true, GameManager.instance.destroyCancellationToken).Forget();
                GameManager.instance.AddChat($"<color=yellow><b>{playerComponent.userName}</b> left the game</color>");
                //destroyNames.AddNoResize(playerComponent.userName);
                parallelWriter.AddComponent(chunkIndex, entity, new DestroyMark());
            }
        }
    }
}
