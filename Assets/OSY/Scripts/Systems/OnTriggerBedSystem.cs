using Cysharp.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

[BurstCompile]
partial class OnTriggerBedSystem : SystemBase
{
    float Timer;
    [BurstCompile]
    protected override void OnUpdate()
    {
        Timer += SystemAPI.Time.DeltaTime;
        //물리월드 싱글턴 불러오기
        //CastHit NativeList 만들기
        //물리월드를 통해 원하는 충돌검사 실행하기
        PhysicsWorldSingleton physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();

        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(CheckedStateRef.WorldUnmanaged);

        var bodyPartLookup = SystemAPI.GetComponentLookup<BodyPartComponent>(true);
        var playerLookup = SystemAPI.GetComponentLookup<PlayerComponent>(false);

        foreach (var (bed, bedTransformRef) in SystemAPI.Query<RefRO<BedTag>, RefRO<LocalTransform>>())
        {
            NativeList<DistanceHit> hits = new NativeList<DistanceHit>(Allocator.TempJob);

            var bedTransform = bedTransformRef.ValueRO;
            physicsWorld.OverlapBox(bedTransform.Position, bedTransform.Rotation, new float3(0.6f, 0.8f, 1.3f), ref hits, CollisionFilter.Default);
            NativeParallelHashMap<FixedString128Bytes, float> playerScoreInfoes = new NativeParallelHashMap<FixedString128Bytes, float>(GameManager.instance.viewerInfos.Count, Allocator.TempJob);
            new ProcessHitsJob { playerScoreInfoes = playerScoreInfoes.AsParallelWriter(), BodyPartLookup = bodyPartLookup, DeltaTime = SystemAPI.Time.DeltaTime, parallelWriter = ecb.AsParallelWriter(), Hits = hits, PlayerLookup = playerLookup }
            .Schedule(hits.Length, 500, CheckedStateRef.Dependency).Complete();

            foreach (var playerScoreInfo in playerScoreInfoes)
            {
                if(playerScoreInfo.Key == "") continue;
                var playerInfo = GameManager.instance.viewerInfos[playerScoreInfo.Key];
                var playerScore = playerScoreInfo.Value.ToString("0.00");
                playerInfo.nameTagTMP.text = playerInfo.subscribeMonth > 0 ? $"{playerScoreInfo.Key.ToString()}\n[{playerInfo.subscribeMonth}Month]\nScore:{playerScore}" : $"{playerScoreInfo.Key.ToString()}\nScore:{playerScore}";
                playerInfo.UpdatePlayerBoardScore(playerScore);
                if (Timer > 1)
                {
                    Timer = 0;
                    playerInfo.UpdateNameTag().Forget();
                }
            }

            playerScoreInfoes.Dispose();
            hits.Dispose();
        }
    }

    [BurstCompile]
    public struct ProcessHitsJob : IJobParallelFor
    {
        public NativeParallelHashMap<FixedString128Bytes, float>.ParallelWriter playerScoreInfoes;
        [ReadOnly] public NativeList<DistanceHit> Hits;
        [ReadOnly] public ComponentLookup<BodyPartComponent> BodyPartLookup;
        [ReadOnly] public ComponentLookup<PlayerComponent> PlayerLookup;
        public EntityCommandBuffer.ParallelWriter parallelWriter;
        public float DeltaTime;

        public void Execute(int index)
        {
            var hit = Hits[index];
            Entity entityHit = hit.Entity;
            if (BodyPartLookup.HasComponent(entityHit))
            {
                Entity hitPartOwnerEntity = BodyPartLookup[entityHit].ownerEntity;

                var tempPlayerComponent = PlayerLookup[hitPartOwnerEntity];
                if (tempPlayerComponent.userName == "") return;
                tempPlayerComponent.score += DeltaTime;

                playerScoreInfoes.TryAdd(tempPlayerComponent.userName, tempPlayerComponent.score);

                parallelWriter.SetComponent(index, hitPartOwnerEntity, tempPlayerComponent);
            }
        }
    }
}
