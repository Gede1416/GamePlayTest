using UnityEngine;

/// <summary>生命值。挂在可被攻击的物体上。</summary>
public class Health : MonoBehaviour
{
    public float maxHealth = 100f;

    [Tooltip("阵营：数值相同算己方（寻路找目标时用）")]
    public int team;

    public float Current;

    public bool IsDead => Current <= 0f;

    void Awake()
    {
        Current = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        if (amount <= 0f) return;
        if (IsDead) return;
        Current = Mathf.Max(0f, Current - amount);
        if (IsDead) this.gameObject.SetActive(false);
    }
}
