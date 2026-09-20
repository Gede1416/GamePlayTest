using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 战斗界面：数据全从事件管线来——回合数（TurnChangedEvent）、伤害数字（DamageEvent）、
/// 胜利队伍（BattleEndedEvent），血条与 buff 条在实体就绪（EntitySpawnedEvent）时给每个实体各挂一条。
/// HUD 是场景 Canvas 上的 TMP 文字（直接引用），血条 / buff 条 / 伤害数字都是**预制体**——这里只负责加载、实例化、销毁，
/// 不在代码里组装任何界面元素。
/// 由 BattleManager 调 Init / Clear，自己不写 Awake。
/// 成员顺序：属性 → 生命周期 → 公开方法 → 私有方法（各组内按调用顺序）。
/// </summary>
public class BattleUIManager : MonoBehaviour
{
    #region 属性

    [Header("HUD（场景 Canvas 下的 TMP 文字）")][Tooltip("回合数文字")]
    [SerializeField] TMP_Text roundText;

    [Tooltip("战斗结果文字")]
    [SerializeField] TMP_Text resultText;

    [Header("预制体路径（先按文件名走 Resources，编辑器里再按资源路径加载）")][Tooltip("血条预制体：实例化到实体的 UI 点位下")]
    [SerializeField] string healthBarPath = "Assets/prefab/HealthBar.prefab";

    [Tooltip("伤害数字预制体：实例化到被打实体的 UI 点位下")]
    [SerializeField] string damagePopupPath = "Assets/prefab/DamagePopup.prefab";

    [Tooltip("buff 条预制体：实例化到实体的 UI 点位下（显示正在生效的 buff）")]
    [SerializeField] string buffBarPath = "Assets/prefab/BuffBar.prefab";

    GameObject healthBarPrefab;                 // 加载一次，之后反复实例化
    GameObject damagePopupPrefab;
    GameObject buffBarPrefab;
    readonly List<GameObject> spawned = new();  // 这次实例化出来的界面元素（清场时一起销毁）

    #endregion

    #region 公开方法

    /// <summary>初始化：加载预制体 + 订事件（由 BattleManager 调，要在实体 Init 之前，免得漏掉实体就绪消息）</summary>
    public void Init()
    {
        healthBarPrefab = PrefabLoader.Load(healthBarPath);
        damagePopupPrefab = PrefabLoader.Load(damagePopupPath);
        buffBarPrefab = PrefabLoader.Load(buffBarPath);

        SetText(roundText, string.Empty);
        SetText(resultText, string.Empty);

        EventPipeline.Subscribe<TurnChangedEvent>(OnTurnChanged);
        EventPipeline.Subscribe<DamageEvent>(OnDamage);
        EventPipeline.Subscribe<EntitySpawnedEvent>(OnEntitySpawned);
        EventPipeline.Subscribe<BattleEndedEvent>(OnBattleEnded);
    }

    /// <summary>清理：退订 + 销毁实例化出来的血条 / 伤害数字（由 BattleManager 调）</summary>
    public void Clear()
    {
        EventPipeline.Unsubscribe<TurnChangedEvent>(OnTurnChanged);
        EventPipeline.Unsubscribe<DamageEvent>(OnDamage);
        EventPipeline.Unsubscribe<EntitySpawnedEvent>(OnEntitySpawned);
        EventPipeline.Unsubscribe<BattleEndedEvent>(OnBattleEnded);

        foreach (var go in spawned)
        {
            if (go == null) continue;

            // 血条 / buff 条自己退订，别等全局 Clear
            var healthBar = go.GetComponent<HealthBar>();
            if (healthBar != null) healthBar.Clear();

            var buffBar = go.GetComponent<BuffBar>();
            if (buffBar != null) buffBar.Clear();

            Destroy(go);
        }
        spawned.Clear();
    }

    #endregion

    #region 私有方法

    /// <summary>回合刷新：更新回合数</summary>
    void OnTurnChanged(TurnChangedEvent e) => SetText(roundText, $"Round {e.round}/{e.totalRounds}");

    /// <summary>战斗结束：显示胜方阵营（没有幸存者就显示 Draw）</summary>
    void OnBattleEnded(BattleEndedEvent e) => SetText(resultText, e.winnerTeam >= 0 ? $"Team {e.winnerTeam} Wins" : "Draw");

    /// <summary>有人挨打：在它头顶的 UI 点位下实例化伤害数字预制体</summary>
    void OnDamage(DamageEvent e)
    {
        var anchor = e.target != null ? e.target.uiPoint : null;
        if (anchor == null || damagePopupPrefab == null) return;

        var go = Instantiate(damagePopupPrefab, anchor, false);
        go.GetComponent<DamagePopup>().Init(e.amount);
        spawned.Add(go);
    }

    /// <summary>实体就绪：在它头顶的 UI 点位下实例化血条与 buff 条预制体</summary>
    void OnEntitySpawned(EntitySpawnedEvent e)
    {
        if (e.entity == null || e.entity.uiPoint == null) return;

        if (healthBarPrefab != null)
        {
            var bar = Instantiate(healthBarPrefab, e.entity.uiPoint, false);
            bar.GetComponent<HealthBar>().Init(e.entity);
            spawned.Add(bar);
        }

        if (buffBarPrefab != null)
        {
            var buffs = Instantiate(buffBarPrefab, e.entity.uiPoint, false);
            buffs.GetComponent<BuffBar>().Init(e.entity);
            spawned.Add(buffs);
        }
    }

    static void SetText(TMP_Text text, string value)
    {
        if (text != null) text.text = value;
    }

    #endregion
}
