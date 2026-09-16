using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 地图管理器（3D 俯视角，y 为高度）：
/// 按地图 GameObject 的长宽（x / z）和指定单位距离划分 XZ 平面网格，并管理实体的初始位置。
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
    [Tooltip("实体列表")]
    public List<GameObject> entities = new();

    [Tooltip("初始位置列表，与实体列表一一对应；直接写世界坐标，y 就是高度")]
    public List<Vector2Int> spawnPoints = new();

    readonly HashSet<Vector2Int> occupied = new();     // 格子占用数据：只由本脚本改，外部只读

    void Awake() => Build();

    void Start() => ResetEntities();

    public void Build()
    {
        if (map == null || cellSize <= 0f) return;

        Bounds b = GetBounds(map);
        MinXZ = new Vector2(b.min.x, b.min.z);
        GridY = b.max.y + 0.5f;
        // ponytail: 向上取整保证整张地图都被覆盖（最后一列/行可能超出地图 ≤ cellSize）；要精确贴合改 Mathf.FloorToInt
        Cols = Mathf.Max(1, Mathf.CeilToInt(b.size.x / cellSize));
        Rows = Mathf.Max(1, Mathf.CeilToInt(b.size.z / cellSize));
    }

    /// <summary>把所有实体放回各自的初始位置（多余/缺位的实体保持原位）</summary>
    public void ResetEntities()
    {
        for (int i = 0; i < entities.Count && i < spawnPoints.Count; i++)
            if (entities[i] != null) entities[i].transform.position = CellToWorld(spawnPoints[i].x, spawnPoints[i].y);

        SyncOccupied();
    }

    /// <summary>按 entities 的当前位置重建占用数据；实体被搬动/销毁后调它对齐</summary>
    public void SyncOccupied()
    {
        occupied.Clear();
        foreach (var e in entities)
            if (e != null) occupied.Add(WorldToCell(e.transform.position));
    }

    /// <summary>格子 (col,row) 中心的世界坐标，y 取网格高度</summary>
    public Vector3 CellToWorld(int col, int row)
    {
        return new Vector3(MinXZ.x + (col + 0.5f) * cellSize, GridY, MinXZ.y + (row + 0.5f) * cellSize);
    }

    /// <summary>世界坐标落在哪个格子（只看 x / z，忽略高度）；超出范围用 InBounds 判断</summary>
    public Vector2Int WorldToCell(Vector3 pos)
    {
        return new Vector2Int(Mathf.FloorToInt((pos.x - MinXZ.x) / cellSize),
                              Mathf.FloorToInt((pos.z - MinXZ.y) / cellSize));
    }

    /// <summary>格子坐标是否在网格内（Vector2Int 的 x = 列，y = 行，不是世界高度）</summary>
    public bool InBounds(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < Cols && cell.y >= 0 && cell.y < Rows;
    }

    /// <summary>格子是否已被占用（只读查询）</summary>
    public bool IsOccupied(Vector2Int cell) => occupied.Contains(cell);

    /// <summary>可进入 = 在网格内 且 未被占用（只读查询）</summary>
    public bool CanEnter(Vector2Int cell) => InBounds(cell) && !IsOccupied(cell);

    /// <summary>两格之间的曼哈顿距离（两个分量差的绝对值之和）</summary>
    public static int Manhattan(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    /// <summary>
    /// 移动指令裁决：从 from 沿 step 走一格。合法（界内且落点未被占用）就把占用数据从 from 移到落点，
    /// 并用 to 返回落点格；不合法返回 false 且不动任何数据。占用数据只有这里和 CancelMove 能改。
    /// </summary>
    public bool TryMove(Vector2Int from, Vector2Int step, out Vector2Int to)
    {
        to = from + step;
        if (!CanEnter(to)) return false;
        occupied.Remove(from);
        occupied.Add(to);
        return true;
    }

    /// <summary>移动没走到落点（被中断）：占用数据退回起点</summary>
    public void CancelMove(Vector2Int from, Vector2Int to)
    {
        occupied.Remove(to);
        occupied.Add(from);
    }

    static Bounds GetBounds(GameObject go)
    {
        var r = go.GetComponentInChildren<Renderer>();
        return r != null ? r.bounds : new Bounds(go.transform.position, go.transform.lossyScale);
    }

    // 选中物体时在 Scene 视图画出格子线，用来核对划分结果；不需要可整段删掉
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

        Gizmos.color = Color.red;      // 已被占用的格子（运行中才有）
        foreach (var c in occupied)
            Gizmos.DrawWireCube(CellToWorld(c.x, c.y), new Vector3(cellSize, 0.02f, cellSize));
    }
}
