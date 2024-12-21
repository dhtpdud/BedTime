using Unity.Entities;
using UnityEngine;

class DragableAuthoring : MonoBehaviour
{
    
}

class DragableAuthoringBaker : Baker<DragableAuthoring>
{
    public override void Bake(DragableAuthoring authoring)
    {
        var entity = GetEntity(authoring.gameObject, TransformUsageFlags.Dynamic);
        AddComponent(entity, new DragableTag());
    }
}
