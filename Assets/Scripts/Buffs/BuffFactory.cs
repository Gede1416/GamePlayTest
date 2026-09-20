using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Buff 类型（枚举跟着工厂走，和 AttackType / TargetSourceType 一个套路）。
/// </summary>
public enum BuffType
{
    Heal,           // 治疗：每次结算回 value 点血（不产生常驻加成）
    AddMoveSteps,   // 增加移动步数：挂上 +value，移除时减回去
    AddAttack,      // 增加攻击力：挂上 +value，移除时减回去
}

/// <summary>
/// Buff 静态工厂：按类型造一条 Buff（配置 + 效果 + 目标）。
/// 数值与持续回合作为常量写在这里（和 AttackPipeline.cs 里"近战 1 格 / 远程 3 格"是常量一个套路）；
/// 要能在 Inspector 里调数值，再把下面这组常量挪进一份 BuffCfg 资产即可，别的地方不用动。
/// </summary>
public static class BuffFactory
{
    // ---------- 每种 buff 的数值与持续回合（duration <= 0 = 永久） ----------
    const float HealPerTurn = 10f;      // 每回合回多少血
    const int HealTurns = 3;            // 回几回合
    const float MoveStepsBonus = 2f;    // 多加几格
    const int MoveStepsTurns = 3;
    const float AttackBonus = 5f;       // 多加多少伤害
    const int AttackTurns = 3;

    /// <summary>按类型造一条 Buff（还没挂上：调用方拿到后 Add + 入队；类型没接实现就返回 null）</summary>
    public static Buff Create(BuffType type, List<Entity> targets)
    {
        switch (type)
        {
            case BuffType.Heal:
                return new Buff(new BuffCfg(type, HealPerTurn, HealTurns), new HealEffect(), targets);
            case BuffType.AddMoveSteps:
                return new Buff(new BuffCfg(type, MoveStepsBonus, MoveStepsTurns), new MoveStepsEffect(), targets);
            case BuffType.AddAttack:
                return new Buff(new BuffCfg(type, AttackBonus, AttackTurns), new AttackEffect(), targets);
        }

        Debug.LogWarning($"BuffFactory：{type} 没接实现");
        return null;
    }
}
