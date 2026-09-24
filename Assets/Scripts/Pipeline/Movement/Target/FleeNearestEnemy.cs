using System;
using UnityEngine;
/// <summary>远离：在"本回合走得到的格子"里，挑离最近的非己方单位最远的那个当落点。</summary>
public class FleeNearestEnemy : ITargetSource
{
    #region 属性

    readonly MapManager map;
    readonly Transform self;
    readonly int team;
    readonly int stepsLeft;      // 本回合可走步数（问 AutoPilot 要，别的段不拦步数）

    #endregion

    #region 构造

    /// <param name="team">自己的阵营（构造时取一次；阵营会变就重新构造一个）</param>
    /// <param name="stepsLeft">本回合可走步数；返回 0 或负数视为不限（和 Entity.moveSteps 的 0 = 不限一致）</param>
    public FleeNearestEnemy(MapManager map, Transform self, int team, int stepsLeft)
    {
        this.map = map;
        this.self = self;
        this.team = team;
        this.stepsLeft = stepsLeft;
    }

    #endregion

    #region 公开方法

    // ponytail: 每次调用全图扫一遍找候选格；10×10 网格无所谓，格子大了要改成缓存。
    //           候选格用"曼哈顿距离 <= 剩余步数"的菱形筛（地图现在没有障碍数据，这样等价于可达）；
    //           以后有墙/地形了要换成按步数上限做 BFS 洪泛。
    public bool TryGetTarget(out Vector2Int cell)
    {
        cell = default;
        if (map == null || self == null) return false;

        var selfCell = map.WorldToCell(self.position);

        // 这回合还能走几格；不限步数时当成无穷大（退化成全图找最远）
        int budget = stepsLeft;
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

    #endregion

}
