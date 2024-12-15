using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Random = Unity.Mathematics.Random;
using Ray = UnityEngine.Ray;

public struct DragableTag : IComponentData
{

}
public struct DestroyMark : IComponentData
{

}
public struct HashIDComponent : IComponentData
{
    public int ID;
}
public struct TimeLimitedLifeComponent : IComponentData
{
    public float lifeTime;
}
public enum SteveState
{
    Born,
    Ragdoll,
    Dragging
}
public struct NameTagComponent : IComponentData //해당 컨포넌트를 가지고 있는 Entity는 전부 NameTag를 표시
{
    public FixedString64Bytes name;
}
public struct PlayerComponent : IComponentData
{
    public FixedString64Bytes userName;
    public SteveState lastState;
    public SteveState currentState;
    public float bedTime;
    public bool isBed;
}
public enum SteveBodyPart
{
    Head,
    Chest,
    Spine,
    RightUpperArm,
    RightHand,
    LeftUpperArm,
    LeftHand,
    RightThigh,
    RightFoot,
    LeftThigh,
    LeftFoot
}
public struct BodyPartComponent : IComponentData
{
    public Entity ownerEntity;
    public SteveBodyPart partType;
}
public struct DonationConfig
{
    public float objectCountFactor;
    public float objectLifeTime;

    public float MinSize;
    public float MaxSize;
}
public struct SteveConfig
{
    public float DefalutLifeTime;
    public float AddLifeTime;
    public float MaxLifeTime;

    public float DefaultSize;
    public float MinSize;
    public float MaxSize;

    public float switchTimeImpact;
    public float switchIdleAnimationTime;

    public float maxVelocity;
}
public struct EntityStoreComponent : IComponentData
{
    public Entity steve;

    public EntityStoreComponent(Entity steve)
    {
        this.steve = steve;
    }
}
public struct SpawnerComponent : IComponentData
{
    public Entity targetEntity;
    public int maxCount;
    public int spawnedCount;

    public float spawnIntervalSec;
    public float currentSec;

    public bool isRandomSize;
    public float minSize;
    public float maxSize;
}
public struct GameManagerSingletonComponent : IComponentData
{
    public struct DragingEntityInfo
    {
        readonly public Entity entity;
        readonly public RigidBody rigidbody;
        readonly public ColliderKey colliderKey;
        readonly public Material material;

        public DragingEntityInfo(Entity entity, RigidBody rigidbody, ColliderKey colliderKey, Material material)
        {
            this.entity = entity;
            this.rigidbody = rigidbody;
            this.colliderKey = colliderKey;
            this.material = material;
        }
    }
    public DragingEntityInfo dragingEntityInfo;

    public Ray ScreenPointToRayOfMainCam;
    public float2 ScreenToWorldPointMainCam;

    public float gravity;

    public float2 SpawnMinSpeed;
    public float2 SpawnMaxSpeed;

    public float dragPower;
    public float stabilityPower;

    public float physicMaxVelocity;
    public BlobAssetReference<SteveConfig> steveConfig;
    public BlobAssetReference<DonationConfig> donationConfig;
}
public struct RandomDataComponent : IComponentData
{
    public Random Random;
}
