using UnityEngine;

public enum FormationBoundsType
{
    Aabb,
    Obb
}

/// <summary>矩形阵型，支持世界轴对齐和沿移动朝向旋转的边界。</summary>
public class BoidFormation
{
    // 箭头最远顶点距单位中心 0.36，边缘检测与出生位置都预留这段距离。
    public const float AgentRadius = 0.36f;
    const float ReturnStrength = 8f;
    const float ReturnDamping = 2f;
    const float MinHeadingSpeed = 0.2f;
    const float RotationDeadZone = 3f;
    const float TurnSpeed = 120f;

    public FormationBoundsType BoundsType { get; set; }
    public Vector3 Center { get; private set; }
    public Vector3 Velocity { get; private set; }
    public Vector2 Size { get; private set; }
    public Vector3 Forward { get; private set; } = Vector3.forward;

    Quaternion Rotation => BoundsType == FormationBoundsType.Obb
        ? Quaternion.LookRotation(Forward) : Quaternion.identity;

    public BoidFormation(Vector3 center, Vector2 size)
    {
        center.y = 0;
        Center = center;
        SetSize(size);
    }

    public void SetSize(Vector2 size)
    {
        Size = new Vector2(Mathf.Max(AgentRadius * 2, size.x), Mathf.Max(AgentRadius * 2, size.y));
    }

    public void Step(Vector3? target, float speed, float deltaTime)
    {
        // 读取上一步速度，过滤低速方向噪声，并限制边框每秒的旋转角度。
        if (Velocity.sqrMagnitude > MinHeadingSpeed * MinHeadingSpeed)
        {
            Quaternion current = Quaternion.LookRotation(Forward);
            Quaternion desired = Quaternion.LookRotation(Velocity);
            if (Quaternion.Angle(current, desired) > RotationDeadZone)
                Forward = Quaternion.RotateTowards(current, desired, TurnSpeed * deltaTime) * Vector3.forward;
        }

        Vector3 center = Center;
        Vector3 next = center;
        if (target.HasValue)
        {
            Vector3 destination = target.Value;
            destination.y = 0;
            next = Vector3.MoveTowards(center, destination, Mathf.Max(0, speed) * deltaTime);
        }
        Vector3 movement = next - center;
        Velocity = deltaTime > 0 ? movement / deltaTime : Vector3.zero;
        Center = next;
    }

    public Vector3 GetReturnAcceleration(Vector3 position, Vector3 agentVelocity)
    {
        Vector3 offset = GetReturnOffset(position);
        if (offset.sqrMagnitude < 0.000001f) return Vector3.zero;
        // 回归力随越界距离增加，相对阵型速度的阻尼减少来回振荡。
        return offset * ReturnStrength - (agentVelocity - Velocity) * ReturnDamping;
    }

    public float GetSpeedMultiplier(Vector3 position)
    {
        return 1f + Mathf.Clamp01(GetReturnOffset(position).magnitude);
    }

    public Vector3 ToWorld(Vector3 localPosition)
    {
        return Center + Rotation * localPosition;
    }

    public Vector3 GetCorner(int index)
    {
        float x = index < 2 ? -Size.x * 0.5f : Size.x * 0.5f;
        float z = index == 0 || index == 3 ? -Size.y * 0.5f : Size.y * 0.5f;
        return ToWorld(new Vector3(x, 0, z));
    }

    Vector3 GetReturnOffset(Vector3 position)
    {
        Quaternion rotation = Rotation;
        Vector3 local = Quaternion.Inverse(rotation) * (position - Center);
        Vector2 extent = Size * 0.5f - Vector2.one * AgentRadius;
        Vector3 nearest = new Vector3(
            Mathf.Clamp(local.x, -extent.x, extent.x), local.y,
            Mathf.Clamp(local.z, -extent.y, extent.y));
        return rotation * (nearest - local);
    }
}
