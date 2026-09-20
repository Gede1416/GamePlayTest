using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 增加攻击力 buff：**一条 buff = 一组效果零件拼起来**（这个类就是那个组合）。
/// 挂上时伤害 +5、移除时 -5（加到各伤害类技能的阶段三上），3 个回合后到期。
/// 数值与持续回合是这里的常量（零件自己带数值，构造函数注入），目标由外面传进来（挂给谁）。
/// 成员顺序：属性 → 构造 → 公开方法。
/// </summary>
public class AddAttackBuff : IBuff
{
    /// <summary>多加多少伤害</summary>
    const float AttackBonus = 5f;

    /// <summary>持续几回合（&lt;= 0 = 永久）</summary>
    const int Duration = 3;

    readonly List<IBuffEffect> effects = new()
    {
        new AddAttackEffect(AttackBonus),
    };

    readonly List<Entity> targets;      // 挂给谁
    int elapsed;                        // 已经结算过几次

    /// <summary>类型（界面显示用）</summary>
    public BuffType Type => BuffType.AddAttack;

    /// <summary>是否该结束了（永久 buff 永远不结束）</summary>
    public bool IsOver => Duration > 0 && elapsed >= Duration;

    /// <summary>还剩几次结算（永久返回 -1）</summary>
    public int Left => Duration > 0 ? Mathf.Max(0, Duration - elapsed) : -1;

    public AddAttackBuff(List<Entity> targets)
    {
        this.targets = targets;
    }

    /// <summary>挂上：对每个目标跑一遍各零件的 Apply（伤害加成当场生效）</summary>
    public void Add()
    {
        foreach (var target in targets)
        {
            if (target == null) continue;
            foreach (var effect in effects) effect.Apply(target);
        }
    }

    /// <summary>结算一次：对每个目标跑一遍各零件的 Tick，然后记一次已结算</summary>
    public void Trigger()
    {
        foreach (var target in targets)
        {
            if (target == null) continue;
            foreach (var effect in effects) effect.Tick(target);
        }

        elapsed++;
    }

    /// <summary>移除：对每个目标跑一遍各零件的 Revert（伤害加成减回去）</summary>
    public void Remove()
    {
        foreach (var target in targets)
        {
            if (target == null) continue;
            foreach (var effect in effects) effect.Revert(target);
        }
    }
}
