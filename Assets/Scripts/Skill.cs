using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 技能（普通类）：技能释放前置条件 + 目标获取参数（攻击范围 / 目标数量）+ 对目标附加效果。
/// 想加新技能就继承它重写 CanCast / Cast。
/// </summary>
[System.Serializable]
public class Skill
{
    [Tooltip("技能名（日志用）")]
    public string skillName = "普通攻击";

    [Header("目标获取")]
    [Tooltip("攻击范围（格，曼哈顿距离）")]
    public int range = 1;

    [Tooltip("最多命中几个目标")]
    public int targetCount = 1;

    [Header("前置条件")]
    [Tooltip("冷却回合数；0 = 无冷却")]
    public int cooldown;

    [Header("效果")]
    [Tooltip("默认 Cast 对每个目标造成的伤害")]
    public float damage = 10f;

    /// <summary>剩余冷却回合（Attacker.TickTurn 每回合减一）</summary>
    public int CooldownLeft { get; private set; }

    /// <summary>技能释放前置条件：现在能不能放。子类重写它加条件（蓝量、姿态、连击点…）</summary>
    public virtual bool CanCast(Entity caster)
    {
        if (caster == null || caster.Health == null || caster.Health.IsDead) return false;
        return CooldownLeft <= 0;
    }

    /// <summary>释放成功后进冷却</summary>
    public void StartCooldown() => CooldownLeft = cooldown;

    /// <summary>回合推进：冷却减一（每回合调一次）</summary>
    public void TickTurn()
    {
        if (CooldownLeft > 0) CooldownLeft--;
    }

    /// <summary>对目标附加效果。默认每个目标扣 damage 血；子类重写成别的效果（击退、上 buff…）</summary>
    public virtual void Cast(Entity caster, List<Entity> targets)
    {
        if (targets == null) return;

        foreach (var t in targets)
        {
            if (t == null || t.Health == null) continue;
            t.Health.TakeDamage(damage);
        }
    }
}
