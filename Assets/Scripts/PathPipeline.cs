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


// ============ 阶段二：BFS 寻路 ============

/// <summary>BFS（四方向、每格等权）：界外和被占用的格子都不通。</summary>
public class BfsPathPlanner : IPathPlanner
{
    readonly MapManager map;

    public BfsPathPlanner(MapManager map)
    {
        this.map = map;
    }

    static readonly Vector2Int[] Dirs = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };

    public bool TryBuild(Vector2Int start, Vector2Int goal, List<Vector2Int> path)
    {
        path.Clear();
        if (map == null) return false;
        if (start == goal) return true;              // 已经站在目标格上，空路径也算成功
        if (!map.CanEnter(goal)) return false;       // 目标进不去（界外或被占）

        var cameFrom = new Dictionary<Vector2Int, Vector2Int> { [start] = start };
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            foreach (var dir in Dirs)
            {
                var next = cur + dir;
                if (cameFrom.ContainsKey(next) || !map.CanEnter(next)) continue;
                cameFrom[next] = cur;

                if (next != goal)
                {
                    queue.Enqueue(next);
                    continue;
                }

                for (var c = goal; c != start; c = cameFrom[c]) path.Add(c);   // 回溯，反着走回起点
                path.Reverse();
                return true;
            }
        }
        return false;                                 // 走不到
    }
}


// ============ 阶段三：用现有移动组件执行 ============

/// <summary>交给 ObjectMover 逐格走：等上一次动画走完再下一条指令。</summary>
public class MoverPathExecutor : IPathExecutor
{
    readonly MapManager map;
    readonly ObjectMover mover;

    public MoverPathExecutor(MapManager map, ObjectMover mover)
    {
        this.map = map;
        this.mover = mover;
    }

    public IEnumerator Run(List<Vector2Int> path)
    {
        if (map == null || mover == null) yield break;

        var prev = map.WorldToCell(mover.transform.position);

        for (int i = 0; i < path.Count; i++)            // 下标遍历：外部 Stop() 清空 path 也不会炸
        {
            while (mover.IsMoving) yield return null;   // 动画中移动脚本会拒指令，等它走完
            if (!map.CanEnter(path[i]) || !mover.Move(path[i] - prev)) yield break;   // 被挡/被拒就放弃
            prev = path[i];
        }
    }
}
