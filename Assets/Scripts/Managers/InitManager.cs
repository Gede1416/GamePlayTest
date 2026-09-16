using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 初始化管理器：**唯一需要挂在场景上的管理器**。
/// 负责构建 MapManager 与 TurnManager，并把运行时依赖注入给需要它的组件
/// （MapManager 已是普通类，组件不能再 FindObjectOfType 找它）。
/// </summary>
public class InitManager : MonoBehaviour
{
    [Header("地图")]
    [Tooltip("地图物体：取其 Renderer 包围盒的 x / z 算网格")]
    public GameObject map;

    [Tooltip("参战实体物体（会被放回下面的初始位置）")]
    public List<GameObject> entities = new();

    [Tooltip("初始位置（格子坐标），与实体列表一一对应")]
    public List<Vector2Int> spawnPoints = new();

    [Tooltip("单位距离：每个格子的边长")]
    public float cellSize = 1f;

    [Header("回合")]
    [Tooltip("参战角色：行动顺序按各自 Entity.speed 排（大的先动）")]
    public List<Entity> actors = new();

    [Tooltip("给定回合数：跑满这个回合数就结束")]
    public int totalRounds = 5;

    [Tooltip("角色行动完等多久（秒）")]
    public float turnDelay = 0.2f;

    [Tooltip("进入播放就开打")]
    public bool autoStart = true;

    /// <summary>构建出来的地图管理器</summary>
    public MapManager Map { get; private set; }

    /// <summary>构建出来的回合管理器</summary>
    public TurnManager Turns { get; private set; }

    Coroutine routine;

    void Awake()
    {
        // 1) 地图：划分网格 + 把实体放回初始位置
        Map = new MapManager { cellSize = cellSize };
        Map.Init(entities, spawnPoints, map);

        // 2) 把地图发给需要它的组件
        foreach (var go in entities)
        {
            if (go == null) continue;
            var mover = go.GetComponent<ObjectMover>();
            if (mover != null) mover.map = Map;
            var entity = go.GetComponent<Entity>();
            if (entity != null) entity.Setup(Map);
        }
        foreach (var actor in actors)
            if (actor != null) actor.Setup(Map);

        var navTest = FindObjectOfType<NavTest>();     // 测试按钮，可没有
        if (navTest != null) navTest.map = Map;

        // 3) 回合
        Turns = new TurnManager { totalRounds = totalRounds, turnDelay = turnDelay };
        Turns.Init(actors);
    }

    void Start()
    {
        if (autoStart) StartBattle();
    }

    /// <summary>开打（先停掉正在跑的那局）</summary>
    public void StartBattle()
    {
        StopBattle();
        routine = StartCoroutine(Turns.Run());
    }

    /// <summary>停手（回合数状态留在 Turns 上）</summary>
    public void StopBattle()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
    }

    // 选中时画出网格与占用（原来在 MapManager.OnDrawGizmosSelected 里；普通类画不了 gizmo）
    void OnDrawGizmosSelected()
    {
        if (map == null || cellSize <= 0f) return;

        Bounds b = MapManager.GetBounds(map);
        float y = b.max.y + 0.01f;
        Gizmos.color = Color.green;
        for (float x = b.min.x; x <= b.max.x + 0.001f; x += cellSize)
            Gizmos.DrawLine(new Vector3(x, y, b.min.z), new Vector3(x, y, b.max.z));
        for (float z = b.min.z; z <= b.max.z + 0.001f; z += cellSize)
            Gizmos.DrawLine(new Vector3(b.min.x, y, z), new Vector3(b.max.x, y, z));

        if (Map == null) return;      // 占用数据运行中才有
        Gizmos.color = Color.red;
        foreach (var c in Map.Occupied)
            Gizmos.DrawWireCube(Map.CellToWorld(c.x, c.y), new Vector3(cellSize, 0.02f, cellSize));
    }
}
