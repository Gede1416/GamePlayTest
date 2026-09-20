using UnityEngine;

/// <summary>增加攻击力：挂上时把 amount 加给伤害类技能的伤害，移除时减回去</summary>
public class AddAttackEffect : IBuffEffect
{
    #region 属性

    readonly float amount;

    #endregion

    #region 构造

    public AddAttackEffect(float amount)
    {
        this.amount = amount;
    }

    #endregion

    #region 公开方法

    public void Apply(Entity target) => Change(target, amount);

    public void Tick(Entity target) { }

    public void Revert(Entity target) => Change(target, -amount);

    #endregion

    #region 私有方法

    /// <summary>delta 正负就是挂上 / 移除（伤害的绝对值在各技能的阶段三里，这里只报变化量）</summary>
    static void Change(Entity target, float delta)
    {
        if (target == null) return;

        target.AddDamage(delta);
        Debug.Log($"[AddAttackEffect] {target.name} 攻击力 {delta:+0.#;-0.#}{(delta < 0 ? "（移除加成）" : "")}", target);
    }

    #endregion
}
