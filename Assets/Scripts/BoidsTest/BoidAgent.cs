using UnityEngine;

/// <summary>单个群集单位，持有自己的运动状态与显示对象。</summary>
public class BoidAgent
{
    public const float MaxSpeed = 4f;

    readonly Transform view;

    public Vector3 Position { get; private set; }
    public Vector3 Velocity { get; private set; }

    public BoidAgent(Transform view, Vector3 position, Quaternion heading)
    {
        this.view = view;
        Position = position;
        Velocity = heading * Vector3.forward * MaxSpeed;
        view.SetPositionAndRotation(position, heading);
    }

    public void Move(Vector3 acceleration, float deltaTime, Vector2 halfExtents)
    {
        Vector3 velocity = Vector3.ClampMagnitude(Velocity + acceleration * deltaTime, MaxSpeed);
        Vector3 position = Position + velocity * deltaTime;
        // 在画面边缘折返，即使鼠标移出也能继续观察群集。
        ReflectAtEdge(ref position.x, ref velocity.x, halfExtents.x);
        ReflectAtEdge(ref position.z, ref velocity.z, halfExtents.y);
        Position = position;
        Velocity = velocity;
        view.position = position;
        if (velocity.sqrMagnitude > 0.2f)
            view.rotation = Quaternion.LookRotation(velocity);
    }

    static void ReflectAtEdge(ref float position, ref float velocity, float extent)
    {
        if (position > extent)
        {
            position = extent;
            velocity = -Mathf.Abs(velocity);
        }
        else if (position < -extent)
        {
            position = -extent;
            velocity = Mathf.Abs(velocity);
        }
    }
}
