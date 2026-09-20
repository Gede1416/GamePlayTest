using System.Collections.Generic;

/// <summary>
/// 一条技能的**完整定义**：技能名 + 整条流水线（阶段一 判断 / 阶段二 目标 / 阶段三 释放）+ 额度配置。
/// 由 SkillFactory 按技能名造（一个技能一份新实例），Skill 只引用它、把"能不能放"和"放一次"都交给它，
/// 自己只记额度（本回合放过没 / 本场放过没）。
/// 这样 Inspector 上就不用再一个技能配一堆字段：技能长什么样只由技能名决定。
/// 成员顺序：属性 → 构造 → 公开方法。
/// </summary>
public class SkillDefinition
{
    #region 属性

    /// <summary>技能名（决定这条技能长什么样）</summary>
    public SkillType type;

    /// <summary>一场战斗只能放一次（例如加攻 buff）</summary>
    public readonly bool oncePerBattle;

    /// <summary>阶段一：释放判断（活着 / 冷却 / 血量这些条件都在实现里）</summary>
    public ICastCheck CastCheck { get; private set; }

    /// <summary>阶段二：目标获取</summary>
    public ITargetFinder TargetFinder { get; private set; }

    /// <summary>阶段三：技能释放</summary>
    public ISkillCaster Caster { get; private set; }

    /// <summary>现在这条技能的伤害（伤害类技能才有，读阶段三那个值；给日志 / 界面看）</summary>
    public float Damage => Caster is DamageCaster damageCaster ? damageCaster.damage : 0f;

    #endregion

    #region 构造

    public SkillDefinition(SkillType type, ICastCheck castCheck, ITargetFinder targetFinder, ISkillCaster caster, bool oncePerBattle = false)
    {
        this.type = type;
        this.oncePerBattle = oncePerBattle;
        CastCheck = castCheck;
        TargetFinder = targetFinder;
        Caster = caster;
    }

    #endregion

    #region 公开方法

    /// <summary>跑完整条流水线：判断 → 目标 → 释放，放成了才进冷却；任一段没过就返回 false</summary>
    public bool Run(Entity caster, List<Entity> targets)
    {
        if (CastCheck == null || TargetFinder == null || Caster == null) return false;

        if (!CastCheck.CanCast(caster)) return false;                       // 阶段一
        if (!TargetFinder.TryFindTargets(caster, targets)) return false;    // 阶段二
        if (!Caster.Cast(caster, targets)) return false;                    // 阶段三

        (CastCheck as ICooldown)?.StartCooldown();
        return true;
    }

    /// <summary>回合推进（自己的回合开始调）：推这条技能自己的冷却</summary>
    public void TickTurn() => (CastCheck as ICooldown)?.TickTurn();

    /// <summary>加 / 减伤害（buff 用）：只有伤害类技能吃这个加成，buff 技能没有伤害就跳过</summary>
    public void AddDamage(float delta)
    {
        if (Caster is DamageCaster damageCaster) damageCaster.AddDamage(delta);
    }

    #endregion
}
