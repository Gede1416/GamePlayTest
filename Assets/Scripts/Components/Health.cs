using UnityEngine;

/// <summary>
/// 生命值。挂在可被攻击的物体上。
/// 死亡逻辑单独放在 Die()：扣血流程只负责把血扣到 0 再交给它，它负责判定死亡并发出死亡消息。
/// "停用自己"这一步：有 DeathEffect（阵亡淡出）就交给它，淡完由它停用；没有就直接停用。
/// 对外发三种消息：DamageEvent（伤害数字）、HealthChangedEvent（血条）、EntityDiedEvent（阵亡结算）。
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

    Entity owner;              // 发死亡消息时要带上它
    DeathEffect deathEffect;   // 阵亡淡出组件，可能没有
    bool dead;                 // 死亡流程走过没有（防止重复发消息）

    #endregion

    #region 公开方法

    /// <summary>初始化：由 Entity.Init 调用（组件自己不靠 Awake 干活）：记住主人与死亡表现、补满血、报一次生命变化</summary>
    public void Init()
    {
        owner = GetComponent<Entity>();
        deathEffect = GetComponent<DeathEffect>();
        dead = false;
        Current = maxHealth;
        SendChanged();
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
        EventPipeline.Send(new DamageEvent(owner, amount));     // 伤害数字
        SendChanged();                                          // 血条
        if (IsDead) Die();
    }

    /// <summary>治疗：回血但不超过上限，已阵亡的不再被治疗（buff 挂在尸体上也回不来）</summary>
    public void Heal(float amount)
    {
        if (amount <= 0f) return;
        if (IsDead) return;

        Current = Mathf.Min(maxHealth, Current + amount);
        SendChanged();                                          // 血条
    }

    /// <summary>死亡：单独处理死亡逻辑——交给死亡表现（没配就直接停用自己）+ 发死亡消息（同一条命只走一次）</summary>
    public void Die()
    {
        if (dead) return;

        dead = true;
        Current = 0f;

        // 表现与逻辑分开：逻辑（结算 / 放开格子）立刻生效，淡出只是跟着走
        if (deathEffect != null) deathEffect.Play();
        else gameObject.SetActive(false);

        EventPipeline.Send(new EntityDiedEvent(owner, team));
    }

    #endregion

    #region 私有方法

    /// <summary>报一次生命变化（血条靠它刷新）</summary>
    void SendChanged() => EventPipeline.Send(new HealthChangedEvent(owner, Current, maxHealth));

    #endregion
}
