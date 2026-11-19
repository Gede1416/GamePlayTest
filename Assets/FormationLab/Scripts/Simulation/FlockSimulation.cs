using UnityEngine;

namespace FormationLab
{
    internal sealed class FlockSimulation
    {
        readonly MarchingFormation formation;
        readonly FlockMap map;
        readonly FlockUnit[] units;
        readonly Vector2[] nextPositions;

        internal FlockSimulation(MarchingFormation formation, FlockMap map, FlockUnit[] units)
        {
            this.formation = formation;
            this.map = map;
            this.units = units;
            nextPositions = new Vector2[units.Length];
        }

        internal void Step(Vector2 destination, Vector2 enemy, float marchSpeed, float dt)
        {
            formation.Advance(destination, marchSpeed, dt);
            for (int i = 0; i < units.Length; i++)
            {
                FlockUnit unit = units[i];
                Vector2 follow = unit.Formation.GetAcceleration(unit.Position, unit.Velocity, FlockUnit.Radius, out float catchUpSpeed);
                Vector2 acceleration = follow + NeighborAcceleration(i) + unit.EnemyAcceleration(enemy);
                Vector2 candidate = unit.PredictPosition(acceleration, marchSpeed + 0.7f + catchUpSpeed, dt);
                nextPositions[i] = map.ResolveMove(unit.Position, candidate, FlockUnit.Radius);
            }
            // 所有单位都读完本步起点后，统一提交地图裁决的落点。
            for (int i = 0; i < units.Length; i++)
                units[i].ApplyPosition(nextPositions[i], dt);
        }

        Vector2 NeighborAcceleration(int index)
        {
            FlockUnit self = units[index];
            Vector2 separation = Vector2.zero;
            Vector2 alignment = Vector2.zero;
            Vector2 cohesion = Vector2.zero;
            int nearby = 0;
            for (int i = 0; i < units.Length; i++)
            {
                if (i == index) continue;
                Vector2 offset = units[i].Position - self.Position;
                float distance = offset.magnitude;
                if (distance > 1.6f) continue;
                if (distance < 0.85f)
                {
                    Vector2 away = distance > 0.0001f ? -offset / distance : (i < index ? Vector2.right : Vector2.left);
                    separation += away * (0.85f - distance) * 12;
                }
                alignment += units[i].Velocity - self.Velocity;
                cohesion += offset;
                nearby++;
            }
            return nearby == 0 ? Vector2.zero : separation + (alignment * 0.6f + cohesion * 0.3f) / nearby;
        }
    }
}
