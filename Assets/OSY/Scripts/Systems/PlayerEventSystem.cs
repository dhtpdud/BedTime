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

[BurstCompile]
public partial class PlayerEventSystem : SystemBase
{
    #region string 캐싱
    public List<FixedString128Bytes> adminNames = new List<FixedString128Bytes>();

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
    public const string stringLSAA = "lsaa";
    public const string stringLoopStopAllAdmin = "loopstopalladmin";

    public const string stringReset = "reset";
    public const string stringRA = "ra";
    public const string stringResetAll = "resetall";
    public bool isResettingAll;

    public const string stringDT = "dt";
    public const string stringDanceTime = "dancetime";
    CancellationTokenSource danceCTS;

    public const string stringCreeper = "creeper";

    public const string stringHead = "head";
    public const string stringChest = "chest";
    public const string stringSpine = "spine";

    public const string stringRightUpperArm = "rightUpperArm";
    public const string stringRUA = "rua";
    public const string stringRightHand = "righthand";
    public const string stringRH = "rh";

    public const string stringLeftUpperArm = "leftUpperArm";
    public const string stringLUA = "lua";
    public const string stringLeftHand = "lefthand";
    public const string stringLH = "lh";

    public const string stringRightThigh = "rightthigh";
    public const string stringRT = "rt";
    public const string stringRightFoot = "rightfoot";
    public const string stringRF = "rf";

    public const string stringLeftThigh = "leftthigh";
    public const string stringLT = "lt";
    public const string stringLeftFoot = "leftfoot";
    public const string stringLF = "lf";

    public const string stringALL = "all";


    public const string commandSplitter = "//";
    #endregion

    public Action<string> OnSpawn;
    //만약 string이 되면 hash를 string으로
    public Action<string, string, float, int> OnChat;
    public Action<int, int> OnSubscription;
    public Action<int> OnBan;


    /*BlobAssetReference<SteveConfig> steveConfig;
    BlobAssetReference<DonationConfig> donationConfig;*/
    TimeData timeData;

    Dictionary<FixedString128Bytes, Dictionary<FixedString128Bytes, CancellationTokenSource>> loopCommands = new Dictionary<FixedString128Bytes, Dictionary<FixedString128Bytes, CancellationTokenSource>>();

    EntityStoreComponent store;

    protected override void OnCreate()
    {
        base.OnCreate();
        CheckedStateRef.RequireForUpdate<EntityStoreComponent>();
        adminNames.Add($"{PlatformNameCache.Chzzk}\n보노 보노");
        adminNames.Add($"{PlatformNameCache.YouTube}\nKamer");
    }
    protected override void OnStartRunning()
    {
        base.OnStartRunning();
        /*steveConfig = SystemAPI.GetSingleton<GameManagerSingletonComponent>().steveConfig;
        donationConfig = SystemAPI.GetSingleton<GameManagerSingletonComponent>().donationConfig;*/
        timeData = SystemAPI.Time;
        store = SystemAPI.GetSingleton<EntityStoreComponent>();


        var initPlayerComponent = new PlayerComponent { currentState = SteveState.Ragdoll };
        var initPlayerLocalTransform = EntityManager.GetComponentData<LocalTransform>(store.steve);
        var initLife = new TimeLimitedLifeComponent { lifeTime = 86400 };
        initPlayerLocalTransform.Position = GameManager.instance.playerSpawnTransform.position;
        //new LocalTransform { Position = GameManager.instance.spawnTransform.position, Rotation = quaternion.Euler(0, 180, 0), Scale = 1 };

        OnSpawn = async (userNameString) =>
        {
            lock (GameManager.instance)
                GameManager.instance.playerCount++;

            FixedString128Bytes userName = new FixedString128Bytes(userNameString);
            EntityCommandBuffer ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(CheckedStateRef.WorldUnmanaged);

            Entity spawnedPlayer = ecb.Instantiate(store.steve);
            initPlayerComponent.userName = userName;
            ecb.SetComponent(spawnedPlayer, initPlayerComponent);
            ecb.SetComponent(spawnedPlayer, initPlayerLocalTransform);
            ecb.AddComponent(spawnedPlayer, initLife);

            await UniTask.Yield();
            await UniTask.Yield();

            ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(CheckedStateRef.WorldUnmanaged);


            foreach (var (playerRef, entity) in SystemAPI.Query<RefRO<PlayerComponent>>().WithEntityAccess())
            {
                if (playerRef.ValueRO.userName == userName)
                    new PlayerNameTagJob { targetPlayerEntity = entity, userName = userName, parallelWriter = ecb.AsParallelWriter() }.ScheduleParallel(CheckedStateRef.Dependency).Complete();
            }
        };
        OnChat = async (userNameString, text, addValueLife, payAmount) =>
        {
            int cheezeCount = (int)(payAmount * 1/*donationConfig.Value.objectCountFactor*/);
            bool isUnkown = userNameString == null || userNameString == string.Empty;
            bool isAdmin = false;
            FixedString128Bytes userName = string.Empty;
            if (!isUnkown)
            {
                userName = new FixedString128Bytes(userNameString);
                isAdmin = adminNames.Contains(userName);
            }
            foreach (var (playerInfoRef, lifeRef) in SystemAPI.Query<RefRO<PlayerComponent>, RefRW<TimeLimitedLifeComponent>>())
            {
                if (playerInfoRef.ValueRO.userName == userName)
                    lifeRef.ValueRW.lifeTime = 86400;
            }
            try
            {
                Debug.Log(userName + ": " + text);
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
                                if (!isAdmin && payAmount == 0) break;
                                if (danceCTS != null && !danceCTS.IsCancellationRequested)
                                {
                                    danceCTS?.Cancel();
                                }
                                danceCTS?.Dispose();

                                await UniTask.Yield();
                                await UniTask.Yield();

                                float limitSec = float.Parse(commands[1]);
                                limitSec = (limitSec <= -1 || limitSec > 10) && !isAdmin ? 10 : limitSec;

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
                                if (isUnkown) break;
                                FixedString128Bytes commandName1 = commands[1];
                                if (loopCommands.ContainsKey(userName))
                                {
                                    if (loopCommands[userName].ContainsKey(commandName1))
                                        break;
                                }
                                else
                                    loopCommands.TryAdd(userName, new Dictionary<FixedString128Bytes, CancellationTokenSource>());

                                var loopCommandCTS = CancellationTokenSource.CreateLinkedTokenSource(GameManager.instance.destroyCancellationToken);

                                Debug.Log($"루프명: {userName}-{commandName1}\r\n횟수: {commands[2]}, 간격: {commands[3]}");
                                int maxCount = int.Parse(commands[2]);
                                float delay = float.Parse(commands[3]);
                                string remainCommands = commandSplitter + string.Join(commandSplitter, commandLines.SubArray(2, commandLines.Length - 2));
                                int loopEndIndex = remainCommands.IndexOf(';');
                                string targetCommands = remainCommands.Substring(0, loopEndIndex);
                                commandLines = remainCommands.Substring(loopEndIndex).Split(commandSplitter);
                                commandLineIndex = 0;
                                loopCommands[userName].TryAdd(commandName1, loopCommandCTS);

                                UniTask.RunOnThreadPool(async () =>
                                {
                                    await UniTask.Yield();
                                    try
                                    {
                                        for (int count = 0; maxCount == -1 || count < maxCount; count++)
                                        {
                                            if (loopCommandCTS.IsCancellationRequested) return;
                                            OnChat(userNameString, targetCommands, addValueLife, payAmount);
                                            await UniTask.Delay(TimeSpan.FromSeconds(delay));
                                            if (loopCommandCTS.IsCancellationRequested) return;
                                        }
                                    }
                                    finally
                                    {
                                        loopCommands[userName].Remove(commandName1);
                                    }
                                }, true, loopCommandCTS.Token).Forget();
                                break;

                            case stringLS:
                            case stringLoopStop:
                                if (isUnkown) break;
                                FixedString128Bytes commandName2 = commands[1];
                                if (!loopCommands.ContainsKey(userName) || !loopCommands[userName].ContainsKey(commandName2)) return;
                                loopCommands[userName]?[commandName2]?.Cancel();
                                loopCommands[userName]?[commandName2]?.Dispose();
                                loopCommands[userName]?.Remove(commandName2);
                                break;

                            case stringLSA:
                            case stringLoopStopAll:
                                if (isUnkown) break;
                                foreach (var CTS in loopCommands[userName].Values)
                                {
                                    CTS?.Cancel();
                                    CTS?.Dispose();
                                }
                                loopCommands[userName].Clear();
                                break;

                            case stringLSAA:
                            case stringLoopStopAllAdmin:
                                if (!isAdmin) break;
                                foreach (var tempName in loopCommands.Keys)
                                {
                                    foreach (var CTS in loopCommands[tempName].Values)
                                    {
                                        CTS?.Cancel();
                                        CTS?.Dispose();
                                    }
                                }
                                loopCommands.Clear();
                                break;

                            case stringReset:
                                foreach (var (playerRef, playerEntity) in SystemAPI.Query<RefRW<PlayerComponent>>().WithEntityAccess())
                                {
                                    var player = playerRef.ValueRO;
                                    if (player.userName != userName) continue;
                                    playerRef.ValueRW.score = 0;
                                    //GameManager.instance.viewerInfos[userName].UpdatePlayerBoardScore(GameManager.stringZero);
                                    GameManager.instance.viewerInfos[userName].score = GameManager.stringZero;
                                    var playerInfo = GameManager.instance.viewerInfos[userName];
                                    playerInfo.nameTagTMP.text = playerInfo.subscribeMonth > 0 ? $"{userName}\n[{playerInfo.subscribeMonth}Month]\nScore:0" : $"{userName}\nScore:0";
                                    foreach (var (bodyPart, rigidBodyAspect) in SystemAPI.Query<RefRO<BodyPartComponent>, RigidBodyAspect>())
                                    {
                                        if (bodyPart.ValueRO.ownerEntity == playerEntity)
                                        {
                                            rigidBodyAspect.Position = GameManager.instance.playerSpawnTransform.position;
                                            rigidBodyAspect.LinearVelocity *= 0;
                                        }
                                    };
                                }
                                //new PlayerResetJob { spawnPoint = GameManager.instance.spawnTransform.position, targetEntity = playerEntity }.ScheduleParallel(CheckedStateRef.Dependency).Complete();
                                break;
                            case stringRA:
                            case stringResetAll:
                                if (!isAdmin && payAmount == 0 || isResettingAll) break;
                                isResettingAll = true;
                                int batchCountRA = 0;
                                float3 spawnPoint = GameManager.instance.playerSpawnTransform.position;

                                //new PlayerResetAllJob { spawnPoint = spawnPoint }.ScheduleParallel(CheckedStateRef.Dependency).Complete();
                                foreach (var (bodyPart, rigidBodyAspect) in SystemAPI.Query<RefRO<BodyPartComponent>, RigidBodyAspect>())
                                {
                                    rigidBodyAspect.Position = spawnPoint;
                                    rigidBodyAspect.AngularVelocityLocalSpace *= 0;
                                    rigidBodyAspect.LinearVelocity *= 0;
                                    if (++batchCountRA > 500)
                                    {
                                        batchCountRA = 0;
                                        await UniTask.Yield();
                                    }
                                };
                                isResettingAll = false;
                                break;

                            case stringP:
                            case stringPush:
                                foreach (var (playerRef, playerEntity) in SystemAPI.Query<RefRO<PlayerComponent>>().WithEntityAccess())
                                {
                                    var player = playerRef.ValueRO;
                                    if (player.userName != userName) continue;

                                    float3 force = new float3(float.Parse(commands[2]), float.Parse(commands[3]), float.Parse(commands[4]));
                                    switch (commands[1].ToLower())
                                    {
                                        case stringHead:
                                            part = SteveBodyPart.Head;
                                            break;
                                        case stringChest:
                                            part = SteveBodyPart.Chest;
                                            break;
                                        case stringSpine:
                                            part = SteveBodyPart.Spine;
                                            break;

                                        case stringRightUpperArm:
                                        case stringRA:
                                            part = SteveBodyPart.RightUpperArm;
                                            break;
                                        case stringRightHand:
                                        case stringRH:
                                            part = SteveBodyPart.RightHand;
                                            break;

                                        case stringLeftUpperArm:
                                        case stringLUA:
                                            part = SteveBodyPart.LeftUpperArm;
                                            break;
                                        case stringLeftHand:
                                        case stringLH:
                                            part = SteveBodyPart.LeftHand;
                                            break;

                                        case stringRightThigh:
                                        case stringRT:
                                            part = SteveBodyPart.RightThigh;
                                            break;
                                        case stringRightFoot:
                                        case stringRF:
                                            part = SteveBodyPart.RightFoot;
                                            break;

                                        case stringLeftThigh:
                                        case stringLT:
                                            part = SteveBodyPart.LeftThigh;
                                            break;
                                        case stringLeftFoot:
                                        case stringLF:
                                            part = SteveBodyPart.LeftFoot;
                                            break;

                                        case stringALL:
                                        default:
                                            new BodyPartsPushAllJob { force = force, targetEntity = playerEntity }.ScheduleParallel(CheckedStateRef.Dependency).Complete();
                                            continue;
                                    }

                                    new BodyPartsPushJob { targetPart = part, force = force, targetEntity = playerEntity }.ScheduleParallel(CheckedStateRef.Dependency).Complete();
                                }
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
                                if (!isAdmin && spawnCount > 2000)
                                    spawnCount = 2000;
                                float3 creeperSpawnPoint = GameManager.instance.screenSpawnTransform.position;
                                var creeperLocalTransform = EntityManager.GetComponentData<LocalTransform>(store.creeper);
                                var creeperVelocity = EntityManager.GetComponentData<PhysicsVelocity>(store.creeper);
                                creeperLocalTransform.Position = creeperSpawnPoint;
                                creeperVelocity.Linear += new float3(0, 0, 2);
                                EntityCommandBuffer ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(CheckedStateRef.WorldUnmanaged);
                                for (int i = 0; i < spawnCount; i++)
                                {
                                    AudioManager.instance.audioSource.PlayOneShot(AudioManager.instance.creeperTrigger);
                                    Entity spawnedCreeper = ecb.Instantiate(store.creeper);
                                    ecb.SetComponent(spawnedCreeper, creeperLocalTransform);
                                    ecb.SetComponent(spawnedCreeper, creeperVelocity);

                                    //new SpawnEntity { parallelWriter = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(CheckedStateRef.WorldUnmanaged).AsParallelWriter(), targetEntity = store.creeper }.ScheduleParallel(CheckedStateRef.Dependency).Complete();
                                    if (i % 100 == 0)
                                    {
                                        await UniTask.Yield();
                                        ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(CheckedStateRef.WorldUnmanaged);
                                    }
                                }
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

            /*if (addValueLife != 0)
                new OnChatSteveJob { hashID = hashID, addValue = addValueLife, peepoConfig = steveConfig.Value }.ScheduleParallel(CheckedStateRef.Dependency).Complete();*/
        };
        OnBan = (hashID) =>
        {
            //new OnBanJob { hashID = hashID }.ScheduleParallel(CheckedStateRef.Dependency).Complete();
        };
        OnSubscription = (hashID, subMonth) =>
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

    [BurstCompile]
    protected override void OnUpdate()
    {
    }
    [BurstCompile]
    partial struct SpawnEntity : IJobEntity
    {
        [ReadOnly] public Entity targetEntity;
        public EntityCommandBuffer.ParallelWriter parallelWriter;

        public void Execute([ChunkIndexInQuery] int chunkIndex, in Entity entity, in MainSpawnerTag mainSpawnerTag, in LocalTransform localTransform)
        {
            Entity spawnedEntity = parallelWriter.Instantiate(chunkIndex, targetEntity);
            var initTransform = new LocalTransform { Position = localTransform.Position, Rotation = localTransform.Rotation, Scale = 1 };
            parallelWriter.SetComponent(chunkIndex, spawnedEntity, initTransform);
        }
    }
    [BurstCompile]
    partial struct PlayerNameTagJob : IJobEntity
    {
        [ReadOnly] public FixedString128Bytes userName;
        [ReadOnly] public Entity targetPlayerEntity;
        public EntityCommandBuffer.ParallelWriter parallelWriter;
        public void Execute([ChunkIndexInQuery] int chunkIndex, in Entity entity, in BodyPartComponent bodyPart)
        {
            if (bodyPart.ownerEntity == default) return;
            if (bodyPart.partType == SteveBodyPart.Head && bodyPart.ownerEntity == targetPlayerEntity)
                parallelWriter.AddComponent(chunkIndex, entity, new NameTagComponent { name = userName });

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
                rigidBodyAspect.AngularVelocityLocalSpace *= 0;
            }
        }
    }
    [BurstCompile]
    partial struct PlayerResetAllJob : IJobEntity
    {
        [ReadOnly] public float3 spawnPoint;
        public void Execute(in BodyPartComponent bodyPartComponent, RigidBodyAspect rigidBodyAspect)
        {
            rigidBodyAspect.Position = spawnPoint;
            rigidBodyAspect.LinearVelocity *= 0;
            rigidBodyAspect.AngularVelocityLocalSpace *= 0;
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

    [BurstCompile]
    partial struct BodyPartsPushAllJob : IJobEntity
    {
        [ReadOnly] public Entity targetEntity;
        [ReadOnly] public float3 force;
        public void Execute(in BodyPartComponent bodyPartComponent, RigidBodyAspect rigidBodyAspect)
        {
            if (bodyPartComponent.ownerEntity == targetEntity)
                rigidBodyAspect.LinearVelocity += force;
        }
    }
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
}
