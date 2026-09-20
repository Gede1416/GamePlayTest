using System.Collections.Generic;

// ============ Buff 的两条接口 ============
// 一条 buff = **一组效果零件拼起来的组合**（见 Combination/），三步生命周期和零件一一对应：
// 挂上 <c>Add()</c> → 每次结算 <c>Trigger()</c> → 过期 / 清场 <c>Remove()</c>，每个动作对所有目标、所有零件各跑一遍。
// 持续回合与数值由各自的 buff 自己带（常量），零件也自己带数值（构造函数注入），
// 所以接口只收"这次作用在谁身上"，不传配置对象。

/// <summary>
/// 一条正在生效的 buff（组合出来的）：类型 + 三步生命周期 + 还剩几次结算。
/// 持续回合按**结算次数**算：duration = 3 表示还会结算 3 次，结算满就该移除；duration &lt;= 0 表示永久。
/// </summary>
public interface IBuff
{
    /// <summary>类型（界面显示 / 按名字找它用）</summary>
    BuffType Type { get; }

    /// <summary>是否该结束了（永久 buff 永远不结束）</summary>
    bool IsOver { get; }

    /// <summary>还剩几次结算（永久 buff 返回 -1）；给界面显示用</summary>
    int Left { get; }

    /// <summary>挂上：常驻加成在这里生效（治疗这种要等 Trigger 才见效的也照跑一遍）</summary>
    void Add();

    /// <summary>结算一次：一次性效果在这里做，然后记一次已结算</summary>
    void Trigger();

    /// <summary>移除：把 Apply 加上去的东西撤回来（清场时也会调，别把加成留在实体上）</summary>
    void Remove();
}

/// <summary>
/// Buff 效果零件：一条 buff 生命周期里的三个动作，**数值自己带**（构造函数注入）。
/// 注意「增加移动步数 / 增加攻击力」与「移除增加的移动步数 / 攻击力」**不是两个零件**：
/// 它们是同一个实现的两头——Apply 加上去、Revert 减回来；拆成两个零件就得各记一份数值，
/// 迟早和实际的加成对不上。
/// 每个动作都会打一条日志（带目标名字与变化量），方便在控制台里对着回合看。
/// </summary>
public interface IBuffEffect
{
    /// <summary>挂上：常驻加成在这里生效</summary>
    void Apply(Entity target);

    /// <summary>每次结算：一次性效果（治疗）在这里做</summary>
    void Tick(Entity target);

    /// <summary>移除：把 Apply 加上去的东西撤回来</summary>
    void Revert(Entity target);
}
