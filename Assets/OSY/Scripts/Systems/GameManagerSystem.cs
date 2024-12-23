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
    [BurstCompile]
    protected override void OnStartRunning()
    {
        base.OnStartRunning();
        if (!SystemAPI.HasSingleton<GameManagerSingletonComponent>())
            EntityManager.CreateSingleton<GameManagerSingletonComponent>();
        //Debug.Log("최대 속도는?: " + GameManager.Instance.physicMaxVelocity + "/" + gameManagerRW.physicMaxVelocity);


        var blobBuilder = new BlobBuilder(Allocator.TempJob);
        ref PlayerConfig peepoConfig = ref blobBuilder.ConstructRoot<PlayerConfig>();
        ref DonationConfig donationConfig = ref blobBuilder.ConstructRoot<DonationConfig>();

        var playerConfigRef = blobBuilder.CreateBlobAssetReference<PlayerConfig>(Allocator.Persistent);
        var donationConfigRef = blobBuilder.CreateBlobAssetReference<DonationConfig>(Allocator.Persistent);

        SystemAPI.GetSingletonRW<GameManagerSingletonComponent>().ValueRW.playerConfig = playerConfigRef;
        SystemAPI.GetSingletonRW<GameManagerSingletonComponent>().ValueRW.donationConfig = donationConfigRef;
        SystemAPI.GetSingletonRW<GameManagerSingletonComponent>().ValueRW.playerSpawnPoint = GameManager.instance.playerSpawnTransform.position;
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
        ref var playerConfigRW = ref gameManagerRW.playerConfig.Value;
        ref var donationConfigRW = ref gameManagerRW.donationConfig.Value;

        gameManagerRW.stabilityPower = GameManager.instance.stabilityPower;
        gameManagerRW.dragPower = GameManager.instance.dragPower;
        gameManagerRW.physicMaxVelocity = GameManager.instance.physicMaxVelocity;
        gameManagerRW.gravity = GameManager.instance.gravity;
        gameManagerRW.SpawnMinSpeed = GameManager.instance.SpawnMinSpeed;
        gameManagerRW.SpawnMaxSpeed = GameManager.instance.SpawnMaxSpeed;
        playerConfigRW.DefalutLifeTime = GameManager.instance.playerConfig.defalutLifeTime;
        playerConfigRW.MaxLifeTime = GameManager.instance.playerConfig.maxLifeTime;
        playerConfigRW.AddLifeTime = GameManager.instance.playerConfig.addLifeTime;

        playerConfigRW.DefaultSize = GameManager.instance.playerConfig.defaultSize;
        playerConfigRW.MaxSize = GameManager.instance.playerConfig.maxSize;
        playerConfigRW.MinSize = GameManager.instance.playerConfig.minSize;
        playerConfigRW.DefalutLifeTime = GameManager.instance.playerConfig.defalutLifeTime;
        playerConfigRW.MaxLifeTime = GameManager.instance.playerConfig.maxLifeTime;
        playerConfigRW.AddLifeTime = GameManager.instance.playerConfig.addLifeTime;

        playerConfigRW.switchIdleAnimationTime = GameManager.instance.playerConfig.switchIdleAnimationTime;
        playerConfigRW.switchTimeImpact = GameManager.instance.playerConfig.switchTimeImpact;

        donationConfigRW.objectCountFactor = GameManager.instance.donationConfig.objectCountFactor;
        donationConfigRW.objectLifeTime = GameManager.instance.donationConfig.objectLifeTime;
        donationConfigRW.MinSize = GameManager.instance.donationConfig.minSize;
        donationConfigRW.MaxSize = GameManager.instance.donationConfig.maxSize;
    }
    [BurstCompile]
    protected override void OnDestroy()
    {
        base.OnDestroy();
        SystemAPI.GetSingletonRW<GameManagerSingletonComponent>().ValueRW.playerConfig.Dispose();
        SystemAPI.GetSingletonRW<GameManagerSingletonComponent>().ValueRW.donationConfig.Dispose();
    }
}