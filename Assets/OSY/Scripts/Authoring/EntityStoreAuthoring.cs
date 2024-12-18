using Unity.Entities;
using UnityEngine;

public class EntityStoreAuthoring : MonoBehaviour
{
    public GameObject steve;
    public GameObject creeper;
}
public class EntityStoreAuthoringBaker : Baker<EntityStoreAuthoring>
{
    public override void Bake(EntityStoreAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.None);
        AddComponent(entity, new EntityStoreComponent { steve = GetEntity(authoring.steve, TransformUsageFlags.Dynamic), creeper = GetEntity(authoring.creeper, TransformUsageFlags.Dynamic) });
    }
}
