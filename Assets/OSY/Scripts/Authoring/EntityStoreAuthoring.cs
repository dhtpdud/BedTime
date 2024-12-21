using Unity.Entities;
using UnityEngine;

public class EntityStoreAuthoring : MonoBehaviour
{
    public GameObject steve;
    public GameObject creeper;
    public GameObject diamond;
    public GameObject particleExplosionWhite;
    public GameObject particleExplosionBlack;
}
public class EntityStoreAuthoringBaker : Baker<EntityStoreAuthoring>
{
    public override void Bake(EntityStoreAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.None);
        AddComponent(entity, new EntityStoreComponent 
        { 
            steve = GetEntity(authoring.steve, TransformUsageFlags.Dynamic), 
            creeper = GetEntity(authoring.creeper, TransformUsageFlags.Dynamic),
            diamond = GetEntity(authoring.diamond, TransformUsageFlags.Dynamic),
            particleExplosionWhite = GetEntity(authoring.particleExplosionWhite, TransformUsageFlags.Dynamic),
            particleExplosionBlack = GetEntity(authoring.particleExplosionBlack, TransformUsageFlags.Dynamic)
        });
    }
}
