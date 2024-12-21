using Cysharp.Threading.Tasks;
using OSY;
using System.Security.Principal;
using Unity.Burst;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Aspects;
using Unity.Transforms;
using UnityEngine;
using Material = Unity.Physics.Material;
using RaycastHit = Unity.Physics.RaycastHit;

[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class MouseInteractionSystem : SystemBase
{
    private float dragingTimer;
    private CollisionFilter clickableFilter;
    private PhysicsWorldSingleton _physicsWorldSingleton;
    private EntityManager entityManager;
    TimeData time;
    float3 entityPositionOnDown;

    public bool isDraging;

    public float2 mouseLastPosition;
    public float2 mouseVelocity;
    public float3 onMouseDownPosition;
    public float lastEntityRotation;
    [BurstCompile]
    protected override void OnCreate()
    {
        clickableFilter = new CollisionFilter { BelongsTo = 1u << 0, CollidesWith = ~(1u << 3), GroupIndex = 0 };
        CheckedStateRef.RequireForUpdate<GameManagerSingletonComponent>();
        CheckedStateRef.RequireForUpdate<EntityStoreComponent>();
        entityManager = CheckedStateRef.EntityManager;
    }

    [BurstCompile]
    protected override void OnUpdate()
    {
        time = SystemAPI.Time;
        _physicsWorldSingleton = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        if (Input.GetMouseButtonDown(0))
        {
            //RefRW는 매 프레임 마다. 사용할 때 호출해서 사용해야함.
            OnMouseDown(ref SystemAPI.GetSingletonRW<GameManagerSingletonComponent>().ValueRW, ref CheckedStateRef);
        }
        else if (Input.GetMouseButtonUp(0))
        {
            OnMouseUp(ref SystemAPI.GetSingletonRW<GameManagerSingletonComponent>().ValueRW);
        }
        if (Input.GetMouseButton(0))
        {
            OnMouse(ref SystemAPI.GetSingletonRW<GameManagerSingletonComponent>().ValueRW);
            EntityCommandBuffer ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(CheckedStateRef.WorldUnmanaged);
        }
    }
    private void OnMouseDown(ref GameManagerSingletonComponent gameManagerRW, ref SystemState state)
    {
        onMouseDownPosition = new float3(gameManagerRW.ScreenToWorldPointMainCam.x, gameManagerRW.ScreenToWorldPointMainCam.y, 0);

        float3 rayStart = gameManagerRW.ScreenPointToRayMainCam.origin;
        float3 rayEnd = gameManagerRW.ScreenPointToRayMainCam.GetPoint(10000f);
        if (Raycast(rayStart, rayEnd, out RaycastHit raycastHit))
        {
            RigidBody hitRigidBody = _physicsWorldSingleton.PhysicsWorld.Bodies[raycastHit.RigidBodyIndex];
            Entity hitEntity = hitRigidBody.Entity;
            if (entityManager.HasComponent<DragableTag>(hitEntity))
            {
                LocalTransform localTransform = entityManager.GetComponentData<LocalTransform>(hitEntity);
                lastEntityRotation = localTransform.Rotation.value.z;
                entityPositionOnDown = localTransform.Position;
                isDraging = true;

                Material material = Utils.GetMaterial(hitRigidBody, raycastHit.ColliderKey);
                gameManagerRW.dragingEntityInfo = new GameManagerSingletonComponent.DragingEntityInfo(hitEntity, hitRigidBody, raycastHit.ColliderKey, material, raycastHit.Position);

                if (Input.GetKey(KeyCode.LeftAlt))
                    onMouseDownPosition = raycastHit.Position;

                material.RestitutionCombinePolicy = Material.CombinePolicy.Minimum;
                Utils.SetMaterial(gameManagerRW.dragingEntityInfo.rigidbody, material, raycastHit.ColliderKey);
            }
            if (entityManager.HasComponent<BodyPartComponent>(hitEntity))
            {
                BodyPartComponent bodyPartComponent = entityManager.GetComponentData<BodyPartComponent>(hitEntity);
                if (Input.GetKey(KeyCode.LeftControl))
                    foreach (var (bodyPart, rigidBodyAspect) in SystemAPI.Query<RefRO<BodyPartComponent>, RigidBodyAspect>())
                    {
                        if (bodyPart.ValueRO.ownerEntity == bodyPartComponent.ownerEntity)
                        {
                            rigidBodyAspect.Position = gameManagerRW.playerSpawnPoint;
                            rigidBodyAspect.LinearVelocity *= 0;
                        }
                    };
            }
            if(entityManager.HasComponent<ExplosiveComponent>(hitEntity))
            {
                ExplosiveComponent explosiveComponent = entityManager.GetComponentData<ExplosiveComponent>(hitEntity);
                explosiveComponent.isEnable = false;
                entityManager.SetComponentData(hitEntity, explosiveComponent);
            }
        }
    }
    private void OnMouse(ref GameManagerSingletonComponent gameManagerRW)
    {
        if (!isDraging || Input.GetKey(KeyCode.LeftControl)) return;

        dragingTimer += time.DeltaTime;
        if (entityManager.HasComponent<BodyPartComponent>(gameManagerRW.dragingEntityInfo.entity) && dragingTimer > 1)
        {
            dragingTimer = 0;

            BodyPartComponent bodyPartComponent = entityManager.GetComponentData<BodyPartComponent>(gameManagerRW.dragingEntityInfo.entity);
            PlayerComponent playerComponent = entityManager.GetComponentData<PlayerComponent>(bodyPartComponent.ownerEntity);
            GameManager.instance.viewerInfos[playerComponent.userName].UpdateNameTag().Forget();
        }

        PhysicsVelocity velocity = entityManager.GetComponentData<PhysicsVelocity>(gameManagerRW.dragingEntityInfo.entity);
        LocalTransform localTransform = entityManager.GetComponentData<LocalTransform>(gameManagerRW.dragingEntityInfo.entity);

        float3 entityPosition = localTransform.Position;
        float3 entityPositionFromGrabingPoint = entityPosition - entityPositionOnDown;
        float3 mousePositionFromGrabingPoint;
        if (Input.GetKey(KeyCode.LeftAlt))
        {
            float3 rayStart = gameManagerRW.ScreenPointToRayMainCam.origin;
            float3 rayEnd = gameManagerRW.ScreenPointToRayMainCam.GetPoint(10000f);
            Raycast(rayStart, rayEnd, out RaycastHit raycastHit);
            mousePositionFromGrabingPoint = raycastHit.Position - onMouseDownPosition; 
        }
        else
            mousePositionFromGrabingPoint = new float3(gameManagerRW.ScreenToWorldPointMainCam.x, gameManagerRW.ScreenToWorldPointMainCam.y,0) - onMouseDownPosition;
        float3 entitiyToMouse = mousePositionFromGrabingPoint - entityPositionFromGrabingPoint;
        /*float2 mouseToEntity = entityPositionFromGrabingPoint - mousePositionFromGrabingPoint;

        float angularForce = lastEntityRotation - Vector2.Angle(Vector2.up, mouseToEntity);
        velocity.Angular += angularForce * time.DeltaTime;*/

        velocity.Linear = math.lerp(velocity.Linear, float3.zero, gameManagerRW.stabilityPower * time.DeltaTime);
        velocity.Linear += entitiyToMouse * gameManagerRW.dragPower * time.DeltaTime;

        entityManager.SetComponentData(gameManagerRW.dragingEntityInfo.entity, velocity);

        lastEntityRotation = localTransform.Rotation.value.z;
    }
    private void OnMouseUp(ref GameManagerSingletonComponent gameManagerRW)
    {
        if (!isDraging) return;
        isDraging = false;
        dragingTimer = 0;

        if (entityManager.HasComponent<ExplosiveComponent>(gameManagerRW.dragingEntityInfo.entity))
        {
            ExplosiveComponent explosiveComponent = entityManager.GetComponentData<ExplosiveComponent>(gameManagerRW.dragingEntityInfo.entity);
            explosiveComponent.isEnable = true;
            entityManager.SetComponentData(gameManagerRW.dragingEntityInfo.entity, explosiveComponent);
        }

        if (entityManager.HasComponent<PlayerComponent>(gameManagerRW.dragingEntityInfo.entity))
        {
            PlayerComponent peepoComponent = entityManager.GetComponentData<PlayerComponent>(gameManagerRW.dragingEntityInfo.entity);
            peepoComponent.currentState = SteveState.Ragdoll;
            Utils.SetMaterial(gameManagerRW.dragingEntityInfo.rigidbody, gameManagerRW.dragingEntityInfo.material, gameManagerRW.dragingEntityInfo.colliderKey);
            entityManager.SetComponentData(gameManagerRW.dragingEntityInfo.entity, peepoComponent);
        }
        gameManagerRW.dragingEntityInfo = default;
    }
    private bool Raycast(float3 rayStart, float3 rayEnd, out RaycastHit raycastHit)
    {
        var raycastInput = new RaycastInput
        {
            Start = rayStart,
            End = rayEnd,
            Filter = clickableFilter
        };
        return _physicsWorldSingleton.CastRay(raycastInput, out raycastHit);
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
}
