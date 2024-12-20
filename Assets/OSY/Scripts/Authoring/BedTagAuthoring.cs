using Unity.Entities;
using UnityEngine;

class BedTagAuthoring : MonoBehaviour
{
    
}

class BedTagAuthoringBaker : Baker<BedTagAuthoring>
{
    public override void Bake(BedTagAuthoring authoring)
    {
        Entity entity = GetEntity(authoring.gameObject, TransformUsageFlags.Dynamic);
        AddComponent(entity, new BedTag());
    }
}
