using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 战斗界面：数据全从事件管线来——回合数（TurnChangedEvent）、伤害数字（DamageEvent）、
/// 胜利队伍（BattleEndedEvent），血条在实体就绪（EntitySpawnedEvent）时给每个实体挂一条。
/// HUD（回合数 / 结果）挂在主相机下；伤害数字与血条挂在实体自己的 UI 点位下。
/// 文字用 TextMesh（不用 Canvas / 字体资源；内置字体没有中文字形，所以只写英文和数字）。
/// 由 BattleManager 调 Init / Clear，自己不写 Awake。
/// 成员顺序：属性 → 生命周期 → 公开方法 → 私有方法（各组内按调用顺序）。
/// </summary>
public class BattleUIManager : MonoBehaviour
{
    #region 属性

    [Header("文字外观")][Tooltip("TextMesh 字号（像素）")]
    public int fontSize = 48;

    [Tooltip("字的相对大小：1 = TextMesh 默认，越小越细")]
    public float textScale = 0.08f;

    [Header("HUD 位置（相机空间，挂在主相机下）")][Tooltip("回合数相对相机的位置")]
    public Vector3 roundPos = new Vector3(-0.5f, 0.3f, 1.2f);

    [Tooltip("战斗结果相对相机的位置")]
    public Vector3 resultPos = new Vector3(0f, 0.12f, 1.2f);

    [Header("伤害数字")][Tooltip("往上飘多高（世界单位）")]
    public float popupRise = 1f;

    [Tooltip("显示多久（秒）")]
    public float popupLife = 0.9f;

    [Header("血条")][Tooltip("血条宽度（世界单位）")]
    public float barWidth = 0.8f;

    [Tooltip("血条高度（世界单位）")]
    public float barHeight = 0.12f;

    Transform hudRoot;                          // HUD 根（主相机的子物体）
    TextMesh roundText;                         // 回合数
    TextMesh resultText;                        // 战斗结果
    readonly List<GameObject> spawned = new();  // 这次创建出来的 UI（清场时一起销毁）

    #endregion

    #region 公开方法

    /// <summary>初始化：建 HUD + 订事件（由 BattleManager 调，要在实体 Init 之前，免得漏掉实体就绪消息）</summary>
    public void Init()
    {
        BuildHud();
        SetText(roundText, string.Empty);
        SetText(resultText, string.Empty);

        EventPipeline.Subscribe<TurnChangedEvent>(OnTurnChanged);
        EventPipeline.Subscribe<DamageEvent>(OnDamage);
        EventPipeline.Subscribe<EntitySpawnedEvent>(OnEntitySpawned);
        EventPipeline.Subscribe<BattleEndedEvent>(OnBattleEnded);
    }

    /// <summary>清理：退订 + 销毁这次创建的 UI（由 BattleManager 调）</summary>
    public void Clear()
    {
        EventPipeline.Unsubscribe<TurnChangedEvent>(OnTurnChanged);
        EventPipeline.Unsubscribe<DamageEvent>(OnDamage);
        EventPipeline.Unsubscribe<EntitySpawnedEvent>(OnEntitySpawned);
        EventPipeline.Unsubscribe<BattleEndedEvent>(OnBattleEnded);

        foreach (var go in spawned)
        {
            if (go == null) continue;

            var bar = go.GetComponent<HealthBar>();
            if (bar != null) bar.Clear();       // 血条自己退订，别等全局 Clear

            Destroy(go);
        }
        spawned.Clear();

        if (hudRoot != null) Destroy(hudRoot.gameObject);

        hudRoot = null;
        roundText = null;
        resultText = null;
    }

    #endregion

    #region 私有方法

    /// <summary>回合刷新：更新左上角的回合数</summary>
    void OnTurnChanged(TurnChangedEvent e) => SetText(roundText, $"Round {e.round}/{e.totalRounds}");

    /// <summary>战斗结束：显示胜方阵营（没有幸存者就显示 Draw）</summary>
    void OnBattleEnded(BattleEndedEvent e) => SetText(resultText, e.winnerTeam >= 0 ? $"Team {e.winnerTeam} Wins" : "Draw");

    /// <summary>有人挨打：在它头顶飘一个伤害数字</summary>
    void OnDamage(DamageEvent e)
    {
        var anchor = e.target != null ? e.target.uiPoint : null;
        if (anchor == null) return;

        var label = CreateText($"Damage {Mathf.RoundToInt(e.amount)}", anchor);
        label.gameObject.AddComponent<DamagePopup>().Init(e.amount, popupRise, popupLife);
        spawned.Add(label.gameObject);
    }

    /// <summary>实体就绪：给它挂一条血条（数值靠 HealthBar 自己订的生命变化消息刷新）</summary>
    void OnEntitySpawned(EntitySpawnedEvent e)
    {
        if (e.entity == null || e.entity.uiPoint == null) return;

        var go = new GameObject($"HealthBar {e.entity.name}");
        go.transform.SetParent(e.entity.uiPoint, false);

        go.AddComponent<HealthBar>().Init(e.entity, barWidth, barHeight);
        spawned.Add(go);
    }

    /// <summary>在相机前面建 HUD（回合数 / 结果）</summary>
    void BuildHud()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[BattleUIManager] 场景里没有主相机，回合数与战斗结果不显示", this);
            return;
        }

        hudRoot = new GameObject("BattleHUD").transform;
        hudRoot.SetParent(cam.transform, false);

        roundText = CreateText("RoundText", hudRoot);
        roundText.transform.localPosition = roundPos;

        resultText = CreateText("ResultText", hudRoot);
        resultText.transform.localPosition = resultPos;
    }

    /// <summary>建一个世界空间文字：TextMesh 自带网格渲染，不需要 Canvas / 字体资源</summary>
    TextMesh CreateText(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var text = go.AddComponent<TextMesh>();
        text.font = BuiltinFont;
        text.fontSize = fontSize;
        text.characterSize = textScale;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.color = Color.white;

        var renderer = go.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            if (text.font != null) renderer.sharedMaterial = text.font.material;
            renderer.sortingOrder = 10;         // 别被血条盖住
        }

        return text;
    }

    static void SetText(TextMesh text, string value)
    {
        if (text != null) text.text = value;
    }

    static Font builtinFont;

    /// <summary>内置字体：Unity 2022 之后叫 LegacyRuntime.ttf（老版本是 Arial.ttf）</summary>
    static Font BuiltinFont
    {
        get
        {
            if (builtinFont == null) builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (builtinFont == null) builtinFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

            return builtinFont;
        }
    }

    #endregion
}
