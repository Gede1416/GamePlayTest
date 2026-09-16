using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 地图管理器（3D 俯视角，y 为高度）：
/// 按地图 GameObject 的长宽（x / z）和单位距离划分 XZ 网格。
/// 地图数据有两份索引：**格子→实体 uuid 的二维图**（cells），以及 **uuid→二维位置 / uuid→实体** 的字典；
/// 占用判定、坐标换算、以后寻路都从这里问（外部只读，改数据只走 TryMove / CancelMove / SyncOccupied）。
/// </summary>
public class MapManager : MonoBehaviour
{
    [Tooltip("地图物体：取其 Renderer 包围盒的 x / z 尺寸；没有 Renderer 则用它的缩放")]
    public GameObject map;

    [Tooltip("单位距离：每个格子的边长")]
    public float cellSize = 1f;

    public int Cols { get; private set; }        // x 方向格子数
    public int Rows { get; private set; }        // z 方向格子数
    public Vector2 MinXZ { get; private set; }   // 网格角点在地面的坐标 (x, z)
    public float GridY { get; private set; }     // 网格所在高度 = 地图顶面 y

    [Header("实体")]
    [Tooltip("实体列表（顺序与初始位置列表一一对应）")]
    public List<GameObject> entities = new();

    [Tooltip("初始位置列表（格子坐标），与实体列表一一对应")]
    public List<Vector2Int> spawnPoints = new();

    /// <summary>格子空着时的 uuid</summary>
    public const int Empty = 0;

    /// <summary>格子被占住、但占用者没有登记 uuid（没挂 Entity 或 uuid <= 0）</summary>
    public const int Unknown = -1;

    // ---------- 地图数据 ----------
    int[,] cells;                                              // 二维图：格子 -> 实体 uuid
    readonly Dictionary<int, Vector2Int> positions = new();    // uuid -> 二维位置
    readonly Dictionary<int, Entity> byUuid = new();           // uuid -> 实体

    void Awake() => Build();

    void Start() => ResetEntities();

    /// <summary>重新构建网格并放好实体（外部想从头来一遍时调它）</summary>
    public void Init()
    {
        Build();
        ResetEntities();
    }

    public void Build()
    {
        if (map == null || cellSize <= 0f) return;

        Bounds b = GetBounds(map);
        MinXZ = new Vector2(b.min.x, b.min.z);
        GridY = b.max.y + 0.5f;
        // ponytail: 向上取整保证整张地图都被覆盖（最后一列/行可能超出地图 ≤ cellSize）；要精确贴合改 Mathf.FloorToInt
        Cols = Mathf.Max(1, Mathf.CeilToInt(b.size.x / cellSize));
        Rows = Mathf.Max(1, Mathf.CeilToInt(b.size.z / cellSize));

        cells = new int[Cols, Rows];
        positions.Clear();
        byUuid.Clear();
    }

    /// <summary>把所有实体放回各自的初始位置（多余/缺位的实体保持原位），然后重建地图数据</summary>
    public void ResetEntities()
    {
        for (int i = 0; i < entities.Count && i < spawnPoints.Count; i++)
            if (entities[i] != null) entities[i].transform.position = CellToWorld(spawnPoints[i].x, spawnPoints[i].y);

        SyncOccupied();
    }

    /// <summary>
    /// 按 entities 的当前位置重建地图数据（格子→uuid、uuid→位置、uuid→实体）。
    /// 没登记 uuid 的实体格子照样占住，记 Unknown；uuid 重复只警告，后者跳过。
    /// </summary>
    public void SyncOccupied()
    {
        if (cells == null) Build();
        if (cells == null) return;

        System.Array.Clear(cells, 0, cells.Length);
        positions.Clear();
        byUuid.Clear();

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
                cells[cell.x, cell.y] = Unknown;
                Debug.LogWarning($"[MapManager] {go.name} 没有 Entity 或 uuid <= 0，格子占用记 Unknown（索引里查不到它）", go);
                continue;
            }

            if (byUuid.ContainsKey(entity.Uuid))
            {
                Debug.LogWarning($"[MapManager] uuid {entity.Uuid} 重复：{go.name} 与 {byUuid[entity.Uuid].name}，后者这次被跳过", go);
                continue;
            }

            cells[cell.x, cell.y] = entity.Uuid;
            positions[entity.Uuid] = cell;
            byUuid[entity.Uuid] = entity;
        }
    }

    // ---------- 查询 ----------

    /// <summary>格子里的实体 uuid：空 = Empty(0)，占用但没登记 = Unknown(-1)</summary>
    public int UuidAt(Vector2Int cell)
    {
        if (cells == null || !InBounds(cell)) return Empty;
        return cells[cell.x, cell.y];
    }

    /// <summary>格子里的实体；空 / Unknown 都返回 null</summary>
    public Entity EntityAt(Vector2Int cell)
    {
        int id = UuidAt(cell);
        return id > Empty ? EntityOf(id) : null;
    }

    /// <summary>一批格子里的实体（跳过空格与 Unknown）</summary>
    public List<Entity> EntitiesAt(IEnumerable<Vector2Int> list)
    {
        var result = new List<Entity>();
        if (list == null) return result;

        foreach (var cell in list)
        {
            var e = EntityAt(cell);
            if (e != null) result.Add(e);
        }
        return result;
    }

    /// <summary>实体 uuid 对应的实体；没有返回 null</summary>
    public Entity EntityOf(int uuid)
    {
        return byUuid.TryGetValue(uuid, out var e) ? e : null;
    }

    /// <summary>实体 uuid 当前所在的格子</summary>
    public bool TryGetCell(int uuid, out Vector2Int cell)
    {
        return positions.TryGetValue(uuid, out cell);
    }

    /// <summary>格子是否已被占用（没登记 uuid 的也算占用）</summary>
    public bool IsOccupied(Vector2Int cell) => UuidAt(cell) != Empty;

    /// <summary>可进入 = 在网格内 且 未被占用</summary>
    public bool CanEnter(Vector2Int cell) => InBounds(cell) && !IsOccupied(cell);

    /// <summary>格子坐标是否在网格内（Vector2Int 的 x = 列，y = 行，不是世界高度）</summary>
    public bool InBounds(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < Cols && cell.y >= 0 && cell.y < Rows;
    }

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

    static Bounds GetBounds(GameObject go)
    {
        var r = go.GetComponentInChildren<Renderer>();
        return r != null ? r.bounds : new Bounds(go.transform.position, go.transform.lossyScale);
    }

    /// <summary>
    /// 移动指令裁决：从 from 沿 step 走一格。合法（界内且落点未被占用）就把地图数据从 from 移到落点
    /// （二维图 + uuid→位置），并用 to 返回落点格；不合法返回 false 且不动任何数据。
    /// </summary>
    public bool TryMove(Vector2Int from, Vector2Int step, out Vector2Int to)
    {
        to = from + step;
        if (cells == null || !InBounds(from) || !CanEnter(to)) return false;

        int id = cells[from.x, from.y];
        if (id == Empty) id = Unknown;      // 没登记的移动者：占住落点，但不进索引

        cells[from.x, from.y] = Empty;
        cells[to.x, to.y] = id;
        if (id > Empty) positions[id] = to;
        return true;
    }

    /// <summary>移动没走到落点（被中断）：地图数据退回起点</summary>
    public void CancelMove(Vector2Int from, Vector2Int to)
    {
        if (cells == null || !InBounds(from) || !InBounds(to)) return;

        int id = cells[to.x, to.y];
        cells[to.x, to.y] = Empty;
        cells[from.x, from.y] = id;
        if (id > Empty) positions[id] = from;
    }

    // ---------- 寻路 ----------

    static readonly Vector2Int[] Dirs = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };

    /// <summary>
    /// 寻路：从 from 到 to 的最短路径（四方向、每格等权 BFS），结果写进 path（不含起点）。
    /// from == to 时 path 清空并返回 true（已经站在目标格上）；目标进不去或走不到返回 false。
    /// </summary>
    public bool FindPath(Vector2Int from, Vector2Int to, List<Vector2Int> path)
    {
        path.Clear();
        if (cells == null) return false;
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

    /// <summary>自检：随便挑两个空格走一遍寻路，校验路径连续、可走、终点对得上（右键组件菜单可跑）</summary>
    [ContextMenu("自检：寻路")]
    public void SelfCheckPath()
    {
        if (cells == null) { Debug.LogWarning("[MapManager] 还没 Build，没有地图数据可查"); return; }

        var free = new List<Vector2Int>();
        for (int x = 0; x < Cols; x++)
            for (int z = 0; z < Rows; z++)
                if (cells[x, z] == Empty) free.Add(new Vector2Int(x, z));

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

    /// <summary>自检：二维图与 uuid→位置/实体 两份索引是否一致（右键组件菜单可跑）</summary>
    [ContextMenu("自检：地图数据一致性")]
    public void SelfCheck()
    {
        if (cells == null) { Debug.LogWarning("[MapManager] 还没 Build，没有地图数据可查"); return; }

        int occupied = 0, known = 0;
        for (int x = 0; x < Cols; x++)
            for (int z = 0; z < Rows; z++)
                if (cells[x, z] != Empty)
                {
                    occupied++;
                    if (cells[x, z] > Empty) known++;
                }

        bool ok = known == positions.Count && known == byUuid.Count;
        foreach (var pair in positions)
        {
            var cell = pair.Value;
            if (InBounds(cell) && cells[cell.x, cell.y] == pair.Key) continue;

            ok = false;
            Debug.LogError($"[MapManager] uuid {pair.Key} 记的位置 {cell} 与二维图对不上", this);
        }

        Debug.Log(ok
            ? $"[MapManager] 自检通过：占格 {occupied}（登记 uuid {known}），positions/byUuid = {positions.Count}/{byUuid.Count}"
            : $"[MapManager] 自检不通过：占格 {occupied}，登记 {known}，positions {positions.Count}，byUuid {byUuid.Count}", this);
    }

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

        if (cells == null) return;      // 地图数据运行中才有
        Gizmos.color = Color.red;
        for (int x = 0; x < Cols; x++)
            for (int z = 0; z < Rows; z++)
                if (cells[x, z] != Empty)
                    Gizmos.DrawWireCube(CellToWorld(x, z), new Vector3(cellSize, 0.02f, cellSize));
    }
}
