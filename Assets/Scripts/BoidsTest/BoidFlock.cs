using UnityEngine;

/// <summary>群集规则与更新顺序：统一计算转向后，再让单位移动。</summary>
public class BoidFlock
{
    const float MaxAcceleration = 8f;
    const float NeighborRadius = 2.6f;
    const float SeparationRadius = 0.9f;

    readonly BoidAgent[] agents;

    public float SeparationWeight { get; set; }
    public float AlignmentWeight { get; set; }
    public float CohesionWeight { get; set; }
    public float FormationFollowWeight { get; set; }

    public BoidFlock(BoidAgent[] agents)
    {
        this.agents = agents;
    }

    public void SetAttackParameters(float alertRange, float attackRange, float attackWeight)
    {
        // 攻击权重始终低于阵型追随权重，关闭追随时也关闭主动追敌。
        float weight = Mathf.Clamp(attackWeight, 0, Mathf.Max(0, FormationFollowWeight) * 0.9f);
        for (int i = 0; i < agents.Length; i++)
            agents[i].SetAttackParameters(alertRange, attackRange, weight);
    }

    public void Step(float deltaTime, BoidFormation formation, Vector3? enemyPosition = null)
    {
        for (int i = 0; i < agents.Length; i++)
            agents[i].SetAcceleration(Steering(agents[i], formation, enemyPosition));

        for (int i = 0; i < agents.Length; i++)
            agents[i].Move(deltaTime, formation);
    }

    Vector3 Steering(BoidAgent agent, BoidFormation formation, Vector3? enemyPosition)
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
        force += (formation.Velocity - agent.Velocity) * FormationFollowWeight;
        force += agent.GetAttackAcceleration(enemyPosition);
        force += formation.GetReturnAcceleration(agent.Position, agent.Velocity);
        return Vector3.ClampMagnitude(force, MaxAcceleration);
    }
}
