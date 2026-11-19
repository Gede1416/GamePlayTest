using UnityEngine;

namespace FormationLab
{
    // 阵内跟随当前速度，阵外沿最短位移回到边界。
    internal sealed class MarchingFormation
    {
        const float FollowWeight = 3f;
        const float ReturnWeight = 7f;
        GroundBox bounds;

        internal GroundBox Bounds => bounds;
        internal Vector2 Velocity { get; private set; }

        internal MarchingFormation(GroundBox bounds)
        {
            this.bounds = bounds;
        }

        internal void Advance(Vector2 destination, float speed, float dt)
        {
            Vector2 previous = bounds.Center;
            bounds.Center = Vector2.MoveTowards(previous, destination, speed * dt);
            Velocity = (bounds.Center - previous) / dt;
            if (Velocity.sqrMagnitude > 0.0001f) bounds.Forward = Velocity.normalized;
        }

        internal Vector2 GetAcceleration(Vector2 position, Vector2 velocity, float radius, out float catchUpSpeed)
        {
            Vector2 home = bounds.ReturnOffset(position, radius);
            catchUpSpeed = Mathf.Min(home.magnitude, 2f);
            return (Velocity - velocity) * FollowWeight + home * ReturnWeight;
        }
    }
}
