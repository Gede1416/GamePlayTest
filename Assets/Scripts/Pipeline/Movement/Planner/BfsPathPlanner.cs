using System.Collections.Generic;
using UnityEngine;
// ============ 阶段二：寻路 ============

/// <summary>寻路交给地图：BFS 实现在 MapManager.FindPath（界外和被占用的格子都不通）。</summary>
public class BfsPathPlanner : IPathPlanner
{
    #region 属性

    readonly MapManager map;

    #endregion

    #region 构造

    public BfsPathPlanner(MapManager map)
    {
        this.map = map;
    }

    #endregion

    #region 公开方法

    /// <summary>寻路就是问地图：把 start -> goal 的最短路径写进 path（不含起点）</summary>
    public bool TryBuild(Vector2Int start, Vector2Int goal, List<Vector2Int> path)
    {
        if (map == null) { path.Clear(); return false; }
        return map.FindPath(start, goal, path);
    }

    #endregion
}
