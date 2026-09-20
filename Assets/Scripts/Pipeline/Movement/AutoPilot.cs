using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 自动寻路管线编排（挂在实体上），三段可替换：
/// 阶段一 获得目标点（ITargetSource）-> 阶段二 构建行动路径（IPathPlanner）-> 阶段三 执行路径（IPathExecutor）。
/// 装配在本组件内完成（Build() 调 PathPipelineFactory，阶段三自己挪位置，不再有独立的移动组件）；
/// 本回合可走步数（ApplySteps）存在这里，只有"远离"阶段一挑落点时读它。
/// 和技能管线的 SkillManager 一个套路。只读地图数据（CanEnter），占用数据由 MapManager 维护。
/// 成员顺序：属性 → 生命周期 → 公开方法 → 私有方法（各组内按调用顺序）。
/// </summary>
public class AutoPilot : MonoBehaviour
{
    #region 属性

    [Tooltip("移动管线类型（阶段一）：靠近 / 远离")][SerializeField] TargetSourceType targetSourceType = TargetSourceType.ApproachNearestEnemy;

    [Tooltip("地图管理器；留空则取场景里的第一个")]
    public MapManager map;

    [Tooltip("移动速度（世界单位/秒）；走一格用时 = 地图格子边长 / 速度")]
    public float speed = 5f;

    [Tooltip("当前路径（格子，不含起点）")]
    public readonly List<Vector2Int> path = new();

    Coroutine routine;
    Health health;
    int stepsThisTurn;      // 本回合可走步数（0 = 不限）

    /// <summary>阶段一：获得目标点</summary>
    public ITargetSource TargetSource { get; set; }

    /// <summary>阶段二：构建行动路径</summary>
    public IPathPlanner Planner { get; set; }

    /// <summary>阶段三：执行路径</summary>
    public IPathExecutor Executor { get; set; }

    /// <summary>是否正在走（动画中，输入被阻断）</summary>
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

    #endregion

    #region 生命周期

    void OnDisable() => Clear();

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

    #endregion

    #region 公开方法

    /// <summary>初始化：由 Entity.Init 调用（地图由上级发下来），这里只找身上的 Health 并装配三段</summary>
    public void Init()
    {
        health = GetComponent<Health>();
        Build();
    }

    /// <summary>按当前枚举装配三段（工厂造接口，这里只负责装上；也可以外部塞别的实现进来）</summary>
    public void Build()
    {
        PathPipelineFactory.Wire(this, targetSourceType, map, transform, Team, () => stepsThisTurn, speed);
    }

    /// <summary>完整管线：阶段一 -> 阶段二 -> 阶段三</summary>
    public bool RunPipeline()
    {
        if (TargetSource == null || !TargetSource.TryGetTarget(out var goal)) return false;
        return MoveTo(goal);
    }

    /// <summary>阶段二 + 阶段三：指定目标格，构建路径并出发</summary>
    public bool MoveTo(Vector2Int target)
    {
        Clear();                                            // 先清理上一次的移动
        if (map == null || Planner == null || Executor == null) return false;

        var start = map.WorldToCell(transform.position);
        if (!Planner.TryBuild(start, target, path)) return false;

        routine = StartCoroutine(Follow());
        return true;
    }

    /// <summary>阶段二 + 阶段三：指定目标点（世界坐标）</summary>
    public bool MoveTo(Vector3 target)
    {
        return map != null && MoveTo(map.WorldToCell(target));
    }

    /// <summary>清理：停下正在走的协程，让地图数据跟坐标对齐，并清空路径（由 Entity.Clear 调）</summary>
    public void Clear()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
            if (map != null) map.SyncOccupied();   // 走到一半被停：让地图数据跟当前坐标对齐
        }

        path.Clear();
    }

    /// <summary>记下本回合可走步数（0 = 不限），Entity 开局与每回合开始时调它</summary>
    public void ApplySteps(int steps) => stepsThisTurn = steps;

    #endregion

    #region 私有方法

    [ContextMenu("跑一次管线")]
    void RunPipelineMenu() => RunPipeline();

    /// <summary>阶段三的包装：执行完把状态收回来（执行器只管走路，不用操心 AutoPilot 的状态）</summary>
    IEnumerator Follow()
    {
        yield return Executor.Run(path);
        routine = null;
    }

    #endregion

}
