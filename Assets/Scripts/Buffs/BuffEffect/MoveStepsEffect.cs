using UnityEngine;

/// <summary>
/// 增加移动步数的 buff：挂上时把 steps 加给目标，移除时减回去（本回合预算也跟着同步）。
/// 加几格、持续几回合都由技能的阶段三给（MoveStepsBuffCaster 的常量）；
/// 挂给谁每次都当参数传进来（零件不记目标）。
/// </summary>
public class MoveStepsEffect : IBuff
{
    readonly int steps;

    readonly int duration;      // 结算几次（<= 0 = 永久）

    int elapsed;                // 已经结算过几次

    /// <summary>是否该结束了（永久 buff 永远不结束）</summary>
    public bool IsOver => duration > 0 && elapsed >= duration;

    /// <summary>多加几格 + 结算几次</summary>
    public MoveStepsEffect(int steps, int duration)
    {
        this.steps = steps;
        this.duration = duration;
    }

    /// <summary>挂上：加步数（常驻加成）</summary>
    public void Apply(Entity entity) => Change(entity, steps);

    /// <summary>结算一次：这条 buff 没有每次结算的效果，只记一次已结算</summary>
    public void Tick(Entity entity) => elapsed++;

    /// <summary>移除：把加上去的步数减回来</summary>
    public void Revert(Entity entity) => Change(entity, -steps);

    /// <summary>delta 正负就是挂上 / 移除；日志打出变完之后目标上的步数</summary>
    static void Change(Entity entity, int delta)
    {
        if (entity == null) return;

        entity.AddMoveSteps(delta);
        Debug.Log($"[MoveStepsEffect] {entity.name} 移动步数 {delta:+0;-0}，现在 {entity.moveSteps}{(delta < 0 ? "（移除加成）" : "")}", entity);
    }
}
