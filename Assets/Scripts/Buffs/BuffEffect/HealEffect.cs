using UnityEngine;

/// <summary>治疗：自己没有常驻加成，挂上与每次结算都按 amount 回一次血（回多少由构造函数带）</summary>
public class HealEffect : IBuffEffect
{
    readonly float amount;

    public HealEffect(float amount)
    {
        this.amount = amount;
    }

    /// <summary>挂上：也回一次（和原来一样，挂上当场就见效一次）</summary>
    public void Apply(Entity target) => Heal(target);

    /// <summary>结算一次回血</summary>
    public void Tick(Entity target) => Heal(target);

    /// <summary>移除：治疗没有常驻加成，不用撤什么</summary>
    public void Revert(Entity target) { }

    /// <summary>回一次血（日志打出回完之后的血量，方便对上限截断）</summary>
    void Heal(Entity target)
    {
        if (target == null) return;

        target.Heal(amount);
        Debug.Log($"[HealEffect] {target.name} 回血 {amount}，现在 {target.Hp}/{target.MaxHp}", target);
    }
}
