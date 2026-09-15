using UnityEngine;

/// <summary>
/// 攻击：挂在作为“碰触体”的物体上，该物体需要有 Collider 并勾选 Is Trigger。
/// 碰到带 Health 的物体时扣血。
/// </summary>
public class Attack : MonoBehaviour
{
    [Tooltip("每次接触造成的伤害")]
    public float damage = 10f;
    public Health haver;
    void OnTriggerEnter(Collider other)
    {
        var health = other.GetComponentInParent<Health>();
        if (health != null && health != haver) health.TakeDamage(damage);
    }
}
