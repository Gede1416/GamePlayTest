using System.Collections.Generic;

/// <summary>
/// 治疗技能：给自己挂一条 Heal buff（回血是 buff 每回合结算的，技能本身不直接回血）。
/// 释放判断是**两条检查的"与"**：冷却 1（顺带管活着，也顺带就是"每回合最多放一次"，别一回合叠好几条）+ **血量没满**。
/// 目标就是自己，所以阶段二用 SelfTargetFinder。
/// 成员顺序：属性 → 公开方法。
/// </summary>
public class HealBuffSkill : ISkill
{
    #region 属性

    /// <summary>技能名（自己的身份：释放判断拿它去施法者身上取使用次数）</summary>
    const SkillType Name = SkillType.HealBuff;

    /// <summary>挂哪种 buff（数值与持续回合是 BuffFactory 的常量）</summary>
    const BuffType Buff = BuffType.Heal;

    /// <summary>冷却回合数（≥ 1 顺带就是"每回合最多放一次"）</summary>
    const int Cooldown = 1;

    readonly List<ICastCheck> castChecks = new()
    {
        new CooldownCastCheck(Name, Cooldown),      // 活着 + 冷却好了
        new WoundedCastCheck(),                     // 自己血量没满（两条判断是"与"）
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

    /// <summary>这条技能放成过几次（各释放零件的使用次数之和，SkillManager 拿它刷使用次数缓存）</summary>
    public int UsedCount
    {
        get
        {
            int total = 0;
            foreach (var caster in skillCasters) total += caster.UsedCount;
            return total;
        }
    }

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
