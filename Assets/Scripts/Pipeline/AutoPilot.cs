using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 自动寻路管线编排，三段可替换：
/// 阶段一 获得目标点（ITargetSource）-> 阶段二 构建行动路径（IPathPlanner）-> 阶段三 执行路径（IPathExecutor）。
/// 三段都在构造时注入依赖，这里只负责装配默认实现并驱动流程；以后交给实体类装配。
/// 只读地图数据（CanEnter），自己不占格子；每一步的合法性仍由 ObjectMover + MapManager 裁决。
/// </summary>
[RequireComponent(typeof(ObjectMover))]
public class AutoPilot : MonoBehaviour
{
    [Tooltip("地图管理器；留空则取场景里的第一个")]
    public MapManager map;

    [Tooltip("移动组件；留空则取自己身上的")]
    public ObjectMover mover;

    [Tooltip("当前路径（格子，不含起点）")]
    public readonly List<Vector2Int> path = new();

    /// <summary>阶段一：获得目标点</summary>
    public ITargetSource TargetSource { get; set; }

    /// <summary>阶段二：构建行动路径</summary>
    public IPathPlanner Planner { get; set; }

    /// <summary>阶段三：执行路径</summary>
    public IPathExecutor Executor { get; set; }

    Coroutine routine;
    Health health;

    public bool IsFollowing => routine != null;

    /// <summary>自己的阵营；没有 Health 就当 0</summary>
    public int Team => health != null ? health.team : 0;

    void Awake()
    {
        if (mover == null) mover = GetComponent<ObjectMover>();
        health = GetComponent<Health>();

        // 三段默认可先建起来（这时 map 可能还是 null）；带地图的真正装配由 Entity.Setup 完成，
        // 所以这里的 ??= 只是"没人装配过"时的兜底，不会覆盖 Entity.Setup 建好的实例。
        TargetSource ??= new ApproachNearestEnemy(map, transform, Team);
        Planner ??= new BfsPathPlanner(map);
        Executor ??= new MoverPathExecutor(map, mover);
    }

    /// <summary>完整管线：阶段一 -> 阶段二 -> 阶段三</summary>
    public bool RunPipeline()
    {
        if (TargetSource == null || !TargetSource.TryGetTarget(out var goal)) return false;
        return MoveTo(goal);
    }

    [ContextMenu("跑一次管线")]
    void RunPipelineMenu() => RunPipeline();

    /// <summary>阶段二 + 阶段三：指定目标格，构建路径并出发</summary>
    public bool MoveTo(Vector2Int target)
    {
        Stop();
        if (map == null || mover == null || Planner == null || Executor == null) return false;

        var start = map.WorldToCell(mover.transform.position);
        if (!Planner.TryBuild(start, target, path)) return false;

        routine = StartCoroutine(Follow());
        return true;
    }

    /// <summary>阶段三的包装：执行完把状态收回来（执行器只管走路，不用操心 AutoPilot 的状态）</summary>
    IEnumerator Follow()
    {
        yield return Executor.Run(path);
        routine = null;
    }

    /// <summary>阶段二 + 阶段三：指定目标点（世界坐标）</summary>
    public bool MoveTo(Vector3 target)
    {
        return map != null && MoveTo(map.WorldToCell(target));
    }

    /// <summary>停下并清空路径</summary>
    public void Stop()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
        path.Clear();
    }

    // 选中时画出当前路径
    void OnDrawGizmosSelected()
    {
        if (map == null || path.Count == 0) return;

        Gizmos.color = Color.yellow;
        var from = map.WorldToCell(transform.position);
        foreach (var c in path)
        {
            Gizmos.DrawLine(map.CellToWorld(from.x, from.y), map.CellToWorld(c.x, c.y));
            from = c;
        }
    }
}
