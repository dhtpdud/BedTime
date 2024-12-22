using Cysharp.Threading.Tasks;
using OSY;
using Unity.Burst;
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
public sealed partial class MouseInteractionSystem : SystemBase
{
    float dragingTimer;
    CollisionFilter clickableFilter;
    PhysicsWorldSingleton physicsWorld;
    float3 entityPosOnDown;
    bool isDragging;
    float3 onMouseDownPosition;
    float lastEntityRotation;

    protected override void OnCreate()
    {
        base.OnCreate();
        clickableFilter = new CollisionFilter { BelongsTo = 1u << 0, CollidesWith = ~(1u << 3), GroupIndex = 0 };
        RequireForUpdate<GameManagerSingletonComponent>();
        RequireForUpdate<EntityStoreComponent>();
    }
    protected override void OnUpdate()
    {
        physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        //var gm = SystemAPI.GetSingletonRW<GameManagerSingletonComponent>().ValueRW;
        if (Input.GetMouseButtonDown(0)) OnMouseDown(ref SystemAPI.GetSingletonRW<GameManagerSingletonComponent>().ValueRW);
        else if (Input.GetMouseButtonUp(0)) OnMouseUp(ref SystemAPI.GetSingletonRW<GameManagerSingletonComponent>().ValueRW);
        if (Input.GetMouseButton(0)) OnMouseDrag(ref SystemAPI.GetSingletonRW<GameManagerSingletonComponent>().ValueRW);
    }
    void OnMouseDown(ref GameManagerSingletonComponent gameManager)
    {
        onMouseDownPosition = new float3(gameManager.ScreenToWorldPointMainCam.x, gameManager.ScreenToWorldPointMainCam.y, 0);
        float3 rs = gameManager.ScreenPointToRayMainCam.origin;
        float3 re = gameManager.ScreenPointToRayMainCam.GetPoint(9999f);
        if (Raycast(rs, re, out var hit))
        {
            var rb = physicsWorld.PhysicsWorld.Bodies[hit.RigidBodyIndex];
            var e = rb.Entity;
            if (EntityManager.HasComponent<DragableTag>(e))
            {
                var lt = EntityManager.GetComponentData<LocalTransform>(e);
                lastEntityRotation = lt.Rotation.value.z;
                entityPosOnDown = lt.Position;
                isDragging = true;
                var mat = Utils.GetMaterial(rb, hit.ColliderKey);
                gameManager.dragingEntityInfo = new GameManagerSingletonComponent.DragingEntityInfo(e, rb, hit.ColliderKey, mat, hit.Position);
                if (Input.GetKey(KeyCode.LeftAlt)) onMouseDownPosition = hit.Position;
                mat.RestitutionCombinePolicy = Material.CombinePolicy.Minimum;
                Utils.SetMaterial(gameManager.dragingEntityInfo.rigidbody, mat, hit.ColliderKey);
            }
            if (EntityManager.HasComponent<BodyPartComponent>(e))
            {
                var bp = EntityManager.GetComponentData<BodyPartComponent>(e);
                if (Input.GetKey(KeyCode.LeftControl))
                {
                    foreach (var (b, r) in SystemAPI.Query<RefRO<BodyPartComponent>, RigidBodyAspect>())
                    {
                        if (b.ValueRO.ownerEntity == bp.ownerEntity)
                        {
                            r.Position = gameManager.playerSpawnPoint;
                            r.LinearVelocity = float3.zero;
                        }
                    }
                }
            }
            if (EntityManager.HasComponent<ExplosiveComponent>(e))
            {
                var ex = EntityManager.GetComponentData<ExplosiveComponent>(e);
                ex.isEnable = false;
                EntityManager.SetComponentData(e, ex);
            }
        }
    }
    void OnMouseDrag(ref GameManagerSingletonComponent gm)
    {
        if (!isDragging || Input.GetKey(KeyCode.LeftControl)) return;
        float dt = SystemAPI.Time.DeltaTime;
        dragingTimer += dt;
        if (EntityManager.HasComponent<BodyPartComponent>(gm.dragingEntityInfo.entity) && dragingTimer > 1f)
        {
            dragingTimer = 0;
            var bp = EntityManager.GetComponentData<BodyPartComponent>(gm.dragingEntityInfo.entity);
            var pc = EntityManager.GetComponentData<PlayerComponent>(bp.ownerEntity);
            GameManager.instance.viewerInfos[pc.userName].UpdateNameTag().Forget();
        }
        var vel = EntityManager.GetComponentData<PhysicsVelocity>(gm.dragingEntityInfo.entity);
        var lt = EntityManager.GetComponentData<LocalTransform>(gm.dragingEntityInfo.entity);
        float3 entPos = lt.Position;
        float3 offset = entPos - entityPosOnDown;
        float3 mouseOffset;
        if (Input.GetKey(KeyCode.LeftAlt))
        {
            float3 rs = gm.ScreenPointToRayMainCam.origin;
            float3 re = gm.ScreenPointToRayMainCam.GetPoint(9999f);
            if (Raycast(rs, re, out var hit)) mouseOffset = hit.Position - onMouseDownPosition;
            else mouseOffset = float3.zero;
        }
        else
        {
            mouseOffset = new float3(gm.ScreenToWorldPointMainCam.x, gm.ScreenToWorldPointMainCam.y, 0) - onMouseDownPosition;
        }
        float3 force = mouseOffset - offset;
        vel.Linear = math.lerp(vel.Linear, float3.zero, gm.stabilityPower * dt);
        vel.Linear += force * gm.dragPower * dt;
        EntityManager.SetComponentData(gm.dragingEntityInfo.entity, vel);
        lastEntityRotation = lt.Rotation.value.z;
    }
    void OnMouseUp(ref GameManagerSingletonComponent gm)
    {
        if (!isDragging) return;
        isDragging = false;
        dragingTimer = 0;
        if (EntityManager.HasComponent<ExplosiveComponent>(gm.dragingEntityInfo.entity))
        {
            var ex = EntityManager.GetComponentData<ExplosiveComponent>(gm.dragingEntityInfo.entity);
            ex.isEnable = true;
            EntityManager.SetComponentData(gm.dragingEntityInfo.entity, ex);
        }
        if (EntityManager.HasComponent<PlayerComponent>(gm.dragingEntityInfo.entity))
        {
            var pc = EntityManager.GetComponentData<PlayerComponent>(gm.dragingEntityInfo.entity);
            Utils.SetMaterial(gm.dragingEntityInfo.rigidbody, gm.dragingEntityInfo.material, gm.dragingEntityInfo.colliderKey);
            EntityManager.SetComponentData(gm.dragingEntityInfo.entity, pc);
        }
        gm.dragingEntityInfo = default;
    }
    bool Raycast(float3 start, float3 end, out RaycastHit hit)
    {
        var input = new RaycastInput { Start = start, End = end, Filter = clickableFilter };
        return physicsWorld.CastRay(input, out hit);
    }
}
