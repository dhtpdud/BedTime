using Cysharp.Threading.Tasks;
using Unity.Entities;
using UnityEngine;

public class SpawnerAuthoring : MonoBehaviour
{
    public GameObject target;
    public int totalCount;
    public float intervalSec;
    public bool isRandomSize;
    public float minSize;
    public float maxSize;
    public class SpawnerAuthoringBaker : Baker<SpawnerAuthoring>
    {
        public override void Bake(SpawnerAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new SpawnerComponent
            {
                targetEntity = GetEntity(authoring.target,TransformUsageFlags.Dynamic),
                maxCount = authoring.totalCount,
                spawnIntervalSec = authoring.intervalSec,
                isRandomSize = authoring.isRandomSize,
                minSize = authoring.minSize,
                maxSize = authoring.maxSize
            });
            AddComponent(entity, new RandomDataComponent
            {
                Random = new Unity.Mathematics.Random((uint)Random.Range(int.MinValue, int.MaxValue))
            });
        }
    }
}
