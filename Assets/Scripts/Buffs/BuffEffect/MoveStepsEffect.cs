using UnityEngine;

/// <summary>
/// 增加移动步数的 buff：挂上时把 steps 加给 Entity，移除时减回去（本回合预算也跟着同步）。
/// 加几格、持续几回合都由技能的阶段三给（MoveStepsBuffCaster 的常量）。
/// </summary>
public class MoveStepsEffect : IBuff
{
    readonly int steps;

    readonly int duration;      // 结算几次（<= 0 = 永久）

    Entity target;              // 挂给谁（BuffManager 收下时发下来）
    int elapsed;                // 已经结算过几次

    /// <summary>是否该结束了（永久 buff 永远不结束）</summary>
    public bool IsOver => duration > 0 && elapsed >= duration;

    /// <summary>还剩几次结算（永久返回 -1）</summary>
    public int Left => duration > 0 ? Mathf.Max(0, duration - elapsed) : -1;

    /// <summary>多加几格 + 结算几次</summary>
    public MoveStepsEffect(int steps, int duration)
    {
        this.steps = steps;
        this.duration = duration;
    }

    /// <summary>装配：挂给谁</summary>
    public void Init(Entity target) => this.target = target;

    /// <summary>挂上：加步数（常驻加成）</summary>
    public void Apply() => Change(steps);

    /// <summary>结算一次：这条 buff 没有每次结算的效果，只记一次已结算</summary>
    public void Tick() => elapsed++;

    /// <summary>移除：把加上去的步数减回来</summary>
    public void Revert() => Change(-steps);

    /// <summary>delta 正负就是挂上 / 移除；日志打出变完之后 Entity 上的步数</summary>
    void Change(int delta)
    {
        if (target == null) return;

        target.AddMoveSteps(delta);
        Debug.Log($"[MoveStepsEffect] {target.name} 移动步数 {delta:+0;-0}，现在 {target.moveSteps}{(delta < 0 ? "（移除加成）" : "")}", target);
    }
}
