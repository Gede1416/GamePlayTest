using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 一条技能的配置：**在预制体上配**（数值是模板，运行期状态存在 Skill 里，清场重建不会把加成带过去）。
/// 只有伤害类技能看 damage / cooldown，只有 Buff 类技能看 buff。
/// </summary>
[System.Serializable]
public class SkillCfg
{
    [Tooltip("技能类型：近战 / 远程 / 施放 buff")]
    public SkillType type = SkillType.Melee;

    [Tooltip("伤害（伤害类技能用）")]
    public float damage = 10f;

    [Tooltip("冷却回合数（给阶段一）；0 = 无冷却")]
    public int cooldown;

    [Tooltip("挂哪种 buff（Buff 类技能用）")]
    public BuffType buff = BuffType.Heal;

    [Tooltip("一场战斗只能放一次（buff 技能勾上）")]
    public bool oncePerBattle;
}

/// <summary>
/// 一条技能（运行期）：三段 + 使用额度。
/// 额度两条，都在这里把关：
/// ① **每个技能每回合最多放一次**——放成了才记（没目标的空放不算），每个自己的回合开始时清掉；
///    所以"攻击→移动→攻击"里第二次还能补放"开局不在范围内、走完才够得着"的那次；
/// ② `cfg.oncePerBattle` 的**一场战斗只放一次**——一直记到清场 / 下一场 Init。
/// 三段由 SkillPipelineFactory 按 cfg.type 造好塞进来，这里只管额度与调用顺序。
/// </summary>
public class Skill
{
    #region 属性

    readonly SkillCfg cfg;
    bool usedThisTurn;          // 本回合放成过没有
    bool usedThisBattle;        // 本场放成过没有（只有 oncePerBattle 的技能看它）

    /// <summary>这条技能的配置（模板，不要在运行期改它）</summary>
    public SkillCfg Cfg => cfg;

    /// <summary>阶段一：释放判断</summary>
    public ICastCheck CastCheck { get; set; }

    /// <summary>阶段二：目标获取</summary>
    public ITargetFinder TargetFinder { get; set; }

    /// <summary>阶段三：技能释放</summary>
    public ISkillCaster Caster { get; set; }

    /// <summary>这场战斗里放过没有（界面 / 调试看）</summary>
    public bool UsedThisBattle => usedThisBattle;

    /// <summary>现在能不能放：本回合没放过，且（要每场一次时）本场没放过</summary>
    public bool CanUse => !usedThisTurn && !(cfg.oncePerBattle && usedThisBattle);

    #endregion

    #region 构造

    public Skill(SkillCfg cfg)
    {
        this.cfg = cfg;
    }

    #endregion

    #region 公开方法

    /// <summary>
    /// 放一次：三段依次跑（判断 → 找目标 → 释放），**放成了才记额度**——
    /// 空放（不在范围内 / 冷却没好）不占"每回合一次"的名额，移动之后还能再试。
    /// </summary>
    public bool TryCast(Entity caster, List<Entity> targets)
    {
        if (!CanUse) return false;
        if (CastCheck == null || TargetFinder == null || Caster == null) return false;

        if (!CastCheck.CanCast(caster)) return false;                       // 阶段一
        if (!TargetFinder.TryFindTargets(caster, targets)) return false;    // 阶段二
        if (!Caster.Cast(caster, targets)) return false;                    // 阶段三

        usedThisTurn = true;
        usedThisBattle = true;
        (CastCheck as ICooldown)?.StartCooldown();                          // 放成了才进冷却
        return true;
    }

    /// <summary>回合推进（自己的回合开始调）：清掉"本回合放过" + 推自己的冷却</summary>
    public void TickTurn()
    {
        usedThisTurn = false;
        (CastCheck as ICooldown)?.TickTurn();
    }

    /// <summary>加 / 减伤害（buff 用；只有伤害类技能吃这个加成，buff 技能没有伤害就跳过）</summary>
    public void AddDamage(float delta)
    {
        if (Caster is DamageCaster damageCaster) damageCaster.AddDamage(delta);
    }

    #endregion
}
