using UnityEngine;

/// <summary>
/// 生命值。挂在可被攻击的物体上。
/// 成员顺序：属性 → 生命周期 → 公开方法 → 私有方法（各组内按调用顺序）。
/// </summary>
public class Health : MonoBehaviour
{
    #region 属性

    [Tooltip("生命值上限；Awake 时把当前值补满")]
    public float maxHealth = 100f;

    [Tooltip("阵营：数值相同算己方（寻路找目标时用）")]
    public int team;

    [Tooltip("当前生命值（运行期状态，归 Health 自己）")]
    public float Current;

    /// <summary>是否已阵亡</summary>
    public bool IsDead => Current <= 0f;

    #endregion

    #region 公开方法

    /// <summary>初始化：由 Entity.Init 调用（组件自己不靠 Awake 干活），把当前值补满</summary>
    public void Init() => Current = maxHealth;

    /// <summary>受到伤害：扣到 0 为止；死了就 SetActive(false)，伤害只从 Entity.TakeDamage 进来</summary>
    public void TakeDamage(float amount)
    {
        if (amount <= 0f) return;
        if (IsDead) return;
        Current = Mathf.Max(0f, Current - amount);
        if (IsDead) this.gameObject.SetActive(false);
    }

    #endregion

}
