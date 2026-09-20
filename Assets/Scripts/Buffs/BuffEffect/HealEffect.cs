using UnityEngine;

/// <summary>
/// 治疗 buff：自己没有常驻加成，挂上与每次结算都按 amount 回一次血。
/// 回多少、持续几回合都由技能的阶段三给（HealBuffCaster 的常量），它自己只管记账；
/// 挂给谁每次都当参数传进来（零件不记目标）。
/// </summary>
public class HealEffect : IBuff
{
    readonly float amount;

    readonly int duration;      // 结算几次（<= 0 = 永久）

    int elapsed;                // 已经结算过几次

    /// <summary>是否该结束了（永久 buff 永远不结束）</summary>
    public bool IsOver => duration > 0 && elapsed >= duration;

    /// <summary>每次回多少血 + 结算几次</summary>
    public HealEffect(float amount, int duration)
    {
        this.amount = amount;
        this.duration = duration;
    }

    /// <summary>挂上：也回一次（和原来一样，挂上当场就见效一次）</summary>
    public void Apply(Entity entity) => Heal(entity);

    /// <summary>结算一次回血</summary>
    public void Tick(Entity entity)
    {
        Heal(entity);
        elapsed++;
    }

    /// <summary>移除：治疗没有常驻加成，不用撤什么</summary>
    public void Revert(Entity entity) { }

    /// <summary>回一次血（日志打出回完之后的血量，方便对上限截断）</summary>
    void Heal(Entity entity)
    {
        if (entity == null) return;

        entity.Heal(amount);
        Debug.Log($"[HealEffect] {entity.name} 回血 {amount}，现在 {entity.Hp}/{entity.MaxHp}", entity);
    }
}
