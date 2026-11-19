using UnityEngine;

namespace FormationLab
{
    // 阵内跟随当前速度，阵外沿最短位移回到边界。
    internal sealed class MarchingFormation
    {
        const float FollowWeight = 3f;
        const float ReturnWeight = 7f;
        const float InnerScale = 0.6f;
        GroundBox bounds;

        internal Vector2 Destination { get; set; }
        internal Vector2 Velocity { get; private set; }

        internal MarchingFormation(GroundBox bounds)
        {
            this.bounds = bounds;
            Destination = bounds.Center;
        }

        internal GroundBox GetBounds(UnitType type)
        {
            GroundBox region = bounds;
            if (type == UnitType.Ranged) region.HalfSize *= InnerScale;
            return region;
        }

        internal void Advance(float speed, float dt)
        {
            Vector2 previous = bounds.Center;
            bounds.Center = Vector2.MoveTowards(previous, Destination, speed * dt);
            Velocity = (bounds.Center - previous) / dt;
            if (Velocity.sqrMagnitude > 0.0001f) bounds.Forward = Velocity.normalized;
        }

        internal Vector2 GetAcceleration(FlockUnit unit, out float catchUpSpeed)
        {
            Vector2 home = GetBounds(unit.Type).ReturnOffset(unit.Position, FlockUnit.Radius);
            catchUpSpeed = Mathf.Min(home.magnitude, 2f);
            return (Velocity - unit.Velocity) * FollowWeight + home * ReturnWeight;
        }
    }
}
