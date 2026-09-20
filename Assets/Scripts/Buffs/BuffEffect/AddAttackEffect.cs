using UnityEngine;

/// <summary>
/// 增加攻击力的 buff：挂上时把 amount 加给伤害类技能的伤害，移除时减回去。
/// 加多少、持续几回合都由技能的阶段三给（AddAttackBuffCaster 的常量）；
/// 挂给谁每次都当参数传进来（零件不记目标）。
/// </summary>
public class AddAttackEffect : IBuff
{
    readonly float amount;

    readonly int duration;      // 结算几次（<= 0 = 永久）

    int elapsed;                // 已经结算过几次

    /// <summary>是否该结束了（永久 buff 永远不结束）</summary>
    public bool IsOver => duration > 0 && elapsed >= duration;

    /// <summary>加多少伤害 + 结算几次</summary>
    public AddAttackEffect(float amount, int duration)
    {
        this.amount = amount;
        this.duration = duration;
    }

    /// <summary>挂上：加攻击力（常驻加成）</summary>
    public void Apply(Entity entity) => Change(entity, amount);

    /// <summary>结算一次：这条 buff 没有每次结算的效果，只记一次已结算</summary>
    public void Tick(Entity entity) => elapsed++;

    /// <summary>移除：把加上去的攻击力减回来</summary>
    public void Revert(Entity entity) => Change(entity, -amount);

    /// <summary>delta 正负就是挂上 / 移除（伤害的绝对值在各技能的阶段三里，这里只报变化量）</summary>
    static void Change(Entity entity, float delta)
    {
        if (entity == null) return;

        entity.AddDamage(delta);
        Debug.Log($"[AddAttackEffect] {entity.name} 攻击力 {delta:+0.#;-0.#}{(delta < 0 ? "（移除加成）" : "")}", entity);
    }
}
