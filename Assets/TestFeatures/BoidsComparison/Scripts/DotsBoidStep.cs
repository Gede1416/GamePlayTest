using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace DotsBoids
{
    struct BoidState : IComponentData
    {
        // 两个分量分别对应世界坐标 x、z。
        public float2 Position;
        public float2 Velocity;
    }

    [BurstCompile]
    struct DotsBoidStep : IJobParallelFor
    {
        public const float Speed = 2.5f;
        public const float FieldSize = 18f;
        const float NeighborRadius = 2.5f;
        const float SeparationRadius = 1f;

        [ReadOnly] public NativeArray<BoidState> Previous;
        [WriteOnly] public NativeArray<BoidState> Next;
        public float SeparationWeight;
        public float AlignmentWeight;
        public float CohesionWeight;
        public float DeltaTime;

        public void Execute(int index)
        {
            BoidState self = Previous[index];
            float2 separation = 0, velocitySum = 0, offsetSum = 0;
            int neighbors = 0;

            for (int i = 0; i < Previous.Length; i++)
            {
                if (i == index) continue;
                BoidState other = Previous[i];
                // 场地两侧相连，跨边界邻居也取最短位移。
                float2 offset = other.Position - self.Position;
                offset -= FieldSize * math.round(offset / FieldSize);
                float distanceSq = math.lengthsq(offset);
                if (distanceSq > NeighborRadius * NeighborRadius) continue;

                if (distanceSq < SeparationRadius * SeparationRadius)
                    separation -= offset / math.max(distanceSq, 0.01f);
                velocitySum += other.Velocity;
                offsetSum += offset;
                neighbors++;
            }

            if (neighbors > 0)
            {
                float2 alignment = velocitySum / neighbors - self.Velocity;
                float2 cohesion = offsetSum / neighbors;
                float2 steering = separation * SeparationWeight
                    + alignment * AlignmentWeight + cohesion * CohesionWeight;
                float2 velocity = self.Velocity + steering * DeltaTime;
                self.Velocity = math.normalizesafe(velocity, self.Velocity / Speed) * Speed;
            }

            self.Position += self.Velocity * DeltaTime;
            self.Position -= FieldSize * math.floor(self.Position / FieldSize + 0.5f);
            Next[index] = self;
        }
    }
}
