using System.Collections.Generic;

/// <summary>
/// 一条技能（运行期）：**只引用一份 SkillDefinition**（完整流水线）并记两条额度，效果全由定义去做。
/// 额度① **每个技能每回合最多放一次**——放成了才记（没目标的空放不算），每个自己的回合开始时清掉；
/// 额度② 定义里 `oncePerBattle` 的**一场战斗只放一次**——一直记到清场 / 下一场 Init。
/// 成员顺序：属性 → 构造 → 公开方法。
/// </summary>
public class Skill
{
    #region 属性

    readonly SkillDefinition definition;
    bool usedThisTurn;          // 本回合放成过没有
    bool usedThisBattle;        // 本场放成过没有（只有 oncePerBattle 的技能看它）

    /// <summary>引用着的那份完整定义（流水线 + 数值 + 额度配置）</summary>
    public SkillDefinition Definition => definition;

    /// <summary>技能名（转发自定义）</summary>
    public SkillType Type => definition.type;

    /// <summary>这场战斗里放过没有（界面 / 调试看）</summary>
    public bool UsedThisBattle => usedThisBattle;

    /// <summary>现在能不能放：本回合没放过，且（要每场一次时）本场没放过</summary>
    public bool CanUse => !usedThisTurn && !(definition.oncePerBattle && usedThisBattle);

    #endregion

    #region 构造

    public Skill(SkillDefinition definition)
    {
        this.definition = definition;
    }

    #endregion

    #region 公开方法

    /// <summary>
    /// 放一次：把整条流水线交给定义去跑（判断 → 找目标 → 释放），**放成了才记额度**——
    /// 空放（不在范围内 / 冷却没好 / 血量满了）不占"每回合一次"的名额，移动之后还能再试。
    /// </summary>
    public bool TryCast(Entity caster, List<Entity> targets)
    {
        if (!CanUse) return false;
        if (!definition.Run(caster, targets)) return false;

        usedThisTurn = true;
        usedThisBattle = true;
        return true;
    }

    /// <summary>回合推进（自己的回合开始调）：清掉"本回合放过" + 推这条技能的冷却</summary>
    public void TickTurn()
    {
        usedThisTurn = false;
        definition.TickTurn();
    }

    /// <summary>加 / 减伤害（buff 用，交给定义转发给阶段三）</summary>
    public void AddDamage(float delta) => definition.AddDamage(delta);

    #endregion
}
