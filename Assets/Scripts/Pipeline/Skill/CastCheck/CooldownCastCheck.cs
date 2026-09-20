
// ============ 阶段一：释放判断 ============

/// <summary>
/// 默认的释放判断：施法者活着 + 冷却好了。
/// 冷却**自己记账**——"放成没放成"这条数它拿不到（放成要在后两段之后才知道），所以靠消息：
/// 收到 <see cref="SkillCastEvent"/>（自己那条技能放成了）就进冷却，收到 <see cref="TurnChangedEvent"/>（进新回合）减一。
/// 于是**冷却 ≥ 1 顺带就是"每回合最多放一次"**。
/// 退订靠 EventPipeline.Clear()（BattleManager.ClearBattle 末尾兜一道），技能重建时旧实例直接扔掉。
/// </summary>
public class CooldownCastCheck : ICastCheck, ISkillPart
{
    #region 属性

    readonly int cooldown;

    ISkill owner;          // 靠它从消息里认出"放成的是不是我那条技能"
    int cooldownLeft;

    #endregion

    #region 构造

    public CooldownCastCheck(int cooldown = 0)
    {
        this.cooldown = cooldown;
    }

    #endregion

    #region 公开方法

    /// <summary>装配：记下自己属于哪条技能 + 订"技能放成了 / 进新回合了"两条消息</summary>
    public void Init(ISkill owner, MapManager map)
    {
        this.owner = owner;
        EventPipeline.Subscribe<SkillCastEvent>(OnSkillCast);
        EventPipeline.Subscribe<TurnChangedEvent>(OnTurnChanged);
    }

    /// <summary>能不能放：施法者活着 且 冷却已经好了</summary>
    public virtual bool CanCast(Entity caster)
    {
        if (caster == null || caster.Health == null || caster.Health.IsDead) return false;
        return cooldownLeft <= 0;
    }

    #endregion

    #region 私有方法

    /// <summary>自己那条技能放成了：进冷却</summary>
    void OnSkillCast(SkillCastEvent e)
    {
        if (e.skill == owner) cooldownLeft = cooldown;
    }

    /// <summary>进新回合：冷却减一</summary>
    void OnTurnChanged(TurnChangedEvent e)
    {
        if (cooldownLeft > 0) cooldownLeft--;
    }

    #endregion
}
