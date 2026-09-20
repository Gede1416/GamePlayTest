
/// <summary>
/// 一场战斗只放一次：放成了就再也不放（例如加攻 buff）。
/// "这场放过没有"这条数也不能直接问（放成要在后两段之后才知道），同样靠 <see cref="SkillCastEvent"/> 自己记账。
/// **一场 = 一个实例**：每场战斗 SkillManager 按名单把技能重建一遍，这个标记跟着归零。
/// </summary>
public class OnlyCastOnceCastCheck : ICastCheck, ISkillPart
{
    #region 属性

    ISkill owner;          // 靠它从消息里认出"放成的是不是我那条技能"
    bool casted;

    #endregion

    #region 公开方法

    /// <summary>装配：记下自己属于哪条技能 + 订"技能放成了"这条消息</summary>
    public void Init(ISkill owner, MapManager map)
    {
        this.owner = owner;
        EventPipeline.Subscribe<SkillCastEvent>(OnSkillCast);
    }

    /// <summary>能不能放：这场还没放过</summary>
    public bool CanCast(Entity caster) => !casted;

    #endregion

    #region 私有方法

    /// <summary>自己那条技能放成了：这场不再放</summary>
    void OnSkillCast(SkillCastEvent e)
    {
        if (e.skill == owner) casted = true;
    }

    #endregion
}
