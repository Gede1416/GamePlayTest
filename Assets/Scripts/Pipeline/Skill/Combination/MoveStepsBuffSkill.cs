using System.Collections.Generic;

/// <summary>
/// 提升移动能力：给自己挂一条 AddMoveSteps buff（挂上 +2 步、到期或清场时减回去）。
/// 加几格、持续几回合在 Buffs/Combination 里那份组合 buff 自己带，这里只管"什么时候能放"。
/// 释放判断只有一条：活着 + 冷却 5 回合（冷却 ≥ 1 顺带就是"每回合最多放一次"）。
/// 目标就是自己，所以阶段二用 SelfTargetFinder。
/// </summary>
public class MoveStepsBuffSkill : ISkill
{
    /// <summary>技能名（自己的身份：释放判断拿它去对消息里的技能名）</summary>
    const SkillType Name = SkillType.MoveStepsBuff;

    /// <summary>冷却回合数</summary>
    const int Cooldown = 1;

    readonly List<ICastCheck> castChecks = new()
    {
        new CooldownCastCheck(Name, Cooldown),      // 活着 + 冷却好了
    };

    readonly List<ITargetFinder> targetFinders = new()
    {
        new SelfTargetFinder(),                     // 目标是自己
    };

    readonly List<ISkillCaster> skillCasters = new()
    {
        new MoveStepsBuffCaster(),                  // 挂哪种 buff 由技能这边挑（内容在那个 caster 里）
    };

    /// <summary>技能名</summary>
    public SkillType Type => Name;

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
}
