
/// <summary>
/// 一场战斗只放一次：放成了就再也不放（例如加攻 buff）。
/// **技能名缓存在这里**，施法者 id 在第一次 <see cref="CanCast"/> 时记下；收到 <see cref="SkillCastEvent"/> 后
/// 两个都对上，就把自己标记成"这场放过了"。
/// **一场 = 一份技能**：每场战斗 SkillManager 按名单把技能重建一遍，这个标记跟着归零。
/// </summary>
public class OnlyCastOnceCastCheck : ICastCheck
{
    readonly SkillType skillType;    // 认自己那条技能：跟消息里的技能名对

    int casterId;                    // 认自己的施法者：跟消息里的 uuid 对（CanCast 时记下）
    bool casted;

    public OnlyCastOnceCastCheck(SkillType skillType)
    {
        this.skillType = skillType;

        EventPipeline.Subscribe<SkillCastEvent>(OnSkillCast);
    }

    /// <summary>能不能放：施法者在，且这条技能这场还没放成过（顺手记下自己属于哪个施法者）</summary>
    public bool CanCast(Entity caster)
    {
        if (caster == null) return false;

        casterId = caster.Uuid;
        return !casted;
    }

    /// <summary>自己那条技能放成了（uuid 与技能名都对上）：这场不再放</summary>
    void OnSkillCast(SkillCastEvent e)
    {
        if (e.uuid != casterId || e.skillType != skillType) return;

        casted = true;
    }
}
