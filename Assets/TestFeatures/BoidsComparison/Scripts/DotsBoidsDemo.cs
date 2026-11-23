using BoidsComparison;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace DotsBoids
{
    public sealed class DotsBoidsDemo : MonoBehaviour
    {
        [SerializeField, Min(1), Tooltip("重新启用场景组件后生效")] int count = 10000;
        [SerializeField] Material boidMaterial;
        [SerializeField, Range(0, 6), Tooltip("远离过近的邻居")] float separationWeight = 3f;
        [SerializeField, Range(0, 6), Tooltip("朝邻居的平均速度转向")] float alignmentWeight = 1f;
        [SerializeField, Range(0, 6), Tooltip("朝附近邻居的中心转向")] float cohesionWeight = 0.8f;

        World world;
        EntityQuery boids;
        NativeArray<BoidState> next;
        BoidDisplay display;

        void OnEnable()
        {
            world = new World("DOTS Boids");
            EntityManager manager = world.EntityManager;
            var archetype = manager.CreateArchetype(typeof(BoidState));
            boids = manager.CreateEntityQuery(ComponentType.ReadWrite<BoidState>());
            next = new NativeArray<BoidState>(count, Allocator.Persistent);
            var random = new Unity.Mathematics.Random(42);
            for (int i = 0; i < next.Length; i++)
            {
                next[i] = new BoidState
                {
                    Position = random.NextFloat2(-8f, 8f),
                    Velocity = random.NextFloat2Direction() * DotsBoidStep.Speed
                };
                manager.CreateEntity(archetype);
            }
            boids.CopyFromComponentDataArray(next);
            display = new BoidDisplay(count, boidMaterial);
            UpdateMatrices();
        }

        void FixedUpdate()
        {
            // 所有个体读取同一步的快照，然后一次写回 ECS 组件。
            using var previous = boids.ToComponentDataArray<BoidState>(Allocator.TempJob);
            new DotsBoidStep
            {
                Previous = previous,
                Next = next,
                SeparationWeight = separationWeight,
                AlignmentWeight = alignmentWeight,
                CohesionWeight = cohesionWeight,
                DeltaTime = Time.fixedDeltaTime
            }.Schedule(next.Length, 16).Complete();
            boids.CopyFromComponentDataArray(next);
            UpdateMatrices();
        }

        void LateUpdate()
        {
            display.Draw();
        }

        void OnDisable()
        {
            next.Dispose();
            // 退出播放时 Unity 可能已释放 World 及其查询。
            if (world.IsCreated) world.Dispose();
            display.Release();
        }

        void UpdateMatrices()
        {
            for (int i = 0; i < next.Length; i++)
            {
                BoidState state = next[i];
                display.Set(i, state.Position, state.Velocity);
            }
        }
    }
}
