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