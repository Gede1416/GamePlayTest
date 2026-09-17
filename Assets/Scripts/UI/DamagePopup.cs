using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 伤害数字：外观在预制体里做好（世界空间 Canvas + 一个 Text），这里只负责写数字、
/// 让它一边往上飘一边淡出，飘完自毁。
/// 由 BattleUIManager 在被打实体的 UI 点位下实例化预制体后调 Init。
/// 成员顺序：属性 → 生命周期 → 公开方法 → 私有方法（各组内按调用顺序）。
/// </summary>
public class DamagePopup : MonoBehaviour
{
    #region 属性

    [Tooltip("数字文字（预制体里接好）")]
    [SerializeField] Text label;

    [Tooltip("往上飘多高（世界单位）")]
    [SerializeField] float rise = 1f;

    [Tooltip("显示多久（秒）")]
    [SerializeField] float life = 0.9f;

    [Tooltip("起始高度（相对实体的 UI 点位）")]
    [SerializeField] float startHeight = 0.25f;

    float age;
    Vector3 start;

    #endregion

    #region 生命周期

    void Update()
    {
        age += Time.deltaTime;

        var cam = Camera.main;                      // 世界空间 Canvas 是个平面，转过去才不会看成一条线
        if (cam != null) transform.rotation = cam.transform.rotation;

        float t = life > 0f ? Mathf.Clamp01(age / life) : 1f;
        transform.localPosition = start + Vector3.up * (rise * t);
        if (label != null) label.color = new Color(label.color.r, label.color.g, label.color.b, 1f - t);

        if (t >= 1f) Destroy(gameObject);
    }

    #endregion

    #region 公开方法

    /// <summary>初始化：写数字 + 记下起点（由 BattleUIManager 实例化后调）</summary>
    public void Init(float amount)
    {
        if (label != null) label.text = Mathf.RoundToInt(amount).ToString();

        start = new Vector3(0f, startHeight, 0f);
        transform.localPosition = start;
    }

    #endregion
}
