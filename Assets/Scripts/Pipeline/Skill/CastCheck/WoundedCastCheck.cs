
/// <summary>
/// 只加一条血量判断：**自己的血量没满**（满血时治疗没意义，不放）。
/// 不管活着 / 冷却这些——要哪些条件凑一起，由组合技能在 `castChecks` 列表里与起来（例如 HealBuffSkill = 冷却 + 血量）。
/// 血量读的是 Entity 门面（Hp / MaxHp），不直接碰 Health 的数据。
/// </summary>
public class WoundedCastCheck : ICastCheck
{
    /// <summary>能不能放：自己的血量小于上限（没挂 Health 时 HP / MaxHP 都是 0，判断为不放）</summary>
    public bool CanCast(Entity caster)
    {
        if (caster == null) return false;
        return caster.Hp < caster.MaxHp;
    }
}
