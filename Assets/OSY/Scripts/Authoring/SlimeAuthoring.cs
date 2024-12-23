using Unity.Entities;
using UnityEngine;

class SlimeAuthoring : MonoBehaviour
{
    
}

class SlimeAuthoringBaker : Baker<SlimeAuthoring>
{
    public override void Bake(SlimeAuthoring authoring)
    {
        var entity = GetEntity(authoring.gameObject, TransformUsageFlags.Dynamic);
        AddComponent(entity, new SlimeTag());
    }
}
