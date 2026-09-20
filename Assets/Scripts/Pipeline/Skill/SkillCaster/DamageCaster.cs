using System.Collections.Generic;
using UnityEngine;

// ============ 阶段三：技能释放 ============

/// <summary>伤害类技能的释放：对每个目标扣 damage 血（伤害自己带）</summary>
public class DamageCaster : ISkillCaster
{
    /// <summary>每个目标扣多少血（buff 加攻就是改它）</summary>
    public float damage;

    public DamageCaster(float damage = 10f)
    {
        this.damage = damage;
    }

    /// <summary>加 / 减伤害（buff 用；delta 为负就是减回去）</summary>
    public void AddDamage(float delta) => damage += delta;

    /// <summary>对每个目标扣 damage 血；没有目标就返回 false</summary>
    public bool Cast(Entity caster, List<Entity> targets)
    {
        if (caster == null || targets == null || targets.Count == 0) return false;

        // 伤害只从 Entity 这个门面进去（Entity.TakeDamage 再转给 Health）
        foreach (var t in targets)
            if (t != null) t.TakeDamage(damage);

        Debug.Log($"[DamageCaster] {caster.name} 命中 {targets.Count} 个目标，每个 {damage} 伤害");
        return true;
    }
}
