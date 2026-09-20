using System.Collections.Generic;
using UnityEngine;

/// <summary>Buff 类技能的释放：给每个目标挂一条 buff（挂哪种由构造函数带）</summary>
public class BuffCaster : ISkillCaster
{
    readonly BuffType buff;

    public BuffCaster(BuffType buff)
    {
        this.buff = buff;
    }

    /// <summary>对每个目标挂 buff（走 Entity.AddBuff 门面，目标自己的 BuffManager 收下来）</summary>
    public bool Cast(Entity caster, List<Entity> targets)
    {
        if (caster == null || targets == null || targets.Count == 0) return false;

        foreach (var t in targets)
            if (t != null) t.AddBuff(buff);

        Debug.Log($"[BuffCaster] {caster.name} 放下 {buff}");
        return true;
    }
}
