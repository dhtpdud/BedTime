using Cysharp.Threading.Tasks;
using OSY;
using System;
using System.Collections.Generic;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Aspects;
using Unity.Transforms;
using UnityEngine;
using WebSocketSharp;

public partial class SteveEventSystem : SystemBase
{
    #region string 캐싱
    public List<string> adminNames = new List<string>();

    public const string stringP = "p";
    public const string stringPush = "push";

    public const string stringD = "d";
    public const string stringDelay = "delay";

    public const string stringL = "l";
    public const string stringLoop = "loop";
    public const string stringLS = "ls";
    public const string stringLoopStop = "loopstop";
    public const string stringLSA = "lsa";
    public const string stringLoopStopAll = "loopstopall";

    public const string stringReset = "reset";
    public const string stringRA = "ra";
    public const string stringResetAll = "resetall";
    public bool isResettingAll;

    public const string stringDT = "dt";
    public const string stringDanceTime = "dancetime";
    CancellationTokenSource danceCTS;

    public const string stringCreeper = "creeper";

    public const string stringHead = "head";
    public const string stringRightHand = "righthand";
    public const string stringRH = "rh";
    public const string stringLeftHand = "lefthand";
    public const string stringLH = "lh";
    public const string stringRightFoot = "rightfoot";
    public const string stringRF = "rf";
    public const string stringLeftFoot = "leftfoot";
    public const string stringLF = "lf";


    public const string commandSplitter = "//";
    #endregion

    public Action<string> OnSpawn;
    //만약 string이 되면 hash를 string으로
    public Action<string, string, float, int> OnChat;
    public Action<int, int> OnSubscription;
    public Action<int> OnBan;


    BlobAssetReference<SteveConfig> steveConfig;
    BlobAssetReference<DonationConfig> donationConfig;
    TimeData timeData;

    Dictionary<FixedString64Bytes, CancellationTokenSource> loopCommands = new Dictionary<FixedString64Bytes, CancellationTokenSource>();

    protected override void OnCreate()
    {
        base.OnCreate();
        CheckedStateRef.RequireForUpdate<GameManagerSingletonComponent>();
        adminNames.Add($"{PlatformNameCache.Chzzk}!:보노 보노");
        adminNames.Add($"{PlatformNameCache.YouTube}!:Kamer");
    }
    protected override void OnStartRunning()
    {
        base.OnStartRunning();
        steveConfig = SystemAPI.GetSingleton<GameManagerSingletonComponent>().steveConfig;
        donationConfig = SystemAPI.GetSingleton<GameManagerSingletonComponent>().donationConfig;
        timeData = SystemAPI.Time;

        OnSpawn = (username) =>
        {
            EntityStoreComponent store = SystemAPI.GetSingleton<EntityStoreComponent>();

            //EntityCommandBuffer ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(CheckedStateRef.WorldUnmanaged);
            /*Debug.Log("시작전");

            NativeReference<Entity> spawnedPlayerRef = new NativeReference<Entity>(Allocator.TempJob);
            new SpawnEntity { parallelWriter = ecb.AsParallelWriter(), targetEntity = store.steve, spawnedEntityRef = spawnedPlayerRef }.ScheduleParallel(CheckedStateRef.Dependency).Complete();

            Debug.Log("잡완료");
            Entity spawnedPlayer = spawnedPlayerRef.Value;
            spawnedPlayerRef.Dispose();*/
            Entity spawnedPlayer = EntityManager.Instantiate(store.steve);
            Debug.Log("test");
            var playerComponent = EntityManager.GetComponentData<PlayerComponent>(spawnedPlayer);
            var localTransform = EntityManager.GetComponentData<LocalTransform>(spawnedPlayer);

            playerComponent.currentState = SteveState.Ragdoll;
            playerComponent.userName = username;
            localTransform.Position = GameManager.instance.spawnTransform.position;

            /*EntityManager.AddComponentData(spawnedSteve, new TimeLimitedLifeComponent
            {
                lifeTime = steveConfig.Value.DefalutLifeTime
            });*/
            EntityManager.SetComponentData(spawnedPlayer, playerComponent);
            EntityManager.SetComponentData(spawnedPlayer, localTransform);
            new PlayerInitJob { targetPlayerEntity = spawnedPlayer, userName = username, parallelWriter = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(CheckedStateRef.WorldUnmanaged).AsParallelWriter() }.ScheduleParallel(CheckedStateRef.Dependency).Complete();
        };
        OnChat = async (userName, text, addValueLife, payAmount) =>
        {
            EntityStoreComponent store = SystemAPI.GetSingleton<EntityStoreComponent>();
            int cheezeCount = (int)(payAmount * donationConfig.Value.objectCountFactor);

            foreach (var (playerRef, playerEntity) in SystemAPI.Query<RefRO<PlayerComponent>>().WithEntityAccess())
            {
                var player = playerRef.ValueRO;
                if (player.userName == userName)
                {
                    try
                    {
                        Debug.Log(userName +": "+ text);

                        if (text.Contains(commandSplitter))
                        {
                            string[] commandLines = text.Split(commandSplitter);
                            for (int commandLineIndex = 1; commandLineIndex < commandLines.Length; commandLineIndex++)
                            {
                                string[] commands = commandLines[commandLineIndex].Split(Utils.stringSpace);

                                SteveBodyPart part;
                                switch (commands[0].ToLower())
                                {
                                    case stringD:
                                    case stringDelay:
                                        await UniTask.Delay(TimeSpan.FromSeconds(float.Parse(commands[1])));
                                        break;

                                    case stringDT:
                                    case stringDanceTime:
                                        if (!adminNames.Contains(userName) && payAmount == 0) break;
                                        if (danceCTS != null && !danceCTS.IsCancellationRequested)
                                        {
                                            danceCTS?.Cancel();
                                        }
                                        danceCTS?.Dispose();

                                        await UniTask.Yield();
                                        await UniTask.Yield();

                                        float limitSec = float.Parse(commands[1]);
                                        limitSec = limitSec <= -1 || limitSec > 10 && !adminNames.Contains(userName) ? 10 : limitSec;

                                        danceCTS = CancellationTokenSource.CreateLinkedTokenSource(GameManager.instance.destroyCancellationToken);

                                        UniTask.RunOnThreadPool(async () =>
                                        {
                                            await UniTask.SwitchToMainThread();
                                            try
                                            {
                                                for (float timer = 0; limitSec == -1 || timer <= limitSec; timer += 0.5f)
                                                {
                                                    if (danceCTS.IsCancellationRequested) return;
                                                    CheckedStateRef.Dependency = new StevePopupJob().ScheduleParallel(CheckedStateRef.Dependency);
                                                    CheckedStateRef.Dependency.Complete();
                                                    if (danceCTS.IsCancellationRequested) return;
                                                    await UniTask.Delay(TimeSpan.FromSeconds(0.5f));
                                                    if (danceCTS.IsCancellationRequested) return;
                                                }
                                            }
                                            finally
                                            {
                                                danceCTS?.Cancel();
                                            }
                                        }, true, danceCTS.Token).Forget();
                                        break;

                                    case stringL:
                                    case stringLoop:
                                        FixedString64Bytes commandName1 = commands[1];
                                        FixedString64Bytes commandKey1 = $"{userName}!:{commandName1}";
                                        if (loopCommands.ContainsKey(commandKey1)) break;
                                        var loopCommandCTS = CancellationTokenSource.CreateLinkedTokenSource(GameManager.instance.destroyCancellationToken);

                                        Debug.Log($"루프명: {commandKey1}\r\n횟수: {commands[2]}, 간격: {commands[3]}");
                                        int maxCount = int.Parse(commands[2]);
                                        float delay = float.Parse(commands[3]);
                                        string remainCommands = commandSplitter + string.Join(commandSplitter, commandLines.SubArray(2, commandLines.Length - 2));
                                        int loopEndIndex = remainCommands.IndexOf('}');
                                        string targetCommands = remainCommands.Substring(0, loopEndIndex);
                                        commandLines = remainCommands.Substring(loopEndIndex).Split(commandSplitter);
                                        commandLineIndex = 0;
                                        loopCommands.TryAdd(commandKey1, loopCommandCTS);

                                        UniTask.RunOnThreadPool(async () =>
                                        {
                                            await UniTask.Yield();
                                            try
                                            {
                                                for (int count = 0; maxCount == -1 || count < maxCount; count++)
                                                {
                                                    if (loopCommandCTS.IsCancellationRequested) return;
                                                    OnChat(userName, targetCommands, addValueLife, payAmount);
                                                    await UniTask.Delay(TimeSpan.FromSeconds(delay));
                                                    if (loopCommandCTS.IsCancellationRequested) return;
                                                }
                                            }
                                            finally
                                            {
                                                loopCommands.Remove(commandKey1);
                                            }
                                        }, true, loopCommandCTS.Token).Forget();
                                        break;

                                    case stringLS:
                                    case stringLoopStop:
                                        FixedString64Bytes commandName2 = commands[1];
                                        FixedString64Bytes commandKey2 = $"{userName}!:{commandName2}";
                                        if (!loopCommands.ContainsKey(commandKey2)) return;
                                        loopCommands[commandKey2]?.Cancel();
                                        loopCommands[commandKey2]?.Dispose();
                                        loopCommands.Remove(commandKey2);
                                        break;

                                    case stringLSA:
                                    case stringLoopStopAll:
                                        foreach (var CTS in loopCommands.Values)
                                        {
                                            CTS?.Cancel();
                                            CTS?.Dispose();
                                        }
                                        loopCommands.Clear();
                                        break;

                                    case stringReset:
                                        new PlayerResetJob { spawnPoint = GameManager.instance.spawnTransform.position, targetEntity = playerEntity }.ScheduleParallel(CheckedStateRef.Dependency).Complete();
                                        break;
                                    case stringRA:
                                    case stringResetAll:
                                        if (!adminNames.Contains(userName) && payAmount == 0 || isResettingAll) break;
                                        isResettingAll = true;
                                        int batchCountRA = 0;
                                        foreach (var bodyPart in SystemAPI.Query<RefRO<BodyPartComponent>>())
                                        {
                                            new PlayerResetJob { spawnPoint = GameManager.instance.spawnTransform.position, targetEntity = bodyPart.ValueRO.ownerEntity }.ScheduleParallel(CheckedStateRef.Dependency).Complete();
                                            if(++batchCountRA > 100)
                                            {
                                                batchCountRA = 0;
                                                await UniTask.Yield();
                                            }
                                        };
                                        isResettingAll = false;
                                        break;

                                    case stringP:
                                    case stringPush:
                                        switch (commands[1].ToLower())
                                        {
                                            case stringHead:
                                                part = SteveBodyPart.Head;
                                                break;
                                            case stringRightHand:
                                            case stringRH:
                                                part = SteveBodyPart.RightHand;
                                                break;
                                            case stringLeftHand:
                                            case stringLH:
                                                part = SteveBodyPart.LeftHand;
                                                break;
                                            case stringRightFoot:
                                            case stringRF:
                                                part = SteveBodyPart.RightFoot;
                                                break;
                                            case stringLeftFoot:
                                            case stringLF:
                                                part = SteveBodyPart.LeftFoot;
                                                break;
                                            default:
                                                part = SteveBodyPart.Head;
                                                break;
                                        }
                                        float3 force = new float3(float.Parse(commands[2]), float.Parse(commands[3]), float.Parse(commands[4]));

                                        new BodyPartsPushJob { targetPart = part, force = force, targetEntity = playerEntity }.ScheduleParallel(CheckedStateRef.Dependency).Complete();
                                        break;

                                    case stringCreeper:
                                        int spawnCount = 0;
                                        try
                                        {
                                            spawnCount = int.Parse(commands[1]);
                                        }
                                        catch (IndexOutOfRangeException)
                                        {
                                            spawnCount = 1;
                                        }
                                        EntityCommandBuffer ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(CheckedStateRef.WorldUnmanaged);
                                        for (int i = 0; i < spawnCount; i++)
                                            new SpawnEntity { parallelWriter = ecb.AsParallelWriter(), targetEntity = store.creeper }.ScheduleParallel(CheckedStateRef.Dependency).Complete();
                                        break;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                        Debug.LogWarning("잘못된 명령어");
                    }
                }
            };

            /*if (addValueLife != 0)
                new OnChatSteveJob { hashID = hashID, addValue = addValueLife, peepoConfig = steveConfig.Value }.ScheduleParallel(CheckedStateRef.Dependency).Complete();*/
        };
        OnBan = (hashID) =>
        {
            //new OnBanJob { hashID = hashID }.ScheduleParallel(CheckedStateRef.Dependency).Complete();
        };
        OnSubscription = async (hashID, subMonth) =>
        {
            /*for (int i = 0; i < subMonth; i++)
            {
                new SpawnDonationObjectJob { donationConfig = donationConfig.Value, hashID = hashID, spawnObject = SystemAPI.GetSingleton<EntityStoreComponent>().cheeze, parallelWriter = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(CheckedStateRef.WorldUnmanaged).AsParallelWriter() }.ScheduleParallel(CheckedStateRef.Dependency).Complete();
                new SpawnDonationObjectJob { donationConfig = donationConfig.Value, hashID = hashID, spawnObject = SystemAPI.GetSingleton<EntityStoreComponent>().cheeze, parallelWriter = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(CheckedStateRef.WorldUnmanaged).AsParallelWriter() }.ScheduleParallel(CheckedStateRef.Dependency).Complete();
                new SpawnDonationObjectJob { donationConfig = donationConfig.Value, hashID = hashID, spawnObject = SystemAPI.GetSingleton<EntityStoreComponent>().cheeze, parallelWriter = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(CheckedStateRef.WorldUnmanaged).AsParallelWriter() }.ScheduleParallel(CheckedStateRef.Dependency).Complete();
                new SpawnDonationObjectJob { donationConfig = donationConfig.Value, hashID = hashID, spawnObject = SystemAPI.GetSingleton<EntityStoreComponent>().cheeze, parallelWriter = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(CheckedStateRef.WorldUnmanaged).AsParallelWriter() }.ScheduleParallel(CheckedStateRef.Dependency).Complete();
                new SpawnDonationObjectJob { donationConfig = donationConfig.Value, hashID = hashID, spawnObject = SystemAPI.GetSingleton<EntityStoreComponent>().cheeze, parallelWriter = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(CheckedStateRef.WorldUnmanaged).AsParallelWriter() }.ScheduleParallel(CheckedStateRef.Dependency).Complete();
                await Utils.YieldCaches.UniTaskYield;
            }*/
        };

    }

    protected override void OnUpdate()
    {
    }
    [BurstCompile]
    partial struct SpawnEntity : IJobEntity
    {
        [ReadOnly] public Entity targetEntity;
        public EntityCommandBuffer.ParallelWriter parallelWriter;

        public void Execute([ChunkIndexInQuery] int chunkIndex, in MainSpawnerTag mainSpawnerTag, in LocalTransform localTransform)
        {
            Debug.Log("test");
            Entity spawnedEntity = parallelWriter.Instantiate(chunkIndex, targetEntity);
            var initTransform = new LocalTransform { Position = localTransform.Position, Rotation = localTransform.Rotation, Scale = 1 };
            parallelWriter.SetComponent(chunkIndex, spawnedEntity, initTransform);
        }
    }
    [BurstCompile]
    partial struct PlayerInitJob : IJobEntity
    {
        [ReadOnly] public FixedString64Bytes userName;
        [ReadOnly] public Entity targetPlayerEntity;
        public EntityCommandBuffer.ParallelWriter parallelWriter;
        public void Execute([ChunkIndexInQuery] int chunkIndex, in Entity entity, in BodyPartComponent bodyPart)
        {
            if (bodyPart.ownerEntity == default) return;
            if (bodyPart.partType == SteveBodyPart.Head && bodyPart.ownerEntity == targetPlayerEntity)
                parallelWriter.AddComponent<NameTagComponent>(chunkIndex, entity, new NameTagComponent { name = userName });

        }
    }

    [BurstCompile]
    partial struct StevePopupJob : IJobEntity
    {
        public void Execute(in BodyPartComponent bodyPart, RigidBodyAspect rigidBodyAspect)
        {
            switch (bodyPart.partType)
            {
                case SteveBodyPart.RightHand:
                case SteveBodyPart.LeftHand:
                    rigidBodyAspect.LinearVelocity += new float3(0, 25, 0);
                    break;
            }
        }
    }
    [BurstCompile]
    partial struct PlayerResetJob : IJobEntity
    {
        [ReadOnly] public Entity targetEntity;
        [ReadOnly] public float3 spawnPoint;
        public void Execute(in BodyPartComponent bodyPartComponent, RigidBodyAspect rigidBodyAspect)
        {
            if (bodyPartComponent.ownerEntity == targetEntity)
            {
                rigidBodyAspect.Position = spawnPoint;
                rigidBodyAspect.LinearVelocity *= 0;
            }
        }
    }
    [BurstCompile]
    partial struct BodyPartsPushJob : IJobEntity
    {
        [ReadOnly] public Entity targetEntity;
        [ReadOnly] public SteveBodyPart targetPart;
        [ReadOnly] public float3 force;
        public void Execute(in BodyPartComponent bodyPartComponent, RigidBodyAspect rigidBodyAspect)
        {
            if (bodyPartComponent.ownerEntity == targetEntity && bodyPartComponent.partType == targetPart)
                rigidBodyAspect.LinearVelocity += force;
        }
    }
    /*[BurstCompile]
    partial struct OnChatSteveJob : IJobEntity
    {
        [ReadOnly] public int hashID;
        [ReadOnly] public float addValue;
        [ReadOnly] public SteveConfig peepoConfig;
        public void Execute(ref TimeLimitedLifeComponent timeLimitedLifeComponent, in PlayerComponent playerComponent)
        {
            //Debug.Log("채팅");
            if (hash.ID == hashID)
                timeLimitedLifeComponent.lifeTime = math.clamp(timeLimitedLifeComponent.lifeTime + addValue, 0, peepoConfig.MaxLifeTime);
        }
    }

    [BurstCompile]
    partial struct SpawnDonationObjectJob : IJobEntity
    {
        [ReadOnly] public int hashID;
        [ReadOnly] public Entity spawnObject;
        [ReadOnly] public DonationConfig donationConfig;
        public EntityCommandBuffer.ParallelWriter parallelWriter;
        public void Execute([ChunkIndexInQuery] int chunkIndex, in LocalTransform peepoLocalTransform, ref RandomDataComponent randomDataComponent, ref PlayerComponent playerComponent)
        {
            if (hash.ID == hashID)
            {
                playerComponent.currentState = SteveState.Ragdoll;
                Entity spawnedCheeze = parallelWriter.Instantiate(chunkIndex, spawnObject);
                randomDataComponent.Random = new Unity.Mathematics.Random(randomDataComponent.Random.NextUInt(uint.MinValue, uint.MaxValue));
                var initTransform = new LocalTransform { Position = peepoLocalTransform.Position, Rotation = quaternion.identity, Scale = randomDataComponent.Random.NextFloat(donationConfig.MinSize, donationConfig.MaxSize) };
                var initVelocity = new PhysicsVelocity { Linear = new float3(randomDataComponent.Random.NextFloat(-5f, 5f), 0, 0) };
                parallelWriter.SetComponent(chunkIndex, spawnedCheeze, initTransform);
                parallelWriter.SetComponent(chunkIndex, spawnedCheeze, initVelocity);
                parallelWriter.AddComponent(chunkIndex, spawnedCheeze, new TimeLimitedLifeComponent
                {
                    lifeTime = donationConfig.objectLifeTime
                });
            }
        }
    }*/
    [BurstCompile]
    partial struct SpawnDonationObjectUnknownJob : IJobEntity
    {
        [ReadOnly] public Entity spawnObject;
        [ReadOnly] public DonationConfig donationConfig;
        [ReadOnly] public float2 topRightScreenPoint;
        public EntityCommandBuffer.ParallelWriter parallelWriter;
        public void Execute([ChunkIndexInQuery] int chunkIndex, ref RandomDataComponent randomDataComponent, SpawnerComponent spawner)
        {
            Entity spawnedCheeze = parallelWriter.Instantiate(chunkIndex, spawnObject);
            randomDataComponent.Random = new Unity.Mathematics.Random(randomDataComponent.Random.NextUInt(uint.MinValue, uint.MaxValue));
            var initTransform = new LocalTransform { Position = new float3(randomDataComponent.Random.NextFloat(-topRightScreenPoint.x, topRightScreenPoint.x), topRightScreenPoint.y, 0), Rotation = quaternion.identity, Scale = randomDataComponent.Random.NextFloat(donationConfig.MinSize, donationConfig.MaxSize) };
            var initVelocity = new PhysicsVelocity { Linear = new float3(randomDataComponent.Random.NextFloat(-5f, 5f), 0, 0) };
            parallelWriter.SetComponent(chunkIndex, spawnedCheeze, initTransform);
            parallelWriter.SetComponent(chunkIndex, spawnedCheeze, initVelocity);
            parallelWriter.AddComponent(chunkIndex, spawnedCheeze, new TimeLimitedLifeComponent
            {
                lifeTime = donationConfig.objectLifeTime
            });
        }
    }

    /*[BurstCompile]
    public partial struct OnBanJob : IJobEntity
    {
        [ReadOnly] public int hashID;
        public EntityCommandBuffer.ParallelWriter parallelWriter;

        public void Execute(Entity entity, ref TimeLimitedLifeComponent timeLimitedLifeComponent, in HashIDComponent hash)
        {
            if (hash.ID == hashID)
            {
                timeLimitedLifeComponent.lifeTime = 0;
            }
        }
    }*/
}
