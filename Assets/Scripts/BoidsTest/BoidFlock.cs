using UnityEngine;

/// <summary>群集规则与更新顺序：统一计算转向后，再让单位移动。</summary>
public class BoidFlock
{
    const float MaxAcceleration = 8f;
    const float NeighborRadius = 2.6f;
    const float SeparationRadius = 0.9f;

    readonly BoidAgent[] agents;
    readonly Vector3[] accelerations;

    public float SeparationWeight { get; set; }
    public float AlignmentWeight { get; set; }
    public float CohesionWeight { get; set; }
    public float MouseWeight { get; set; }

    public BoidFlock(BoidAgent[] agents)
    {
        this.agents = agents;
        accelerations = new Vector3[agents.Length];
    }

    public void Step(Vector3? target, float deltaTime, Vector2 halfExtents)
    {
        for (int i = 0; i < agents.Length; i++)
            accelerations[i] = Steering(agents[i], target);

        for (int i = 0; i < agents.Length; i++)
            agents[i].Move(accelerations[i], deltaTime, halfExtents);
    }

    Vector3 Steering(BoidAgent agent, Vector3? target)
    {
        Vector3 separation = Vector3.zero; // 分离量
        Vector3 velocitySum = Vector3.zero; // 速度量
        Vector3 offsetSum = Vector3.zero; // 接近量
        int neighbors = 0;

        for (int j = 0; j < agents.Length; j++)
        {
            BoidAgent neighbor = agents[j];
            if (neighbor == agent) continue;
            Vector3 offset = neighbor.Position - agent.Position;
            float distanceSq = offset.sqrMagnitude;
            if (distanceSq > NeighborRadius * NeighborRadius) continue;
            velocitySum += neighbor.Velocity;
            offsetSum += offset;
            neighbors++;
            if (distanceSq < SeparationRadius * SeparationRadius)
                separation -= offset / Mathf.Max(distanceSq, 0.0001f);
        }

        Vector3 force = separation * SeparationWeight;
        if (neighbors > 0)
        {
            force += (velocitySum / neighbors - agent.Velocity) * AlignmentWeight;
            force += offsetSum / neighbors * CohesionWeight;
        }
        if (target.HasValue)
        {
            Vector3 desiredVelocity = Vector3.ClampMagnitude(target.Value - agent.Position, BoidAgent.MaxSpeed);
            force += (desiredVelocity - agent.Velocity) * MouseWeight;
        }
        return Vector3.ClampMagnitude(force, MaxAcceleration);
    }
}
