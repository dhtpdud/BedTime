using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public class NameTagAuthoring : MonoBehaviour
{
    public FixedString64Bytes name;
    public class NameTagBaker : Baker<NameTagAuthoring>
    {
        public override void Bake(NameTagAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new NameTagComponent { name = authoring.name });
        }
    }
}
