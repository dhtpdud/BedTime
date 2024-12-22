using Cysharp.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

/// <summary>
/// 플레이어가 침대 영역(OverlapBox) 위에 올라오면 점수를 추가하는 예제 시스템입니다.
/// IL2CPP 빌드시 에러가 생기지 않도록 Job과 SystemAPI 호출 방식을 정리했습니다.
/// </summary>
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public sealed partial class OnTriggerBedSystem : SystemBase
{
    /// <summary> 1초마다 UI 업데이트를 하기 위한 간단한 타이머. </summary>
    private float _timer;

    [BurstCompile]
    protected override void OnUpdate()
    {
        // 1) 메인 쓰레드(=SystemBase.OnUpdate)에서만 UnityEngine API / GameManager.instance 등을 접근합니다.
        var time = SystemAPI.Time;
        float deltaTime = time.DeltaTime;
        _timer += deltaTime;

        // PhysicsWorldSingleton (물리 월드)
        var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();

        // EntityCommandBuffer (끝나고 DestroyMark 등 적용)
        var ecb = SystemAPI
            .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged);

        // ComponentLookup 미리 획득
        var bodyPartLookup = GetComponentLookup<BodyPartComponent>(isReadOnly: true);
        var playerLookup = GetComponentLookup<PlayerComponent>(isReadOnly: false);  // Job에서 score 업데이트

        // 침대(Entity)에 달린 LocalTransform 등 조회
        foreach (var (bedTag, bedTransformRef)
                 in SystemAPI.Query<RefRO<BedTag>, RefRO<LocalTransform>>())
        {
            // 2) OverlapBox 결과(충돌한 엔티티들)를 NativeList에 담습니다.
            var hits = new NativeList<DistanceHit>(Allocator.TempJob);
            physicsWorld.OverlapBox(
                center: bedTransformRef.ValueRO.Position,
                orientation: bedTransformRef.ValueRO.Rotation,
                halfExtents: new float3(0.4f, 0.8f, 1.1f),
                ref hits,
                filter: CollisionFilter.Default
            );

            // 3) Job에서 쓸 HashMap (플레이어별로 점수 누적)
            //    GameManager.instance.viewerInfos.Count 등 관리형 접근은 반드시 OnUpdate 바깥에서 값만 가져와야 합니다.
            int viewerInfosCount = 0;
            if (GameManager.instance != null)
                viewerInfosCount = GameManager.instance.viewerInfos.Count;

            var playerScoreInfoes = new NativeParallelHashMap<FixedString128Bytes, float>(
                capacity: math.max(1, viewerInfosCount),
                allocator: Allocator.TempJob
            );

            // 4) 실제 점수 계산 Job
            //    SystemAPI, GameManager.instance, UnityEngine.* 참조는 여기서 하지 않습니다.
            var addScoreJob = new AddScoreJob
            {
                Hits = hits,
                BodyPartLookup = bodyPartLookup,
                PlayerLookup = playerLookup,
                playerScoreInfoes = playerScoreInfoes,
                deltaTime = deltaTime,
                parallelWriter = ecb.AsParallelWriter()
            };

            // 일정 크기(=hits.Length)의 for문을 병렬 수행하는 IJobFor
            // (ScheduleParallel 대신 Schedule/Complete 등으로 사용해도 됩니다.)
            var handle = addScoreJob.Schedule(hits.Length, Dependency);
            handle.Complete();
            // 이 예제에서는 Job 완료 후에 바로 메인 스레드에서 해석해야 하므로 Complete() 호출

            // 5) Job에서 계산된 결과를 메인 스레드에서 읽어 UI, GameManager 업데이트
            foreach (var kvp in playerScoreInfoes)
            {
                var userName = kvp.Key;
                if (userName.IsEmpty)
                    continue;

                float updatedScore = kvp.Value;

                // 이제 Job이 끝났으므로 GameManager.instance, UnityEngine API 접근 가능
                if (GameManager.instance != null && GameManager.instance.viewerInfos.ContainsKey(userName))
                {
                    var playerInfo = GameManager.instance.viewerInfos[userName];
                    // 원하는 대로 UI 표시
                    string scoreStr = updatedScore.ToString("F2");
                    playerInfo.score = scoreStr;

                    playerInfo.nameTagTMP.text = (playerInfo.subscribeMonth > 0)
                        ? $"{userName}\n[{playerInfo.subscribeMonth}Month]\nScore:{scoreStr}"
                        : $"{userName}\nScore:{scoreStr}";

                    // 1초마다 한 번씩만 NameTag를 새로고침해보는 예시
                    if (_timer > 1f)
                    {
                        playerInfo.UpdateNameTag().Forget();
                    }
                }
            }
            // 타이머 리셋
            if (_timer > 1f)
                _timer = 0f;

            // 할당해둔 NativeList, HashMap 해제
            playerScoreInfoes.Dispose();
            hits.Dispose();
        }
    }

    /// <summary>
    /// OverlapBox로 수집된 DistanceHit 정보를 바탕으로,
    /// 부위(BodyPart) -> 소유자(플레이어) 매핑 후 점수를 누적하는 Job
    /// </summary>
    [BurstCompile]
    private struct AddScoreJob : IJobFor
    {
        [ReadOnly] public NativeList<DistanceHit> Hits;
        [ReadOnly] public ComponentLookup<BodyPartComponent> BodyPartLookup;
        // 플레이어 정보는 점수 갱신해야 하므로 ReadOnly가 아님
        public ComponentLookup<PlayerComponent> PlayerLookup;

        public NativeParallelHashMap<FixedString128Bytes, float> playerScoreInfoes;

        [ReadOnly] public float deltaTime;
        public EntityCommandBuffer.ParallelWriter parallelWriter;

        public void Execute(int index)
        {
            var hit = Hits[index];
            Entity hitEntity = hit.Entity;

            // BodyPart가 있는 엔티티인지 확인
            if (!BodyPartLookup.HasComponent(hitEntity))
                return;

            // 주인(플레이어) 엔티티를 찾아 PlayerComponent 갱신
            var bodyPart = BodyPartLookup[hitEntity];
            Entity ownerEntity = bodyPart.ownerEntity;

            if (!PlayerLookup.HasComponent(ownerEntity))
                return;

            var playerComp = PlayerLookup[ownerEntity];
            if (playerComp.userName.IsEmpty)
                return;

            // NativeParallelHashMap에 (플레이어 이름 -> 점수) 누적
            if (!playerScoreInfoes.TryAdd(playerComp.userName, playerComp.score))
            {
                // 이미 있으면 += deltaTime
                playerScoreInfoes[playerComp.userName] += deltaTime;
            }

            // 누적된 점수를 다시 PlayerComponent에 기록
            float newScore = playerScoreInfoes[playerComp.userName];
            playerComp.score = newScore;

            // 병렬 ECB로 컴포넌트 업데이트
            parallelWriter.SetComponent(index, ownerEntity, playerComp);
        }
    }
}
