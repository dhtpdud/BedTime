using OSY;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

[BurstCompile]
[UpdateInGroup(typeof(Unity.Entities.InitializationSystemGroup))]
public sealed partial class GameManagerInfoSystem : SystemBase
{
    public bool isReady;
    public BlobAssetReference<SteveConfig> peepoConfigRef;
    public BlobAssetReference<DonationConfig> donationConfigRef;
    [BurstCompile]
    protected override void OnStartRunning()
    {
        base.OnStartRunning();
        if (!SystemAPI.HasSingleton<GameManagerSingletonComponent>())
            EntityManager.CreateSingleton<GameManagerSingletonComponent>();
        //Debug.Log("�ִ� �ӵ���?: " + GameManager.Instance.physicMaxVelocity + "/" + gameManagerRW.physicMaxVelocity);


        var blobBuilder = new BlobBuilder(Allocator.TempJob);
        ref SteveConfig peepoConfig = ref blobBuilder.ConstructRoot<SteveConfig>();
        ref DonationConfig donationConfig = ref blobBuilder.ConstructRoot<DonationConfig>();

        ref GameManagerSingletonComponent gameManagerRW = ref SystemAPI.GetSingletonRW<GameManagerSingletonComponent>().ValueRW;

        peepoConfigRef = blobBuilder.CreateBlobAssetReference<SteveConfig>(Allocator.Persistent);
        donationConfigRef = blobBuilder.CreateBlobAssetReference<DonationConfig>(Allocator.Persistent);

        gameManagerRW.steveConfig = peepoConfigRef;
        gameManagerRW.donationConfig = donationConfigRef;
        gameManagerRW.playerSpawnPoint = GameManager.instance.playerSpawnTransform.position;
        isReady = true;

        blobBuilder.Dispose();
    }
    [BurstCompile]
    protected override void OnUpdate()
    {
        ref var gameManagerRW = ref SystemAPI.GetSingletonRW<GameManagerSingletonComponent>().ValueRW;
        gameManagerRW.ScreenPointToRayMainCam = GameManager.instance.mainCam.ScreenPointToRay(Input.mousePosition);
        Vector3 depth = new Vector3(0, 0, ((Vector3)gameManagerRW.dragingEntityInfo.hitPoint - GameManager.instance.mainCamTrans.position).z);
        gameManagerRW.ScreenToWorldPointMainCam = GameManager.instance.mainCam.ScreenToWorldPoint(Input.mousePosition + depth).ToFloat2();
    }
    [BurstCompile]
    public void UpdateSetting()
    {
        ref var gameManagerRW = ref SystemAPI.GetSingletonRW<GameManagerSingletonComponent>().ValueRW;
        ref var peepoConfigRW = ref peepoConfigRef.Value;
        ref var donationConfigRW = ref donationConfigRef.Value;

        gameManagerRW.stabilityPower = GameManager.instance.stabilityPower;
        gameManagerRW.dragPower = GameManager.instance.dragPower;
        gameManagerRW.physicMaxVelocity = GameManager.instance.physicMaxVelocity;
        gameManagerRW.gravity = GameManager.instance.gravity;
        gameManagerRW.SpawnMinSpeed = GameManager.instance.SpawnMinSpeed;
        gameManagerRW.SpawnMaxSpeed = GameManager.instance.SpawnMaxSpeed;
        peepoConfigRW.DefalutLifeTime = GameManager.instance.peepoConfig.defalutLifeTime;
        peepoConfigRW.MaxLifeTime = GameManager.instance.peepoConfig.maxLifeTime;
        peepoConfigRW.AddLifeTime = GameManager.instance.peepoConfig.addLifeTime;

        peepoConfigRW.DefaultSize = GameManager.instance.peepoConfig.defaultSize;
        peepoConfigRW.MaxSize = GameManager.instance.peepoConfig.maxSize;
        peepoConfigRW.MinSize = GameManager.instance.peepoConfig.minSize;
        peepoConfigRW.DefalutLifeTime = GameManager.instance.peepoConfig.defalutLifeTime;
        peepoConfigRW.MaxLifeTime = GameManager.instance.peepoConfig.maxLifeTime;
        peepoConfigRW.AddLifeTime = GameManager.instance.peepoConfig.addLifeTime;

        peepoConfigRW.switchIdleAnimationTime = GameManager.instance.peepoConfig.switchIdleAnimationTime;
        peepoConfigRW.switchTimeImpact = GameManager.instance.peepoConfig.switchTimeImpact;

        donationConfigRW.objectCountFactor = GameManager.instance.donationConfig.objectCountFactor;
        donationConfigRW.objectLifeTime = GameManager.instance.donationConfig.objectLifeTime;
        donationConfigRW.MinSize = GameManager.instance.donationConfig.minSize;
        donationConfigRW.MaxSize = GameManager.instance.donationConfig.maxSize;
    }
    [BurstCompile]
    protected override void OnDestroy()
    {
        base.OnDestroy();
        peepoConfigRef.Dispose();
        donationConfigRef.Dispose();
    }
}