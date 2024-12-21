using Cysharp.Threading.Tasks;
using System;
using System.Linq;
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
            physicsWorld.OverlapBox(bedTransform.Position, bedTransform.Rotation, new float3(0.4f, 0.8f, 1.1f), ref hits, CollisionFilter.Default);

            NativeParallelHashMap<FixedString128Bytes, float> playerScoreInfoes = new NativeParallelHashMap<FixedString128Bytes, float>(GameManager.instance.viewerInfos.Count, Allocator.TempJob);
            new ProcessHitsJob { playerScoreInfoes = playerScoreInfoes, BodyPartLookup = bodyPartLookup, DeltaTime = SystemAPI.Time.DeltaTime, parallelWriter = ecb.AsParallelWriter(), Hits = hits, PlayerLookup = playerLookup }
            .Schedule(hits.Length, CheckedStateRef.Dependency).Complete();


            foreach (var playerScoreInfo in playerScoreInfoes)
            {
                if (playerScoreInfo.Key == "") continue;
                var playerInfo = GameManager.instance.viewerInfos[playerScoreInfo.Key];
                var playerScore = playerScoreInfo.Value.ToString("0.00");
                playerInfo.nameTagTMP.text = playerInfo.subscribeMonth > 0 ? $"{playerScoreInfo.Key.ToString()}\n[{playerInfo.subscribeMonth}Month]\nScore:{playerScore}" : $"{playerScoreInfo.Key.ToString()}\nScore:{playerScore}";
                if (Timer > 1)
                    playerInfo.UpdateNameTag().Forget();
                //playerInfo.UpdatePlayerBoardScore(playerScore);
                playerInfo.score = playerScore;
                GameManager.instance.UpdateLeaderBoard();
            }
            if (Timer > 1)
                Timer = 0;

            playerScoreInfoes.Dispose();
            hits.Dispose();
        }
    }

    [BurstCompile]
    public struct ProcessHitsJob : IJobFor
    {
        public NativeParallelHashMap<FixedString128Bytes, float> playerScoreInfoes;
        [ReadOnly] public NativeList<DistanceHit> Hits;
        [ReadOnly] public ComponentLookup<BodyPartComponent> BodyPartLookup;
        [ReadOnly] public ComponentLookup<PlayerComponent> PlayerLookup;
        public EntityCommandBuffer.ParallelWriter parallelWriter;
        public float DeltaTime;

        public void Execute(int index)
        {
            var hit = Hits[index];
            Entity entityHit = hit.Entity;
            var tempBodyPartLookup = BodyPartLookup;
            if (tempBodyPartLookup.HasComponent(entityHit))
            {
                Entity hitPartOwnerEntity = tempBodyPartLookup[entityHit].ownerEntity;

                var tempPlayerComponent = PlayerLookup[hitPartOwnerEntity];
                if (tempPlayerComponent.userName == "") return;

                if (!playerScoreInfoes.TryAdd(tempPlayerComponent.userName, tempPlayerComponent.score))
                    playerScoreInfoes[tempPlayerComponent.userName] += DeltaTime;

                tempPlayerComponent.score = playerScoreInfoes[tempPlayerComponent.userName];

                parallelWriter.SetComponent(index, hitPartOwnerEntity, tempPlayerComponent);
            }
        }
    }
}
