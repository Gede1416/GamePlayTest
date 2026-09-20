using UnityEngine;

/// <summary>
/// 治疗 buff：自己没有常驻加成，挂上与每次结算都按 amount 回一次血。
/// 回多少、持续几回合都由技能的阶段三给（HealBuffCaster 的常量），它自己只管记账。
/// </summary>
public class HealEffect : IBuff
{
    readonly float amount;

    readonly int duration;      // 结算几次（<= 0 = 永久）

    Entity target;              // 挂给谁（BuffManager 收下时发下来）
    int elapsed;                // 已经结算过几次

    /// <summary>是否该结束了（永久 buff 永远不结束）</summary>
    public bool IsOver => duration > 0 && elapsed >= duration;

    /// <summary>还剩几次结算（永久返回 -1）</summary>
    public int Left => duration > 0 ? Mathf.Max(0, duration - elapsed) : -1;

    /// <summary>每次回多少血 + 结算几次</summary>
    public HealEffect(float amount, int duration)
    {
        this.amount = amount;
        this.duration = duration;
    }

    /// <summary>装配：挂给谁</summary>
    public void Init(Entity target) => this.target = target;

    /// <summary>挂上：也回一次（和原来一样，挂上当场就见效一次）</summary>
    public void Apply() => Heal();

    /// <summary>结算一次回血</summary>
    public void Tick()
    {
        Heal();
        elapsed++;
    }

    /// <summary>移除：治疗没有常驻加成，不用撤什么</summary>
    public void Revert() { }

    /// <summary>回一次血（日志打出回完之后的血量，方便对上限截断）</summary>
    void Heal()
    {
        if (target == null) return;

        target.Heal(amount);
        Debug.Log($"[HealEffect] {target.name} 回血 {amount}，现在 {target.Hp}/{target.MaxHp}", target);
    }
}
