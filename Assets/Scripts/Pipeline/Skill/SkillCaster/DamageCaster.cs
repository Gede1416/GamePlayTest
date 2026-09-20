using System.Collections.Generic;
using UnityEngine;

// ============ 阶段三：技能释放 ============

/// <summary>伤害类技能的释放：对每个目标扣 damage 血（伤害自己带）</summary>
public class DamageCaster : ISkillCaster
{
    #region 属性

    /// <summary>每个目标扣多少血（buff 加攻就是改它）</summary>
    public float damage;

    /// <summary>使用次数缓存：这个零件放成过几次（放成一次 +1）</summary>
    public int UsedCount { get; private set; }

    #endregion

    #region 构造

    public DamageCaster(float damage = 10f)
    {
        this.damage = damage;
    }

    #endregion

    #region 公开方法

    /// <summary>加 / 减伤害（buff 用；delta 为负就是减回去）</summary>
    public void AddDamage(float delta) => damage += delta;

    /// <summary>对每个目标扣 damage 血；没有目标就返回 false（也就没记这次使用）</summary>
    public bool Cast(Entity caster, List<Entity> targets)
    {
        if (caster == null || targets == null || targets.Count == 0) return false;

        // 伤害只从 Entity 这个门面进去（Entity.TakeDamage 再转给 Health）
        foreach (var t in targets)
            if (t != null) t.TakeDamage(damage);

        UsedCount++;
        Debug.Log($"[DamageCaster] {caster.name} 命中 {targets.Count} 个目标，每个 {damage} 伤害");
        return true;
    }

    #endregion
}
