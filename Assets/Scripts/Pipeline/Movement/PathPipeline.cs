using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ============ 管线三段接口 ============
// 阶段一：获得目标点 -> 阶段二：构建行动路径 -> 阶段三：执行路径
// 各段需要的地图 / 自己 / 阵营等数据一律由构造函数注入，接口本身只收"这次要处理什么"。

/// <summary>阶段一：获得目标点，决定"这次要去哪一格"。</summary>
public interface ITargetSource
{
    /// <summary>给出目标格；返回 false 表示这次没有目标，管线到此为止</summary>
    bool TryGetTarget(out Vector2Int cell);
}

/// <summary>阶段二：构建行动路径。起点格 -> 目标格，结果写进 path（不含起点）。</summary>
public interface IPathPlanner
{
    bool TryBuild(Vector2Int start, Vector2Int goal, List<Vector2Int> path);
}

/// <summary>阶段三：执行路径。沿 path 逐格把物体挪过去。</summary>
public interface IPathExecutor
{
    IEnumerator Run(List<Vector2Int> path);
}


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


// ============ 阶段三：执行路径 ============

/// <summary>
/// 阶段三：直接控制实体位置——沿路径逐格插值走过去。
/// 只负责"挪位置"：落点与占用数据一律问 MapManager（TryMove 裁决并维护），
/// 步数之类的行动约束由前两段决定，这里不再拦。
/// </summary>
public class MoverPathExecutor : IPathExecutor
{
    #region 属性

    readonly MapManager map;
    readonly Transform self;
    readonly float speed;

    #endregion

    #region 构造

    /// <param name="self">要挪的物体（实体自己的 Transform）</param>
    /// <param name="speed">移动速度（世界单位/秒）；走一格用时 = 地图格子边长 / speed</param>
    public MoverPathExecutor(MapManager map, Transform self, float speed)
    {
        this.map = map;
        this.self = self;
        this.speed = speed;
    }

    #endregion

    #region 公开方法

    /// <summary>沿路径逐格插值移动：每格先问地图要落点（TryMove 裁决并维护占用），说不合法就停</summary>
    public IEnumerator Run(List<Vector2Int> path)
    {
        if (map == null || self == null || speed <= 0f) yield break;

        for (int i = 0; i < path.Count; i++)            // 下标遍历：外部 Stop() 清空 path 也不会炸
        {
            var from = map.WorldToCell(self.position);
            if (!map.TryMove(from, path[i] - from, out var to)) yield break;   // 地图说不合法就放弃

            Vector3 start = self.position;
            Vector3 target = map.CellToWorld(to.x, to.y);
            target.y = start.y;                                             // 高度不变，只走平面
            float dur = map.cellSize / speed;

            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                self.position = Vector3.Lerp(start, target, t / dur);        // 想缓动改 Vector3.SmoothStep
                yield return null;
            }

            self.position = target;
        }
    }

    #endregion
}
