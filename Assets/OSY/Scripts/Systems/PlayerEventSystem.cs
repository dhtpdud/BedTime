using Cysharp.Threading.Tasks;
using OSY;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Aspects;
using Unity.Transforms;
using UnityEngine;
using WebSocketSharp;
using Random = Unity.Mathematics.Random;

[BurstCompile]
public partial class PlayerEventSystem : SystemBase
{
    #region string 캐싱
    public List<FixedString128Bytes> adminNames = new List<FixedString128Bytes>();

    public const string commandSplitter = "//";

    public const string stringP = "p";
    public const string stringPush = "push";
    public const string stringR = "r";
    public const string stringRotate = "rotate";

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

    public const string stringRS = "rs";
    public const string stringResetSlime = "resetslime";
    public const string stringReset = "reset";
    public const string stringRA = "ra";
    public const string stringResetAll = "resetall";
    public bool isResettingAll;

    public const string stringDT = "dt";
    public const string stringDanceTime = "dancetime";

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

    public const string stringA = "a";
    public const string stringALL = "all";

    public const string stringGravity = "gravity";
    #endregion

    public Action<string> OnSpawn;
    //만약 string이 되면 hash를 string으로
    public Action<string, string, float, int, bool> OnChat;
    public Action<int, int> OnSubscription;
    public Action<int> OnBan;

    Dictionary<FixedString128Bytes, Dictionary<FixedString128Bytes, CancellationTokenSource>> loopCommands = new Dictionary<FixedString128Bytes, Dictionary<FixedString128Bytes, CancellationTokenSource>>();


    protected override void OnCreate()
    {
        base.OnCreate();
        CheckedStateRef.RequireForUpdate<EntityStoreComponent>();
        CheckedStateRef.RequireForUpdate<GameManagerSingletonComponent>();
        adminNames.Add($"{PlatformNameCache.Chzzk}\n보노 보노");
        adminNames.Add($"{PlatformNameCache.YouTube}\nKamer");
        adminNames.Add($"{PlatformNameCache.YouTube}\nVg");
    }
    protected override void OnStartRunning()
    {
        base.OnStartRunning();

        OnSpawn = async (userNameString) =>
        {
            var store = SystemAPI.GetSingleton<EntityStoreComponent>();
            BlobAssetReference<PlayerConfig> playerConfig = SystemAPI.GetSingleton<GameManagerSingletonComponent>().playerConfig;
            var initPlayerComponent = EntityManager.GetComponentData<PlayerComponent>(store.steve);
            var initPlayerLocalTransform = EntityManager.GetComponentData<LocalTransform>(store.steve);
            initPlayerLocalTransform.Position = GameManager.instance.playerSpawnTransform.position;
            var initLife = new TimeLimitedLifeComponent { lifeTime = playerConfig.Value.DefalutLifeTime };
            lock (GameManager.instance)
                GameManager.instance.playerCount++;
            GameManager.instance.UpdatePlayerCount();
            GameManager.instance.AddChat($"<color=yellow><b>{userNameString}</b> joined the game</color>");

            FixedString128Bytes userName = new FixedString128Bytes(userNameString);
            EntityCommandBuffer ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(CheckedStateRef.WorldUnmanaged);

            Entity spawnedPlayerEntity = ecb.Instantiate(store.steve);
            initPlayerComponent.userName = userName;
            if (GameManager.instance.viewerInfos.ContainsKey(userName))
            {
                initPlayerComponent.score = float.Parse(GameManager.instance.viewerInfos[userName].score);
            }
            ecb.SetComponent(spawnedPlayerEntity, initPlayerComponent);
            ecb.SetComponent(spawnedPlayerEntity, initPlayerLocalTransform);
            ecb.AddComponent(spawnedPlayerEntity, initLife);

            await UniTask.Yield();
            await UniTask.Yield();


            NativeArray<Entity> playerEntityRef = new NativeArray<Entity>(1, Allocator.TempJob);
            new GetPlayerEntityJob { playerEntityRef = playerEntityRef, userName = userName }.ScheduleParallel(CheckedStateRef.Dependency).Complete();

            var parallelWriter = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(CheckedStateRef.WorldUnmanaged).AsParallelWriter();
            new PlayerNameTagJob { targetPlayerEntity = playerEntityRef[0], userName = userName, parallelWriter = parallelWriter }.ScheduleParallel(CheckedStateRef.Dependency).Complete();
            playerEntityRef.Dispose();
            /*foreach (var (playerRef, entity) in SystemAPI.Query<RefRO<PlayerComponent>>().WithEntityAccess())
            {
                if (playerRef.ValueRO.userName == userName)
                    new PlayerNameTagJob { targetPlayerEntity = entity, userName = userName, parallelWriter = ecb.AsParallelWriter() }.ScheduleParallel(CheckedStateRef.Dependency).Complete();
            }*/
        };
        OnChat = async (userNameString, text, addValueLife, payAmount, isLoop) =>
        {
            var loopCommands = this.loopCommands;
            var random = new Random((uint)UnityEngine.Random.Range(uint.MinValue, uint.MaxValue));
            CancellationTokenSource danceCTS = CancellationTokenSource.CreateLinkedTokenSource(GameManager.instance.destroyCancellationToken);
            CancellationTokenSource gravityeCTS = CancellationTokenSource.CreateLinkedTokenSource(GameManager.instance.destroyCancellationToken);
            BlobAssetReference<PlayerConfig> playerConfig = SystemAPI.GetSingleton<GameManagerSingletonComponent>().playerConfig;
            BlobAssetReference<DonationConfig> donationConfig = SystemAPI.GetSingleton<GameManagerSingletonComponent>().donationConfig;
            var store = SystemAPI.GetSingleton<EntityStoreComponent>();
            int diamondCount = (int)(payAmount * donationConfig.Value.objectCountFactor);
            bool isUnkown = userNameString == null || userNameString == string.Empty;
            bool isAdmin = false;
            FixedString128Bytes userName = string.Empty;
            bool isFoundPlayer = false;
            EntityCommandBuffer ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(CheckedStateRef.WorldUnmanaged);
            if (diamondCount > 0)
            {
                UniTask.RunOnThreadPool(async () =>
                {
                    await UniTask.SwitchToMainThread();
                    float3 diaSpawnPoint = GameManager.instance.screenSpawnTransform.position;
                    var diaLocalTransform = EntityManager.GetComponentData<LocalTransform>(store.diamond);
                    var diaVelocity = EntityManager.GetComponentData<PhysicsVelocity>(store.diamond);
                    var lifeComponent = new TimeLimitedLifeComponent { lifeTime = 10 };

                    int spawnedDiaCount = 0;
                    for (int i = 0; i < diamondCount; i++)
                    {
                        if (GameManager.instance.diaCount > GameManager.instance.MaxDiaCount)
                        {
                            await UniTask.Yield();
                            continue;
                        }
                        lock (GameManager.instance)
                        {
                            GameManager.instance.diaCount++;
                            spawnedDiaCount++;
                        }
                        Quaternion rotation;
                        if (i % 2 == 0)
                        {
                            diaLocalTransform.Position = diaSpawnPoint + new float3(2, 0, 0);
                            rotation = Quaternion.Euler(0, -45, 0);
                        }
                        else
                        {
                            diaLocalTransform.Position = diaSpawnPoint + new float3(-2, 0, 0);
                            rotation = Quaternion.Euler(0, 45, 0);
                        }

                        random = new Random(random.NextUInt(uint.MinValue, uint.MaxValue));
                        diaVelocity.Linear = ((Vector3)(diaLocalTransform.Position - float3.zero)).normalized + (rotation * new Vector3(random.NextFloat(-2, 2), random.NextFloat(0, 3), random.NextFloat(2, 4)));
                        Entity spawnedDia = ecb.Instantiate(store.diamond);
                        ecb.SetComponent(spawnedDia, diaLocalTransform);
                        ecb.SetComponent(spawnedDia, diaVelocity);
                        ecb.AddComponent(spawnedDia, lifeComponent);
                        if (i % 10 == 0)
                        {
                            await UniTask.Yield();
                            ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(CheckedStateRef.WorldUnmanaged);
                        }
                    }

                    UniTask.RunOnThreadPool(async () =>
                    {
                        await UniTask.Delay(TimeSpan.FromSeconds(10));
                        lock (GameManager.instance)
                            GameManager.instance.diaCount -= spawnedDiaCount;
                    }, true, GameManager.instance.destroyCancellationToken).Forget();

                }, true, GameManager.instance.destroyCancellationToken).Forget();
            }

            int subMonth = 0;
            int subMouthCut = 1;
            if (!isUnkown)
            {
                userName = new FixedString128Bytes(userNameString);
                isAdmin = adminNames.Contains(userName);
                subMonth = GameManager.instance.viewerInfos[userName].subscribeMonth;

                foreach (var (playerInfoRef, lifeRef) in SystemAPI.Query<RefRO<PlayerComponent>, RefRW<TimeLimitedLifeComponent>>())
                {
                    if (playerInfoRef.ValueRO.userName == userName)
                    {
                        if (!isLoop)
                            lifeRef.ValueRW.lifeTime = playerConfig.Value.DefalutLifeTime;
                        isFoundPlayer = true;
                    }
                }
                if (!isFoundPlayer)
                {
                    OnSpawn(userNameString);
                    await UniTask.Yield();
                    await UniTask.Yield();
                }

                if (!isLoop)
                    GameManager.instance.viewerInfos[userName].UpdateNameTag().Forget();
                GameManager.instance.viewerInfos[userName].UpdatePlayerBoard();
            }


            try
            {
                //Debug.Log(userName + ": " + text);
                if (text.Contains(commandSplitter))
                {
                    string[] commandLines = text.Split(commandSplitter);
                    for (int commandLineIndex = 1; commandLineIndex < commandLines.Length; commandLineIndex++)
                    {
                        var stringCommands = commandLines[commandLineIndex].Split(Utils.stringSpace);
                        var fixedCommands = stringCommands.Select(val => (FixedString64Bytes)val).ToArray();

                        SteveBodyPart part;
                        switch (stringCommands[0].ToLower())
                        {
                            case stringD:
                            case stringDelay:
                                await UniTask.Delay(TimeSpan.FromSeconds(float.Parse(stringCommands[1])));
                                break;

                            case stringDT:
                            case stringDanceTime:
                                //if (!isAdmin && payAmount <= 0 && subMonth <= subMouthCut) break;
                                if (danceCTS != null && !danceCTS.IsCancellationRequested)
                                {
                                    danceCTS?.Cancel();
                                }
                                danceCTS?.Dispose();

                                await UniTask.Yield();
                                await UniTask.Yield();

                                float dtLimitSec = float.Parse(stringCommands[1]);
                                //dtLimitSec = (dtLimitSec <= -1 || dtLimitSec > 10) && !isAdmin ? 10 : dtLimitSec;

                                danceCTS = CancellationTokenSource.CreateLinkedTokenSource(GameManager.instance.destroyCancellationToken);

                                UniTask.RunOnThreadPool(async () =>
                                {
                                    await UniTask.SwitchToMainThread();
                                    try
                                    {
                                        for (float timer = 0; dtLimitSec == -1 || timer <= dtLimitSec; timer += 0.5f)
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
                                FixedString128Bytes commandName1 = stringCommands[1];
                                if (loopCommands.ContainsKey(userName))
                                {
                                    if (loopCommands[userName].ContainsKey(commandName1))
                                        break;
                                }
                                else
                                    loopCommands.TryAdd(userName, new Dictionary<FixedString128Bytes, CancellationTokenSource>());

                                var loopCommandCTS = CancellationTokenSource.CreateLinkedTokenSource(GameManager.instance.destroyCancellationToken);

                                Debug.Log($"루프명: {userName}-{commandName1}\r\n횟수: {stringCommands[2]}, 간격: {stringCommands[3]}");
                                int maxCount = int.Parse(stringCommands[2]);
                                float delay = float.Parse(stringCommands[3]);
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
                                            OnChat(userNameString, targetCommands, addValueLife, 0, true);
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
                                FixedString128Bytes commandName2 = stringCommands[1];
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
                                if (isUnkown) break;
                                foreach (var (playerComponent, playerEntity) in SystemAPI.Query<RefRW<PlayerComponent>>().WithEntityAccess())
                                {
                                    if (playerComponent.ValueRO.userName != userName) continue;
                                    playerComponent.ValueRW.score -= 10;
                                    if (playerComponent.ValueRO.score < -100)
                                    {
                                        playerComponent.ValueRW.score = -100;
                                    }
                                    float playerScore = playerComponent.ValueRW.score;
                                    //GameManager.instance.viewerInfos[userName].UpdatePlayerBoardScore(GameManager.stringZero);
                                    GameManager.instance.viewerInfos[userName].score = playerScore.ToString(GameManager.stringDecimal2);
                                    var playerInfo = GameManager.instance.viewerInfos[userName];
                                    playerInfo.nameTagTMP.text = playerInfo.subscribeMonth > 0 ? $"{userName}\n[{playerInfo.subscribeMonth}Month]\nScore:{GameManager.instance.viewerInfos[userName].score}" : $"{userName}\nScore:{GameManager.instance.viewerInfos[userName].score}";
                                    GameManager.instance.UpdateLeaderBoard();
                                    new PlayerResetJob { spawnPoint = GameManager.instance.playerSpawnTransform.position, targetPlayerEntity = playerEntity }.ScheduleParallel(CheckedStateRef.Dependency).Complete();
                                }
                                //new PlayerResetJob { spawnPoint = GameManager.instance.spawnTransform.position, targetEntity = playerEntity }.ScheduleParallel(CheckedStateRef.Dependency).Complete();
                                break;
                            case stringRA:
                            case stringResetAll:
                                //if (!isAdmin /*&& payAmount <= 0 && subMonth <= subMouthCut*/ /*|| isResettingAll*/) break;
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

                            case stringRS:
                            case stringResetSlime:
                                new ResetSlimeJob { spawnPoint = GameManager.instance.playerSpawnTransform.position }.ScheduleParallel(CheckedStateRef.Dependency).Complete();
                                break;

                            case stringP:
                            case stringPush:
                                NativeArray<Entity> playerEntityRefP = new NativeArray<Entity>(1, Allocator.TempJob);
                                new GetPlayerEntityJob { playerEntityRef = playerEntityRefP, userName = userName }.ScheduleParallel(CheckedStateRef.Dependency).Complete();
                                if (playerEntityRefP[0] == default) return;
                                var partNameStringP = stringCommands[1].ToLower();
                                float3 force = new float3(fixedCommands[2].ToFloat(), fixedCommands[3].ToFloat(), fixedCommands[4].ToFloat());
                                if (partNameStringP == stringALL || partNameStringP == stringA)
                                    new BodyPartsPushAllJob { force = force, targetEntity = playerEntityRefP[0] }.ScheduleParallel(CheckedStateRef.Dependency).Complete();
                                else
                                    new BodyPartsPushJob { targetPart = GetPart(partNameStringP), force = force, targetEntity = playerEntityRefP[0] }.ScheduleParallel(CheckedStateRef.Dependency).Complete();
                                playerEntityRefP.Dispose();
                                break;
                            case stringR:
                            case stringRotate:
                                NativeArray<Entity> playerEntityRefR = new NativeArray<Entity>(1, Allocator.TempJob);
                                new GetPlayerEntityJob { playerEntityRef = playerEntityRefR, userName = userName }.ScheduleParallel(CheckedStateRef.Dependency).Complete();
                                if (playerEntityRefR[0] == default) return;
                                var partNameStringR = stringCommands[1].ToLower();
                                float3 roatation = new float3(fixedCommands[2].ToFloat(), fixedCommands[3].ToFloat(), fixedCommands[4].ToFloat());
                                if (partNameStringR == stringALL || partNameStringR == stringA)
                                    new BodyPartsRotateAllJob { roatation = roatation, targetEntity = playerEntityRefR[0] }.ScheduleParallel(CheckedStateRef.Dependency).Complete();
                                else
                                    new BodyPartsRotateJob { targetPart = GetPart(partNameStringR), roatation = roatation, targetEntity = playerEntityRefR[0] }.ScheduleParallel(CheckedStateRef.Dependency).Complete();
                                playerEntityRefR.Dispose();
                                break;

                            case stringCreeper:
                                //if (!isAdmin /*&& payAmount <= 0 && subMonth <= subMouthCut*/) break;
                                int spawnCount = 0;
                                try
                                {
                                    spawnCount = int.Parse(stringCommands[1]);
                                }
                                catch (IndexOutOfRangeException)
                                {
                                    spawnCount = 1;
                                }
                                if (spawnCount > 2000)
                                    spawnCount = 2000;
                                float3 creeperSpawnPoint = GameManager.instance.screenSpawnTransform.position;
                                var creeperLocalTransform = EntityManager.GetComponentData<LocalTransform>(store.creeper);
                                var creeperVelocity = EntityManager.GetComponentData<PhysicsVelocity>(store.creeper);
                                creeperLocalTransform.Position = creeperSpawnPoint;
                                creeperVelocity.Linear += new float3(0, 0, 2);
                                ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(CheckedStateRef.WorldUnmanaged);
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
                            case stringGravity:
                                //if (!isAdmin /*&& payAmount <= 0 && subMonth <= subMouthCut*/) break;
                                if (gravityeCTS != null && !gravityeCTS.IsCancellationRequested)
                                {
                                    gravityeCTS?.Cancel();
                                }

                                await UniTask.Yield();
                                await UniTask.Yield();

                                float gLimitSec = float.Parse(stringCommands[1]);
                                //gLimitSec = (gLimitSec <= -1 || gLimitSec > 60 * 3) && !isAdmin ? 60 * 3 : gLimitSec;

                                float3 gravity = new float3(float.Parse(stringCommands[2]), float.Parse(stringCommands[3]), float.Parse(stringCommands[4]));

                                gravityeCTS = CancellationTokenSource.CreateLinkedTokenSource(GameManager.instance.destroyCancellationToken);

                                UniTask.RunOnThreadPool(async () =>
                                {
                                    SystemAPI.GetSingletonRW<PhysicsStep>().ValueRW.Gravity = gravity;
                                    await UniTask.Delay(TimeSpan.FromSeconds(gLimitSec), false, PlayerLoopTiming.Update, gravityeCTS.Token, true);
                                    gravityeCTS?.Cancel();
                                    SystemAPI.GetSingletonRW<PhysicsStep>().ValueRW.Gravity = new float3(0, -9.81f, 0);
                                }, true, gravityeCTS.Token).Forget();
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);

                if (!isUnkown)
                    GameManager.instance.AddChat($"<color=red><b>{userName}</b> entered an invalid command</color>");
                else
                    GameManager.instance.AddChat("<color=red>Unkown entered an invalid command</color>");

                if (!isUnkown && payAmount > 0)
                    foreach (var (playerComponent, playerEntity) in SystemAPI.Query<RefRW<PlayerComponent>>().WithEntityAccess())
                    {
                        if (playerComponent.ValueRO.userName != userName) return;

                        playerComponent.ValueRW.score += payAmount;
                        var playerScore = playerComponent.ValueRO.score;
                        //GameManager.instance.viewerInfos[userName].UpdatePlayerBoardScore(GameManager.stringZero);
                        GameManager.instance.viewerInfos[userName].score = playerScore.ToString(GameManager.stringDecimal2);
                        var playerInfo = GameManager.instance.viewerInfos[userName];
                        playerInfo.nameTagTMP.text = playerInfo.subscribeMonth > 0 ? $"{userName}\n[{playerInfo.subscribeMonth}Month]\nScore:{GameManager.instance.viewerInfos[userName].score}" : $"{userName}\nScore:{GameManager.instance.viewerInfos[userName].score}";
                        GameManager.instance.UpdateLeaderBoard();
                    }
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

    public SteveBodyPart GetPart(string partName)
    {
        switch (partName)
        {
            case stringChest:
                return SteveBodyPart.Chest;
            case stringSpine:
                return SteveBodyPart.Spine;
            case stringRightUpperArm:
            case stringRA:
                return SteveBodyPart.RightUpperArm;
            case stringRightHand:
            case stringRH:
                return SteveBodyPart.RightHand;
            case stringLeftUpperArm:
            case stringLUA:
                return SteveBodyPart.LeftUpperArm;
            case stringLeftHand:
            case stringLH:
                return SteveBodyPart.LeftHand;
            case stringRightThigh:
            case stringRT:
                return SteveBodyPart.RightThigh;
            case stringRightFoot:
            case stringRF:
                return SteveBodyPart.RightFoot;
            case stringLeftThigh:
            case stringLT:
                return SteveBodyPart.LeftThigh;
            case stringLeftFoot:
            case stringLF:
                return SteveBodyPart.LeftFoot;
            case stringALL:
            default:
            case stringHead:
                return SteveBodyPart.Head;
        }
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
    partial struct GetPlayerEntityJob : IJobEntity
    {
        [ReadOnly] public FixedString128Bytes userName;
        public NativeArray<Entity> playerEntityRef;
        public void Execute(in Entity entity, in PlayerComponent playerComponent)
        {
            if (playerComponent.userName == userName)
                playerEntityRef[0] = entity;
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
        [ReadOnly] public Entity targetPlayerEntity;
        [ReadOnly] public float3 spawnPoint;
        public void Execute(in BodyPartComponent bodyPartComponent, RigidBodyAspect rigidBodyAspect)
        {
            if (bodyPartComponent.ownerEntity == targetPlayerEntity)
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
    partial struct ResetSlimeJob : IJobEntity
    {
        [ReadOnly] public float3 spawnPoint;
        public void Execute(in SlimeTag slime, RigidBodyAspect rigidBodyAspect)
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
    partial struct BodyPartsRotateJob : IJobEntity
    {
        [ReadOnly] public Entity targetEntity;
        [ReadOnly] public SteveBodyPart targetPart;
        [ReadOnly] public float3 roatation;
        public void Execute(in BodyPartComponent bodyPartComponent, RigidBodyAspect rigidBodyAspect)
        {
            if (bodyPartComponent.ownerEntity == targetEntity && bodyPartComponent.partType == targetPart)
                rigidBodyAspect.AngularVelocityLocalSpace += roatation;
        }
    }

    [BurstCompile]
    partial struct BodyPartsRotateAllJob : IJobEntity
    {
        [ReadOnly] public Entity targetEntity;
        [ReadOnly] public float3 roatation;
        public void Execute(in BodyPartComponent bodyPartComponent, RigidBodyAspect rigidBodyAspect)
        {
            if (bodyPartComponent.ownerEntity == targetEntity && bodyPartComponent.partType == SteveBodyPart.Spine)
                rigidBodyAspect.AngularVelocityLocalSpace += roatation;
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
