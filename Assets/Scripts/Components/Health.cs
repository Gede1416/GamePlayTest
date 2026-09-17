using UnityEngine;

/// <summary>
/// 生命值。挂在可被攻击的物体上。
/// 死亡逻辑单独放在 Die()：扣血流程只负责把血扣到 0 再交给它，它负责停用自己并发出死亡消息。
/// 成员顺序：属性 → 生命周期 → 公开方法 → 私有方法（各组内按调用顺序）。
/// </summary>
public class Health : MonoBehaviour
{
    #region 属性

    [Tooltip("生命值上限；Init 时把当前值补满")]
    public float maxHealth = 100f;

    [Tooltip("阵营：数值相同算己方（寻路找目标时用）")]
    public int team;

    [Tooltip("当前生命值（运行期状态，归 Health 自己）")]
    public float Current;

    /// <summary>是否已阵亡</summary>
    public bool IsDead => Current <= 0f;

    Entity owner;      // 发死亡消息时要带上它
    bool dead;         // 死亡流程走过没有（防止重复发消息）

    #endregion

    #region 公开方法

    /// <summary>初始化：由 Entity.Init 调用（组件自己不靠 Awake 干活）：记住主人并把当前值补满</summary>
    public void Init()
    {
        owner = GetComponent<Entity>();
        dead = false;
        Current = maxHealth;
    }

    /// <summary>清理：回到未初始化状态（由 Entity.Clear 调，下次 Init 会重新补满）</summary>
    public void Clear()
    {
        dead = false;
        Current = 0f;
    }

    /// <summary>受到伤害：扣到 0 为止，见底就交给 Die()（伤害只从 Entity.TakeDamage 进来）</summary>
    public void TakeDamage(float amount)
    {
        if (amount <= 0f) return;
        if (IsDead) return;

        Current = Mathf.Max(0f, Current - amount);
        if (IsDead) Die();
    }

    /// <summary>死亡：单独处理死亡逻辑——停用自己 + 发死亡消息（同一条命只走一次）</summary>
    public void Die()
    {
        if (dead) return;

        dead = true;
        Current = 0f;
        gameObject.SetActive(false);
        EventPipeline.Send(BattleEvent.EntityDied(owner, team));
    }

    #endregion
}
