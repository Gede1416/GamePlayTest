using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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
