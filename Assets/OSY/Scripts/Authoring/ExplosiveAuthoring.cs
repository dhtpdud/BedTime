using Unity.Entities;
using UnityEngine;

class ExplosiveAuthoring : MonoBehaviour
{
    public bool isEnable;
    public float range;
    public float power;
    public float maxTime;
}

class ExplosiveAuthoringBaker : Baker<ExplosiveAuthoring>
{
    public override void Bake(ExplosiveAuthoring authoring)
    {
        Entity entity = GetEntity(authoring.gameObject, TransformUsageFlags.Dynamic);
        AddComponent(entity, new ExplosiveComponent { isEnable = authoring.isEnable, range = authoring.range, power = authoring.power, time = authoring.maxTime, maxTime = authoring.maxTime });
        AddComponent(entity, new RandomDataComponent
        {
            Random = new Unity.Mathematics.Random((uint)Random.Range(int.MinValue, int.MaxValue))
        });
    }
}
