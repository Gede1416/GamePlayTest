using System.Collections.Generic;
using UnityEngine;

namespace FormationLab
{
    internal enum UnitType { Melee, Ranged }

    internal sealed class FlockUnit
    {
        internal const float Radius = 0.24f;
        const float MaxAcceleration = 12f;

        internal MarchingFormation Formation { get; }
        internal UnitType Type { get; }
        internal Vector2 Position { get; private set; }
        internal Vector2 Velocity { get; private set; }

        internal FlockUnit(MarchingFormation formation, UnitType type, Vector2 position)
        {
            Formation = formation;
            Type = type;
            Position = position;
        }

        internal Vector2 EnemyAcceleration(Vector2 target)
        {
            Vector2 offset = target - Position;
            float distance = offset.magnitude;
            if (distance > 4.5f) return Vector2.zero;
            float min = Type == UnitType.Ranged ? 2.4f : 0.7f;
            float max = Type == UnitType.Ranged ? 3.2f : 1f;
            float error = distance - Mathf.Clamp(distance, min, max);
            Vector2 toward = distance > 0.0001f ? offset / distance : Vector2.right;
            return toward * Mathf.Clamp(error * 4, -5, 5);
        }

        // 计算单个对象带来的 影响
        public Vector2 UnitAcceleration(FlockUnitData unitData)
        {
            //根据 FlockUnitData 中的信息 分辨状态获取数据 进行计算
            return Vector2.zero;
        }

        internal Vector2 PredictPosition(Vector2 acceleration, float speedLimit, float dt)
        {
            Vector2 velocity = Vector2.ClampMagnitude(Velocity + Vector2.ClampMagnitude(acceleration, MaxAcceleration) * dt, speedLimit);
            return Position + velocity * dt;
        }

        internal void ApplyPosition(Vector2 position, float dt)
        {
            Velocity = (position - Position) / dt;
            Position = position;
        }
    }

    struct NeighborWeight
    {
        float f_n_w; // 友方接近权重
        float f_f_w; // 友方远离权重
        float e_n_w; // 敌方接近权重
        float e_f_w; // 敌方远离权重
    }

    struct Neighbor接近
    {
        float f; // 友方
        float e; // 敌方
    }

    struct Neighbor远离
    {
        float f; // 友方
        float e; // 敌方
    }


    public class FlockUnitData
    {
        private UnitType unitType;
        private int teamId;
        private Dictionary<UnitType, NeighborWeight> w = new(); //根据类型获取对应权重
        private Dictionary<UnitType, Neighbor接近> n = new(); //根据类型获取对应权重
        private Dictionary<UnitType, Neighbor远离> f = new(); //根据类型获取对应权重
    }
}
