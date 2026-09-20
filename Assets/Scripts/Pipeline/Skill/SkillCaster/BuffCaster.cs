using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Buff 类技能的释放：给每个目标挂一条 buff——**挂哪种由技能那边配**：泛型参数就是那份组合 buff
/// （`BuffCaster&lt;HealBuff&gt;` / `BuffCaster&lt;AddMoveStepsBuff&gt;` …），
/// 回多少 / 加几格 / 持续几回合都在那份 buff 自己身上。
/// 每次挂都**新建一条**：buff 带自己的结算次数，几个目标不能共用一条。
/// </summary>
public class BuffCaster<T> : ISkillCaster where T : IBuff, new()
{
    /// <summary>对每个目标各挂一条（走 Entity.AddBuff 门面，目标自己的 BuffManager 收下来）</summary>
    public bool Cast(Entity caster, List<Entity> targets)
    {
        if (caster == null || targets == null || targets.Count == 0) return false;

        foreach (var t in targets)
            if (t != null) t.AddBuff(new T());

        Debug.Log($"[BuffCaster] {caster.name} 放下 {typeof(T).Name}");
        return true;
    }
}
