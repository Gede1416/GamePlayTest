
/// <summary>
/// **唯一的一条 buff 接口**：效果零件自己就是一条 buff——队列里装的就是它，没有"组合 buff"那一层。
/// 数值与**持续回合**都自己带（构造函数注入），所以零件还要回答"还剩几次结算 / 该不该结束"：
/// 持续回合按结算次数算——Duration = 3 表示还会结算 3 次，结算满就该移除；Duration &lt;= 0 表示永久。
/// 三步生命周期和零件一一对应：挂上 <c>Apply()</c> → 每次结算 <c>Tick()</c> → 过期 / 清场 <c>Revert()</c>。
/// 注意「增加移动步数 / 增加攻击力」与「移除增加的移动步数 / 攻击力」**不是两个零件**：
/// 它们是同一个实现的两头——Apply 加上去、Revert 减回来；拆成两个零件就得各记一份数值，
/// 迟早和实际的加成对不上。
/// 每个动作都会打一条日志（带目标名字与变化量），方便在控制台里对着回合看。
/// </summary>
public interface IBuff
{
    /// <summary>是否该结束了（永久 buff 永远不结束）</summary>
    bool IsOver { get; }

    /// <summary>还剩几次结算（永久 buff 返回 -1）；给界面显示用</summary>
    int Left { get; }

    /// <summary>装配：挂给谁——由 BuffManager 在收下这条 buff 时发下来，后面三个动作都作用在它身上</summary>
    void Init(Entity target);

    /// <summary>挂上：常驻加成在这里生效</summary>
    void Apply();

    /// <summary>每次结算：一次性效果（治疗）在这里做，最后记一次已结算</summary>
    void Tick();

    /// <summary>移除：把 Apply 加上去的东西撤回来</summary>
    void Revert();
}
