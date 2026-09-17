using UnityEngine;

/// <summary>生命值。挂在可被攻击的物体上。</summary>
public class Health : MonoBehaviour
{
    [Tooltip("生命值上限；Awake 时把当前值补满")]
    public float maxHealth = 100f;

    [Tooltip("阵营：数值相同算己方（寻路找目标时用）")]
    public int team;

    [Tooltip("当前生命值（运行期状态，归 Health 自己）")]
    public float Current;

    /// <summary>是否已阵亡</summary>
    public bool IsDead => Current <= 0f;

    /// <summary>受到伤害：扣到 0 为止；死了就 SetActive(false)，伤害只从 Entity.TakeDamage 进来</summary>
    public void TakeDamage(float amount)
    {
        if (amount <= 0f) return;
        if (IsDead) return;
        Current = Mathf.Max(0f, Current - amount);
        if (IsDead) this.gameObject.SetActive(false);
    }

    // ---------- 私有 ----------

    void Awake() => Current = maxHealth;
}
