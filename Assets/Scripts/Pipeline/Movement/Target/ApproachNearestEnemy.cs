using UnityEngine;
using UnityEngine.UIElements;

// 阶段一（获得目标点）的两个实现。依赖在构造时注入，距离一律用曼哈顿距离（MapManager.Manhattan）。
// "非己方" = 对方的 Health.team 和构造时传进来的 team 不一样。
// 成员顺序：属性 → 构造 → 公开方法 → 私有方法（各组内按调用顺序）。

/// <summary>靠近：走向距离最近的非己方单位，站到它四周离自己最近的那个空格上。</summary>
public class ApproachNearestEnemy : ITargetSource
{
    #region 属性

    readonly MapManager map;
    readonly Transform self;
    readonly int team;
    readonly int stepsLeft;

    static readonly Vector2Int[] Dirs = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };

    #endregion

    #region 构造

    /// <param name="team">自己的阵营（构造时取一次；阵营会变就重新构造一个）</param>
    public ApproachNearestEnemy(MapManager map, Transform self, int team, int stepsLeft)
    {
        this.map = map;
        this.self = self;
        this.team = team;
        this.stepsLeft = stepsLeft;
    }

    #endregion

    #region 公开方法

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

            var h = go.Team;
            if (h == team) continue;                          // 己方（含自己）跳过
            var enemy = map.WorldToCell(go.transform.position);
            int d = MapManager.Manhattan(selfCell, enemy);
            if (d >= nearest) continue;                                         // 没有更近
            if (d <= 1) return false;                                           // 最近的敌人已经贴着了，不用动
            if (!TrySpot(map, selfCell, enemy, out var spot)) continue;         // 它四周站不进去，换下一个
            nearest = d;
            cell = spot;
        }
        int steps = Mathf.Abs(cell.x - selfCell.x) + stepsLeft

        return nearest != int.MaxValue;
    }

    #endregion

    #region 私有方法

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

    #endregion

}
