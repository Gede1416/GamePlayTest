using UnityEngine;

/// <summary>
/// 增加攻击力的 buff：挂上时把 amount 加给伤害类技能的伤害，移除时减回去。
/// 加多少、持续几回合都由技能的阶段三给（AddAttackBuffCaster 的常量）。
/// </summary>
public class AddAttackEffect : IBuff
{
    readonly float amount;

    readonly int duration;      // 结算几次（<= 0 = 永久）

    Entity target;              // 挂给谁（BuffManager 收下时发下来）
    int elapsed;                // 已经结算过几次

    /// <summary>是否该结束了（永久 buff 永远不结束）</summary>
    public bool IsOver => duration > 0 && elapsed >= duration;

    /// <summary>还剩几次结算（永久返回 -1）</summary>
    public int Left => duration > 0 ? Mathf.Max(0, duration - elapsed) : -1;

    /// <summary>加多少伤害 + 结算几次</summary>
    public AddAttackEffect(float amount, int duration)
    {
        this.amount = amount;
        this.duration = duration;
    }

    /// <summary>装配：挂给谁</summary>
    public void Init(Entity target) => this.target = target;

    /// <summary>挂上：加攻击力（常驻加成）</summary>
    public void Apply() => Change(amount);

    /// <summary>结算一次：这条 buff 没有每次结算的效果，只记一次已结算</summary>
    public void Tick() => elapsed++;

    /// <summary>移除：把加上去的攻击力减回来</summary>
    public void Revert() => Change(-amount);

    /// <summary>delta 正负就是挂上 / 移除（伤害的绝对值在各技能的阶段三里，这里只报变化量）</summary>
    void Change(float delta)
    {
        if (target == null) return;

        target.AddDamage(delta);
        Debug.Log($"[AddAttackEffect] {target.name} 攻击力 {delta:+0.#;-0.#}{(delta < 0 ? "（移除加成）" : "")}", target);
    }
}
