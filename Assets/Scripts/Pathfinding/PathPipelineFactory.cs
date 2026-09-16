using UnityEngine;

/// <summary>阶段一（获得目标点）可选哪几种实现——工厂按这个枚举造接口，Inspector 里直接选。</summary>
public enum TargetSourceType
{
    /// <summary>靠近：走向距离最近的非己方单位</summary>
    ApproachNearestEnemy = 0,

    /// <summary>远离：躲开距离最近的非己方单位</summary>
    FleeNearestEnemy = 1,
}

/// <summary>
/// 管线工厂：按枚举造出管线三段需要的接口实现，调用方（Entity）只管把造好的东西塞进 AutoPilot。
/// 阶段二/三目前只有一种实现，也走这里，换实现时只改这个文件。
/// </summary>
public static class PathPipelineFactory
{
    /// <summary>阶段一：获得目标点（mover 是给"远离"读本回合步数用的）</summary>
    public static ITargetSource CreateSource(TargetSourceType type, MapManager map, Transform self, int team, ObjectMover mover)
    {
        switch (type)
        {
            case TargetSourceType.FleeNearestEnemy:
                return new FleeNearestEnemy(map, self, team, mover);
            default:
                return new ApproachNearestEnemy(map, self, team);
        }
    }

    /// <summary>阶段二：构建行动路径</summary>
    public static IPathPlanner CreatePlanner(MapManager map)
    {
        return new BfsPathPlanner(map);
    }

    /// <summary>阶段三：执行路径</summary>
    public static IPathExecutor CreateExecutor(MapManager map, ObjectMover mover)
    {
        return new MoverPathExecutor(map, mover);
    }

    /// <summary>一把装配好三段（按枚举选阶段一）</summary>
    public static void Wire(AutoPilot pilot, TargetSourceType type, MapManager map, Transform self, int team, ObjectMover mover)
    {
        if (pilot == null) return;

        pilot.TargetSource = CreateSource(type, map, self, team, mover);
        pilot.Planner = CreatePlanner(map);
        pilot.Executor = CreateExecutor(map, mover);
    }
}
