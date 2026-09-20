using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 提升移动能力技能的释放：给每个目标挂一条加步数的 buff——**挂的是什么 buff 全在这儿**
/// （挂哪个零件 + 数值 + 持续回合；原来 Buffs/Combination/AddMoveStepsBuff 那份职责搬到这里）。
/// 每次挂都**新建一条**：buff 带自己的结算次数，几个目标不能共用一条。
/// </summary>
public class MoveStepsBuffCaster : ISkillCaster
{
    /// <summary>多加几格</summary>
    const int MoveStepsBonus = 2;

    /// <summary>持续几回合（&lt;= 0 = 永久）</summary>
    const int Duration = 3;

    /// <summary>对每个目标各挂一条（走 Entity.AddBuff 门面，目标自己的 BuffManager 收下来）</summary>
    public bool Cast(Entity caster, List<Entity> targets)
    {
        if (caster == null || targets == null || targets.Count == 0) return false;

        foreach (var t in targets)
            if (t != null) t.AddBuff(new MoveStepsEffect(MoveStepsBonus, Duration));

        Debug.Log($"[MoveStepsBuffCaster] {caster.name} 放下加移动步数 buff（{Duration} 回合）");
        return true;
    }
}
