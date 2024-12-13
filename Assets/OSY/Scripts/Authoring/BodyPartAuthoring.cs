using Unity.Entities;
using UnityEngine;

public class BodyPartAuthoring : MonoBehaviour
{
    public GameObject owner;
    public SteveBodyPart partType;
    public class BodyPartBaker : Baker<BodyPartAuthoring>
    {
        public override void Bake(BodyPartAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new BodyPartComponent { ownerEntity = GetEntity(authoring.owner, TransformUsageFlags.Dynamic), partType = authoring.partType });
        }
    }
}
