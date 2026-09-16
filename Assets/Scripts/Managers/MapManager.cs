using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 地图管理器（**普通类**，由 InitManager 构建；3D 俯视角，y 为高度）：
/// 按地图 GameObject 的长宽（x / z）和单位距离划分 XZ 平面网格，管理实体初始位置与格子占用。
/// </summary>
public class MapManager
{
    /// <summary>单位距离：每个格子的边长（在 Init 之前设置）</summary>
    public float cellSize = 1f;

    /// <summary>地图物体：取其 Renderer 包围盒的 x / z 尺寸；没有 Renderer 则用它的缩放</summary>
    public GameObject map;

    public int Cols { get; private set; }        // x 方向格子数
    public int Rows { get; private set; }        // z 方向格子数
    public Vector2 MinXZ { get; private set; }   // 网格角点在地面的坐标 (x, z)
    public float GridY { get; private set; }     // 网格所在高度

    /// <summary>实体列表（Init 时给）</summary>
    public readonly List<GameObject> entities = new();

    /// <summary>初始位置列表（格子坐标），与 entities 一一对应</summary>
    public readonly List<Vector2Int> spawnPoints = new();

    /// <summary>已被占用的格子（只读，给 Gizmos 看）</summary>
    public IEnumerable<Vector2Int> Occupied => occupied;

    readonly HashSet<Vector2Int> occupied = new();     // 格子占用数据：只由本类改，外部只读

    /// <summary>构建地图：记下实体与初始位置，划分网格，并把实体放回各自的初始位置</summary>
    public void Init(List<GameObject> entities, List<Vector2Int> spawnPoints, GameObject map)
    {
        this.map = map;

        this.entities.Clear();
        if (entities != null) this.entities.AddRange(entities);

        this.spawnPoints.Clear();
        if (spawnPoints != null) this.spawnPoints.AddRange(spawnPoints);

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

    /// <summary>地图物体的包围盒（没有 Renderer 就用它的位置与缩放）</summary>
    public static Bounds GetBounds(GameObject go)
    {
        var r = go.GetComponentInChildren<Renderer>();
        return r != null ? r.bounds : new Bounds(go.transform.position, go.transform.lossyScale);
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
}
