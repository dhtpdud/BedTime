using Rukhanka;
using Unity.Burst;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
public partial struct SpawnerSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        new SpawnerJob { time = SystemAPI.Time, parallelWriter = ecb.AsParallelWriter() }.ScheduleParallel();
    }
    [BurstCompile]
    partial struct SpawnerJob : IJobEntity
    {
        [ReadOnly] public TimeData time;
        public EntityCommandBuffer.ParallelWriter parallelWriter;
        public void Execute([ChunkIndexInQuery] int chunkIndex, ref SpawnerComponent spawnerComponent, ref RandomDataComponent randomDataComponent, in LocalTransform spawnerTransformComponent)
        {
            if (spawnerComponent.spawnedCount >= spawnerComponent.maxCount) return;
            if (spawnerComponent.currentSec < spawnerComponent.spawnIntervalSec)
            {
                spawnerComponent.currentSec += time.DeltaTime;
                return;
            }
            spawnerComponent.currentSec = 0;
            randomDataComponent.Random = new Random((uint)randomDataComponent.Random.NextInt(int.MinValue, int.MaxValue));
            Entity spawnedEntity = parallelWriter.Instantiate(chunkIndex, spawnerComponent.targetEntity);
            var initTransform = new LocalTransform { Position = spawnerTransformComponent.Position, Rotation = spawnerTransformComponent.Rotation, Scale = spawnerComponent.isRandomSize ? randomDataComponent.Random.NextFloat(spawnerComponent.minSize, spawnerComponent.maxSize) : 1 };
            parallelWriter.SetComponent(chunkIndex, spawnedEntity, initTransform);
            parallelWriter.SetName(chunkIndex, spawnedEntity, $"Steve{++spawnerComponent.spawnedCount}");

            //spawnerComponent.spawnedCount++;
        }
    }/*
    [BurstCompile]
    partial struct SpawnedEntityInitJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter parallelWriter;
        public void Execute([ChunkIndexInQuery] int chunkIndex, in Entity entity, in AnimatorEntityRefComponent animatorEntityRef)
        {
            if(animatorEntityRef.boneIndexInAnimationRig == 1)
                parallelWriter.SetComponent()
        }
    }*/
}
