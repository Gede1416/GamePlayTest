using System.Collections.Generic;

/// <summary>
/// 加攻技能：给自己挂一条 AddAttack buff（伤害加成落到伤害技能的阶段三上）。
/// 释放判断是**两条检查的"与"**：活着（CooldownCastCheck 顺带管）+ **一场战斗只放一次**（OnlyCastOnceCastCheck，
/// 次数从施法者身上的使用次数缓存里取）。
/// 目标就是自己，所以阶段二用 SelfTargetFinder。
/// 成员顺序：属性 → 公开方法。
/// </summary>
public class AttackBuffSkill : ISkill
{
    #region 属性

    /// <summary>技能名（自己的身份：释放判断拿它去施法者身上取使用次数）</summary>
    const SkillType Name = SkillType.AttackBuff;

    /// <summary>挂哪种 buff（数值与持续回合在 Buffs/Combination 里那份组合 buff 自己带）</summary>
    const BuffType Buff = BuffType.AddAttack;

    readonly List<ICastCheck> castChecks = new()
    {
        new CooldownCastCheck(Name),                // 活着（冷却 0：场次额度由下一条管）
        new OnlyCastOnceCastCheck(Name),            // 一场战斗只放一次
    };

    readonly List<ITargetFinder> targetFinders = new()
    {
        new SelfTargetFinder(),                     // 目标是自己
    };

    readonly List<ISkillCaster> skillCasters = new()
    {
        new BuffCaster(Buff),                       // 挂 buff
    };

    /// <summary>技能名</summary>
    public SkillType Type => Name;

    #endregion

    #region 公开方法

    /// <summary>阶段一：所有释放判断都过才放</summary>
    public bool CanCast(Entity caster)
    {
        foreach (var check in castChecks)
            if (!check.CanCast(caster)) return false;

        return true;
    }

    /// <summary>阶段二：目标获取，谁先找到算谁的（都没找到返回 null）</summary>
    public List<Entity> TryFindTargets(Entity caster, MapManager map)
    {
        foreach (var finder in targetFinders)
        {
            var found = finder.TryFindTargets(caster, map);
            if (found != null && found.Count > 0) return found;
        }

        return null;
    }

    /// <summary>阶段三：技能释放，每个零件都跑一遍（有一个失败这条技能就算没放成）</summary>
    public bool Cast(Entity caster, List<Entity> targets)
    {
        if (skillCasters.Count == 0) return false;      // 没有释放零件 = 什么都没做

        bool ok = true;
        foreach (var part in skillCasters)
            if (!part.Cast(caster, targets)) ok = false;

        return ok;
    }

    /// <summary>加 / 减伤害（buff 用）：这条技能没有伤害零件，空转</summary>
    public void AddDamage(float delta)
    {
        foreach (var part in skillCasters)
            if (part is DamageCaster damageCaster) damageCaster.AddDamage(delta);
    }

    #endregion
}
