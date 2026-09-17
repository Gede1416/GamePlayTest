using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 地图管理器（3D 俯视角，y 为高度）：
/// 按地图 GameObject 的长宽（x / z）和单位距离划分 XZ 网格。
/// 地图数据只有一份：**格子→实体 uuid 的二维图**（cells）；占用判定、坐标换算、寻路都从这里问
/// （外部只读，改数据只走 TryMove / SyncOccupied）。uuid→位置 / uuid→实体 的字典暂时不需要，已删。
/// 订阅了死亡消息：单位阵亡就把它的格子放开（见 OnEntityDied）。
/// 成员顺序：属性 → 生命周期 → 公开方法 → 私有方法（各组内按调用顺序）。
/// </summary>
public class MapManager : MonoBehaviour
{
    #region 属性

    [Tooltip("地图物体：取其 Renderer 包围盒的 x / z 尺寸；没有 Renderer 则用它的缩放")]
    public GameObject map;

    [Tooltip("单位距离：每个格子的边长")]
    public float cellSize = 1f;

    /// <summary>格子空着时的 uuid</summary>
    public const int Empty = 0;

    /// <summary>格子被占住、但占用者没有登记 uuid（没挂 Entity 或 uuid <= 0）</summary>
    public const int Unknown = -1;

    [Header("实体")][Tooltip("实体列表（顺序与初始位置列表一一对应）")]
    public List<GameObject> entities = new();

    [Tooltip("初始位置列表（格子坐标），与实体列表一一对应")]
    public List<Vector2Int> spawnPoints = new();

    int[,] cells;      // 二维图：格子 -> 实体 uuid（Empty = 空，Unknown = 占着但没登记 uuid）

    /// <summary>二维图建好了没有（没建之前所有读写都当空）</summary>
    bool HasGrid => cells != null;

    static readonly Vector2Int[] Dirs = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };

    /// <summary>x 方向格子数</summary>
    public int Cols { get; private set; }

    /// <summary>z 方向格子数</summary>
    public int Rows { get; private set; }

    /// <summary>网格角点在地面的坐标 (x, z)</summary>
    public Vector2 MinXZ { get; private set; }

    /// <summary>网格所在高度 = 地图顶面 y</summary>
    public float GridY { get; private set; }

    #endregion

    #region 生命周期

    // 选中物体时在 Scene 视图画出格子线 + 被占用的格子；不需要可整段删掉
    void OnDrawGizmosSelected()
    {
        if (map == null || cellSize <= 0f) return;

        Bounds b = GetBounds(map);
        float y = b.max.y + 0.01f;
        Gizmos.color = Color.green;
        for (float x = b.min.x; x <= b.max.x + 0.001f; x += cellSize)
            Gizmos.DrawLine(new Vector3(x, y, b.min.z), new Vector3(x, y, b.max.z));
        for (float z = b.min.z; z <= b.max.z + 0.001f; z += cellSize)
            Gizmos.DrawLine(new Vector3(b.min.x, y, z), new Vector3(b.max.x, y, z));

        if (!HasGrid) return;           // 地图数据运行中才有
        Gizmos.color = Color.red;
        for (int x = 0; x < Cols; x++)
            for (int z = 0; z < Rows; z++)
                if (UuidAt(x, z) != Empty)
                    Gizmos.DrawWireCube(CellToWorld(x, z), new Vector3(cellSize, 0.02f, cellSize));
    }

    #endregion

    #region 公开方法

    /// <summary>
    /// 初始化：由 BattleManager 调用（地图组件自己不用 Awake / Start）。
    /// 先建网格，再按初始位置列表摆好实体并重建占用数据。
    /// </summary>
    public void Init()
    {
        Build();
        ResetEntities();

        // 先退订再订，重复 Init 也不会订两遍
        EventPipeline.Unsubscribe(BattleEventType.EntityDied, OnEntityDied);
        EventPipeline.Subscribe(BattleEventType.EntityDied, OnEntityDied);
    }

    /// <summary>清理：放掉地图数据与实体列表（由 BattleManager.ClearBattle 调；spawnPoints 是配置，保留）</summary>
    public void Clear()
    {
        EventPipeline.Unsubscribe(BattleEventType.EntityDied, OnEntityDied);

        cells = null;
        entities.Clear();
    }

    /// <summary>按地图物体的包围盒和 cellSize 重新划分网格，并清空二维图（Init 与 SyncOccupied 内部用）</summary>
    void Build()
    {
        if (map == null || cellSize <= 0f) return;

        Bounds b = GetBounds(map);
        MinXZ = new Vector2(b.min.x, b.min.z);
        GridY = b.max.y + 0.5f;
        // ponytail: 向上取整保证整张地图都被覆盖（最后一列/行可能超出地图 ≤ cellSize）；要精确贴合改 Mathf.FloorToInt
        Cols = Mathf.Max(1, Mathf.CeilToInt(b.size.x / cellSize));
        Rows = Mathf.Max(1, Mathf.CeilToInt(b.size.z / cellSize));

        cells = new int[Cols, Rows];
    }

    /// <summary>把所有实体放回各自的初始位置（多余/缺位的实体保持原位），然后重建地图数据（Init 内部用）</summary>
    void ResetEntities()
    {
        for (int i = 0; i < entities.Count && i < spawnPoints.Count; i++)
            if (entities[i] != null) entities[i].transform.position = CellToWorld(spawnPoints[i].x, spawnPoints[i].y);

        SyncOccupied();
    }

    /// <summary>
    /// 按 entities 的当前位置重建二维图（格子→uuid）。
    /// 没登记 uuid 的实体格子照样占住，记 Unknown；uuid 重复只警告，后者跳过。
    /// </summary>
    public void SyncOccupied()
    {
        if (!HasGrid) Build();
        if (!HasGrid) return;

        ClearCells();

        var placed = new Dictionary<int, string>();     // 本次已经摆下的 uuid -> 物体名，只用来查重

        foreach (var go in entities)
        {
            if (go == null) continue;

            var entity = go.GetComponent<Entity>();
            var cell = WorldToCell(go.transform.position);

            if (!InBounds(cell))
            {
                Debug.LogWarning($"[MapManager] {go.name} 的位置 {cell} 不在网格内，没记进地图数据", go);
                continue;
            }

            if (entity == null || entity.Uuid <= Empty)
            {
                SetUuid(cell, Unknown);
                Debug.LogWarning($"[MapManager] {go.name} 没有 Entity 或 uuid <= 0，格子占用记 Unknown（索引里查不到它）", go);
                continue;
            }

            if (placed.TryGetValue(entity.Uuid, out var other))
            {
                Debug.LogWarning($"[MapManager] uuid {entity.Uuid} 重复：{go.name} 与 {other}，后者这次被跳过", go);
                continue;
            }

            SetUuid(cell, entity.Uuid);
            placed[entity.Uuid] = go.name;
        }
    }

    /// <summary>
    /// 放开一个单位占的格子，并把它从实体列表里摘掉（收到死亡消息时走这里，物体本身由上级销毁）。
    /// 按它当前所在的格子清：登记过 uuid 的要和二维图对得上才清，占着但没登记的（Unknown）也清；
    /// 摘掉之后它不再是目标、也不再挡路，下次 SyncOccupied 也不会把它算回来。
    /// </summary>
    void ReleaseEntity(Entity entity)
    {
        if (entity == null || !HasGrid) return;

        var cell = WorldToCell(entity.transform.position);
        if (!InBounds(cell)) return;

        int id = UuidAt(cell);
        if (id == entity.Uuid || id == Unknown) SetUuid(cell, Empty);

        entities.Remove(entity.gameObject);
    }

    #region 查询

    /// <summary>可进入 = 在网格内 且 没被占（没登记 uuid 的也算占用）</summary>
    public bool CanEnter(Vector2Int cell) => InBounds(cell) && UuidAt(cell) == Empty;

    /// <summary>两格之间的曼哈顿距离（两个分量差的绝对值之和）</summary>
    public static int Manhattan(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    /// <summary>格子 (col,row) 中心的世界坐标，y 取网格高度</summary>
    public Vector3 CellToWorld(int col, int row)
    {
        return new Vector3(MinXZ.x + (col + 0.5f) * cellSize, GridY, MinXZ.y + (row + 0.5f) * cellSize);
    }

    /// <summary>格子中心的世界坐标</summary>
    public Vector3 CellToWorld(Vector2Int cell) => CellToWorld(cell.x, cell.y);

    /// <summary>世界坐标落在哪个格子（只看 x / z，忽略高度）；超出范围用 InBounds 判断</summary>
    public Vector2Int WorldToCell(Vector3 pos)
    {
        return new Vector2Int(Mathf.FloorToInt((pos.x - MinXZ.x) / cellSize),
                              Mathf.FloorToInt((pos.z - MinXZ.y) / cellSize));
    }

    #endregion

    #region 移动裁决

    /// <summary>
    /// 移动指令裁决：从 from 沿 step 走一格。合法（界内且落点未被占用）就把二维图里的 uuid 从 from 挪到落点，
    /// 并用 to 返回落点格；不合法返回 false 且不动任何数据。
    /// </summary>
    public bool TryMove(Vector2Int from, Vector2Int step, out Vector2Int to)
    {
        to = from + step;
        if (!HasGrid || !InBounds(from) || !CanEnter(to)) return false;

        int id = UuidAt(from);
        if (id == Empty) id = Unknown;      // 没登记的移动者：占住落点，但不进索引

        SetUuid(from, Empty);
        SetUuid(to, id);
        return true;
    }

    #endregion

    #region 寻路

    /// <summary>
    /// 寻路：从 from 到 to 的最短路径（四方向、每格等权 BFS），结果写进 path（不含起点）。
    /// from == to 时 path 清空并返回 true（已经站在目标格上）；目标进不去或走不到返回 false。
    /// </summary>
    public bool FindPath(Vector2Int from, Vector2Int to, List<Vector2Int> path)
    {
        path.Clear();
        if (!HasGrid) return false;
        if (from == to) return true;             // 已经在目标格上，空路径也算成功
        if (!CanEnter(to)) return false;         // 目标进不去（界外或被占）

        var cameFrom = new Dictionary<Vector2Int, Vector2Int> { [from] = from };
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(from);

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            foreach (var dir in Dirs)
            {
                var next = cur + dir;
                if (cameFrom.ContainsKey(next) || !CanEnter(next)) continue;
                cameFrom[next] = cur;

                if (next != to)
                {
                    queue.Enqueue(next);
                    continue;
                }

                for (var c = to; c != from; c = cameFrom[c]) path.Add(c);   // 回溯，反着走回起点
                path.Reverse();
                return true;
            }
        }
        return false;                            // 走不到
    }

    #endregion

    #region 自检（右键组件菜单可跑）

    /// <summary>自检：随便挑两个空格走一遍寻路，校验路径连续、可走、终点对得上（右键组件菜单跑）</summary>
    [ContextMenu("自检：寻路")]
    void SelfCheckPath()
    {
        if (!HasGrid) { Debug.LogWarning("[MapManager] 还没 Build，没有地图数据可查"); return; }

        var free = new List<Vector2Int>();
        for (int x = 0; x < Cols; x++)
            for (int z = 0; z < Rows; z++)
                if (UuidAt(x, z) == Empty) free.Add(new Vector2Int(x, z));

        if (free.Count < 2) { Debug.LogWarning("[MapManager] 空格子不足两个，没得测"); return; }

        var from = free[0];
        var to = free[free.Count - 1];
        var path = new List<Vector2Int>();

        if (!FindPath(from, to, path)) { Debug.LogWarning($"[MapManager] 寻路自检：{from} -> {to} 没找到路径（地图被堵死了？）", this); return; }

        var prev = from;
        foreach (var cell in path)
        {
            if (!CanEnter(cell) || Manhattan(prev, cell) != 1)
            {
                Debug.LogError($"[MapManager] 寻路自检不通过：{prev} -> {cell} 不是相邻的可走格", this);
                return;
            }
            prev = cell;
        }

        Debug.Log(prev == to
            ? $"[MapManager] 寻路自检通过：{from} -> {to}，{path.Count} 步"
            : $"[MapManager] 寻路自检不通过：终点是 {prev}，应该是 {to}", this);
    }

    /// <summary>自检：二维图跟实体列表对不对得上——每个登记了 uuid 的单位应该恰好占一格（右键组件菜单跑）</summary>
    [ContextMenu("自检：地图数据一致性")]
    void SelfCheck()
    {
        if (!HasGrid) { Debug.LogWarning("[MapManager] 还没 Build，没有地图数据可查"); return; }

        var times = new Dictionary<int, int>();     // uuid 在二维图里出现了几次
        int occupied = 0, known = 0;
        for (int x = 0; x < Cols; x++)
            for (int z = 0; z < Rows; z++)
            {
                int id = UuidAt(x, z);
                if (id == Empty) continue;

                occupied++;
                if (id <= Empty) continue;          // Unknown：占着但没登记 uuid

                known++;
                times.TryGetValue(id, out var n);
                times[id] = n + 1;
            }

        bool ok = true;
        foreach (var go in entities)
        {
            if (go == null) continue;

            var entity = go.GetComponent<Entity>();
            if (entity == null || entity.Uuid <= Empty) continue;

            times.TryGetValue(entity.Uuid, out var n);
            if (n == 1) continue;

            ok = false;
            Debug.LogError($"[MapManager] uuid {entity.Uuid}（{go.name}）在二维图里出现 {n} 次，应该恰好 1 次", go);
        }

        Debug.Log(ok
            ? $"[MapManager] 自检通过：占格 {occupied}（登记 uuid {known}），实体列表 {entities.Count} 个"
            : $"[MapManager] 自检不通过：占格 {occupied}，登记 {known}，实体列表 {entities.Count} 个", this);
    }

    #endregion

    #endregion

    #region 私有方法

    /// <summary>收到死亡消息：把阵亡单位从地图登记里摘掉，放开它占的格子</summary>
    void OnEntityDied(BattleEvent e) => ReleaseEntity(e.entity);

    #region 二维图存取

    /// <summary>读某格的 uuid（列行版本，遍历时用）：没图 / 界外都当空</summary>
    int UuidAt(int col, int row)
    {
        return HasGrid && col >= 0 && col < Cols && row >= 0 && row < Rows ? cells[col, row] : Empty;
    }

    /// <summary>读某格的 uuid：空 = Empty(0)，占用但没登记 = Unknown(-1)</summary>
    int UuidAt(Vector2Int cell) => UuidAt(cell.x, cell.y);

    /// <summary>写某格的 uuid（界外不写）</summary>
    void SetUuid(Vector2Int cell, int uuid)
    {
        if (HasGrid && InBounds(cell)) cells[cell.x, cell.y] = uuid;
    }

    /// <summary>整张二维图清空（全部置 Empty）</summary>
    void ClearCells()
    {
        if (HasGrid) System.Array.Clear(cells, 0, cells.Length);
    }

    /// <summary>格子坐标是否在网格内（Vector2Int 的 x = 列，y = 行，不是世界高度）</summary>
    bool InBounds(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < Cols && cell.y >= 0 && cell.y < Rows;
    }

    #endregion

    /// <summary>物体的地面包围盒：优先取 Renderer，没有就退化成位置 + 缩放</summary>
    static Bounds GetBounds(GameObject go)
    {
        var r = go.GetComponentInChildren<Renderer>();
        return r != null ? r.bounds : new Bounds(go.transform.position, go.transform.lossyScale);
    }

    #endregion

}
