using Cysharp.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

/// <summary>
/// �÷��̾ ħ�� ����(OverlapBox) ���� �ö���� ������ �߰��ϴ� ���� �ý����Դϴ�.
/// IL2CPP ����� ������ ������ �ʵ��� Job�� SystemAPI ȣ�� ����� �����߽��ϴ�.
/// </summary>
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public sealed partial class OnTriggerBedSystem : SystemBase
{
    /// <summary> 1�ʸ��� UI ������Ʈ�� �ϱ� ���� ������ Ÿ�̸�. </summary>
    private float _timer;

    [BurstCompile]
    protected override void OnUpdate()
    {
        // 1) ���� ������(=SystemBase.OnUpdate)������ UnityEngine API / GameManager.instance ���� �����մϴ�.
        var time = SystemAPI.Time;
        float deltaTime = time.DeltaTime;
        _timer += deltaTime;

        // PhysicsWorldSingleton (���� ����)
        var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();

        // EntityCommandBuffer (������ DestroyMark �� ����)
        var ecb = SystemAPI
            .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged);

        // ComponentLookup �̸� ȹ��
        var bodyPartLookup = GetComponentLookup<BodyPartComponent>(isReadOnly: true);
        var playerLookup = GetComponentLookup<PlayerComponent>(isReadOnly: false);  // Job���� score ������Ʈ

        // ħ��(Entity)�� �޸� LocalTransform �� ��ȸ
        foreach (var (bedTag, bedTransformRef)
                 in SystemAPI.Query<RefRO<BedTag>, RefRO<LocalTransform>>())
        {
            // 2) OverlapBox ���(�浹�� ��ƼƼ��)�� NativeList�� ����ϴ�.
            var hits = new NativeList<DistanceHit>(Allocator.TempJob);
            physicsWorld.OverlapBox(
                center: bedTransformRef.ValueRO.Position,
                orientation: bedTransformRef.ValueRO.Rotation,
                halfExtents: new float3(0.4f, 0.8f, 1.1f),
                ref hits,
                filter: CollisionFilter.Default
            );

            // 3) Job���� �� HashMap (�÷��̾�� ���� ����)
            //    GameManager.instance.viewerInfos.Count �� ������ ������ �ݵ�� OnUpdate �ٱ����� ���� �����;� �մϴ�.
            int viewerInfosCount = 0;
            if (GameManager.instance != null)
                viewerInfosCount = GameManager.instance.viewerInfos.Count;

            var playerScoreInfoes = new NativeParallelHashMap<FixedString128Bytes, float>(
                capacity: math.max(1, viewerInfosCount),
                allocator: Allocator.TempJob
            );

            // 4) ���� ���� ��� Job
            //    SystemAPI, GameManager.instance, UnityEngine.* ������ ���⼭ ���� �ʽ��ϴ�.
            var addScoreJob = new AddScoreJob
            {
                Hits = hits,
                BodyPartLookup = bodyPartLookup,
                PlayerLookup = playerLookup,
                playerScoreInfoes = playerScoreInfoes,
                deltaTime = deltaTime,
                parallelWriter = ecb.AsParallelWriter()
            };

            // ���� ũ��(=hits.Length)�� for���� ���� �����ϴ� IJobFor
            // (ScheduleParallel ��� Schedule/Complete ������ ����ص� �˴ϴ�.)
            var handle = addScoreJob.Schedule(hits.Length, Dependency);
            handle.Complete();
            // �� ���������� Job �Ϸ� �Ŀ� �ٷ� ���� �����忡�� �ؼ��ؾ� �ϹǷ� Complete() ȣ��

            // 5) Job���� ���� ����� ���� �����忡�� �о� UI, GameManager ������Ʈ
            foreach (var kvp in playerScoreInfoes)
            {
                var userName = kvp.Key;
                if (userName.IsEmpty)
                    continue;

                float updatedScore = kvp.Value;

                // ���� Job�� �������Ƿ� GameManager.instance, UnityEngine API ���� ����
                if (GameManager.instance != null && GameManager.instance.viewerInfos.ContainsKey(userName))
                {
                    var playerInfo = GameManager.instance.viewerInfos[userName];
                    // ���ϴ� ��� UI ǥ��
                    string scoreStr = updatedScore.ToString("F2");
                    playerInfo.score = scoreStr;

                    playerInfo.nameTagTMP.text = (playerInfo.subscribeMonth > 0)
                        ? $"{userName}\n[{playerInfo.subscribeMonth}Month]\nScore:{scoreStr}"
                        : $"{userName}\nScore:{scoreStr}";

                    // 1�ʸ��� �� ������ NameTag�� ���ΰ�ħ�غ��� ����
                    if (_timer > 1f)
                    {
                        playerInfo.UpdateNameTag().Forget();
                    }
                }
            }
            // Ÿ�̸� ����
            if (_timer > 1f)
                _timer = 0f;

            // �Ҵ��ص� NativeList, HashMap ����
            playerScoreInfoes.Dispose();
            hits.Dispose();
        }
    }

    /// <summary>
    /// OverlapBox�� ������ DistanceHit ������ ��������,
    /// ����(BodyPart) -> ������(�÷��̾�) ���� �� ������ �����ϴ� Job
    /// </summary>
    [BurstCompile]
    private struct AddScoreJob : IJobFor
    {
        [ReadOnly] public NativeList<DistanceHit> Hits;
        [ReadOnly] public ComponentLookup<BodyPartComponent> BodyPartLookup;
        // �÷��̾� ������ ���� �����ؾ� �ϹǷ� ReadOnly�� �ƴ�
        public ComponentLookup<PlayerComponent> PlayerLookup;

        public NativeParallelHashMap<FixedString128Bytes, float> playerScoreInfoes;

        [ReadOnly] public float deltaTime;
        public EntityCommandBuffer.ParallelWriter parallelWriter;

        public void Execute(int index)
        {
            var hit = Hits[index];
            Entity hitEntity = hit.Entity;

            // BodyPart�� �ִ� ��ƼƼ���� Ȯ��
            if (!BodyPartLookup.HasComponent(hitEntity))
                return;

            // ����(�÷��̾�) ��ƼƼ�� ã�� PlayerComponent ����
            var bodyPart = BodyPartLookup[hitEntity];
            Entity ownerEntity = bodyPart.ownerEntity;

            if (!PlayerLookup.HasComponent(ownerEntity))
                return;

            var playerComp = PlayerLookup[ownerEntity];
            if (playerComp.userName.IsEmpty)
                return;

            // NativeParallelHashMap�� (�÷��̾� �̸� -> ����) ����
            if (!playerScoreInfoes.TryAdd(playerComp.userName, playerComp.score))
            {
                // �̹� ������ += deltaTime
                playerScoreInfoes[playerComp.userName] += deltaTime;
            }

            // ������ ������ �ٽ� PlayerComponent�� ���
            float newScore = playerScoreInfoes[playerComp.userName];
            playerComp.score = newScore;

            // ���� ECB�� ������Ʈ ������Ʈ
            parallelWriter.SetComponent(index, ownerEntity, playerComp);
        }
    }
}
