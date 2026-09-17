using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 血条：外观在预制体里做好（世界空间 Canvas + 底 / 前景两张 Image），这里只做三件事——
/// 初始化时绑实体、按血量比例改前景宽度、LateUpdate 里贴住 UI 点位并转向相机。
/// 数值靠 HealthChangedEvent 刷新；由 BattleUIManager 实例化预制体后调 Init。
/// 成员顺序：属性 → 生命周期 → 公开方法 → 私有方法（各组内按调用顺序）。
/// </summary>
public class HealthBar : MonoBehaviour
{
    #region 属性

    [Tooltip("前景条：按血量比例改宽度（预制体里接好）")]
    [SerializeField] Image fill;

    [Tooltip("满血时的前景宽度（像素，和预制体里的条宽一致）")]
    [SerializeField] float fullWidth = 100f;

    Entity target;          // 跟着谁（生命值读它 / 生命变化消息按它过滤）
    Transform anchor;       // 挂在哪（实体的 UI 点位）

    #endregion

    #region 生命周期

    // 每帧贴住 UI 点位并转向相机（LateUpdate 里做，保证实体这一帧已经动完）
    void LateUpdate()
    {
        if (anchor != null) transform.position = anchor.position;

        var cam = Camera.main;
        if (cam != null) transform.rotation = cam.transform.rotation;
    }

    #endregion

    #region 公开方法

    /// <summary>初始化：记下跟谁、订生命变化消息、按当前血量摆好条子（由 BattleUIManager 实例化后调）</summary>
    public void Init(Entity entity)
    {
        target = entity;
        anchor = entity != null ? entity.uiPoint : null;

        EventPipeline.Subscribe<HealthChangedEvent>(OnHealthChanged);

        // 血条是实体就绪之后才建的，Init 之前那条生命变化消息收不到，所以初始值直接读绑定组件
        var health = entity != null ? entity.Health : null;
        SetRatio(health != null && health.maxHealth > 0f ? health.Current / health.maxHealth : 0f);
    }

    /// <summary>清理：退订（条子本身由 BattleUIManager 销毁）</summary>
    public void Clear() => EventPipeline.Unsubscribe<HealthChangedEvent>(OnHealthChanged);

    #endregion

    #region 私有方法

    /// <summary>收到生命变化：只认自己跟的那个实体</summary>
    void OnHealthChanged(HealthChangedEvent e)
    {
        if (e.entity != target) return;
        SetRatio(e.max > 0f ? e.current / e.max : 0f);
    }

    /// <summary>按比例改前景宽度（左边不动，掉血从右往左）</summary>
    void SetRatio(float ratio)
    {
        if (fill == null) return;

        var rect = fill.rectTransform;
        rect.sizeDelta = new Vector2(fullWidth * Mathf.Clamp01(ratio), rect.sizeDelta.y);
    }

    #endregion
}
