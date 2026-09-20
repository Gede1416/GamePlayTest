using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 治疗技能的释放：给每个目标挂一条治疗 buff——**挂的是什么 buff 全在这儿**
/// （效果零件 + 名字 + 持续回合；原来 Buffs/Combination/HealBuff 那份职责搬到这里）。
/// 每次挂都**新建一条**：buff 带自己的结算次数，几个目标不能共用一条。
/// </summary>
public class HealBuffCaster : ISkillCaster
{
    /// <summary>每次结算回多少血</summary>
    const float HealPerTurn = 20f;

    /// <summary>回几回合（&lt;= 0 = 永久）</summary>
    const int Duration = 3;

    /// <summary>对每个目标各挂一条（走 Entity.AddBuff 门面，目标自己的 BuffManager 收下来）</summary>
    public bool Cast(Entity caster, List<Entity> targets)
    {
        if (caster == null || targets == null || targets.Count == 0) return false;

        foreach (var t in targets)
            if (t != null) t.AddBuff(new Buff(BuffType.Heal, Duration, new HealEffect(HealPerTurn)));

        Debug.Log($"[HealBuffCaster] {caster.name} 放下 {BuffType.Heal}");
        return true;
    }
}
