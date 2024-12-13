using Cysharp.Threading.Tasks;
using OSY;
using System;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Aspects;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

public partial class SteveEventSystem : SystemBase
{
    #region string 캐싱
    public const string stringBonoBono = "보노 보노";

    public const string stringP = "p";
    public const string stringPush = "push";

    public const string stringD = "d";
    public const string stringDelay = "delay";

    public const string stringL = "l";
    public const string stringLoop = "loop";

    public bool isDanceTime;
    public const string stringDT = "dt";
    public const string stringDanceTime = "dancetime";
    CancellationTokenSource danceCTS;

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
    public Action<string, string, float> OnChat;
    public Action<int, int> OnDonation;
    public Action<int, int> OnSubscription;
    public Action<int> OnBan;


    BlobAssetReference<SteveConfig> steveConfig;
    BlobAssetReference<DonationConfig> donationConfig;
    TimeData timeData;

    protected override void OnCreate()
    {
        base.OnCreate();
        CheckedStateRef.RequireForUpdate<GameManagerSingletonComponent>();
    }
    protected override void OnStartRunning()
    {
        base.OnStartRunning();
        danceCTS = CancellationTokenSource.CreateLinkedTokenSource(GameManager.instance.destroyCancellationToken);
        steveConfig = SystemAPI.GetSingleton<GameManagerSingletonComponent>().steveConfig;
        donationConfig = SystemAPI.GetSingleton<GameManagerSingletonComponent>().donationConfig;
        timeData = SystemAPI.Time;

        OnSpawn = (username) =>
        {
            EntityStoreComponent store = SystemAPI.GetSingleton<EntityStoreComponent>();
            //GameManager.SpawnOrder spawnOrder = GameManager.instance.spawnOrderQueue.Dequeue();
            Entity spawnedSteve = EntityManager.Instantiate(store.steve);
            var steveComponent = EntityManager.GetComponentData<SteveComponent>(spawnedSteve);
            //var hash = EntityManager.GetComponentData<HashIDComponent>(spawnedSteve);
            //var velocity = EntityManager.GetComponentData<PhysicsVelocity>(spawnedSteve);
            var localTransform = EntityManager.GetComponentData<LocalTransform>(spawnedSteve);
            var spawnPosition = GameManager.instance.spawnTransform.position;

            steveComponent.currentState = SteveState.Ragdoll;
            steveComponent.userName = username;
            //hash.ID = spawnOrder.hash;
            //velocity.Linear = spawnOrder.initForce;
            //localTransform.Scale = 1;
            localTransform.Position = spawnPosition;

            /*EntityManager.AddComponentData(spawnedSteve, new TimeLimitedLifeComponent
            {
                lifeTime = steveConfig.Value.DefalutLifeTime
            });*/
            EntityManager.SetComponentData(spawnedSteve, steveComponent);
            //EntityManager.SetComponentData(spawnedSteve, hash);
            //EntityManager.SetComponentData(spawnedSteve, velocity);
            EntityManager.SetComponentData(spawnedSteve, localTransform);

        };
        OnChat = async (userName, text, addValueLife) =>
        {
            NativeReference<Entity> tempSteveRef = new NativeReference<Entity>(Allocator.TempJob);
            new GetSteveJob { name = userName, targetSteve = tempSteveRef }.Schedule(CheckedStateRef.Dependency).Complete();
            Entity ownerSteveEntity = tempSteveRef.Value;
            tempSteveRef.Dispose();
            try
            {
                Debug.Log(text);

                if (text.Contains(commandSplitter))
                {
                    string[] commandLines = text.Split(commandSplitter);
                    for (int i = 1; i < commandLines.Length; i++)
                    {
                        string[] commands = commandLines[i].Split(Utils.stringSpace);

                        SteveBodyPart part;
                        switch (commands[0].ToLower())
                        {
                            case stringD:
                            case stringDelay:
                                await UniTask.Delay(TimeSpan.FromSeconds(float.Parse(commands[1])));
                                break;

                            case stringDT:
                            case stringDanceTime:

                                if (isDanceTime)
                                {
                                    danceCTS?.Cancel();
                                    danceCTS?.Dispose();
                                    await UniTask.Yield();
                                    danceCTS = CancellationTokenSource.CreateLinkedTokenSource(GameManager.instance.destroyCancellationToken);
                                }
                                isDanceTime = true;

                                float limitSec = float.Parse(commands[1]);

                                UniTask.RunOnThreadPool(async () =>
                                {
                                    try
                                    {
                                        for (float timer = 0; limitSec == -1 || timer <= limitSec; timer += 0.5f)
                                        {
                                            if (danceCTS.IsCancellationRequested) return;
                                            new StevePopupJob().ScheduleParallel(CheckedStateRef.Dependency).Complete();
                                            await UniTask.Delay(TimeSpan.FromSeconds(0.5f));
                                            if (danceCTS.IsCancellationRequested) return;
                                        }
                                    }
                                    finally
                                    {
                                        isDanceTime = false;
                                    }
                                }, true, danceCTS.Token).Forget();
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

                                new SteveBodyPartPushJob { targetPart = part, force = force, targetEntity = ownerSteveEntity }.ScheduleParallel(CheckedStateRef.Dependency).Complete();
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
            new OnBanJob { hashID = hashID }.ScheduleParallel(CheckedStateRef.Dependency).Complete();
        };
        OnDonation = async (hashID, payAmount) =>
        {
            int cheezeCount = (int)(payAmount * donationConfig.Value.objectCountFactor);

            /*if (hashID == -1) // 익명 후원일 경우
            {
                for (int i = 0; i < cheezeCount; i++)
                {
                    new SpawnDonationObjectUnknownJob { topRightScreenPoint = GameManager.instance.mainCam.ScreenToWorldPoint(new float3(Screen.width, Screen.height, 0), Camera.MonoOrStereoscopicEye.Mono).ToFloat2(), donationConfig = donationConfig.Value, spawnObject = SystemAPI.GetSingleton<EntityStoreComponent>().cheeze, parallelWriter = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(CheckedStateRef.WorldUnmanaged).AsParallelWriter() }.ScheduleParallel(CheckedStateRef.Dependency).Complete();
                    await Utils.YieldCaches.UniTaskYield;
                }
            }
            else
            {
                for (int i = 0; i < cheezeCount; i++)
                {
                    new SpawnDonationObjectJob { donationConfig = donationConfig.Value, hashID = hashID, spawnObject = SystemAPI.GetSingleton<EntityStoreComponent>().cheeze, parallelWriter = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(CheckedStateRef.WorldUnmanaged).AsParallelWriter() }.ScheduleParallel(CheckedStateRef.Dependency).Complete();
                    await Utils.YieldCaches.UniTaskYield;
                }
            }*/
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

    partial struct StevePopupJob : IJobEntity
    {
        public void Execute([ChunkIndexInQuery] int chunkIndex, BodyPartComponent bodyPart, RigidBodyAspect rigidBodyAspect)
        {
            switch (bodyPart.partType)
            {
                case SteveBodyPart.RightHand:
                case SteveBodyPart.LeftHand:
                case SteveBodyPart.Head:
                    rigidBodyAspect.LinearVelocity += new float3(0, 3, 0);
                    break;
            }
        }
    }
    [BurstCompile]
    partial struct GetSteveJob : IJobEntity
    {
        [ReadOnly] public FixedString64Bytes name;
        public NativeReference<Entity> targetSteve;
        public void Execute([ChunkIndexInQuery] int chunkIndex, Entity entity, SteveComponent steve)
        {
            if (steve.userName == name)
                targetSteve.Value = entity;
        }
    }
    [BurstCompile]
    partial struct SteveBodyPartPushJob : IJobEntity
    {
        [ReadOnly] public Entity targetEntity;
        [ReadOnly] public SteveBodyPart targetPart;
        [ReadOnly] public float3 force;
        public void Execute([ChunkIndexInQuery] int chunkIndex, in BodyPartComponent bodyPartComponent, RigidBodyAspect rigidBodyAspect)
        {
            if(bodyPartComponent.ownerEntity == targetEntity && bodyPartComponent.partType == targetPart)
                rigidBodyAspect.LinearVelocity += force;
        }
    }

    [BurstCompile]
    partial struct OnChatTestJob : IJobEntity
    {
        [ReadOnly] public int hashID;
        [ReadOnly] public float addValue;
        [ReadOnly] public SteveConfig peepoConfig;
        public void Execute(ref TimeLimitedLifeComponent timeLimitedLifeComponent, in HashIDComponent hash)
        {
            //Debug.Log("채팅");
            if (hash.ID == hashID)
                timeLimitedLifeComponent.lifeTime = math.clamp(timeLimitedLifeComponent.lifeTime + addValue, 0, peepoConfig.MaxLifeTime);
        }
    }
    [BurstCompile]
    partial struct OnChatSteveJob : IJobEntity
    {
        [ReadOnly] public int hashID;
        [ReadOnly] public float addValue;
        [ReadOnly] public SteveConfig peepoConfig;
        public void Execute(ref TimeLimitedLifeComponent timeLimitedLifeComponent, in HashIDComponent hash)
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
        public void Execute([ChunkIndexInQuery] int chunkIndex, in LocalTransform peepoLocalTransform, ref RandomDataComponent randomDataComponent, ref SteveComponent peepoComponent, in HashIDComponent hash)
        {
            if (hash.ID == hashID)
            {
                peepoComponent.currentState = SteveState.Ragdoll;
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

    [BurstCompile]
    public partial struct OnBanJob : IJobEntity
    {
        [ReadOnly] public int hashID;
        public EntityCommandBuffer.ParallelWriter parallelWriter;

        public void Execute([ChunkIndexInQuery] int chunkIndex, Entity entity, ref TimeLimitedLifeComponent timeLimitedLifeComponent, in HashIDComponent hash)
        {
            if (hash.ID == hashID)
            {
                timeLimitedLifeComponent.lifeTime = 0;
            }
        }
    }
}
