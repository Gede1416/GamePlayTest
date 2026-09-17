using UnityEngine;

/// <summary>
/// 血条：绑在实体的 UI 点位上，数值靠 HealthChangedEvent 刷新，世界位置与朝向放在 LateUpdate 里更新
/// （每帧贴住 UI 点位 + 转向相机，实体走哪它跟哪、从侧面看也不会变成一条线）。
/// 由 BattleUIManager 在实体就绪时创建并调 Init（不写进预制体，省得每个实体手接引用）。
/// 成员顺序：属性 → 生命周期 → 公开方法 → 私有方法（各组内按调用顺序）。
/// </summary>
public class HealthBar : MonoBehaviour
{
    #region 属性

    Entity target;          // 跟着谁（生命值读它 / 生命变化消息按它过滤）
    Transform anchor;       // 挂在哪（实体的 UI 点位）
    Transform fill;         // 前景，按比例缩放
    float width = 0.8f;
    float height = 0.12f;

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

    /// <summary>初始化：记下跟谁、搭出条子、订生命变化消息（由创建者 BattleUIManager 调）</summary>
    public void Init(Entity entity, float width, float height)
    {
        target = entity;
        anchor = entity != null ? entity.uiPoint : null;
        this.width = width;
        this.height = height;

        Build();
        EventPipeline.Subscribe<HealthChangedEvent>(OnHealthChanged);

        // 血条是实体就绪之后才建的，Init 之前那条生命变化消息收不到，所以初始值直接读绑定组件
        var health = entity != null ? entity.Health : null;
        SetRatio(health != null && health.maxHealth > 0f ? health.Current / health.maxHealth : 0f);
    }

    /// <summary>清理：退订（条子本身随实体或 UI 管理器一起销毁）</summary>
    public void Clear() => EventPipeline.Unsubscribe<HealthChangedEvent>(OnHealthChanged);

    #endregion

    #region 私有方法

    /// <summary>收到生命变化：只认自己跟的那个实体</summary>
    void OnHealthChanged(HealthChangedEvent e)
    {
        if (e.entity != target) return;
        SetRatio(e.max > 0f ? e.current / e.max : 0f);
    }

    /// <summary>搭条子：底 + 前景各一块白图</summary>
    void Build()
    {
        var back = CreateQuad("Back", new Color(0.1f, 0.1f, 0.1f, 0.85f), 1);
        back.transform.localScale = new Vector3(width, height, 1f);

        fill = CreateQuad("Fill", new Color(0.25f, 0.9f, 0.3f, 1f), 2).transform;
    }

    SpriteRenderer CreateQuad(string name, Color color, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);

        var quad = go.AddComponent<SpriteRenderer>();
        quad.sprite = WhiteSprite;
        quad.color = color;
        quad.sortingOrder = order;
        return quad;
    }

    /// <summary>按比例缩放前景并让左边对齐（掉血是从右往左）</summary>
    void SetRatio(float ratio)
    {
        if (fill == null) return;

        ratio = Mathf.Clamp01(ratio);
        fill.localScale = new Vector3(width * ratio, height, 1f);
        fill.localPosition = new Vector3(-(width - width * ratio) * 0.5f, 0f, 0f);
    }

    static Sprite whiteSprite;

    /// <summary>1×1 白色方块精灵：不依赖任何美术资源</summary>
    static Sprite WhiteSprite
    {
        get
        {
            if (whiteSprite == null)
                whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);

            return whiteSprite;
        }
    }

    #endregion
}
