using Cysharp.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public partial struct UITransformUpdateSystem : ISystem, ISystemStartStop
{
    public JobHandle eventDepedency;
    float2 topRightScreenPoint;
    [BurstCompile]
    public void OnStartRunning(ref SystemState state)
    {
        topRightScreenPoint = new float2(Screen.width, Screen.height);
    }
    //[BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        /*if (GameManager.instance?.chatBubbleUICanvasTransform != null && GameManager.instance.chatBubbleUICanvasTransform.gameObject.activeInHierarchy)
            new UpdateChatBubbleHUDJob { topRightScreenPoint = this.topRightScreenPoint }.ScheduleParallel();*/
        //if (GameManager.instance?.nameTagUICanvasTransform != null && GameManager.instance.nameTagUICanvasTransform.gameObject.activeInHierarchy)
        //new UpdateNameTagHUDJob { topRightScreenPoint = this.topRightScreenPoint }.Schedule(state.Dependency).Complete();
        foreach (var (localTransform, nameTagComponent) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<NameTagComponent>>())
        {
            float3 position = localTransform.ValueRO.Position;
            float2 maxVal = topRightScreenPoint;
            FixedString128Bytes username = nameTagComponent.ValueRO.name;
            if (GameManager.instance?.nameTagUICanvasTransform != null && GameManager.instance.nameTagUICanvasTransform.gameObject.activeInHierarchy && GameManager.instance.viewerInfos != null)
                if (GameManager.instance.viewerInfos.ContainsKey(username))
                {
                    if (!GameManager.instance.viewerInfos[username].isEnable) continue;
                    RectTransform bubbleTransform = (RectTransform)GameManager.instance?.viewerInfos[username]?.nameTagObject?.transform;
                    if (bubbleTransform != null)
                    {
                        Vector2 targetPosition = GameManager.instance.mainCam.WorldToScreenPoint(position, Camera.MonoOrStereoscopicEye.Mono);
                        targetPosition.y += 80;
                        float MinX = bubbleTransform.rect.width / 2;
                        float MaxX = maxVal.x - bubbleTransform.rect.width / 2;
                        float MinY = 0;
                        float MaxY = maxVal.y - bubbleTransform.rect.height;
                        bubbleTransform.localPosition = (Vector2)math.clamp(targetPosition, new float2(MinX, MinY), new float2(MaxX, MaxY));
                    }
                }
        }
    }
    /*public partial struct UpdateChatBubbleHUDJob : IJobEntity
    {
        [ReadOnly] public float2 topRightScreenPoint;
        public void Execute(in LocalTransform localTransform, in HashIDComponent hash)
        {
            UnitaskExecute(localTransform.Position, hash.ID);
        }
        public void UnitaskExecute(float3 position, int hashID)
        {
            float2 maxVal = topRightScreenPoint;
            UniTask.RunOnThreadPool(async () =>
            {
                await UniTask.SwitchToMainThread();
                *//*if (GameManager.instance.viewerInfos != null)
                    if (GameManager.instance.viewerInfos.ContainsKey(hashID))
                    {
                        RectTransform bubbleTransform = (RectTransform)GameManager.instance.viewerInfos[hashID]?.chatBubbleObjects?.transform;
                        if (bubbleTransform != null)
                        {
                            Vector2 targetPosition = GameManager.instance.mainCam.WorldToScreenPoint(position, Camera.MonoOrStereoscopicEye.Mono);
                            targetPosition.y += 80;
                            float MinX = bubbleTransform.rect.width / 2;
                            float MaxX = maxVal.x - bubbleTransform.rect.width / 2;
                            float MinY = 0;
                            float MaxY = maxVal.y - bubbleTransform.rect.height;
                            bubbleTransform.localPosition = (Vector2)math.clamp(targetPosition, new float2(MinX, MinY), new float2(MaxX, MaxY));
                            //bubbleTransform.localPosition = targetPosition;
                        }
                    }*//*
            }, true, GameManager.instance.destroyCancellationToken).Forget();
        }
    }*/
    public partial struct UpdateNameTagHUDJob : IJobEntity
    {
        [ReadOnly] public float2 topRightScreenPoint;
        public void Execute(in LocalTransform localTransform, in NameTagComponent nameTagComponent)
        {
            float3 position = localTransform.Position;
            float2 maxVal = topRightScreenPoint;
            FixedString128Bytes username = nameTagComponent.name;
            UniTask.RunOnThreadPool(async () =>
            {
                await UniTask.SwitchToMainThread();
                if (GameManager.instance?.nameTagUICanvasTransform != null && GameManager.instance.nameTagUICanvasTransform.gameObject.activeInHierarchy && GameManager.instance.viewerInfos != null)
                    if (GameManager.instance.viewerInfos.ContainsKey(username))
                    {
                        RectTransform bubbleTransform = (RectTransform)GameManager.instance?.viewerInfos[username]?.nameTagObject?.transform;
                        if (bubbleTransform != null)
                        {
                            Vector2 targetPosition = GameManager.instance.mainCam.WorldToScreenPoint(position, Camera.MonoOrStereoscopicEye.Mono);
                            targetPosition.y += 80;
                            float MinX = bubbleTransform.rect.width / 2;
                            float MaxX = maxVal.x - bubbleTransform.rect.width / 2;
                            float MinY = 0;
                            float MaxY = maxVal.y - bubbleTransform.rect.height;
                            bubbleTransform.localPosition = (Vector2)math.clamp(targetPosition, new float2(MinX, MinY), new float2(MaxX, MaxY));
                        }
                    }
            }, true, GameManager.instance.destroyCancellationToken).Forget();
        }
    }

    [BurstCompile]
    public void OnStopRunning(ref SystemState state)
    {
    }
}
