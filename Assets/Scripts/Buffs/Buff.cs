using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 一条正在生效的 buff（运行期）：**内容由技能那边配好、BuffManager 收下时包成这个**——
/// 名字 + 效果零件 + 持续回合 + 挂给谁 + 已结算几次；三步生命周期就是把零件的三个动作对目标跑一遍。
/// 持续回合按**结算次数**算：Duration = 3 表示还会结算 3 次，结算满就该移除；Duration &lt;= 0 表示永久。
/// </summary>
public class Buff : IBuff
{
    readonly BuffType type;
    readonly int duration;
    readonly List<IBuffEffect> effects;

    Entity target;      // 挂给谁（BuffManager 收下这条 buff 时发下来）
    int elapsed;        // 已经结算过几次

    /// <summary>名字（界面显示用）</summary>
    public BuffType Type => type;

    /// <summary>是否该结束了（永久 buff 永远不结束）</summary>
    public bool IsOver => duration > 0 && elapsed >= duration;

    /// <summary>还剩几次结算（永久返回 -1）</summary>
    public int Left => duration > 0 ? Mathf.Max(0, duration - elapsed) : -1;

    /// <summary>构造：效果零件与持续回合由技能的阶段三（caster）给</summary>
    public Buff(BuffType type, int duration, params IBuffEffect[] effects)
    {
        this.type = type;
        this.duration = duration;
        this.effects = new List<IBuffEffect>(effects);
    }

    /// <summary>装配：挂给谁</summary>
    public void Init(Entity target) => this.target = target;

    /// <summary>挂上：跑一遍各零件的 Apply（常驻加成、治疗挂上那一次都在这）</summary>
    public void Add()
    {
        if (target == null) return;

        foreach (var effect in effects) effect.Apply(target);
    }

    /// <summary>结算一次：跑一遍各零件的 Tick，然后记一次已结算</summary>
    public void Trigger()
    {
        if (target != null)
            foreach (var effect in effects) effect.Tick(target);

        elapsed++;
    }

    /// <summary>移除：跑一遍各零件的 Revert（把 Apply 加上去的东西撤回来）</summary>
    public void Remove()
    {
        if (target == null) return;

        foreach (var effect in effects) effect.Revert(target);
    }
}
