using UnityEngine;

namespace FormationLab
{
    internal sealed class FlockSimulation
    {
        readonly MarchingFormation[] formations;
        readonly FlockMap map;
        readonly FlockUnit[] units;
        readonly Vector2[] nextPositions;

        internal FlockSimulation(MarchingFormation[] formations, FlockMap map, FlockUnit[] units)
        {
            this.formations = formations;
            this.map = map;
            this.units = units;
            nextPositions = new Vector2[units.Length];
        }

        internal void Step(float marchSpeed, float dt, float followWeight, float neighborsWeight, float enemyWeight)
        {
            foreach (MarchingFormation formation in formations) formation.Advance(marchSpeed, dt);
            for (int i = 0; i < units.Length; i++)
            {
                FlockUnit unit = units[i];
                Vector2 follow = unit.Formation.GetAcceleration(unit, out float catchUpSpeed);
                GetUnitInfluences(i, out Vector2 neighbors, out Vector2 enemy);
                Vector2 acceleration = follow * followWeight + neighbors * neighborsWeight + enemy * enemyWeight;
                Vector2 candidate = unit.PredictPosition(acceleration, marchSpeed + 0.7f + catchUpSpeed, dt);
                nextPositions[i] = map.ResolveMove(unit.Position, candidate, FlockUnit.Radius);
            }
            // 所有单位都读完本步起点后，统一提交地图裁决的落点。
            for (int i = 0; i < units.Length; i++)
                units[i].ApplyPosition(nextPositions[i], dt);
        }

        void GetUnitInfluences(int index, out Vector2 neighbors, out Vector2 enemy)
        {
            FlockUnit self = units[index];
            Vector2 separation = Vector2.zero;
            Vector2 alignment = Vector2.zero;
            Vector2 cohesion = Vector2.zero;
            int nearby = 0;
            enemy = Vector2.zero;
            for (int i = 0; i < units.Length; i++)
            {
                if (i == index) continue;
                if (units[i].Formation != self.Formation)
                {
                    enemy += self.EnemyAcceleration(units[i].Position);
                    continue;
                }
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
            neighbors = nearby == 0 ? Vector2.zero : separation + (alignment * 0.6f + cohesion * 0.3f) / nearby;
        }
    }
}
