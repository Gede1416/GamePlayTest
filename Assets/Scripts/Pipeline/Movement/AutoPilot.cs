using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 自动寻路管线编排（挂在实体上），三段可替换：
/// 阶段一 获得目标点（ITargetSource）-> 阶段二 构建行动路径（IPathPlanner）-> 阶段三 执行路径（IPathExecutor）。
/// 装配由本组件自己做（Build() 调 PathPipelineFactory，按 TargetSourceType 枚举造阶段一），和攻击管线的 Attacker 一个套路。
/// 只读地图数据（CanEnter），自己不占格子；每一步的合法性仍由 ObjectMover + MapManager 裁决。
/// </summary>
[RequireComponent(typeof(ObjectMover))]
public class AutoPilot : MonoBehaviour
{
    [Tooltip("移动管线类型（阶段一）：靠近 / 远离")]
    [SerializeField] TargetSourceType targetSourceType = TargetSourceType.ApproachNearestEnemy;

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

    /// <summary>向外暴露的移动管线类型：外部改它就会按新枚举重建阶段一</summary>
    public TargetSourceType SourceType
    {
        get => targetSourceType;
        set
        {
            targetSourceType = value;
            Build();
        }
    }

    void Awake()
    {
        if (map == null) map = FindObjectOfType<MapManager>();
        if (mover == null) mover = GetComponent<ObjectMover>();
        health = GetComponent<Health>();
        if (mover != null && mover.map == null) mover.map = map;

        Build();
    }

    /// <summary>按当前枚举装配三段（工厂造接口，这里只负责装上；也可以外部塞别的实现进来）</summary>
    public void Build()
    {
        PathPipelineFactory.Wire(this, targetSourceType, map, transform, Team, mover);
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
