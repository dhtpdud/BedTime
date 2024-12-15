using Unity.Entities;
using UnityEngine;

public class SteveAuthoring : MonoBehaviour
{
    public class SteveBaker : Baker<SteveAuthoring>
    {
        public override void Bake(SteveAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);

            PlayerComponent playerComponent = new PlayerComponent();
            AddComponent(entity, playerComponent);
            //AddComponent(entity, new PhysicsGravityFactor { Value = 1 });

            AddComponent(entity, new RandomDataComponent
            {
                Random = new Unity.Mathematics.Random((uint)Random.Range(int.MinValue, int.MaxValue))
            });
            //AddComponent(entity, new DragableTag());
            AddComponent(entity, new HashIDComponent());
        }
    }
}
