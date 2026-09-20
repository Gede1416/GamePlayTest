using System.Collections.Generic;

/// <summary>
/// 近战技能：**把三段实现拼成一条技能**（阶段一 释放判断 / 阶段二 目标获取 / 阶段三 技能释放）。
/// 三段各自是一组零件：判断之间是"与"（都过才放）、目标获取之间是"或"（谁先找到算谁）、释放之间是"与"（都成功才算放成）。
/// 数值（范围 / 伤害 / 冷却）是这里的常量，零件自己带；额度由零件里的释放判断自己记账。
/// 成员顺序：属性 → 公开方法。
/// </summary>
public class MeleeSkill : ISkill
{
    #region 属性

    /// <summary>每个目标扣多少血</summary>
    const float Damage = 20f;

    /// <summary>冷却回合数（≥ 1 顺带就是"每回合最多放一次"）</summary>
    const int Cooldown = 1;

    readonly List<ICastCheck> castChecks = new()
    {
        new CooldownCastCheck(Cooldown),      // 活着 + 冷却好了
    };

    readonly List<ITargetFinder> targetFinders = new()
    {
        new MeleeTargetFinder(),              // 范围 1 格、1 个目标
    };

    readonly List<ISkillCaster> skillCasters = new()
    {
        new DamageCaster(Damage),             // 扣血
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

    /// <summary>加 / 减伤害（buff 用）：只有伤害类零件认这个加成</summary>
    public void AddDamage(float delta)
    {
        foreach (var caster in skillCasters)
            if (caster is DamageCaster damageCaster) damageCaster.AddDamage(delta);
    }

    #endregion
}
