using UnityEngine;

/// <summary>单个群集单位，持有自己的运动状态与显示对象。</summary>
public class BoidAgent
{
    public const float MaxSpeed = 4f;
    const float SpeedChangeRate = 8f;
    const float FormationFacingBias = 0.65f;
    const float MinHeadingSpeed = 0.2f;
    const float RotationDeadZone = 3f;
    const float TurnSpeed = 180f;

    readonly Transform view;
    Vector3 acceleration;
    float speedLimit = MaxSpeed;
    float alertRange;
    float attackRange;
    float attackWeight;

    public Vector3 Position { get; private set; }
    public Vector3 Velocity { get; private set; }

    public BoidAgent(Transform view, Vector3 position, Quaternion heading)
    {
        this.view = view;
        Position = position;
        Velocity = heading * Vector3.forward * MaxSpeed;
        view.SetPositionAndRotation(position, heading);
    }

    public void SetAcceleration(Vector3 acceleration)
    {
        this.acceleration = acceleration;
    }

    public void SetAttackParameters(float alertRange, float attackRange, float attackWeight)
    {
        this.alertRange = Mathf.Max(0, alertRange);
        this.attackRange = Mathf.Clamp(attackRange, 0, this.alertRange);
        this.attackWeight = Mathf.Max(0, attackWeight);
    }

    public Vector3 GetAttackAcceleration(Vector3? enemyPosition)
    {
        if (!enemyPosition.HasValue) return Vector3.zero;
        Vector3 offset = enemyPosition.Value - Position;
        offset.y = 0;
        float distanceSq = offset.sqrMagnitude;
        if (distanceSq > alertRange * alertRange || distanceSq <= attackRange * attackRange)
            return Vector3.zero;

        float distance = Mathf.Sqrt(distanceSq);
        // 靠近攻击范围时减速，避免始终向敌人中心冲刺。
        float speed = Mathf.Min(MaxSpeed, (distance - attackRange) * 4f);
        Vector3 desiredVelocity = offset / distance * speed;
        return (desiredVelocity - Velocity) * attackWeight;
    }

    public void Move(float deltaTime, BoidFormation formation)
    {
        speedLimit = Mathf.MoveTowards(speedLimit, MaxSpeed * formation.GetSpeedMultiplier(Position),
            SpeedChangeRate * deltaTime);
        Vector3 velocity = Vector3.ClampMagnitude(Velocity + acceleration * deltaTime, speedLimit);
        Vector3 position = Position + velocity * deltaTime;
        Position = position;
        Velocity = velocity;
        view.position = position;
        // 低速时沿用阵型朝向，避免把微小速度归一化后放大成朝向抖动。
        Vector3 facing = formation.Forward;
        if (velocity.sqrMagnitude > MinHeadingSpeed * MinHeadingSpeed)
            facing = Vector3.Lerp(velocity.normalized, formation.Forward, FormationFacingBias);
        Quaternion desired = Quaternion.LookRotation(facing);
        if (Quaternion.Angle(view.rotation, desired) > RotationDeadZone)
            view.rotation = Quaternion.RotateTowards(view.rotation, desired, TurnSpeed * deltaTime);
    }
}
