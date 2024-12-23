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
        new TimerJob { time = SystemAPI.Time }.ScheduleParallel(state.Dependency).Complete();

        EntityCommandBuffer ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

        foreach (var spawner in SystemAPI.Query<RefRW<SpawnerComponent>>())
        {
            if (spawner.ValueRO.currentSec >= spawner.ValueRO.spawnIntervalSec)
            {
                spawner.ValueRW.currentSec = 0;
                new SpawnJob { parallelWriter = ecb.AsParallelWriter() }.ScheduleParallel(state.Dependency).Complete();
            }
        }
    }

    [BurstCompile]
    partial struct TimerJob : IJobEntity
    {
        [ReadOnly] public TimeData time;
        public void Execute(ref SpawnerComponent spawnerComponent)
        {
            if (spawnerComponent.currentSec < spawnerComponent.spawnIntervalSec)
                spawnerComponent.currentSec += time.DeltaTime;
        }
    }
    [BurstCompile]
    partial struct SpawnJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter parallelWriter;
        public void Execute([ChunkIndexInQuery] int chunkIndex, ref SpawnerComponent spawnerComponent, ref RandomDataComponent randomDataComponent, in LocalTransform spawnerTransformComponent)
        {
            if (spawnerComponent.spawnedCount >= spawnerComponent.maxCount) return;
            Entity spawnedEntity;
            LocalTransform initTransform;
            if (spawnerComponent.spawnIntervalSec == 0)
            {
                int remainCount = spawnerComponent.maxCount - spawnerComponent.spawnedCount;
                if (remainCount <= 0) return;
                int batchCount = remainCount > spawnerComponent.batchCount ? spawnerComponent.batchCount : remainCount;
                for (int i = 0; i < batchCount; i++)
                {
                    randomDataComponent.Random = new Random((uint)randomDataComponent.Random.NextInt(int.MinValue, int.MaxValue));
                    spawnedEntity = parallelWriter.Instantiate(chunkIndex, spawnerComponent.targetEntity);
                    initTransform = new LocalTransform { Position = spawnerTransformComponent.Position, Rotation = spawnerTransformComponent.Rotation, Scale = spawnerComponent.isRandomSize ? randomDataComponent.Random.NextFloat(spawnerComponent.minSize, spawnerComponent.maxSize) : 1 };
                    parallelWriter.SetComponent(chunkIndex, spawnedEntity, initTransform);
                    ++spawnerComponent.spawnedCount;
                    //parallelWriter.SetName(chunkIndex, spawnedEntity, $"Steve{}");
                }
                return;
            }
            randomDataComponent.Random = new Random((uint)randomDataComponent.Random.NextInt(int.MinValue, int.MaxValue));
            spawnedEntity = parallelWriter.Instantiate(chunkIndex, spawnerComponent.targetEntity);
            initTransform = new LocalTransform { Position = spawnerTransformComponent.Position, Rotation = spawnerTransformComponent.Rotation, Scale = spawnerComponent.isRandomSize ? randomDataComponent.Random.NextFloat(spawnerComponent.minSize, spawnerComponent.maxSize) : 1 };
            parallelWriter.SetComponent(chunkIndex, spawnedEntity, initTransform);
            ++spawnerComponent.spawnedCount;
            //parallelWriter.SetName(chunkIndex, spawnedEntity, $"Steve{}");


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