
/// <summary>
/// 一场战斗只放一次：放成了就再也不放（例如加攻 buff）。
/// **技能名缓存在这里**，每次判断拿它去施法者身上取使用次数（`caster.Skills.UsedCount(skillType)`，
/// 次数由阶段三的零件数、SkillManager 按技能名缓存在 skillUseCount 里），大于 0 就是放过了。
/// **一场 = 一份技能**：每场战斗 SkillManager 按名单把技能重建一遍，次数跟着归零。
/// </summary>
public class OnlyCastOnceCastCheck : ICastCheck
{
    #region 属性

    readonly SkillType skillType;    // 认自己那条技能：取使用次数时的 key

    #endregion

    #region 构造

    public OnlyCastOnceCastCheck(SkillType skillType)
    {
        this.skillType = skillType;
    }

    #endregion

    #region 公开方法

    /// <summary>能不能放：这条技能这场还没放成过</summary>
    public bool CanCast(Entity caster)
    {
        if (caster == null || caster.Skills == null) return false;
        return caster.Skills.UsedCount(skillType) <= 0;
    }

    #endregion
}
