
/// <summary>
/// 加一条血量判断的释放判断：在"活着 + 冷却好了"之上，再要求**自己的血量没满**（满血时治疗没意义，不放）。
/// 给治疗这类技能用；血量读的是 Entity 门面（Hp / MaxHp），不直接碰 Health 的数据。
/// </summary>
public class WoundedCastCheck : CooldownCastCheck
{
    #region 构造

    public WoundedCastCheck(int cooldown = 0) : base(cooldown)
    {
    }

    #endregion

    #region 公开方法

    /// <summary>能不能放：活着 + 冷却好了 + 血量小于上限</summary>
    public override bool CanCast(Entity caster)
    {
        if (!base.CanCast(caster)) return false;
        return caster.Hp < caster.MaxHp;
    }

    #endregion
}
