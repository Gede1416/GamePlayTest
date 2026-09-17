using UnityEngine;

/// <summary>
/// 伤害数字：挂在被打实体的 UI 点位下，一边往上飘一边淡出，飘完自己销毁。
/// 由 BattleUIManager 收到伤害消息时创建并调 Init。
/// 成员顺序：属性 → 生命周期 → 公开方法 → 私有方法（各组内按调用顺序）。
/// </summary>
public class DamagePopup : MonoBehaviour
{
    #region 属性

    TextMesh label;         // 数字本体（同一个物体上）
    float rise = 1f;        // 往上飘多高
    float life = 0.9f;      // 显示多久
    float age;
    Vector3 start;

    #endregion

    #region 生命周期

    void Update()
    {
        age += Time.deltaTime;

        float t = life > 0f ? Mathf.Clamp01(age / life) : 1f;
        transform.localPosition = start + Vector3.up * (rise * t);
        if (label != null) label.color = new Color(label.color.r, label.color.g, label.color.b, 1f - t);

        if (t >= 1f) Destroy(gameObject);
    }

    #endregion

    #region 公开方法

    /// <summary>初始化：写数字 + 记下起点与参数（由 BattleUIManager 调）</summary>
    public void Init(float amount, float rise, float life)
    {
        label = GetComponent<TextMesh>();
        if (label != null) label.text = Mathf.RoundToInt(amount).ToString();

        this.rise = rise;
        this.life = life;

        // 从血条上方一点点开始飘，别跟血条叠在一起
        start = new Vector3(0f, 0.25f, 0f);
        transform.localPosition = start;
    }

    #endregion
}
