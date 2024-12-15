using OSY;
using Rukhanka;
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
public partial struct MouseInteractionSystem : ISystem, ISystemStartStop
{
    float timer;
    private CollisionFilter clickableFilter;
    private PhysicsWorldSingleton _physicsWorldSingleton;
    private EntityManager entityManager;
    TimeData time;
    float2 entityPositionOnDown;

    public bool isDraging;

    public float2 mouseLastPosition;
    public float2 mouseVelocity;
    public float2 onMouseDownPosition;
    public float lastEntityRotation;
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        clickableFilter = new CollisionFilter { BelongsTo = 1u << 0, CollidesWith = ~(1u << 3), GroupIndex = 0 };
        state.RequireForUpdate<GameManagerSingletonComponent>();
        state.RequireForUpdate<EntityStoreComponent>();
        entityManager = state.EntityManager;
    }


    [BurstCompile]
    public void OnStartRunning(ref SystemState state)
    {

    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        time = SystemAPI.Time;
        //timer += time.DeltaTime;
        _physicsWorldSingleton = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        if (Input.GetMouseButtonDown(0))
        {
            //RefRW는 매 프레임 마다. 사용할 때 호출해서 사용해야함.
            OnMouseDown(ref SystemAPI.GetSingletonRW<GameManagerSingletonComponent>().ValueRW);
        }
        else if (Input.GetMouseButtonUp(0))
        {
            OnMouseUp(ref SystemAPI.GetSingletonRW<GameManagerSingletonComponent>().ValueRW);
        }
        if (Input.GetMouseButton(0))
        {
            OnMouse(ref SystemAPI.GetSingletonRW<GameManagerSingletonComponent>().ValueRW);
            EntityCommandBuffer ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            new SteveTestJob { time = SystemAPI.Time }.ScheduleParallel();
        }

        if(timer > 0.5f)
        {
            EntityCommandBuffer ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            new SteveDanceTestJob().ScheduleParallel();
            timer = 0;
        }
    }
    private void OnMouseDown(ref GameManagerSingletonComponent gameManagerRW)
    {
        onMouseDownPosition = gameManagerRW.ScreenToWorldPointMainCam;

        float3 rayStart = gameManagerRW.ScreenPointToRayOfMainCam.origin;
        float3 rayEnd = gameManagerRW.ScreenPointToRayOfMainCam.GetPoint(1000f);
        if (Raycast(rayStart, rayEnd, out RaycastHit raycastHit))
        {
            RigidBody hitRigidBody = _physicsWorldSingleton.PhysicsWorld.Bodies[raycastHit.RigidBodyIndex];
            Entity hitEntity = hitRigidBody.Entity;
            if (entityManager.HasComponent<DragableTag>(hitEntity))
            {
                LocalTransform localTransform = entityManager.GetComponentData<LocalTransform>(hitEntity);
                lastEntityRotation = localTransform.Rotation.value.z;
                entityPositionOnDown = localTransform.Position.ToFloat2();
                isDraging = true;

                Material material = Utils.GetMaterial(hitRigidBody, raycastHit.ColliderKey);
                gameManagerRW.dragingEntityInfo = new GameManagerSingletonComponent.DragingEntityInfo(hitEntity, hitRigidBody, raycastHit.ColliderKey, material);

                material.RestitutionCombinePolicy = Material.CombinePolicy.Minimum;
                Utils.SetMaterial(gameManagerRW.dragingEntityInfo.rigidbody, material, raycastHit.ColliderKey);
            }
            if (entityManager.HasComponent<PlayerComponent>(hitEntity))
            {
                PlayerComponent peepoComponent = entityManager.GetComponentData<PlayerComponent>(hitEntity);
                peepoComponent.currentState = SteveState.Dragging;
                entityManager.SetComponentData(gameManagerRW.dragingEntityInfo.entity, peepoComponent);
            }
        }
    }
    private void OnMouse(ref GameManagerSingletonComponent gameManagerRW)
    {
        if (!isDraging) return;
        PhysicsVelocity velocity = entityManager.GetComponentData<PhysicsVelocity>(gameManagerRW.dragingEntityInfo.entity);
        LocalTransform localTransform = entityManager.GetComponentData<LocalTransform>(gameManagerRW.dragingEntityInfo.entity);

        float2 entityPosition = localTransform.Position.ToFloat2();
        float2 entityPositionFromGrabingPoint = entityPosition - entityPositionOnDown;
        float2 mousePositionFromGrabingPoint = gameManagerRW.ScreenToWorldPointMainCam - onMouseDownPosition;
        float2 entitiyToMouse = mousePositionFromGrabingPoint - entityPositionFromGrabingPoint;
        /*float2 mouseToEntity = entityPositionFromGrabingPoint - mousePositionFromGrabingPoint;

        float angularForce = lastEntityRotation - Vector2.Angle(Vector2.up, mouseToEntity);
        velocity.Angular += angularForce * time.DeltaTime;*/

        velocity.Linear = math.lerp(velocity.Linear, float3.zero, gameManagerRW.stabilityPower * time.DeltaTime);
        velocity.Linear += (entitiyToMouse * gameManagerRW.dragPower * time.DeltaTime).ToFloat3();

        entityManager.SetComponentData(gameManagerRW.dragingEntityInfo.entity, velocity);

        lastEntityRotation = localTransform.Rotation.value.z;
    }
    private void OnMouseUp(ref GameManagerSingletonComponent gameManagerRW)
    {
        if (!isDraging) return;
        isDraging = false;
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
    public void OnStopRunning(ref SystemState state)
    {
    }

    [BurstCompile]
    partial struct SteveDanceTestJob : IJobEntity
    {
        public void Execute([ChunkIndexInQuery] int chunkIndex, in BodyPartComponent bodyPart, RigidBodyAspect rigidBodyAspect)
        {
            switch(bodyPart.partType)
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
    partial struct SteveTestJob : IJobEntity
    {
        [ReadOnly] public TimeData time;
        public void Execute([ChunkIndexInQuery] int chunkIndex, in BodyPartComponent bodyPart, RigidBodyAspect rigidBodyAspect)
        {
            switch (bodyPart.partType)
            {
                case SteveBodyPart.RightHand:
                case SteveBodyPart.LeftHand:
                    rigidBodyAspect.LinearVelocity += new float3(0, 20*time.DeltaTime, 0);
                    break;
            }
        }
    }
}
