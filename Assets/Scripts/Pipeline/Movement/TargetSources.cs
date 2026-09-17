using System;
using UnityEngine;

// 阶段一（获得目标点）的两个实现。依赖在构造时注入，距离一律用曼哈顿距离（MapManager.Manhattan）。
// "非己方" = 对方的 Health.team 和构造时传进来的 team 不一样。

/// <summary>靠近：走向距离最近的非己方单位，站到它四周离自己最近的那个空格上。</summary>
public class ApproachNearestEnemy : ITargetSource
{
    readonly MapManager map;
    readonly Transform self;
    readonly int team;

    /// <param name="team">自己的阵营（构造时取一次；阵营会变就重新构造一个）</param>
    public ApproachNearestEnemy(MapManager map, Transform self, int team)
    {
        this.map = map;
        this.self = self;
        this.team = team;
    }

    public bool TryGetTarget(out Vector2Int cell)
    {
        cell = default;
        if (map == null || self == null) return false;

        var selfCell = map.WorldToCell(self.position);
        int nearest = int.MaxValue;

        // 敌人从地图的实体列表里找（map 就是单位注册表）
        foreach (var go in map.entities)
        {
            if (go == null) continue;

            var h = go.GetComponent<Health>();
            if (h == null || h.team == team) continue;                          // 己方（含自己）跳过
            var enemy = map.WorldToCell(go.transform.position);
            int d = MapManager.Manhattan(selfCell, enemy);
            if (d >= nearest) continue;                                         // 没有更近
            if (d <= 1) return false;                                           // 最近的敌人已经贴着了，不用动
            if (!TrySpot(map, selfCell, enemy, out var spot)) continue;         // 它四周站不进去，换下一个
            nearest = d;
            cell = spot;
        }

        return nearest != int.MaxValue;
    }

    // ---------- 私有 ----------

    static readonly Vector2Int[] Dirs = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };

    /// <summary>敌人格子四周里，离自己最近的一个可进入格子</summary>
    static bool TrySpot(MapManager map, Vector2Int selfCell, Vector2Int enemy, out Vector2Int spot)
    {
        spot = default;
        int best = int.MaxValue;

        foreach (var dir in Dirs)
        {
            var c = enemy + dir;
            if (!map.CanEnter(c)) continue;
            int d = MapManager.Manhattan(selfCell, c);
            if (d < best)
            {
                best = d;
                spot = c;
            }
        }

        return best != int.MaxValue;
    }
}


/// <summary>远离：在"本回合走得到的格子"里，挑离最近的非己方单位最远的那个当落点。</summary>
public class FleeNearestEnemy : ITargetSource
{
    readonly MapManager map;
    readonly Transform self;
    readonly int team;
    readonly Func<int> stepsLeft;      // 本回合可走步数（问 AutoPilot 要，别的段不拦步数）

    /// <param name="team">自己的阵营（构造时取一次；阵营会变就重新构造一个）</param>
    /// <param name="stepsLeft">本回合可走步数；返回 0 或负数视为不限（和 Entity.moveSteps 的 0 = 不限一致）</param>
    public FleeNearestEnemy(MapManager map, Transform self, int team, Func<int> stepsLeft)
    {
        this.map = map;
        this.self = self;
        this.team = team;
        this.stepsLeft = stepsLeft;
    }

    // ponytail: 每次调用全图扫一遍找候选格；10×10 网格无所谓，格子大了要改成缓存。
    //           候选格用"曼哈顿距离 <= 剩余步数"的菱形筛（地图现在没有障碍数据，这样等价于可达）；
    //           以后有墙/地形了要换成按步数上限做 BFS 洪泛。
    public bool TryGetTarget(out Vector2Int cell)
    {
        cell = default;
        if (map == null || self == null) return false;

        var selfCell = map.WorldToCell(self.position);

        // 这回合还能走几格；不限步数时当成无穷大（退化成全图找最远）
        int budget = stepsLeft != null ? stepsLeft() : 0;
        if (budget <= 0) budget = int.MaxValue;

        int nearest = int.MaxValue;
        var enemy = selfCell;

        // 敌人从地图的实体列表里找（map 就是单位注册表）
        foreach (var go in map.entities)
        {
            if (go == null) continue;

            var h = go.GetComponent<Health>();
            if (h == null || h.team == team) continue;

            var e = map.WorldToCell(go.transform.position);
            int d = MapManager.Manhattan(selfCell, e);
            if (d < nearest)
            {
                nearest = d;
                enemy = e;
            }
        }
        if (nearest == int.MaxValue) return false;          // 场上看不到敌方，不动

        int bestAway = nearest;            // 至少要离得更远，否则这次没有目标
        int bestTravel = int.MaxValue;

        for (int x = 0; x < map.Cols; x++)
            for (int y = 0; y < map.Rows; y++)
            {
                var c = new Vector2Int(x, y);
                if (!map.CanEnter(c)) continue;             // 界外 / 被占

                int travel = MapManager.Manhattan(selfCell, c);
                if (travel == 0 || travel > budget) continue;   // 这回合走不到（原地也不算）

                int away = MapManager.Manhattan(c, enemy);
                if (away > bestAway || (away == bestAway && travel < bestTravel))
                {
                    bestAway = away;
                    bestTravel = travel;
                    cell = c;
                }
            }

        return bestTravel != int.MaxValue;
    }
}
