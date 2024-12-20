using Unity.Entities;
using UnityEngine;

class DestroyMarkAuthoring : MonoBehaviour
{
    
}

class DestroyMarkAuthoringBaker : Baker<DestroyMarkAuthoring>
{
    public override void Bake(DestroyMarkAuthoring authoring)
    {
        Entity entity = GetEntity(authoring.gameObject, TransformUsageFlags.Dynamic);
        AddComponent(entity, new DestroyMark());
    }
}
