using System.Collections.Generic;

/// <summary>
/// 加攻技能：给自己挂一条 AddAttack buff（伤害加成落到伤害技能的阶段三上）。
/// 释放判断是**两条检查的"与"**：活着（CooldownCastCheck 顺带管）+ **一场战斗只放一次**（OnlyCastOnceCastCheck）。
/// 目标就是自己，所以阶段二用 SelfTargetFinder。
/// 成员顺序：属性 → 公开方法。
/// </summary>
public class AttackBuffSkill : ISkill
{
    #region 属性

    /// <summary>挂哪种 buff（数值与持续回合是 BuffFactory 的常量）</summary>
    const BuffType Buff = BuffType.AddAttack;

    readonly List<ICastCheck> castChecks = new()
    {
        new CooldownCastCheck(),              // 活着（冷却 0：场次额度由下一条管）
        new OnlyCastOnceCastCheck(),          // 一场战斗只放一次
    };

    readonly List<ITargetFinder> targetFinders = new()
    {
        new SelfTargetFinder(),               // 目标是自己
    };

    readonly List<ISkillCaster> skillCasters = new()
    {
        new BuffCaster(Buff),                 // 挂 buff
    };

    #endregion

    #region 公开方法

    /// <summary>装配：由 SkillManager 调——把"自己是谁"和地图发给需要依赖的零件</summary>
    public void Init(MapManager map)
    {
        foreach (var check in castChecks)
            if (check is ISkillPart checkPart) checkPart.Init(this, map);

        foreach (var finder in targetFinders)
            if (finder is ISkillPart finderPart) finderPart.Init(this, map);

        foreach (var caster in skillCasters)
            if (caster is ISkillPart casterPart) casterPart.Init(this, map);
    }

    /// <summary>阶段一：所有释放判断都过才放</summary>
    public bool CanCast(Entity caster)
    {
        foreach (var check in castChecks)
            if (!check.CanCast(caster)) return false;

        return true;
    }

    /// <summary>阶段二：目标获取，谁先找到算谁的</summary>
    public bool TryFindTargets(Entity caster, List<Entity> targets)
    {
        foreach (var finder in targetFinders)
            if (finder.TryFindTargets(caster, targets)) return true;

        return false;
    }

    /// <summary>阶段三：技能释放，每个零件都跑一遍（有一个失败这条技能就算没放成）</summary>
    public bool Cast(Entity caster, List<Entity> targets)
    {
        if (skillCasters.Count == 0) return false;      // 没有释放零件 = 什么都没做

        bool ok = true;
        foreach (var casterPart in skillCasters)
            if (!casterPart.Cast(caster, targets)) ok = false;

        return ok;
    }

    /// <summary>加 / 减伤害（buff 用）：这条技能没有伤害零件，空转</summary>
    public void AddDamage(float delta)
    {
        foreach (var caster in skillCasters)
            if (caster is DamageCaster damageCaster) damageCaster.AddDamage(delta);
    }

    #endregion
}
