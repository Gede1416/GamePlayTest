using UnityEngine;

/// <summary>增加攻击力：挂上时把 amount 加给伤害类技能的伤害，移除时减回去</summary>
public class AddAttackEffect : IBuffEffect
{
    readonly float amount;

    public AddAttackEffect(float amount)
    {
        this.amount = amount;
    }

    public void Apply(Entity target) => Change(target, amount);

    public void Tick(Entity target) { }

    public void Revert(Entity target) => Change(target, -amount);

    /// <summary>delta 正负就是挂上 / 移除（伤害的绝对值在各技能的阶段三里，这里只报变化量）</summary>
    static void Change(Entity target, float delta)
    {
        if (target == null) return;

        target.AddDamage(delta);
        Debug.Log($"[AddAttackEffect] {target.name} 攻击力 {delta:+0.#;-0.#}{(delta < 0 ? "（移除加成）" : "")}", target);
    }
}
