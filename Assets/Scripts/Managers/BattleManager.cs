using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 战场管理：统一负责「加载 → 初始化 → 开打 → 清场重来」。
/// 预制体路径在 Inspector 里配（mapPath / turnPath / entityPaths），加载出来的对象统一挂到 mapPos 下。
/// 加载顺序：地图先就位（实体要靠它定位），再把实体列表接进地图、参战列表接进回合管理器，最后各自 Init。
/// RebuildBattle() = 清掉当前加载的全部对象，重新加载初始化一遍。
/// 成员顺序：属性 → 生命周期 → 公开方法 → 私有方法（各组内按调用顺序）。
/// </summary>
public class BattleManager : MonoBehaviour
{
    // ---------- 属性 ----------

    [Header("预制体路径（先按文件名走 Resources，编辑器里再按资源路径加载）")][Tooltip("地图预制体：里面挂着 MapManager 与各实体的初始位置列表")]
    [SerializeField] string mapPath;

    [Tooltip("回合控制器预制体：里面有回合数 / 行动间隔 / autoStart")]
    [SerializeField] string turnPath;

    [Tooltip("参战实体预制体列表，顺序对应地图上的初始位置列表")]
    [SerializeField] List<string> entityPaths = new();

    [Tooltip("加载出来的对象都挂到它下面；留空就放场景根")]
    [SerializeField] Transform mapPos;

    MapManager mapManager;
    TurnManager turnManager;
    List<Entity> entities;

    // ---------- 生命周期 ----------

    void Awake() => BuildBattle();      // 进播放就加载初始化一次，之后随时可以 RebuildBattle()

    // ---------- 公开方法 ----------

    /// <summary>
    /// 加载 + 初始化整场战斗（开局与 RebuildBattle 都走它，开头先清场所以可以反复调）。
    /// 只负责把东西装好，不负责开打：开打由 turn 预制体上的 autoStart 或外部调 StartBattle() 决定。
    /// </summary>
    public void BuildBattle()
    {
        ClearBattle();

        // ---------- 地图 ----------
        var mapPrefab = LoadPrefab(mapPath);
        if (mapPrefab == null) return;                       // 加载不到地图就没法继续（LoadPrefab 已经报错）

        mapManager = InstantiateAt(mapPrefab).GetComponent<MapManager>();
        if (mapManager == null)
        {
            Debug.LogError($"[BattleManager] {mapPath} 上没有 MapManager 组件", this);
            return;
        }

        // ---------- 实体 ----------
        entities = new List<Entity>();
        var entityObjects = new List<GameObject>();

        foreach (var path in entityPaths)
        {
            var prefab = LoadPrefab(path);
            if (prefab == null) continue;

            var go = InstantiateAt(prefab);
            var entity = go.GetComponent<Entity>();
            if (entity == null)
            {
                Debug.LogError($"[BattleManager] {path} 上没有 Entity 组件", go);
                Destroy(go);
                continue;
            }

            // 地图引用直接发下去：别让组件自己 FindObjectOfType（重开时可能找到正在销毁的旧地图）
            entity.map = mapManager;
            entities.Add(entity);
            entityObjects.Add(go);
        }

        mapManager.entities = entityObjects;                // 顺序对应 spawnPoints
        mapManager.Init();                                  // 建网格 + 摆到初始位置 + 重建占用
        foreach (var entity in entities) entity.Init();      // 各自按预制体数据装配管线

        // ---------- 回合 ----------
        var turnPrefab = LoadPrefab(turnPath);
        if (turnPrefab == null) return;

        turnManager = InstantiateAt(turnPrefab).GetComponent<TurnManager>();
        if (turnManager == null)
        {
            Debug.LogError($"[BattleManager] {turnPath} 上没有 TurnManager 组件", this);
            return;
        }

        turnManager.Init(new TurnInitData
        {
            actors = entities,
            totalRounds = turnManager.totalRounds,          // 回合数与间隔沿用预制体上配的
            turnDelay = turnManager.turnDelay,
        });
    }

    /// <summary>开打（回合信息在 BuildBattle 里已经装好）</summary>
    public void StartBattle()
    {
        if (turnManager != null) turnManager.StartBattle();
    }

    /// <summary>清场重来：清掉当前加载的全部对象，再加载初始化一遍</summary>
    public void RebuildBattle() => BuildBattle();

    /// <summary>清掉加载出来的地图 / 实体 / 回合控制器（先让回合停手再销毁）</summary>
    public void ClearBattle()
    {
        if (turnManager != null) turnManager.StopBattle();

        if (mapManager != null) Destroy(mapManager.gameObject);
        if (turnManager != null) Destroy(turnManager.gameObject);
        if (entities != null)
            foreach (var entity in entities)
                if (entity != null) Destroy(entity.gameObject);

        entities = new List<Entity>();
        mapManager = null;
        turnManager = null;
    }

    // ---------- 私有方法 ----------

    /// <summary>加载出来的对象统一挂到 mapPos 下（没配 mapPos 就放场景根）</summary>
    GameObject InstantiateAt(GameObject prefab)
    {
        return mapPos != null
            ? Instantiate(prefab, mapPos.position, Quaternion.identity, mapPos)
            : Instantiate(prefab);
    }

    /// <summary>按路径加载预制体：先按名字走 Resources（打包后也能用），编辑器里再退回按资源路径加载</summary>
    static GameObject LoadPrefab(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        var go = Resources.Load<GameObject>(System.IO.Path.GetFileNameWithoutExtension(path));
#if UNITY_EDITOR
        if (go == null) go = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
#endif
        if (go == null)
            Debug.LogError($"[BattleManager] 加载不到预制体：{path}（放进 Resources 目录就能在打包后也加载到）");

        return go;
    }
}
