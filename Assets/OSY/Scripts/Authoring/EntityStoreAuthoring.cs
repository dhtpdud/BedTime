using Unity.Entities;
using UnityEngine;

public class EntityStoreAuthoring : MonoBehaviour
{
    public GameObject steve;
    public class EntityStoreAuthoringBaker : Baker<EntityStoreAuthoring>
    {
        public override void Bake(EntityStoreAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new EntityStoreComponent(
                GetEntity(authoring.steve, TransformUsageFlags.Dynamic)));
        }
    }
}
