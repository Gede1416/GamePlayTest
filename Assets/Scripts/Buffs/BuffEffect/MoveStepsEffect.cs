using UnityEngine;

/// <summary>增加移动步数：挂上时把 steps 加给 Entity，移除时减回去（本回合预算也跟着同步）</summary>
public class MoveStepsEffect : IBuffEffect
{
    #region 属性

    readonly int steps;

    #endregion

    #region 构造

    public MoveStepsEffect(int steps)
    {
        this.steps = steps;
    }

    #endregion

    #region 公开方法

    public void Apply(Entity target) => Change(target, steps);

    public void Tick(Entity target) { }

    public void Revert(Entity target) => Change(target, -steps);

    #endregion

    #region 私有方法

    /// <summary>delta 正负就是挂上 / 移除；日志打出变完之后 Entity 上的步数</summary>
    static void Change(Entity target, int delta)
    {
        if (target == null) return;

        target.AddMoveSteps(delta);
        Debug.Log($"[MoveStepsEffect] {target.name} 移动步数 {delta:+0;-0}，现在 {target.moveSteps}{(delta < 0 ? "（移除加成）" : "")}", target);
    }

    #endregion
}
