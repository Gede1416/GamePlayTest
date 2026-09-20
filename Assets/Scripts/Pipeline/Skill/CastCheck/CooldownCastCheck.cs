
// ============ 阶段一：释放判断 ============

/// <summary>
/// 默认的释放判断：施法者活着 + 冷却好了。
/// **"放成没放成"由自己问出来**：阶段三的零件把使用次数缓存在自己身上，这里拿技能名去施法者身上取
/// （`caster.Skills.UsedCount(skillType)`），比上次看到的多就是刚放成，进冷却；
/// 进新回合（`TurnChangedEvent`）冷却减一，所以**冷却 ≥ 1 顺带就是"每回合最多放一次"**。
/// 订阅在构造函数里做（不用外面的 Init），退订靠 `BattleManager.ClearBattle()` 末尾的 `EventPipeline.Clear()`。
/// </summary>
public class CooldownCastCheck : ICastCheck
{
    #region 属性

    readonly SkillType skillType;    // 认自己那条技能：取使用次数时的 key
    readonly int cooldown;

    int cooldownLeft;
    int seenUsed;                    // 上次看到的使用次数

    #endregion

    #region 构造

    public CooldownCastCheck(SkillType skillType, int cooldown = 0)
    {
        this.skillType = skillType;
        this.cooldown = cooldown;

        EventPipeline.Subscribe<TurnChangedEvent>(OnTurnChanged);
    }

    #endregion

    #region 公开方法

    /// <summary>能不能放：施法者活着 且 冷却已经好了（顺手把"刚放成过"这笔账记上）</summary>
    public bool CanCast(Entity caster)
    {
        if (caster == null || caster.Health == null || caster.Health.IsDead) return false;

        NoteUsed(caster);
        return cooldownLeft <= 0;
    }

    #endregion

    #region 私有方法

    /// <summary>使用次数比上次多 = 刚放成，进冷却（次数缓存在阶段三的零件里，判断只能这么问）</summary>
    void NoteUsed(Entity caster)
    {
        int used = caster.Skills != null ? caster.Skills.UsedCount(skillType) : 0;
        if (used <= seenUsed) return;

        seenUsed = used;
        cooldownLeft = cooldown;
    }

    /// <summary>进新回合：冷却减一</summary>
    void OnTurnChanged(TurnChangedEvent e)
    {
        if (cooldownLeft > 0) cooldownLeft--;
    }

    #endregion
}
