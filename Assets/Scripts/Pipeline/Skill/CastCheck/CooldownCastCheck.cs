
// ============ 阶段一：释放判断 ============

/// <summary>
/// 默认的释放判断：施法者活着 + 冷却好了。
/// **"放成没放成"靠消息**：订 <see cref="SkillCastEvent"/>，收到后拿自己的施法者 id 与技能名跟消息里的对，
/// 都对上才进冷却；进新回合（<see cref="TurnChangedEvent"/>）冷却减一——
/// 所以**冷却 ≥ 1 顺带就是"每回合最多放一次"**。
/// 施法者 id 在第一次 <see cref="CanCast"/> 时记下（一条技能只属于一个实体）。
/// 订阅在构造函数里做（不用外面的 Init），退订靠 `BattleManager.ClearBattle()` 末尾的 `EventPipeline.Clear()` 兜。
/// </summary>
public class CooldownCastCheck : ICastCheck
{
    readonly SkillType skillType;    // 认自己那条技能：跟消息里的技能名对
    readonly int cooldown;

    int casterId;                    // 认自己的施法者：跟消息里的 uuid 对（CanCast 时记下）
    int cooldownLeft;

    public CooldownCastCheck(SkillType skillType, int cooldown = 0)
    {
        this.skillType = skillType;
        this.cooldown = cooldown;

        EventPipeline.Subscribe<SkillCastEvent>(OnSkillCast);
        EventPipeline.Subscribe<TurnChangedEvent>(OnTurnChanged);
    }

    /// <summary>能不能放：施法者活着 且 冷却已经好了（顺手记下自己属于哪个施法者）</summary>
    public bool CanCast(Entity caster)
    {
        if (caster == null || caster.Health == null || caster.Health.IsDead) return false;

        casterId = caster.Uuid;
        return cooldownLeft <= 0;
    }

    /// <summary>自己那条技能放成了（uuid 与技能名都对上）：进冷却</summary>
    void OnSkillCast(SkillCastEvent e)
    {
        if (e.uuid != casterId || e.skillType != skillType) return;

        cooldownLeft = cooldown;
    }

    /// <summary>进新回合：冷却减一</summary>
    void OnTurnChanged(TurnChangedEvent e)
    {
        if (cooldownLeft > 0) cooldownLeft--;
    }
}
