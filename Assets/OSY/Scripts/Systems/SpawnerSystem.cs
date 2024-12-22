using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
public sealed partial class SpawnerSystem : SystemBase
{
    [BurstCompile]
    partial struct TimerJob : IJobEntity
    {
        [ReadOnly] public float deltaTime;
        public void Execute(ref SpawnerComponent spawner)
        {
            if (spawner.currentSec < spawner.spawnIntervalSec)
                spawner.currentSec += deltaTime;
        }
    }
    [BurstCompile]
    partial struct SpawnJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter ecb;
        public void Execute([ChunkIndexInQuery] int chunkIndex, ref SpawnerComponent spawner, ref RandomDataComponent rnd, in LocalTransform spawnerTransform)
        {
            if (spawner.spawnedCount >= spawner.maxCount) return;
            bool doSpawn = (spawner.spawnIntervalSec == 0) || (spawner.currentSec >= spawner.spawnIntervalSec);
            if (!doSpawn) return;
            spawner.currentSec = 0;
            int remain = spawner.maxCount - spawner.spawnedCount;
            if (remain <= 0) return;
            int batch = (spawner.spawnIntervalSec == 0) ? math.min(remain, spawner.batchCount) : 1;
            for (int i = 0; i < batch; i++)
            {
                rnd.Random = new Unity.Mathematics.Random((uint)rnd.Random.NextInt(int.MinValue, int.MaxValue));
                var e = ecb.Instantiate(chunkIndex, spawner.targetEntity);
                var lt = new LocalTransform
                {
                    Position = spawnerTransform.Position,
                    Rotation = spawnerTransform.Rotation,
                    Scale = spawner.isRandomSize ? rnd.Random.NextFloat(spawner.minSize, spawner.maxSize) : 1f
                };
                ecb.SetComponent(chunkIndex, e, lt);
                spawner.spawnedCount++;
            }
        }
    }
    protected override void OnUpdate()
    {
        float dt = SystemAPI.Time.DeltaTime;
        var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged).AsParallelWriter();

        Dependency = new TimerJob { deltaTime = dt }.ScheduleParallel(Dependency);
        Dependency = new SpawnJob { ecb = ecb }.ScheduleParallel(Dependency);
        Dependency.Complete();
    }
}
